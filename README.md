# server

Backend of the DevOps Test Store: eight .NET 10 processes, three shared infrastructure systems,
and a set of internal NuGet packages they all depend on. This page explains how the pieces are
configured and how to build and run all of them **directly on a developer machine** (no containers).
Packaging and deploying them is your job.

## 1. What runs

```
                 browser (store app :4200, admin portal :4300)
                                   │  HTTPS / JWT
                                   ▼
                         ┌───────────────────┐
                         │ gateway      :5000│  YARP reverse proxy, CORS, JWT check per route,
                         └─────────┬─────────┘  login rate limit, blocks internal paths
     ┌──────────────┬──────────────┼──────────────┬──────────────┬──────────────┐
     ▼              ▼              ▼              ▼              ▼              ▼
 identity :5006  catalog :5001  customer :5002  inventory :5003  order :5004  notification :5005
     │              │  │           │              │  ▲           │  │  │         │
     │              │  └─ Redis    │              │  │ HTTP (reserve/release)   │
     │              │              │              │  └───────────┘  │  │         │
     ▼              ▼              ▼              ▼                 ▼  │         ▼
 identity_db    catalog_db    customers_db   inventory_db     ordering_db  notifications_db
                                                  ▲                    │         ▲
                                                  │   RabbitMQ         │ outbox  │
                                                  └──── exchange "devops-store.events" ◄┘
                                                              │
                                                              ▼
                                                notification-worker :5007
```

| Process | Folder | Port | Needs | Role |
|---|---|---|---|---|
| gateway | `gateway/src/Gateway.Api` | 5000 | all services below | The only public entry point |
| identity-service | `identity-service/src/Identity.Api` | 5006 | PostgreSQL | Users, sign-in, issues JWTs |
| catalog-service | `catalog-service/src/Catalog.Api` | 5001 | PostgreSQL, Redis | Products (cached in Redis) |
| customer-service | `customer-service/src/Customers.Api` | 5002 | PostgreSQL | Customers |
| inventory-service | `inventory-service/src/Inventory.Api` | 5003 | PostgreSQL, RabbitMQ | Stock; consumes `OrderCancelled` |
| order-service | `order-service/src/Ordering.Api` | 5004 | PostgreSQL, RabbitMQ, catalog, customer, inventory | Orders; publishes events through an outbox |
| notification-service | `notification-service/src/Notifications.Api` | 5005 | PostgreSQL | Notification log (read API) |
| notification-worker | `notification-service/src/Notifications.Worker` | 5007 | PostgreSQL, RabbitMQ | Turns order events into notifications |

The ports are only the local defaults (`Properties/launchSettings.json`, or `--urls`).
Each solution (`*.slnx`) is built, tested and published on its own. There is no root solution.
`notification-service` produces **two** deployables (Api + Worker) from one solution.

### How a request travels

1. The browser signs in: `POST /api/auth/login` → gateway → identity-service → JWT (HS256).
2. Every later call carries `Authorization: Bearer <jwt>`. The gateway checks it against the route's
   policy (`anonymous`, `default` = any signed-in user, `staff`, `admin`), then the service checks it again.
3. order-service calls catalog, customer and inventory over HTTP and **forwards the user's token** and the
   `X-Correlation-Id` header. GET calls are retried, and a circuit breaker stops calls to a failing service.
4. The order and an `ordering.order-placed.v1` event are saved in **one database transaction**
   (transactional outbox, table `outbox_messages`). A background loop in order-service publishes pending
   rows to RabbitMQ. Several replicas can run it safely because it uses `FOR UPDATE SKIP LOCKED`.
5. notification-worker consumes the event and stores a notification. When an admin cancels an order,
   inventory-service consumes `ordering.order-cancelled.v1` and puts the stock back.
   Delivery is at-least-once, so both consumers remember processed message ids and skip duplicates.
6. One `X-Correlation-Id` appears in the log lines of every service the request touched, including the worker.

## 2. Building blocks (internal NuGet packages)

`building-blocks/` contains six libraries that every service consumes **as NuGet packages**, not as
project references:

