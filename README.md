# Budget API: step 08, SQL Server and Docker Compose

Exercise: Budget API with Docker Compose and SQL Server

This repository holds the Budget API that you build in the ASP.NET Core exercises. Each exercise has its own branch. This branch is the finished state of the Docker Compose exercise (API and SQL Server) and the starting point for the Azure deployment exercise.

## What this step adds

- `Microsoft.EntityFrameworkCore.SqlServer` 10.0.x replaces the SQLite provider, and `Program.cs` calls `UseSqlServer`
- No connection string in `appsettings.json`. The local container's connection string is in `appsettings.Development.json`; everywhere else it comes from the `ConnectionStrings__DefaultConnection` environment variable
- The SQLite `DateTimeOffset` converter is gone. SQL Server stores timestamps as `datetimeoffset` and amounts as `decimal(18,2)`
- `Data/Migrations` re-created for SQL Server as a single `InitialCreate` migration
- A multi-stage `Dockerfile` that publishes the API project, and a `.dockerignore` that leaves out the build output, logs and the test project
- `docker-compose.yaml` with a SQL Server service (`db`, with a named volume and a `sqlcmd` health check) and the API (`api`, on port 8080), which starts once the database is healthy
- The tests are unchanged apart from comments. They use the in-memory provider and need no SQL Server

## The signing key

`appsettings.Development.json` contains a development key, so the project runs straight after cloning. HMAC-SHA256 needs a key of at least 32 bytes; this one is 64 characters.

In a real project the key must not be committed. Remove it from `appsettings.Development.json` and store it with the Secret Manager, as the exercise does:

```bash
dotnet user-secrets set "Jwt:Key" "$(openssl rand -hex 32)" --project BudgetApi
```

The SQL Server password `Your_password123` in `appsettings.Development.json` and `docker-compose.yaml` is a local development value. Real passwords go into the platform's settings.

In production, set it as an environment variable (`Jwt__Key`) or in a key vault.

## Project layout

```
BudgetApi.slnx
BudgetApi/
  Program.cs
  Endpoints/   AuthEndpoints.cs, TransactionEndpoints.cs, ReportEndpoints.cs, HealthEndpoints.cs, ClaimsPrincipalExtensions.cs
  Errors/      GlobalExceptionHandler.cs
  Dtos/        Auth/, Users/, Transactions/, Reports/
  Models/      User.cs, Transaction.cs, TransactionType.cs
  Services/    IAuthService.cs, AuthService.cs, ITransactionService.cs, TransactionService.cs, IReportService.cs, ReportService.cs, BudgetMetrics.cs
  Data/        ApplicationDbContext.cs, DbSeeder.cs, Migrations/
BudgetApi.Tests/
  Unit/          TestDbContextFactory.cs, TransactionServiceTests.cs, ReportServiceTests.cs, AuthServiceTests.cs
  Integration/   CustomWebApplicationFactory.cs, TestAuthHandler.cs, TransactionEndpointsTests.cs, ReportEndpointsTests.cs, AuthEndpointsTests.cs, HealthEndpointsTests.cs
Dockerfile
.dockerignore
docker-compose.yaml
dotnet-tools.json
```

## Run it with Docker Compose

Requires Docker. From the repository root:

```bash
docker compose up --build
```

The committed `docker-compose.yaml` runs the API in the **Development** environment: it applies the migrations, seeds sample data, reads the JWT key from `appsettings.Development.json` and serves Scalar. The API listens on `http://localhost:8080`.

- Scalar API reference: http://localhost:8080/scalar
- Health: http://localhost:8080/health/ready

The data lives in the `db-data` volume, so it survives `docker compose down`. `docker compose down -v` deletes the volume as well. `docker compose logs api` shows the request log.

### Production mode

To run the stack as it would run on a server, replace `ASPNETCORE_ENVIRONMENT=Development` in the `api` service with `Jwt__Key=<a random string of at least 32 characters>`. In Production the app does not migrate or seed, and `/scalar` returns `404`. Start again with an empty volume and apply the migrations from your machine:

```bash
docker compose down -v
docker compose up --build -d

ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=BudgetDb;User Id=sa;Password=Your_password123;TrustServerCertificate=true" \
  dotnet ef database update --project BudgetApi
```

`/health/ready` returns `Unhealthy` until the migrations are applied. If it still fails afterwards, wait a minute or run `docker compose restart api`, because SqlClient briefly remembers the failed login.

## Run it with dotnet run

Requires the .NET 10 SDK and a SQL Server container. Run these commands from the repository root.

```bash
docker run -d --name budget-sql \
  --platform linux/amd64 \
  -e ACCEPT_EULA=Y \
  -e MSSQL_SA_PASSWORD=Your_password123 \
  -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest

# Install the dotnet-ef and dotnet-counters versions pinned in dotnet-tools.json
dotnet tool restore

dotnet run --project BudgetApi
```

In Development the app applies migrations and seeds the database on startup. To apply migrations without running the app, run `dotnet ef database update --project BudgetApi`. Stop the Compose stack first, or the container above, because both use port 1433.

The API listens on `http://localhost:5090`, and Scalar is at http://localhost:5090/scalar.

Run the tests (no database needed):

```bash
dotnet test
```

## Try it

The examples use port 8080 (Docker Compose). Use 5090 with `dotnet run`.

```bash
curl -X POST http://localhost:8080/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"ada@example.com","password":"Passw0rd!"}'

# Copy the token from the response
TOKEN=<token>

curl http://localhost:8080/api/transactions -H "Authorization: Bearer $TOKEN"

curl -i -X POST http://localhost:8080/api/transactions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"type":"Expense","description":"Rent","amount":900.50}'

curl "http://localhost:8080/api/reports/summary?start=2026-01-01&end=2026-12-31" \
  -H "Authorization: Bearer $TOKEN"

curl http://localhost:8080/health
curl http://localhost:8080/health/ready
curl http://localhost:8080/health/live
```

The seeded users `ada@example.com` (6 transactions) and `alan@example.com` (4 transactions) can log in with `Passw0rd!`. The seed data is dated relative to the day you first start the app, so pick report dates around today. In Scalar, paste the token into the Authentication box to call the protected endpoints.

Watch the custom counters while you create transactions (with `dotnet run`):

```bash
dotnet dotnet-counters monitor --name BudgetApi --counters BudgetApi
```

## Endpoints

| Method | Route | Result |
|---|---|---|
| POST | /auth/register | 201, 400 |
| POST | /auth/login | 200, 401 |
| GET | /auth/me (token) | 200, 401, 404 |
| GET | /api/transactions?type=&from=&to=&search=&page=&pageSize= (token) | 200, 400, 401 |
| GET | /api/transactions/{id} (token, owner only) | 200, 401, 404 |
| POST | /api/transactions (token) | 201, 400, 401 |
| PUT | /api/transactions/{id} (token, owner only) | 200, 400, 401, 404 |
| DELETE | /api/transactions/{id} (token, owner only) | 204, 401, 404 |
| GET | /api/reports/summary?start={date}&end={date}&type={type} (token) | 200, 400, 401 |
| GET | /health | 200, 503 |
| GET | /health/ready | 200, 503 |
| GET | /health/live | 200 |

Every transaction belongs to a user. A request for another user's transaction returns `404`.

Data is stored in SQL Server, in the `BudgetDb` database.

## Next step

This is the last branch. The Azure deployment exercise starts from it and deploys the API to Azure App Service with Azure SQL Database.
