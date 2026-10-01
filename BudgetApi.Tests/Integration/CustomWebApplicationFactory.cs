using System.Text.Json;
using System.Text.Json.Serialization;
using BudgetApi.Data;
using BudgetApi.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BudgetApi.Tests.Integration;

// Starts the real API in memory, with two changes for testing:
// the SQL Server database is replaced by an in-memory one, and a fake auth scheme is added.
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // The API reads and writes enums as text ("Income"). The tests use the same JSON settings
    // with PostAsJsonAsync, PutAsJsonAsync and ReadFromJsonAsync.
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // The settings the app reads from appsettings.Development.json or user secrets,
    // which are not loaded in the Testing environment.
    public const string JwtKey = "integration-test-signing-key-that-is-longer-than-32-bytes";

    // Each factory gets its own database, so tests that use different factories never share data.
    private readonly string _databaseName = $"BudgetApiIntegrationTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Any name other than Development skips the Development-only block in Program.cs
        // (Scalar, MigrateAsync and seeding), so tests start with an empty database.
        builder.UseEnvironment("Testing");

        // Give the app a signing key for the real JWT tests.
        builder.UseSetting("Jwt:Key", JwtKey);

        // ConfigureTestServices runs after Program.cs has registered its services,
        // so anything registered here replaces or adds to the real registrations.
        builder.ConfigureTestServices(services =>
        {
            // Remove the SQL Server configuration that AddDbContext added in Program.cs ...
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();

            // ... and register the in-memory provider instead.
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            // Add the fake scheme next to the real JWT scheme.
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            // RequireAuthorization() uses the default policy. Let it accept a user from either scheme.
            services.Configure<AuthorizationOptions>(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder(
                        JwtBearerDefaults.AuthenticationScheme, TestAuthHandler.SchemeName)
                    .RequireAuthenticatedUser()
                    .Build();
            });
        });
    }

    // A client that is logged in as the given user through the fake scheme.
    public HttpClient CreateClientAs(Guid userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        return client;
    }

    // Adds a user straight to the test database and returns it.
    public async Task<User> AddUserAsync(string name)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            UserName = $"{name}@example.com",
            Email = $"{name}@example.com",
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    // Adds a transaction straight to the test database and returns its id.
    public async Task<Guid> AddTransactionAsync(
        Guid userId, TransactionType type, decimal amount, DateTimeOffset timestamp, string description = "Test")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Description = description,
            Amount = amount,
            Timestamp = timestamp
        };

        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();
        return transaction.Id;
    }
}
