using System.Text;
using System.Text.Json.Serialization;
using BudgetApi.Data;
using BudgetApi.Endpoints;
using BudgetApi.Errors;
using BudgetApi.Models;
using BudgetApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
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

// UserManager<User>, the password hasher and the validators. No cookies or UI.
builder.Services
    .AddIdentityCore<User>(options =>
    {
        // Registration uses the email as the user name, so emails must be unique.
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Authentication: read the "Authorization: Bearer <token>" header and check the token.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep the claim names from the token as they are ("sub", "email", "name").
        options.MapInboundClaims = false;

        var key = builder.Configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// Authorization: decides whether the authenticated user may call an endpoint.
builder.Services.AddAuthorization();

// All services use the DbContext (AuthService through UserManager), so they are scoped.
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddOpenApi(options =>
{
    // Describe the Bearer scheme, so Scalar shows an "Authentication" box for the token.
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Paste the token returned by POST /auth/login."
        };
        return Task.CompletedTask;
    });

    // Mark the endpoints that call RequireAuthorization() as needing the Bearer token.
    options.AddOperationTransformer((operation, context, cancellationToken) =>
    {
        var requiresAuth = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<IAuthorizeData>()
            .Any();

        if (requiresAuth)
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
            });
        }
        return Task.CompletedTask;
    });
});

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
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

    // Apply any pending migrations, then add sample data if the database is empty.
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db, userManager);
}

app.UseExceptionHandler();
app.UseStatusCodePages();

// Authentication first (who are you?), then authorization (are you allowed?).
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapTransactionEndpoints();
app.MapReportEndpoints();

app.Run();
