# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY building-blocks/artifacts/packages/ ./building-blocks/artifacts/packages/
COPY identity-service/ ./identity-service/

WORKDIR /src/identity-service
RUN dotnet restore src/Identity.Api/Identity.Api.csproj

RUN dotnet publish src/Identity.Api/Identity.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
USER root

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=5006

COPY --from=build /app/publish .

EXPOSE 5006

ENTRYPOINT ["dotnet", "Identity.Api.dll"]
