using System.Diagnostics.Metrics;

namespace BudgetApi.Services;

// Custom metrics for the Budget API. Registered as a singleton, because a Meter
// and its instruments should live as long as the app.
public class BudgetMetrics
{
    public const string MeterName = "BudgetApi";

    private readonly Counter<long> _transactionsCreated;
    private readonly Counter<long> _loginsFailed;

    // IMeterFactory is provided by ASP.NET Core. Meters created through it are
    // disposed with the app and can be isolated in tests.
    public BudgetMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _transactionsCreated = meter.CreateCounter<long>(
            "budgetapi.transactions.created",
            unit: "{transaction}",
            description: "Number of transactions created");

        _loginsFailed = meter.CreateCounter<long>(
            "budgetapi.logins.failed",
            unit: "{login}",
            description: "Number of failed login attempts");
    }

    public void TransactionCreated() => _transactionsCreated.Add(1);

    public void LoginFailed() => _loginsFailed.Add(1);
}
