namespace BudgetApi.Models;

public class Transaction
{
    public Guid Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public TransactionType Type { get; set; }
    public string Description { get; set; } = string.Empty;

    // Always positive. Type decides whether it adds to or subtracts from the balance.
    public decimal Amount { get; set; }
}
