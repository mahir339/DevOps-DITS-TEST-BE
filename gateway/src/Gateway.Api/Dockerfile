
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy shared internal packages
COPY building-blocks/artifacts/packages/ ./building-blocks/artifacts/packages/

# Copy Gateway configuration
COPY gateway/Directory.Build.props ./gateway/
COPY gateway/Directory.Packages.props ./gateway/
COPY gateway/nuget.config ./gateway/

# Copy Gateway project
COPY gateway/src/Gateway.Api/Gateway.Api.csproj ./gateway/src/Gateway.Api/

# Restore dependencies
WORKDIR /src/gateway
RUN dotnet restore src/Gateway.Api/Gateway.Api.csproj

# Copy Gateway source code
WORKDIR /src
COPY gateway/ ./gateway/

# Publish application
WORKDIR /src/gateway
RUN dotnet publish src/Gateway.Api/Gateway.Api.csproj \
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

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

EXPOSE 5000

ENTRYPOINT ["dotnet", "Gateway.Api.dll"]
