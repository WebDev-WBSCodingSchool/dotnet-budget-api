using BudgetApi.Data;
using BudgetApi.Dtos.Transactions;
using BudgetApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetApi.Services;

// Stores transactions in the database through ApplicationDbContext.
public class TransactionService : ITransactionService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<TransactionService> _logger;
    private readonly BudgetMetrics _metrics;

    public TransactionService(ApplicationDbContext db, ILogger<TransactionService> logger, BudgetMetrics metrics)
    {
        _db = db;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<IReadOnlyList<TransactionResponseDto>> GetAllAsync(Guid userId, TransactionFilter filter)
    {
        // Start with the user's transactions and add a Where for each filter that was sent.
        // Nothing runs against the database until ToListAsync.
        var query = _db.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId);

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
            // LIKE with % on both sides means "contains". With SQL Server's default collation, LIKE ignores upper and lower case.
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

    public async Task<TransactionResponseDto?> GetByIdAsync(Guid userId, Guid id)
    {
        var transaction = await _db.Transactions
            .AsNoTracking()
            .Where(t => t.Id == id && t.UserId == userId)
            .Select(t => new TransactionResponseDto(t.Id, t.Timestamp, t.Type, t.Description, t.Amount))
            .FirstOrDefaultAsync();

        if (transaction is null)
        {
            _logger.LogWarning("User {UserId} requested transaction {TransactionId}, which does not exist or belongs to another user", userId, id);
        }

        return transaction;
    }

    public async Task<TransactionResponseDto> CreateAsync(Guid userId, CreateTransactionDto dto)
    {
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Timestamp = dto.Timestamp ?? DateTimeOffset.UtcNow,
            // Validation has already checked that Type is present.
            Type = dto.Type!.Value,
            Description = dto.Description,
            Amount = dto.Amount,
            UserId = userId
        };

        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync();

        // {TransactionId} and {UserId} are stored as separate properties, so the logs can be searched by them.
        _logger.LogInformation("User {UserId} created transaction {TransactionId}", userId, transaction.Id);
        _metrics.TransactionCreated();

        return ToDto(transaction);
    }

    public async Task<TransactionResponseDto?> UpdateAsync(Guid userId, Guid id, UpdateTransactionDto dto)
    {
        // A tracked query, so SaveChangesAsync writes the properties that changed.
        // Another user's transaction is not found, so the endpoint returns 404.
        var transaction = await _db.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (transaction is null)
        {
            _logger.LogWarning("User {UserId} tried to update transaction {TransactionId}, which does not exist or belongs to another user", userId, id);
            return null;
        }

        transaction.Type = dto.Type!.Value;
        transaction.Description = dto.Description;
        transaction.Amount = dto.Amount;
        transaction.Timestamp = dto.Timestamp!.Value;
        await _db.SaveChangesAsync();

        _logger.LogInformation("User {UserId} updated transaction {TransactionId}", userId, id);

        return ToDto(transaction);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id)
    {
        var transaction = await _db.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (transaction is null)
        {
            _logger.LogWarning("User {UserId} tried to delete transaction {TransactionId}, which does not exist or belongs to another user", userId, id);
            return false;
        }

        _db.Transactions.Remove(transaction);
        await _db.SaveChangesAsync();

        _logger.LogInformation("User {UserId} deleted transaction {TransactionId}", userId, id);
        return true;
    }

    // The database compares points in time, so a calendar day starts at midnight UTC.
    private static DateTimeOffset StartOfDay(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static TransactionResponseDto ToDto(Transaction t) =>
        new(t.Id, t.Timestamp, t.Type, t.Description, t.Amount);
}