| Package | What it gives a service |
|---|---|
| `DevOpsStore.BuildingBlocks.Results` | `Result`/`Error` types |
| `DevOpsStore.BuildingBlocks.Web` | `AddServiceDefaults` (logging, problem details, CORS, correlation id), `GET /`, `/health/live`, `/health/ready` |
| `DevOpsStore.BuildingBlocks.Http` | Token + correlation-id forwarding, retry/circuit-breaker for service-to-service calls |
| `DevOpsStore.BuildingBlocks.Persistence` | PostgreSQL/EF Core registration, migration gate, `--migrate` mode |
| `DevOpsStore.BuildingBlocks.Messaging` | RabbitMQ publisher/consumers, retries + dead-letter queues, outbox, event contracts |
| `DevOpsStore.BuildingBlocks.Security` | JWT validation, `staff`/`admin` policies, token issuer |

Every service's `nuget.config` maps `DevOpsStore.*` to a feed called `devops-store` and everything else
to nuget.org. Locally that feed is the folder `building-blocks/artifacts/packages` (git-ignored).
**Build order is therefore fixed:** pack the building blocks first, then restore any service.

Versioning rule: the version lives in `building-blocks/Directory.Build.props`, and services pin it in
their own `Directory.Packages.props`. **Every change to a building block needs a new version number.**
NuGet caches packages by version (`~/.nuget/packages`), so re-packing the same version is silently
ignored on machines that already have it. `GET /` on each service reports the building-blocks version
it was built with.

## 3. Prerequisites (installed locally)

| Tool | Version used | Notes |
|---|---|---|
| .NET SDK | 10.0.x | `dotnet --list-sdks` |
| PostgreSQL | 17 | One server is fine locally; one database per service |
| RabbitMQ | 4.x | Management plugin optional (UI on :15672) |
| Redis | 7 or 8 | Optional: set `Cache__Enabled=false` to run catalog without it |
| `dotnet-ef` | 10.0.14 | Local tool, `dotnet tool restore` in each service folder |

Create the databases (any names work, these match the examples below):

```sql
CREATE DATABASE identity_db;  CREATE DATABASE catalog_db;   CREATE DATABASE customers_db;
CREATE DATABASE inventory_db; CREATE DATABASE ordering_db;  CREATE DATABASE notifications_db;
```

In a real environment give every service its **own** database user with rights on its own database only.

## 4. Configuration

All settings come from `appsettings.json` → `appsettings.{Environment}.json` → user-secrets
(Development only) → **environment variables** (last one wins). In environment variables `:` becomes `__`,
for example `ConnectionStrings:CatalogDb` → `ConnectionStrings__CatalogDb`.
`ASPNETCORE_ENVIRONMENT` selects the environment (`Development` locally, `Production` by default).

Nothing secret is committed. A service **refuses to start** when a required value is missing or invalid,
and the error names the setting.

### Secrets (provide per environment, never in appsettings)

| Setting | Used by | Example (local) |
|---|---|---|
| `Jwt__SigningKey` | **all 7 APIs + gateway** (must be identical everywhere) | ≥ 32 random characters |
| `ConnectionStrings__IdentityDb` | identity | `Host=localhost;Port=5432;Database=identity_db;Username=…;Password=…` |
| `ConnectionStrings__CatalogDb` | catalog | … `Database=catalog_db` … |
| `ConnectionStrings__CustomersDb` | customer | … `Database=customers_db` … |
| `ConnectionStrings__InventoryDb` | inventory | … `Database=inventory_db` … |
| `ConnectionStrings__OrderingDb` | order | … `Database=ordering_db` … |
| `ConnectionStrings__NotificationsDb` | notification API **and** worker | … `Database=notifications_db` … |
| `ConnectionStrings__RabbitMq` | inventory, order, notification-worker | `amqp://user:password@localhost:5672/` |
| `ConnectionStrings__Redis` | catalog | `localhost:6379` (or `host:6380,password=…,ssl=true`) |
| `Identity__SeedUsers__0__Password` | identity | password for the first seed user (admin) |
| `Identity__SeedUsers__1__Password` | identity | password for the second seed user (staff) |

### Other settings (defaults in appsettings.json)

