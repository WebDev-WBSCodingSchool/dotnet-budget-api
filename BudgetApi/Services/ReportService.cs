using BudgetApi.Data;
using BudgetApi.Dtos.Reports;
using BudgetApi.Dtos.Transactions;
using BudgetApi.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetApi.Services;

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _db;

    public ReportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<SummaryReportResponseDto> GetSummaryAsync(Guid userId, DateOnly start, DateOnly end, TransactionType? type)
    {
        // Both dates are inclusive: from start 00:00 UTC up to (not including) the day after end.
        var from = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var to = new DateTimeOffset(end.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        // The filters are shared by the two queries below. Only the user's own transactions count.
        var query = _db.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.Timestamp >= from && t.Timestamp < to);

        if (type is not null)
        {
            query = query.Where(t => t.Type == type);
        }

        // Query 1: the database adds up the amounts, one row per type.
        var totals = await query
            .GroupBy(t => t.Type)
            .Select(g => new { Type = g.Key, Total = g.Sum(t => t.Amount) })
            .ToListAsync();

        var totalIncome = totals.Where(t => t.Type == TransactionType.Income).Sum(t => t.Total);
        var totalExpense = totals.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Total);

        // Query 2: the transactions in the range, oldest first.
        var transactions = await query
            .OrderBy(t => t.Timestamp)
            .Select(t => new TransactionResponseDto(t.Id, t.Timestamp, t.Type, t.Description, t.Amount))
            .ToListAsync();

        return new SummaryReportResponseDto(
            start, end, type, totalIncome, totalExpense, totalIncome - totalExpense, transactions);
    }
}
