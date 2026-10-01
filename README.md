# Budget API: step 01, in-memory API

Exercise: Budget API

This repository holds the Budget API that you build in the ASP.NET Core exercises. Each exercise has its own branch. This branch is the finished state of the **Budget API** exercise and the starting point for **Budget API with DB, Part I**.

## What this step adds

- A solution (`BudgetApi.slnx`) with one project in `BudgetApi/`
- `Models/Transaction.cs` and `Models/TransactionType.cs` (`Income`, `Expense`)
- Request and response DTOs in `Dtos/Transactions` and `Dtos/Reports`, validated with DataAnnotations and `AddValidation()`
- `ITransactionService` and `IReportService` with in-memory implementations, registered as singletons
- CRUD endpoints for transactions and a summary report for a date range, grouped with `MapGroup`
- Enums sent and received as text with `JsonStringEnumConverter`
- ProblemDetails for every error, with `GlobalExceptionHandler`
- An OpenAPI document and a Scalar API reference in Development

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
```

## Run it

Requires the .NET 10 SDK. Run these commands from the repository root.

```bash
dotnet run --project BudgetApi
```

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

Data is kept in memory and is lost when the app stops.

## Next step

`step-02-ef-core`
