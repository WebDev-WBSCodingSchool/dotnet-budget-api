using BudgetApi.Data;
using BudgetApi.Models;
using BudgetApi.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BudgetApi.Tests.Unit;

public class ReportServiceTests
{
    // The report range used by most tests: 1 to 31 March 2026, both days included.
    private static readonly DateOnly Start = new(2026, 3, 1);
    private static readonly DateOnly End = new(2026, 3, 31);

    private readonly ApplicationDbContext _db;
    private readonly ReportService _service;

    public ReportServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _service = new ReportService(_db, NullLogger<ReportService>.Instance);
    }

    private async Task<User> AddUserAsync(string name)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            UserName = $"{name}@example.com",
            Email = $"{name}@example.com",
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task AddTransactionAsync(User owner, TransactionType type, decimal amount, DateTimeOffset timestamp)
    {
        _db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = owner.Id,
            Type = type,
            Description = $"{type} {amount}",
            Amount = amount,
            Timestamp = timestamp
        });
        await _db.SaveChangesAsync();
    }

    // Explicit UTC offsets, so the tests do not depend on the time zone of the machine.
    private static DateTimeOffset Utc(int month, int day, int hour = 12) =>
        new(2026, month, day, hour, 0, 0, TimeSpan.Zero);

    // Income 2500 + 150.75, expense 900.50 + 49.25, all inside March.
    private async Task<User> AddMarchTransactionsAsync()
    {
        var ada = await AddUserAsync("ada");
        await AddTransactionAsync(ada, TransactionType.Income, 2500m, Utc(3, 1));
        await AddTransactionAsync(ada, TransactionType.Expense, 900.50m, Utc(3, 2));
        await AddTransactionAsync(ada, TransactionType.Income, 150.75m, Utc(3, 14));
        await AddTransactionAsync(ada, TransactionType.Expense, 49.25m, Utc(3, 20));
        return ada;
    }

    [Fact]
    public async Task GetSummaryAsync_ForRange_ReturnsTotalsAndNet()
    {
        // Arrange
        var ada = await AddMarchTransactionsAsync();

        // Act
        var summary = await _service.GetSummaryAsync(ada.Id, Start, End, null);

        // Assert
        Assert.Equal(2650.75m, summary.TotalIncome);
        Assert.Equal(949.75m, summary.TotalExpense);
        Assert.Equal(1701.00m, summary.Net);
        Assert.Equal(4, summary.Transactions.Count);
    }

    [Fact]
    public async Task GetSummaryAsync_IgnoresTransactionsOutsideRange()
    {
        var ada = await AddMarchTransactionsAsync();
        // One second before the start and the first moment after the end.
        await AddTransactionAsync(ada, TransactionType.Income, 1000m, new DateTimeOffset(2026, 2, 28, 23, 59, 59, TimeSpan.Zero));
        await AddTransactionAsync(ada, TransactionType.Expense, 1000m, new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));

        var summary = await _service.GetSummaryAsync(ada.Id, Start, End, null);

        Assert.Equal(2650.75m, summary.TotalIncome);
        Assert.Equal(949.75m, summary.TotalExpense);
        Assert.Equal(4, summary.Transactions.Count);
    }

    [Fact]
    public async Task GetSummaryAsync_CountsFirstAndLastDayOfRange()
    {
        var ada = await AddUserAsync("ada");
        await AddTransactionAsync(ada, TransactionType.Income, 100m, Utc(3, 1, hour: 0));
        await AddTransactionAsync(ada, TransactionType.Expense, 30m, new DateTimeOffset(2026, 3, 31, 23, 59, 59, TimeSpan.Zero));

        var summary = await _service.GetSummaryAsync(ada.Id, Start, End, null);

        Assert.Equal(100m, summary.TotalIncome);
        Assert.Equal(30m, summary.TotalExpense);
        Assert.Equal(70m, summary.Net);
    }

    [Theory]
    [InlineData(TransactionType.Income, 2650.75, 0)]
    [InlineData(TransactionType.Expense, 0, 949.75)]
    public async Task GetSummaryAsync_WithTypeFilter_CountsOnlyThatType(
        TransactionType type, decimal expectedIncome, decimal expectedExpense)
    {
        var ada = await AddMarchTransactionsAsync();

        var summary = await _service.GetSummaryAsync(ada.Id, Start, End, type);

        Assert.Equal(type, summary.Type);
        Assert.Equal(expectedIncome, summary.TotalIncome);
        Assert.Equal(expectedExpense, summary.TotalExpense);
        Assert.All(summary.Transactions, t => Assert.Equal(type, t.Type));
    }

    [Fact]
    public async Task GetSummaryAsync_IgnoresOtherUsersTransactions()
    {
        var ada = await AddMarchTransactionsAsync();
        var alan = await AddUserAsync("alan");
        await AddTransactionAsync(alan, TransactionType.Income, 5000m, Utc(3, 10));

        var summary = await _service.GetSummaryAsync(ada.Id, Start, End, null);

        Assert.Equal(2650.75m, summary.TotalIncome);
        Assert.Equal(4, summary.Transactions.Count);
    }

    [Fact]
    public async Task GetSummaryAsync_WithNoTransactionsInRange_ReturnsZeroTotals()
    {
        var ada = await AddMarchTransactionsAsync();

        var summary = await _service.GetSummaryAsync(ada.Id, new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), null);

        Assert.Equal(0m, summary.TotalIncome);
        Assert.Equal(0m, summary.TotalExpense);
        Assert.Equal(0m, summary.Net);
        Assert.Empty(summary.Transactions);
    }
}
