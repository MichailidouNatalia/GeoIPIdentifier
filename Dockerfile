# Base runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only project files first
COPY src/GeoIPIdentifier.API/GeoIPIdentifier.API.csproj GeoIPIdentifier.API/
COPY src/GeoIPIdentifier.Application/GeoIPIdentifier.Application.csproj GeoIPIdentifier.Application/
COPY src/GeoIPIdentifier.Domain/GeoIPIdentifier.Domain.csproj GeoIPIdentifier.Domain/
COPY src/GeoIPIdentifier.Adapters/GeoIPIdentifier.Adapters.csproj GeoIPIdentifier.Adapters/
COPY src/GeoIPIdentifier.Shared/GeoIPIdentifier.Shared.csproj GeoIPIdentifier.Shared/

# Restore dependencies
RUN dotnet restore GeoIPIdentifier.API/GeoIPIdentifier.API.csproj

# Copy the full source
COPY src/ .

# Build the API project
WORKDIR /src/GeoIPIdentifier.API
RUN dotnet build GeoIPIdentifier.API.csproj -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish GeoIPIdentifier.API.csproj -c Release -o /app/publish

# Final runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create a non-root user (security best practice)
RUN groupadd -r appuser && useradd -r -g appuser appuser \
    && chown -R appuser:appuser /app
USER appuser

ENTRYPOINT ["dotnet", "GeoIPIdentifier.API.dll"]
