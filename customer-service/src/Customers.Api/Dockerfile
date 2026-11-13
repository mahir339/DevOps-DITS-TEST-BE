# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY building-blocks/artifacts/packages/ ./building-blocks/artifacts/packages/
COPY customer-service/ ./customer-service/

WORKDIR /src/customer-service
RUN dotnet restore src/Customers.Api/Customers.Api.csproj

RUN dotnet publish src/Customers.Api/Customers.Api.csproj \
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
ENV ASPNETCORE_HTTP_PORTS=5002

COPY --from=build /app/publish .

EXPOSE 5002

ENTRYPOINT ["dotnet", "Customers.Api.dll"]
