namespace BudgetApi.Models;

public class Transaction
{
    public Guid Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public TransactionType Type { get; set; }
    public string Description { get; set; } = string.Empty;

    // Always positive. Type decides whether it adds to or subtracts from the balance.
    public decimal Amount { get; set; }

    // Foreign key to the owner of the transaction.
    public Guid UserId { get; set; }

    // Navigation property to the owner.
    public User User { get; set; } = null!;
}