| Setting | Service | Default | Meaning |
|---|---|---|---|
| `Jwt__Issuer`, `Jwt__Audience` | all | `devops-store-identity`, `devops-store-api` | Must match everywhere |
| `Jwt__AccessTokenMinutes` | identity | 60 | Token lifetime |
| `Database__MigrateOnStartup` | DB services | `false` (`true` in Development) | Apply migrations at startup |
| `Database__FailOnPendingMigrations` | DB services | `true` | Refuse to start if the schema is behind the code |
| `Services__CatalogUrl` / `CustomerUrl` / `InventoryUrl` | order | `http://localhost:500x` | Where order-service finds the others |
| `Services__TimeoutSeconds` | order | 5 | Per-attempt timeout of downstream calls |
| `Cache__Enabled`, `Cache__ProductTtlSeconds`, `Cache__KeyPrefix` | catalog | `true`, 120, `catalog:` | Redis cache |
| `Messaging__Exchange`, `__PrefetchCount`, `__MaxRetries`, `__RetryDelayMilliseconds` | inventory, order, worker | `devops-store.events`, 10, 3, 1000 | Broker behaviour |
| `Outbox__PollIntervalMilliseconds`, `__BatchSize`, `__MaxAttempts`, `__RetentionHours` | order | 1000, 20, 20, 72 | Outbox dispatcher |
| `Identity__SeedUsers__N__Email/DisplayName/Roles__0` | identity | admin@example.com (admin, staff), staff@example.com (staff) | Created at startup if missing |
| `Gateway__LoginPermitsPerMinute` | gateway | 10 | Sign-in attempts per client IP per minute (raise it for E2E runs) |
| `Gateway__BlockedPathPatterns__0` | gateway | `^/api/inventory/[^/]+/(reserve\|release)/?$` | Internal paths answered with 404 |
| `ReverseProxy__Clusters__<name>__Destinations__primary__Address` | gateway | `http://localhost:<port>/` | Backend address per service (`identity`, `catalog`, `customer`, `inventory`, `order`, `notification`, `notification-worker`) |
| `Cors__AllowedOrigins__0..n` | gateway | `http://localhost:4200`, `http://localhost:4300` | Browser origins allowed to call the API |
| `Logging__Console__FormatterName` | all | `simple` | `json` = one JSON object per line (for log collectors) |
| `App__Version` | all | `1.0.0` | Shown by `GET /` |

Locally you can keep secrets out of files with user-secrets (each API has a `UserSecretsId`):

```bash
cd catalog-service/src/Catalog.Api
dotnet user-secrets set "Jwt:SigningKey" "<same long random string for every service>"
dotnet user-secrets set "ConnectionStrings:CatalogDb" "Host=localhost;Port=5432;Database=catalog_db;Username=<user>;Password=<password>"
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379"
```

User-secrets are only read when `ASPNETCORE_ENVIRONMENT=Development`. Other environments use real
environment variables, or a secret store mapped to them.

## 5. Build and test

```bash
# 1) building blocks: build, test, pack into the local feed
cd server/building-blocks
dotnet test BuildingBlocks.slnx -c Release
dotnet pack BuildingBlocks.slnx -c Release -o artifacts/packages

# 2) any service (repeat for each folder: catalog-service, customer-service, inventory-service,
#    order-service, notification-service, identity-service, gateway)
cd ../catalog-service
dotnet restore Catalog.slnx
dotnet build Catalog.slnx -c Release --no-restore
dotnet test Catalog.slnx -c Release --no-build --filter "Category!=Integration"

# 3) publish the runnable project (Api, or Worker for the notification worker)
dotnet publish src/Catalog.Api/Catalog.Api.csproj -c Release -o out/catalog
```

Integration tests (`inventory-service/tests/Inventory.IntegrationTests`, tagged `Category=Integration`)
need a **disposable** PostgreSQL database. The tests drop and recreate its schema:

```bash
INVENTORY_TEST_DB="Host=localhost;Port=5432;Database=inventory_test;Username=…;Password=…" \
  dotnet test inventory-service/Inventory.slnx --filter "Category=Integration"
```

Without that variable they fail with a message saying so. Filter them out in a unit-only run.

## 6. Database migrations

Each service owns its schema. EF Core migrations live in `src/*.Infrastructure/Persistence/Migrations`
and also insert the sample data (6 products, 4 customers, stock for products 1-6).

Three ways to apply them:

| Way | Command | Typical use |
|---|---|---|
| Startup | `Database__MigrateOnStartup=true` | Laptop only (default in Development) |
| One-shot mode | `dotnet Catalog.Api.dll --migrate` | A separate deploy step / job before the new version starts. Needs only the connection string. Exits 0 when done |
| EF tooling | `dotnet ef database update --project src/Catalog.Infrastructure --startup-project src/Catalog.Api --connection "<conn>"` or `dotnet ef migrations bundle …` | Pipelines that prefer a migration artifact |

