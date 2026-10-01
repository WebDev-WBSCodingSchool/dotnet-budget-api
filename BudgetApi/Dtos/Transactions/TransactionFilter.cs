using BudgetApi.Models;

namespace BudgetApi.Dtos.Transactions;

// The optional filters of GET /api/transactions, collected in one object for the service.
public record TransactionFilter(
    TransactionType? Type,
    DateOnly? From,
    DateOnly? To,
    string? Search,
    int Page,
    int PageSize);
