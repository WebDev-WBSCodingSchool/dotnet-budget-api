using BudgetApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BudgetApi.Data;

// IdentityDbContext adds the Identity tables (AspNetUsers, AspNetRoles, ...) and a Users DbSet.
public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite has no DateTimeOffset type. Storing it as a number lets SQLite
        // compare and sort timestamps, which the reports need.
        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configures the Identity tables. Keep this call first.
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(user =>
        {
            user.Property(u => u.Name).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<Transaction>(transaction =>
        {
            transaction.Property(t => t.Description).HasMaxLength(200).IsRequired();

            // 18 digits, 2 of them after the decimal point. SQLite ignores this and stores
            // decimals as text, but SQL Server uses it for the column type.
            transaction.Property(t => t.Amount).HasPrecision(18, 2);

            // Store the enum as its name ("Income", "Expense") instead of a number.
            transaction.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);

            // Every query filters by user and most by date, so index both together.
            transaction.HasIndex(t => new { t.UserId, t.Timestamp });

            // One user has many transactions. Deleting a user deletes their transactions.
            transaction.HasOne(t => t.User)
                .WithMany(u => u.Transactions)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