Rules that matter for deployments:

- With `FailOnPendingMigrations=true` (default), a service whose database is behind its code **will not
  start**. It logs which migrations are missing.
- **notification-worker never migrates.** The notification API owns the schema, so migrate it first;
  until then the worker refuses to start.
- **Rebuild the API project after adding a migration.** Migrations are compiled into the Infrastructure
  assembly. A stale API build has no new migrations, so `--migrate` reports "up to date" while the tables
  are missing.
- New migration: `dotnet ef migrations add <Name> --project src/X.Infrastructure --startup-project src/X.Api --output-dir Persistence/Migrations`
  (run `dotnet tool restore` once first).

## 7. Run everything locally

Start PostgreSQL, RabbitMQ and Redis, configure secrets (section 4), pack the building blocks (section 5),
then start each process in its own terminal. Start order matters only where noted:

```bash
cd server/identity-service/src/Identity.Api        && dotnet run   # :5006 - creates seed users
cd server/catalog-service/src/Catalog.Api          && dotnet run   # :5001
cd server/customer-service/src/Customers.Api       && dotnet run   # :5002
cd server/inventory-service/src/Inventory.Api      && dotnet run   # :5003
cd server/order-service/src/Ordering.Api           && dotnet run   # :5004
cd server/notification-service/src/Notifications.Api    && dotnet run   # :5005 - before the worker (it migrates)
cd server/notification-service/src/Notifications.Worker && dotnet run   # :5007
cd server/gateway/src/Gateway.Api                  && dotnet run   # :5000
```

`dotnet run` uses the `Development` environment, which applies migrations automatically. To run a
published build instead: `dotnet out/catalog/Catalog.Api.dll --urls http://localhost:5001`.

Check it works (through the gateway):

```bash
curl http://localhost:5000/status/order/health/ready      # postgres, rabbitmq, outbox: Healthy
curl http://localhost:5000/api/products                   # public
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login -H "content-type: application/json" \
  -d '{"email":"staff@example.com","password":"<seed password>"}' | sed 's/.*"accessToken":"\([^"]*\)".*/\1/')
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/orders
```

## 8. Health, logs and the message broker

- `GET /health/live` (every process): the process is up, checks nothing. Use it for liveness.
- `GET /health/ready`: JSON report of the dependencies (`postgres`, `rabbitmq`, `redis`, `outbox`). HTTP 503
  when one is **Unhealthy**. Redis and outbox problems only report **Degraded** (HTTP 200), because the
  service still works.
- Through the gateway: `/status/<service>/health/ready` and `/status/<service>/`.
- Logs go to stdout. Set `Logging__Console__FormatterName=json` for structured logs; the
  `CorrelationId` is in the scopes.
- RabbitMQ topology (created by the services on startup):
  - topic exchange `devops-store.events`
  - queues `inventory.order-cancelled`, `notifications.order-placed`, `notifications.order-cancelled`
  - after `MaxRetries` failed attempts a message moves to `<queue>.dead-letter` (exchange `devops-store.dead-letter`)
  - a publish with **no queue bound** is rejected, and the outbox retries it. So start the consumers
    (inventory, worker) before expecting events to drain.

## 9. Common problems

| Symptom | Cause |
|---|---|
| `NU1301` / package `DevOpsStore.*` not found | Building blocks not packed, or `nuget.config` does not point at your feed |
| A building-block change has no effect | Same version re-packed: bump the version (section 2) |
| `Jwt:SigningKey is not set` / `must be at least 32 bytes` | Missing or short key; it must be the same in every service |
| Every API call returns 401 after deploying one service | That service has a different `Jwt__SigningKey`, `Issuer` or `Audience` |
| `the database is missing N migration(s)` | Migrations not applied before starting (section 6) |
| Browser shows a CORS error | Origin missing from the gateway's `Cors__AllowedOrigins` |
| 429 on sign-in | `Gateway__LoginPermitsPerMinute` reached (also during automated tests) |
| Orders succeed but no notifications | Worker not running, broker unreachable, or messages in a `.dead-letter` queue |
| `/health/ready` says outbox Degraded | Events are waiting: the broker is down or no queue is bound yet |
