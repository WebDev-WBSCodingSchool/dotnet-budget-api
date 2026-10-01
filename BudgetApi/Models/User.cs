using Microsoft.AspNetCore.Identity;

namespace BudgetApi.Models;

// IdentityUser<Guid> already has Id, UserName, Email, PasswordHash and more.
public class User : IdentityUser<Guid>
{
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation property: the transactions that belong to this user.
    public List<Transaction> Transactions { get; set; } = [];
}
