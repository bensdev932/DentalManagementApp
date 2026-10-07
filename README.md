# Dental Management App - Backend API

Standalone ASP.NET Core Web API backend for the Dental Management App.

---

## Tech Stack & Architecture

- **Runtime & Framework**: .NET 10 (`net10.0`), ASP.NET Core Web API
- **Database**: PostgreSQL (Npgsql Entity Framework Core Provider) / Supabase compatible
- **Authentication**: ASP.NET Core Identity + JWT Bearer Tokens
- **API Documentation**: Scalar OpenAPI (`Scalar.AspNetCore` + `Microsoft.AspNetCore.OpenApi`)
- **Testing**: xUnit, FluentAssertions, Moq, EF Core InMemory Provider

---

##  Repository Structure

```
Dental management app/
├── .agents/
│   └── rules/
│       └── render-supabase-sync.md       # Production hosting & sync architectural rules
├── .github/
│   └── workflows/
│       ├── db-backup.yml                 # Automated nightly PostgreSQL/Supabase backup
│       └── supabase-keep-alive.yml       # Pinger to prevent 7-day Supabase idle pauses
├── ClinicManagementApp.Api/              # ASP.NET Core Web API Project
│   ├── Controllers/                      # REST API endpoints (Appointments, Auth, Patients, Sync, etc.)
│   ├── Data/                             # ApplicationDbContext & EF Core configurations
│   ├── Domain/                           # Entities & domain models
│   ├── Middleware/                       # Exception handling & sync request logging
│   ├── Migrations/                       # EF Core database migrations
│   ├── Models/                           # DTOs & API response wrappers
│   ├── Services/                         # Business logic services & interfaces
│   ├── Dockerfile                        # Multi-stage Docker build for Linux containers
│   └── appsettings.json                  # Connection strings & configuration settings
├── ClinicManagementApp.Api.Tests/        # Backend test suite (43 automated tests)
├── DentalManagementApp.sln               # Standard Visual Studio solution file
├── DentalManagementApp.slnx              # Modern .NET solution file
├── dotnet-tools.json                     # Local .NET tools configuration
└── .gitignore                            # Standard .NET / Visual Studio gitignore
```

---

##  Getting Started

### 1. Build Solution
```bash
dotnet build DentalManagementApp.slnx
```

### 2. Run Test Suite
```bash
dotnet test DentalManagementApp.slnx
```

### 3. Run Web API Locally
```bash
dotnet run --project ClinicManagementApp.Api
```
The API will launch and OpenAPI documentation can be accessed via Scalar at:
`https://localhost:7147/scalar/v1` or `http://localhost:5031/scalar/v1`

### 4. Run via Docker
```bash
docker build -t dental-management-api -f ClinicManagementApp.Api/Dockerfile ClinicManagementApp.Api
docker run -p 8080:8080 dental-management-api
```
