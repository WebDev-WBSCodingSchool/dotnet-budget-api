using BudgetApi.Data;
using BudgetApi.Dtos.Transactions;
using BudgetApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetApi.Services;

// Stores transactions in the database through ApplicationDbContext.
public class TransactionService : ITransactionService
{
    private readonly ApplicationDbContext _db;

    public TransactionService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TransactionResponseDto>> GetAllAsync()
    {
        // Read-only query: no tracking, and only the columns the DTO needs.
        return await _db.Transactions
            .AsNoTracking()
            .OrderByDescending(t => t.Timestamp)
            .Select(t => new TransactionResponseDto(t.Id, t.Timestamp, t.Type, t.Description, t.Amount))
            .ToListAsync();
    }

    // Both dates are inclusive. The database compares points in time, so each day
    // runs from midnight UTC to the next midnight UTC.
    public async Task<IReadOnlyList<TransactionResponseDto>> GetInRangeAsync(DateOnly start, DateOnly end)
    {
        var from = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var to = new DateTimeOffset(end.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        return await _db.Transactions
            .AsNoTracking()
            .Where(t => t.Timestamp >= from && t.Timestamp < to)
            .OrderBy(t => t.Timestamp)
            .Select(t => new TransactionResponseDto(t.Id, t.Timestamp, t.Type, t.Description, t.Amount))
            .ToListAsync();
    }

    public async Task<TransactionResponseDto?> GetByIdAsync(Guid id)
    {
        return await _db.Transactions
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TransactionResponseDto(t.Id, t.Timestamp, t.Type, t.Description, t.Amount))
            .FirstOrDefaultAsync();
    }

    public async Task<TransactionResponseDto> CreateAsync(CreateTransactionDto dto)
    {
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Timestamp = dto.Timestamp ?? DateTimeOffset.UtcNow,
            // Validation has already checked that Type is present.
            Type = dto.Type!.Value,
            Description = dto.Description,
            Amount = dto.Amount
        };

        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync();

        return ToDto(transaction);
    }

    public async Task<TransactionResponseDto?> UpdateAsync(Guid id, UpdateTransactionDto dto)
    {
        // FindAsync returns a tracked entity. SaveChangesAsync writes the properties that changed.
        var transaction = await _db.Transactions.FindAsync(id);
        if (transaction is null)
        {
            return null;
        }

        transaction.Type = dto.Type!.Value;
        transaction.Description = dto.Description;
        transaction.Amount = dto.Amount;
        transaction.Timestamp = dto.Timestamp!.Value;
        await _db.SaveChangesAsync();

        return ToDto(transaction);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var transaction = await _db.Transactions.FindAsync(id);
        if (transaction is null)
        {
            return false;
        }

        _db.Transactions.Remove(transaction);
        await _db.SaveChangesAsync();
        return true;
    }

    private static TransactionResponseDto ToDto(Transaction t) =>
        new(t.Id, t.Timestamp, t.Type, t.Description, t.Amount);
}
