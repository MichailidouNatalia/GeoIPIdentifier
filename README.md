# GeoIPIdentifier

A .NET 8 API service for IP address geolocation with batch processing capabilities, built using Clean Architecture principles.

## Overview

GeoIPIdentifier is a high-performance geolocation service that identifies geographic information for IP addresses. It supports both individual and batch IP address lookups with distributed caching and background job processing.

## Features

- **Individual IP Lookup**: Get geographic information for a single IP address
- **Batch Processing**: Process up to 1000 IP addresses asynchronously
- **Progress Tracking**: Monitor batch processing status in real-time
- **Redis Caching**: Distributed caching for improved performance
- **SQL Server Storage**: Persistent storage for geolocation data
- **Background Jobs**: Quartz.NET scheduler for batch processing
- **Health Checks**: Built-in health monitoring for dependencies
- **Docker Support**: Fully containerized with Docker Compose

## Technology Stack

- **.NET 8**: Latest LTS framework
- **ASP.NET Core Web API**: RESTful API
- **Entity Framework Core 9**: ORM for SQL Server
- **SQL Server 2022**: Primary database
- **Redis**: Distributed caching
- **Quartz.NET**: Background job scheduling
- **AutoMapper**: Object mapping
- **Swagger/OpenAPI**: API documentation
- **Docker & Docker Compose**: Containerization

## API Endpoints

### Individual IP Lookup
```
GET /api/geoip/{ipAddress}
```

### Batch Processing
```
POST /api/geoip/batch
Body: {
  "ipAddresses": ["8.8.8.8", "1.1.1.1", ...]
}
```

### Progress Tracking
```
GET /batch/{batchId}
```

### Health Check
```
GET /health
```

## Getting Started

### Prerequisites

- Docker Desktop
- .NET 8 SDK (for local development)

### Running with Docker Compose

1. Clone the repository:
```bash
git clone <repository-url>
cd GeoIPIdentifier
```

2. Start all services:
```bash
docker-compose up --build
```

3. Access the API:
- API: http://localhost:8080
- Swagger UI: http://localhost:8080/swagger
- Health: http://localhost:8080/health

### Running Locally

1. Install dependencies:
```bash
dotnet restore
```

2. Update connection strings in `appsettings.Development.json`

3. Run the API:
```bash
cd src/GeoIPIdentifier.API
dotnet run
```

## Configuration

### Environment Variables

- `ASPNETCORE_ENVIRONMENT`: Environment (Development/Production)
- `ConnectionStrings__DefaultConnection`: SQL Server connection string
- `ConnectionStrings__Redis`: Redis connection string
- `Quartz__*`: Quartz scheduler configuration
- `IPBase__ApiKey`: Api key configuration

### Docker Services

- **SQL Server**: Port 1433
- **Redis**: Port 6379
- **API**: Port 8080

## Testing

Run all tests:
```bash
dotnet test
```

Run tests for specific project:
```bash
dotnet test tests/GeoIPIdentifier.API.Tests
```

## Development

### Building the Solution
```bash
dotnet build
```

### Running Tests with Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Project Structure

- **Domain Layer**: Contains business entities, domain logic, and exceptions
- **Application Layer**: Implements use cases, DTOs, and service interfaces
- **Adapters Layer**: External infrastructure (database, cache, external APIs)
- **API Layer**: REST endpoints, middleware, and API configuration
- **Shared Layer**: Common utilities and cross-cutting concerns