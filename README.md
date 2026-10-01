# Budget API: step 06, integration tests

Exercise: Budget API integration tests

This repository holds the Budget API that you build in the ASP.NET Core exercises. Each exercise has its own branch. This branch is the finished state of the Budget API integration testing exercise and the starting point for the Budget API observability exercise.

## What this step adds

- `Microsoft.AspNetCore.Mvc.Testing` 10.0.x in `BudgetApi.Tests`
- `Integration/CustomWebApplicationFactory.cs` starts the API in memory in the `Testing` environment. It sets a test `Jwt:Key`, replaces the SQLite `DbContext` registration with an in-memory database unique to each factory, and adds helpers to create clients and test data
- `Integration/TestAuthHandler.cs`, a fake authentication scheme that logs a test in as the user named in the `X-Test-UserId` header
- `Integration/TransactionEndpointsTests.cs`: `401` without a user, `201` with a `Location` header, `400` for invalid bodies, and `404` when another user reads, updates or deletes a transaction
- `Integration/ReportEndpointsTests.cs`: totals for a range, other users' transactions ignored, and `400` for an invalid query
- `Integration/AuthEndpointsTests.cs`: a full register, login and Bearer flow with a real JWT, an invalid token, a wrong password, a weak password and a taken email
- The tests use JSON options with `JsonStringEnumConverter`, so they read the enums the API writes as text
- No changes to the API project. .NET 10 makes the `Program` class public, so `WebApplicationFactory<Program>` works without a `public partial class Program` line

## The signing key

`appsettings.Development.json` contains a development key, so the project runs straight after cloning. HMAC-SHA256 needs a key of at least 32 bytes; this one is 64 characters.

In a real project the key must not be committed. Remove it from `appsettings.Development.json` and store it with the Secret Manager, as the exercise does:

```bash
dotnet user-secrets set "Jwt:Key" "$(openssl rand -hex 32)" --project BudgetApi
```

In production, set it as an environment variable (`Jwt__Key`) or in a key vault.

## Project layout

```
BudgetApi.slnx
BudgetApi/
  Program.cs
  Endpoints/   AuthEndpoints.cs, TransactionEndpoints.cs, ReportEndpoints.cs, ClaimsPrincipalExtensions.cs
  Errors/      GlobalExceptionHandler.cs
  Dtos/        Auth/, Users/, Transactions/, Reports/
  Models/      User.cs, Transaction.cs, TransactionType.cs
  Services/    IAuthService.cs, AuthService.cs, ITransactionService.cs, TransactionService.cs, IReportService.cs, ReportService.cs
  Data/        ApplicationDbContext.cs, DbSeeder.cs, Migrations/
BudgetApi.Tests/
  Unit/          TestDbContextFactory.cs, TransactionServiceTests.cs, ReportServiceTests.cs, AuthServiceTests.cs
  Integration/   CustomWebApplicationFactory.cs, TestAuthHandler.cs, TransactionEndpointsTests.cs, ReportEndpointsTests.cs, AuthEndpointsTests.cs
dotnet-tools.json
```

## Run it

Requires the .NET 10 SDK. Run these commands from the repository root.

```bash
# Install the dotnet-ef version pinned in dotnet-tools.json
dotnet tool restore

dotnet run --project BudgetApi
```

In Development the app applies migrations and seeds the database on startup. To apply migrations without running the app:

```bash
dotnet ef database update --project BudgetApi
```

To start again with a fresh database, stop the app and delete `BudgetApi/budget.db`.

If you have a `budget.db` from step 03, its transactions have no owner, so the `AddUsers` migration fails on it. Delete `BudgetApi/budget.db` (or run `dotnet ef database drop --project BudgetApi --force`) and start the app again.

Run the tests:

```bash
dotnet test
```

The API listens on `http://localhost:5090`.

- OpenAPI document: http://localhost:5090/openapi/v1.json
- Scalar API reference: http://localhost:5090/scalar

```bash
curl -X POST http://localhost:5090/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"ada@example.com","password":"Passw0rd!"}'

# Copy the token from the response
TOKEN=<token>

curl http://localhost:5090/api/transactions -H "Authorization: Bearer $TOKEN"

curl -i -X POST http://localhost:5090/api/transactions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"type":"Expense","description":"Rent","amount":900.50}'

curl "http://localhost:5090/api/reports/summary?start=2026-01-01&end=2026-12-31" \
  -H "Authorization: Bearer $TOKEN"
```

The seeded users `ada@example.com` (6 transactions) and `alan@example.com` (4 transactions) can log in with `Passw0rd!`. The seed data is dated relative to the day you first start the app, so pick report dates around today.

In Scalar, paste the token into the Authentication box to call the protected endpoints.

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

Every transaction belongs to a user. A request for another user's transaction returns `404`.

Data is stored in the SQLite file `BudgetApi/budget.db`, which is ignored by Git.

## Next step

`step-07-observability`
