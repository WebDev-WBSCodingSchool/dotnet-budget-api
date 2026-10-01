using BudgetApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetApi.Data;

// Adds sample transactions so the API has data to show during development.
public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        // Only seed an empty database. Running the app again does not add duplicates.
        if (await db.Transactions.AnyAsync())
        {
            return;
        }

        // Dates relative to today, so the sample data always covers the last two months.
        var today = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);

        db.Transactions.AddRange(
            Create(today.AddDays(-45), TransactionType.Income, "Salary", 2500.00m),
            Create(today.AddDays(-44), TransactionType.Expense, "Rent", 950.00m),
            Create(today.AddDays(-40), TransactionType.Expense, "Groceries", 84.20m),
            Create(today.AddDays(-32), TransactionType.Expense, "Electricity bill", 61.75m),
            Create(today.AddDays(-15), TransactionType.Income, "Salary", 2500.00m),
            Create(today.AddDays(-14), TransactionType.Expense, "Rent", 950.00m),
            Create(today.AddDays(-9), TransactionType.Income, "Freelance project", 400.00m),
            Create(today.AddDays(-6), TransactionType.Expense, "Groceries", 57.35m),
            Create(today.AddDays(-2), TransactionType.Expense, "Cinema tickets", 24.00m));

        await db.SaveChangesAsync();
    }

    // Every sample transaction happens at noon UTC on its day.
    private static Transaction Create(DateTimeOffset day, TransactionType type, string description, decimal amount) =>
        new()
        {
            Id = Guid.NewGuid(),
            Timestamp = day.AddHours(12),
            Type = type,
            Description = description,
            Amount = amount
        };
}
