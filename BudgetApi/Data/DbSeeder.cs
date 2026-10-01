using BudgetApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BudgetApi.Data;

// Adds two users with sample transactions so the API has data to show during development.
public static class DbSeeder
{
    // Every seeded user gets this password, so you can log in as them while developing.
    public const string DevPassword = "Passw0rd!";

    public static async Task SeedAsync(ApplicationDbContext db, UserManager<User> userManager)
    {
        // Only seed an empty database. Running the app again does not add duplicates.
        if (await db.Users.AnyAsync())
        {
            return;
        }

        var ada = await CreateUserAsync(userManager, "Ada Lovelace", "ada@example.com");
        var alan = await CreateUserAsync(userManager, "Alan Turing", "alan@example.com");

        // Dates relative to today, so the sample data always covers the last two months.
        var today = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);

        db.Transactions.AddRange(
            Create(ada, today.AddDays(-45), TransactionType.Income, "Salary", 2500.00m),
            Create(ada, today.AddDays(-44), TransactionType.Expense, "Rent", 950.00m),
            Create(ada, today.AddDays(-40), TransactionType.Expense, "Groceries", 84.20m),
            Create(ada, today.AddDays(-15), TransactionType.Income, "Salary", 2500.00m),
            Create(ada, today.AddDays(-14), TransactionType.Expense, "Rent", 950.00m),
            Create(ada, today.AddDays(-6), TransactionType.Expense, "Groceries", 57.35m),
            Create(alan, today.AddDays(-30), TransactionType.Income, "Salary", 3100.00m),
            Create(alan, today.AddDays(-29), TransactionType.Expense, "Rent", 1200.00m),
            Create(alan, today.AddDays(-9), TransactionType.Income, "Freelance project", 400.00m),
            Create(alan, today.AddDays(-2), TransactionType.Expense, "Cinema tickets", 24.00m));

        await db.SaveChangesAsync();
    }

    // Users go through UserManager so their passwords are hashed like any registered user.
    private static async Task<User> CreateUserAsync(UserManager<User> userManager, string name, string email)
    {
        var user = new User
        {
            Name = name,
            UserName = email,
            Email = email,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, DevPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Could not seed user {email}: {errors}");
        }

        return user;
    }

    // Every sample transaction happens at noon UTC on its day.
    private static Transaction Create(
        User owner, DateTimeOffset day, TransactionType type, string description, decimal amount) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = owner.Id,
            Timestamp = day.AddHours(12),
            Type = type,
            Description = description,
            Amount = amount
        };
}
