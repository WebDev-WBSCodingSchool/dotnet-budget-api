using BudgetApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BudgetApi.Data;

public class ApplicationDbContext : DbContext
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
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Transaction>(transaction =>
        {
            transaction.Property(t => t.Description).HasMaxLength(200).IsRequired();

            // 18 digits, 2 of them after the decimal point. SQLite ignores this and stores
            // decimals as text, but SQL Server uses it for the column type.
            transaction.Property(t => t.Amount).HasPrecision(18, 2);

            // Store the enum as its name ("Income", "Expense") instead of a number.
            transaction.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);

            // Reports filter by date, so an index on Timestamp speeds them up.
            transaction.HasIndex(t => t.Timestamp);
        });
    }
}
