# Budget API: step 02, EF Core with SQLite

Exercise: Budget API with DB, Part I

This repository holds the Budget API that you build in the ASP.NET Core exercises. Each exercise has its own branch. This branch is the finished state of **Budget API with DB, Part I** and the starting point for **Budget API with DB, Part II**.

## What this step adds

- `Microsoft.EntityFrameworkCore.Sqlite` and `Microsoft.EntityFrameworkCore.Design` 10.0.x
- The `dotnet-ef` tool in a local tool manifest, `dotnet-tools.json`
- `Data/ApplicationDbContext.cs` with a `DateTimeOffset` value converter, the enum stored as text, a decimal precision for `Amount` and an index on `Timestamp`
- A `DefaultConnection` connection string in `appsettings.json`
- The DbContext, `TransactionService` and `ReportService` registered as scoped
- `TransactionService` reads and writes the database. `GetInRangeAsync` compares points in time, so each day runs from midnight UTC to the next midnight UTC
- The `InitialCreate` migration in `Data/Migrations`
- The endpoints, DTOs, interfaces and `ReportService` are unchanged

## Project layout

```
BudgetApi.slnx
BudgetApi/
  Program.cs
  Endpoints/   TransactionEndpoints.cs, ReportEndpoints.cs
  Errors/      GlobalExceptionHandler.cs
  Dtos/        Transactions/, Reports/
  Models/      Transaction.cs, TransactionType.cs
  Services/    ITransactionService.cs, TransactionService.cs, IReportService.cs, ReportService.cs
  Data/        ApplicationDbContext.cs, Migrations/
dotnet-tools.json
```

## Run it

Requires the .NET 10 SDK. Run these commands from the repository root.

```bash
# Install the dotnet-ef version pinned in dotnet-tools.json
dotnet tool restore

# Create the database from the migrations
dotnet ef database update --project BudgetApi

dotnet run --project BudgetApi
```

To start again with a fresh database, stop the app and delete `BudgetApi/budget.db`, then run `dotnet ef database update --project BudgetApi` again.

The API listens on `http://localhost:5090`.

- OpenAPI document: http://localhost:5090/openapi/v1.json
- Scalar API reference: http://localhost:5090/scalar

```bash
curl -i -X POST http://localhost:5090/api/transactions \
  -H "Content-Type: application/json" \
  -d '{"type":"Income","description":"Salary","amount":2500,"timestamp":"2026-03-01T09:00:00+01:00"}'

curl -X POST http://localhost:5090/api/transactions \
  -H "Content-Type: application/json" \
  -d '{"type":"Expense","description":"Rent","amount":900.50,"timestamp":"2026-03-02T10:00:00+01:00"}'

curl "http://localhost:5090/api/reports/summary?start=2026-03-01&end=2026-03-31"
```

## Endpoints

| Method | Route | Result |
|---|---|---|
| GET | /api/transactions | 200 |
| GET | /api/transactions/{id} | 200, 404 |
| POST | /api/transactions | 201, 400 |
| PUT | /api/transactions/{id} | 200, 400, 404 |
| DELETE | /api/transactions/{id} | 204, 404 |
| GET | /api/reports/summary?start={date}&end={date}&type={type} | 200, 400 |

Data is stored in the SQLite file `BudgetApi/budget.db`, which is ignored by Git.

## Next step

`step-03-queries`
