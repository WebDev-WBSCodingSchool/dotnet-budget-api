using BudgetApi.Data;
using BudgetApi.Dtos.Transactions;
using BudgetApi.Models;
using BudgetApi.Services;

namespace BudgetApi.Tests.Unit;

public class TransactionServiceTests
{
    private static readonly DateTimeOffset March14 = new(2026, 3, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly ApplicationDbContext _db;
    private readonly TransactionService _service;

    // xUnit creates a new instance of this class for every test,
    // so each test gets its own database and service.
    public TransactionServiceTests()
    {
        _db = TestDbContextFactory.Create();
        _service = new TransactionService(_db);
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

    private async Task<Transaction> AddTransactionAsync(
        User owner, TransactionType type, string description, decimal amount, DateTimeOffset timestamp)
    {
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = owner.Id,
            Type = type,
            Description = description,
            Amount = amount,
            Timestamp = timestamp
        };
        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync();
        return transaction;
    }

    private static TransactionFilter AllTransactions() => new(null, null, null, null, 1, 20);

    [Fact]
    public async Task CreateAsync_ForUser_SavesTransactionAndReturnsIt()
    {
        // Arrange
        var ada = await AddUserAsync("ada");
        var dto = new CreateTransactionDto(TransactionType.Expense, "Rent", 900.50m, March14);

        // Act
        var result = await _service.CreateAsync(ada.Id, dto);

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(TransactionType.Expense, result.Type);
        Assert.Equal("Rent", result.Description);
        Assert.Equal(900.50m, result.Amount);
        Assert.Equal(March14, result.Timestamp);

        var saved = Assert.Single(_db.Transactions);
        Assert.Equal(result.Id, saved.Id);
        Assert.Equal(ada.Id, saved.UserId);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyTheUsersTransactions()
    {
        var ada = await AddUserAsync("ada");
        var alan = await AddUserAsync("alan");
        await AddTransactionAsync(ada, TransactionType.Income, "Salary", 2500m, March14);
        await AddTransactionAsync(ada, TransactionType.Expense, "Rent", 900m, March14.AddDays(1));
        await AddTransactionAsync(alan, TransactionType.Expense, "Books", 40m, March14);

        var result = await _service.GetAllAsync(ada.Id, AllTransactions());

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, t => t.Description == "Books");
    }

    [Fact]
    public async Task GetByIdAsync_ForOwner_ReturnsTransaction()
    {
        var ada = await AddUserAsync("ada");
        var salary = await AddTransactionAsync(ada, TransactionType.Income, "Salary", 2500m, March14);

        var result = await _service.GetByIdAsync(ada.Id, salary.Id);

        Assert.NotNull(result);
        Assert.Equal("Salary", result.Description);
    }

    [Fact]
    public async Task GetByIdAsync_ForAnotherUser_ReturnsNull()
    {
        var ada = await AddUserAsync("ada");
        var alan = await AddUserAsync("alan");
        var salary = await AddTransactionAsync(ada, TransactionType.Income, "Salary", 2500m, March14);

        var result = await _service.GetByIdAsync(alan.Id, salary.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
    {
        var ada = await AddUserAsync("ada");

        var result = await _service.GetByIdAsync(ada.Id, Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_ForOwner_ReplacesFieldsAndSaves()
    {
        var ada = await AddUserAsync("ada");
        var rent = await AddTransactionAsync(ada, TransactionType.Expense, "Rent", 900m, March14);
        var newTimestamp = March14.AddDays(1);
        var dto = new UpdateTransactionDto(TransactionType.Expense, "Rent March", 950m, newTimestamp);

        var result = await _service.UpdateAsync(ada.Id, rent.Id, dto);

        Assert.NotNull(result);
        Assert.Equal("Rent March", result.Description);
        Assert.Equal(950m, result.Amount);

        var saved = await _db.Transactions.FindAsync(rent.Id);
        Assert.NotNull(saved);
        Assert.Equal("Rent March", saved.Description);
        Assert.Equal(950m, saved.Amount);
        Assert.Equal(newTimestamp, saved.Timestamp);
    }

    [Fact]
    public async Task UpdateAsync_WithUnknownId_ReturnsNull()
    {
        var ada = await AddUserAsync("ada");
        var dto = new UpdateTransactionDto(TransactionType.Expense, "Rent", 950m, March14);

        var result = await _service.UpdateAsync(ada.Id, Guid.NewGuid(), dto);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_ForAnotherUser_ReturnsNullAndChangesNothing()
    {
        var ada = await AddUserAsync("ada");
        var alan = await AddUserAsync("alan");
        var rent = await AddTransactionAsync(ada, TransactionType.Expense, "Rent", 900m, March14);
        var dto = new UpdateTransactionDto(TransactionType.Income, "Changed", 1m, March14);

        var result = await _service.UpdateAsync(alan.Id, rent.Id, dto);

        Assert.Null(result);
        var saved = await _db.Transactions.FindAsync(rent.Id);
        Assert.NotNull(saved);
        Assert.Equal("Rent", saved.Description);
        Assert.Equal(900m, saved.Amount);
        Assert.Equal(TransactionType.Expense, saved.Type);
    }

    [Fact]
    public async Task DeleteAsync_ForOwner_RemovesTransactionAndReturnsTrue()
    {
        var ada = await AddUserAsync("ada");
        var rent = await AddTransactionAsync(ada, TransactionType.Expense, "Rent", 900m, March14);

        var deleted = await _service.DeleteAsync(ada.Id, rent.Id);

        Assert.True(deleted);
        Assert.Empty(_db.Transactions);
    }

    [Fact]
    public async Task DeleteAsync_WithUnknownId_ReturnsFalse()
    {
        var ada = await AddUserAsync("ada");

        var deleted = await _service.DeleteAsync(ada.Id, Guid.NewGuid());

        Assert.False(deleted);
    }

    [Fact]
    public async Task DeleteAsync_ForAnotherUser_ReturnsFalseAndKeepsTransaction()
    {
        var ada = await AddUserAsync("ada");
        var alan = await AddUserAsync("alan");
        var rent = await AddTransactionAsync(ada, TransactionType.Expense, "Rent", 900m, March14);

        var deleted = await _service.DeleteAsync(alan.Id, rent.Id);

        Assert.False(deleted);
        Assert.Single(_db.Transactions);
    }
}
