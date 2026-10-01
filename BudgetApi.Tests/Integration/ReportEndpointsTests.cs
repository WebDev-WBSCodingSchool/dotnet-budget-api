using System.Net;
using System.Net.Http.Json;
using BudgetApi.Dtos.Reports;
using BudgetApi.Models;

namespace BudgetApi.Tests.Integration;

public class ReportEndpointsTests : IDisposable
{
    private const string MarchSummary = "/api/reports/summary?start=2026-03-01&end=2026-03-31";

    private readonly CustomWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static DateTimeOffset Utc(int month, int day) => new(2026, month, day, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Summary_WithoutUser_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(MarchSummary);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Summary_AsUser_ReturnsTotalsForRange()
    {
        var ada = await _factory.AddUserAsync("ada");
        await _factory.AddTransactionAsync(ada.Id, TransactionType.Income, 2500m, Utc(3, 1));
        await _factory.AddTransactionAsync(ada.Id, TransactionType.Expense, 900.50m, Utc(3, 2));
        await _factory.AddTransactionAsync(ada.Id, TransactionType.Expense, 100m, Utc(4, 2));
        var client = _factory.CreateClientAs(ada.Id);

        var response = await client.GetAsync(MarchSummary);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<SummaryReportResponseDto>(CustomWebApplicationFactory.JsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(2500m, summary.TotalIncome);
        Assert.Equal(900.50m, summary.TotalExpense);
        Assert.Equal(1599.50m, summary.Net);
        Assert.Equal(2, summary.Transactions.Count);
    }

    [Fact]
    public async Task Summary_IgnoresOtherUsersTransactions()
    {
        var ada = await _factory.AddUserAsync("ada");
        var alan = await _factory.AddUserAsync("alan");
        await _factory.AddTransactionAsync(ada.Id, TransactionType.Income, 2500m, Utc(3, 1));
        await _factory.AddTransactionAsync(alan.Id, TransactionType.Income, 5000m, Utc(3, 1));
        await _factory.AddTransactionAsync(alan.Id, TransactionType.Expense, 300m, Utc(3, 5));
        var client = _factory.CreateClientAs(ada.Id);

        var summary = await client.GetFromJsonAsync<SummaryReportResponseDto>(MarchSummary, CustomWebApplicationFactory.JsonOptions);

        Assert.NotNull(summary);
        Assert.Equal(2500m, summary.TotalIncome);
        Assert.Equal(0m, summary.TotalExpense);
        Assert.Single(summary.Transactions);
    }

    [Theory]
    [InlineData("/api/reports/summary?start=2026-03-31&end=2026-03-01")]
    [InlineData("/api/reports/summary?start=31-03-2026&end=2026-04-30")]
    [InlineData("/api/reports/summary?start=2026-03-01")]
    public async Task Summary_WithInvalidQuery_Returns400(string url)
    {
        var ada = await _factory.AddUserAsync("ada");
        var client = _factory.CreateClientAs(ada.Id);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
