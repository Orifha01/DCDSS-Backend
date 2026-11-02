# DCDSS Backend Scaffold

This is the greenfield .NET 8 scaffold only. No business code has been added
(entities, services, controllers logic, and `Program.cs` wiring are deferred to
later steps). `Program.cs` is left as the minimal Web API template.

## .NET SDK (verified)
- SDK version: **8.0.427** (`dotnet --version`)
- SDKs installed: `8.0.427 [C:\Program Files\dotnet\sdk]`
- Runtimes include `Microsoft.AspNetCore.App 8.0.33` and `Microsoft.NETCore.App 8.0.33`
- Target framework: `net8.0` (Nullable enabled, ImplicitUsings enabled)

## Solution & Project Paths
- Solution: `DCDSS.sln` at workspace root
  `c:/Users/nm/IdeaProjects/Digital Clinical Decision Support System (DCDSS)/`
- Project: `DCDSS/DCDSS.csproj`
  (ASP.NET Core Web API, controllers-based via `dotnet new webapi --use-controllers`)
- SDK: `Microsoft.NET.Sdk.Web`

## NuGet Packages (pinned, .NET 8 / EF Core 8 compatible)
| Package | Version | Notes |
|---|---|---|
| Pomelo.EntityFrameworkCore.MySql | 8.0.3 | MySQL EF Core provider (EF Core 8 line) |
| Microsoft.EntityFrameworkCore.Design | 8.0.33 | EF Core tooling; `PrivateAssets=all` |
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.33 | JWT authentication |
| Swashbuckle.AspNetCore | 6.6.2 | Swagger/OpenAPI (included by the template) |

Versions are the latest 8.x available on the configured NuGet feed, pinned for
reproducibility.

## Folder / Namespace Structure
Root namespace: **DCDSS**. Sub-namespaces aligned to folders.

```
DCDSS/
  Controllers/          DCDSS.Controllers
  Services/             DCDSS.Services
    Interfaces/         DCDSS.Services.Interfaces
  Repositories/         DCDSS.Repositories
    Interfaces/         DCDSS.Repositories.Interfaces
  Entities/             DCDSS.Entities
  Models/               DCDSS.Models
  DTOs/                 DCDSS.DTOs
  Data/                 DCDSS.Data
  Middleware/           DCDSS.Middleware
  Configuration/        DCDSS.Configuration
  Security/             DCDSS.Security
  Program.cs            DCDSS (minimal template; wiring in later steps)
  appsettings.json
  appsettings.Development.json
  Properties/launchSettings.json
  DCDSS.csproj
  DCDSS.http
```

Each structural folder contains a `.gitkeep` so the empty layout is tracked by git.

## Cleanup
- Removed the default sample files `WeatherForecastController.cs` and
  `WeatherForecast.cs` produced by the template.

## Build Verification
- `dotnet build` run from the `DCDSS` subfolder:
  **Build succeeded. 0 Warning(s) 0 Error(s).**

## Other Artifacts
- Git repository initialized at the workspace root (no commits staged yet).
- Standard .NET `.gitignore` added at the workspace root (`dotnet new gitignore`).
- Workflow artifact directory created: `.agents/tasks/`.

## Deferred to Later Steps
- DbContext + entity models (MySQL code-first)
- Repositories + interfaces
- Services + interfaces
- Controllers + DTOs (Auth, Patients, CDS, Laboratory, Medications,
  Appointments, Reports/Audit)
- JWT auth + RBAC wiring in `Program.cs`
- Global exception middleware
- EF Core migrations + `appsettings.json` MySQL configuration
