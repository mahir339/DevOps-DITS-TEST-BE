
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY building-blocks/artifacts/packages/ ./building-blocks/artifacts/packages/
COPY notification-service/ ./notification-service/

WORKDIR /src/notification-service

RUN dotnet restore src/Notifications.Worker/Notifications.Worker.csproj

RUN dotnet publish src/Notifications.Worker/Notifications.Worker.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
USER root

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=5007

COPY --from=build /app/publish .

EXPOSE 5007

ENTRYPOINT ["dotnet", "Notifications.Worker.dll"]
