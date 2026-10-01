# Budget API: step 03, queries and seeding

Exercise: Budget API with DB, Part II

This repository holds the Budget API that you build in the ASP.NET Core exercises. Each exercise has its own branch. This branch is the finished state of **Budget API with DB, Part II** and the starting point for **Budget API: Multi-User**.

## What this step adds

- `GET /api/transactions` takes optional filters: `type`, `from`, `to`, `search` and paging with `page` and `pageSize` (1 to 100, default 20)
- `Dtos/Transactions/TransactionFilter.cs` collects the filters for `ITransactionService.GetAllAsync`
- `ReportService` uses the DbContext and computes the totals in the database with `GroupBy` and `Sum`
- `Data/DbSeeder.cs` adds sample transactions when the database is empty
- In Development the app applies pending migrations and runs the seeder on startup
- No model changes, so there is no new migration

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
  Data/        ApplicationDbContext.cs, DbSeeder.cs, Migrations/
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

To start again with a fresh database, stop the app and delete `BudgetApi/budget.db`. The seeder only adds data to an empty database, so delete a `budget.db` from step 02 to see the sample transactions.

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

curl "http://localhost:5090/api/transactions?type=Expense&search=rent&page=1&pageSize=5"
```

The seed data is dated relative to the day you first start the app, so pick report dates around today.

## Endpoints

| Method | Route | Result |
|---|---|---|
| GET | /api/transactions?type=&from=&to=&search=&page=&pageSize= | 200, 400 |
| GET | /api/transactions/{id} | 200, 404 |
| POST | /api/transactions | 201, 400 |
| PUT | /api/transactions/{id} | 200, 400, 404 |
| DELETE | /api/transactions/{id} | 204, 404 |
| GET | /api/reports/summary?start={date}&end={date}&type={type} | 200, 400 |

Data is stored in the SQLite file `BudgetApi/budget.db`, which is ignored by Git.

## Next step

`step-04-multi-user`
