# AcxiomCRM

AcxiomCRM is a role-based customer relationship management application built with ASP.NET Core MVC. It supports customer, lead, opportunity, activity, and follow-up workflows with reporting, validation, audit logging, and a REST API.

## Features

- Role-based access control and resource-level authorization
- Customer, lead, opportunity, activity, and follow-up management
- Opportunity pipeline, dashboards, and operational reports
- ASP.NET Core Identity account management and lockout controls
- REST endpoints for authentication, CRM records, and pipeline reports
- Business-rule validation, audit logging, and centralized error handling
- Entity Framework Core migrations for SQL Server and SQLite
- Automated tests for validation, authorization, workflows, and security

## Technology Stack

- .NET 8 and ASP.NET Core MVC
- Entity Framework Core 8 with SQL Server and SQLite providers
- ASP.NET Core Identity
- Bootstrap 5, Chart.js, and jQuery validation
- xUnit and Moq for tests

## Roles

- **Admin**: manages users and can view audit logs and team reports.
- **Manager**: can use CRM workflows and view team reports.
- **SalesExecutive**: can use CRM workflows subject to resource-level authorization.

The application defines these roles in `Authorization/AppRoles.cs`; access is enforced by authorization policies and resource authorization services.

## Setup

Requirements: .NET 8 SDK, the `dotnet-ef` tool, and either SQL Server LocalDB/a SQL Server instance or SQLite.

1. Clone the repository:

   ```bash
   git clone https://github.com/YOUR-USERNAME/AcxiomCRM.git
   cd AcxiomCRM
   ```

2. Create `AcxiomCRM/appsettings.Development.json` locally. For SQL Server LocalDB, use a local-only configuration such as the following and replace the seed-password placeholder with a strong, throwaway development password:

   ```json
   {
     "DatabaseProvider": "SqlServer",
     "ConnectionStrings": {
       "SqlServerConnection": "Server=(localdb)\\MSSQLLocalDB;Database=AcxiomCRM;Trusted_Connection=True;TrustServerCertificate=True;"
     },
     "SeedData": {
       "DefaultPassword": "REPLACE_WITH_A_STRONG_LOCAL_ONLY_PASSWORD"
     }
   }
   ```

   `appsettings.Development.json` is ignored by Git and is not included in this repository. The configured seed password is used only for local demo users/data; do not use it in production. For other SQL Server setups, use a local secret store or environment variables rather than committing credentials.

3. Restore packages from the repository root:

   ```bash
   dotnet restore
   ```

4. Apply the migrations. If needed, install the EF Core CLI tool first with `dotnet tool install --global dotnet-ef --version 8.0.12`:

   ```bash
   dotnet ef database update --project AcxiomCRM/AcxiomCRM.csproj --startup-project AcxiomCRM/AcxiomCRM.csproj
   ```

5. Run the application:

   ```bash
   dotnet run --project AcxiomCRM/AcxiomCRM.csproj
   ```

6. Run the test suite:

   ```bash
   dotnet test AcxiomCRM.sln
   ```

The default checked-in `appsettings.json` uses a local SQLite database file. That database file is intentionally not included; the application can create a local database when run with its default SQLite provider. The database file is ignored by Git.

## REST API

The API uses ASP.NET Core Identity authentication. Login is anonymous; the other listed endpoints require authentication.

| Method | Endpoint | Purpose |
| --- | --- | --- |
| POST | `/api/auth/login` | Sign in |
| POST | `/api/auth/logout` | Sign out |
| GET, POST | `/api/customers` | List or create customers |
| GET, PUT, DELETE | `/api/customers/{id}` | Read, update, or delete a customer |
| GET, POST | `/api/leads` | List or create leads |
| GET, PUT, DELETE | `/api/leads/{id}` | Read, update, or delete a lead |
| PATCH | `/api/leads/{id}/status` | Change a lead status |
| GET, POST | `/api/opportunities` | List or create opportunities |
| GET, PUT, DELETE | `/api/opportunities/{id}` | Read, update, or delete an opportunity |
| PATCH | `/api/opportunities/{id}/stage` | Change an opportunity stage |
| GET, POST | `/api/followups` | List or create follow-ups |
| GET, PUT, DELETE | `/api/followups/{id}` | Read, update, or delete a follow-up |
| GET | `/api/reports/pipeline` | Retrieve pipeline report data |

## Security Notes

- Never commit passwords, password hashes, API keys, tokens, production connection strings, or local database files.
- Keep `appsettings.Development.json`, `appsettings.Production.json`, `.env` files, and `secrets.json` local; they are excluded by `.gitignore`.
- The checked-in settings contain only local database defaults and no database username/password. Supply any real deployment configuration through a secret manager or environment variables.
- Seed data and a seed password are optional and controlled by the local `SeedData:DefaultPassword` setting. Use development-only values and rotate/remove them before deployment.
- GitHub hosts the source code and documentation; it does not run this ASP.NET Core MVC application as a live website.

## Screenshots

No screenshots were included in the supplied project archive. If added later, place them in `docs/screenshots/` and link them here with repository-relative paths.
