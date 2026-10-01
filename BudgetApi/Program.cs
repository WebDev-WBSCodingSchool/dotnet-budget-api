using System.Text.Json.Serialization;
using BudgetApi.Endpoints;
using BudgetApi.Errors;
using BudgetApi.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Read and write enums as text ("Income") instead of numbers (0).
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Validates DataAnnotations on the DTOs before the handlers run.
builder.Services.AddValidation();

// Error responses use the ProblemDetails format.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// The transactions live in memory inside TransactionService, so it is a singleton:
// one instance, and one dictionary, shared by every request.
// ReportService depends on it and is a singleton too (a singleton must not depend on a scoped service).
builder.Services.AddSingleton<ITransactionService, TransactionService>();
builder.Services.AddSingleton<IReportService, ReportService>();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // The OpenAPI document: /openapi/v1.json
    app.MapOpenApi();

    // Interactive API reference: /scalar
    app.MapScalarApiReference();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapTransactionEndpoints();
app.MapReportEndpoints();

app.Run();
