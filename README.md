# Budget API: step 05, unit tests

Exercise: Budget API unit tests

This repository holds the Budget API that you build in the ASP.NET Core exercises. Each exercise has its own branch. This branch is the finished state of the Budget API unit testing exercise and the starting point for the integration testing exercise.

## What this step adds

- A test project, `BudgetApi.Tests`, created with `dotnet new xunit` and added to `BudgetApi.slnx`
- Packages: Moq 4.21.0 and `Microsoft.EntityFrameworkCore.InMemory` 10.0.x
- `Unit/TestDbContextFactory.cs` creates an in-memory `ApplicationDbContext` with a new database name for every test
- `Unit/TransactionServiceTests.cs` tests create, list, get, update and delete, including another user's transactions and unknown ids
- `Unit/ReportServiceTests.cs` checks the totals, the range boundaries, the `type` filter, other users' transactions and an empty range
- `Unit/AuthServiceTests.cs` tests `AuthService` with a mocked `UserManager<User>` (Moq) and checks the `sub` claim of the issued token
- No changes to the API project

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
  Unit/        TestDbContextFactory.cs, TransactionServiceTests.cs, ReportServiceTests.cs, AuthServiceTests.cs
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

`step-06-integration-tests`
