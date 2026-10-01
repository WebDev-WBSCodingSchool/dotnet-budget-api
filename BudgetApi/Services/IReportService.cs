using BudgetApi.Dtos.Reports;
using BudgetApi.Models;

namespace BudgetApi.Services;

public interface IReportService
{
    // type is null for "all transactions".
    Task<SummaryReportResponseDto> GetSummaryAsync(Guid userId, DateOnly start, DateOnly end, TransactionType? type);
}
