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

    public async Task<IReadOnlyList<TransactionResponseDto>> GetAllAsync(TransactionFilter filter)
    {
        // Start with all transactions and add a Where for each filter that was sent.
        // Nothing runs against the database until ToListAsync.
        var query = _db.Transactions.AsNoTracking();

        if (filter.Type is not null)
        {
            query = query.Where(t => t.Type == filter.Type);
        }

        if (filter.From is not null)
        {
            var from = StartOfDay(filter.From.Value);
            query = query.Where(t => t.Timestamp >= from);
        }

        if (filter.To is not null)
        {
            // Include the whole "to" day.
            var to = StartOfDay(filter.To.Value.AddDays(1));
            query = query.Where(t => t.Timestamp < to);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // LIKE with % on both sides means "contains". In SQLite, LIKE ignores upper and lower case.
            var pattern = $"%{filter.Search}%";
            query = query.Where(t => EF.Functions.Like(t.Description, pattern));
        }

        // Sort before paging, so every page comes from the same order.
        return await query
            .OrderByDescending(t => t.Timestamp)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
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

    // The database compares points in time, so a calendar day starts at midnight UTC.
    private static DateTimeOffset StartOfDay(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static TransactionResponseDto ToDto(Transaction t) =>
        new(t.Id, t.Timestamp, t.Type, t.Description, t.Amount);
}
