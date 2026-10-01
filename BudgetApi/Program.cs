using System.Text.Json.Serialization;
using BudgetApi.Data;
using BudgetApi.Endpoints;
using BudgetApi.Errors;
using BudgetApi.Services;
using Microsoft.EntityFrameworkCore;
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

// The DbContext is registered as scoped: each request gets its own instance.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Both services use the DbContext, so they are scoped too.
// A singleton must not depend on a scoped service.
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IReportService, ReportService>();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // The OpenAPI document: /openapi/v1.json
    app.MapOpenApi();

    // Interactive API reference: /scalar
    app.MapScalarApiReference();

    // The DbContext is scoped, and no request is running yet, so create a scope by hand.
    // The scope and the DbContext are disposed at the end of this block.
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Apply any pending migrations, then add sample data if the database is empty.
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapTransactionEndpoints();
app.MapReportEndpoints();

app.Run();
