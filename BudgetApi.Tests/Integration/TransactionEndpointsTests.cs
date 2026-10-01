using System.Net;
using System.Net.Http.Json;
using BudgetApi.Dtos.Transactions;
using BudgetApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace BudgetApi.Tests.Integration;

// xUnit creates a new instance of the class for every test, so every test
// gets a new factory and a new, empty database.
public class TransactionEndpointsTests : IDisposable
{
    private static readonly DateTimeOffset March14 = new(2026, 3, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly CustomWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static Task<T?> ReadAsync<T>(HttpResponseMessage response) =>
        response.Content.ReadFromJsonAsync<T>(CustomWebApplicationFactory.JsonOptions);

    [Fact]
    public async Task GetAll_WithoutUser_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/transactions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AsUser_ReturnsOnlyTheirTransactions()
    {
        var ada = await _factory.AddUserAsync("ada");
        var alan = await _factory.AddUserAsync("alan");
        await _factory.AddTransactionAsync(ada.Id, TransactionType.Income, 2500m, March14);
        await _factory.AddTransactionAsync(ada.Id, TransactionType.Expense, 900m, March14);
        await _factory.AddTransactionAsync(alan.Id, TransactionType.Expense, 40m, March14);
        var client = _factory.CreateClientAs(ada.Id);

        var response = await client.GetAsync("/api/transactions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transactions = await ReadAsync<List<TransactionResponseDto>>(response);
        Assert.NotNull(transactions);
        Assert.Equal(2, transactions.Count);
    }

    [Fact]
    public async Task Create_WithoutUser_Returns401()
    {
        var client = _factory.CreateClient();
        var dto = new CreateTransactionDto(TransactionType.Expense, "Rent", 900m, March14);

        var response = await client.PostAsJsonAsync("/api/transactions", dto, CustomWebApplicationFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsUser_Returns201WithLocationAndBody()
    {
        var ada = await _factory.AddUserAsync("ada");
        var client = _factory.CreateClientAs(ada.Id);
        var dto = new CreateTransactionDto(TransactionType.Expense, "Rent", 900.50m, March14);

        var response = await client.PostAsJsonAsync("/api/transactions", dto, CustomWebApplicationFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var transaction = await ReadAsync<TransactionResponseDto>(response);
        Assert.NotNull(transaction);
        Assert.Equal($"/api/transactions/{transaction.Id}", response.Headers.Location?.ToString());
        Assert.Equal(TransactionType.Expense, transaction.Type);
        Assert.Equal("Rent", transaction.Description);
        Assert.Equal(900.50m, transaction.Amount);
        Assert.Equal(March14, transaction.Timestamp);
    }

    [Theory]
    [InlineData("", 10)]
    [InlineData("Rent", 0)]
    [InlineData("Rent", -5)]
    public async Task Create_WithInvalidBody_Returns400WithProblemDetails(string description, decimal amount)
    {
        var ada = await _factory.AddUserAsync("ada");
        var client = _factory.CreateClientAs(ada.Id);
        var dto = new CreateTransactionDto(TransactionType.Expense, description, amount, March14);

        var response = await client.PostAsJsonAsync("/api/transactions", dto, CustomWebApplicationFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadAsync<ValidationProblemDetails>(response);
        Assert.NotNull(problem);
        Assert.NotEmpty(problem.Errors);
    }

    [Fact]
    public async Task GetById_AsOwner_Returns200()
    {
        var ada = await _factory.AddUserAsync("ada");
        var id = await _factory.AddTransactionAsync(ada.Id, TransactionType.Income, 2500m, March14, "Salary");
        var client = _factory.CreateClientAs(ada.Id);

        var response = await client.GetAsync($"/api/transactions/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transaction = await ReadAsync<TransactionResponseDto>(response);
        Assert.Equal("Salary", transaction?.Description);
    }

    [Fact]
    public async Task GetById_AsAnotherUser_Returns404()
    {
        var ada = await _factory.AddUserAsync("ada");
        var alan = await _factory.AddUserAsync("alan");
        var id = await _factory.AddTransactionAsync(ada.Id, TransactionType.Income, 2500m, March14);
        var client = _factory.CreateClientAs(alan.Id);

        var response = await client.GetAsync($"/api/transactions/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsOwner_Returns200WithNewValues()
    {
        var ada = await _factory.AddUserAsync("ada");
        var id = await _factory.AddTransactionAsync(ada.Id, TransactionType.Expense, 900m, March14, "Rent");
        var client = _factory.CreateClientAs(ada.Id);
        var dto = new UpdateTransactionDto(TransactionType.Expense, "Rent March", 950m, March14);

        var response = await client.PutAsJsonAsync($"/api/transactions/{id}", dto, CustomWebApplicationFactory.JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transaction = await ReadAsync<TransactionResponseDto>(response);
        Assert.NotNull(transaction);
        Assert.Equal("Rent March", transaction.Description);
        Assert.Equal(950m, transaction.Amount);
    }

    [Fact]
    public async Task Update_AsAnotherUser_Returns404AndKeepsOldValues()
    {
        var ada = await _factory.AddUserAsync("ada");
        var alan = await _factory.AddUserAsync("alan");
        var id = await _factory.AddTransactionAsync(ada.Id, TransactionType.Expense, 900m, March14, "Rent");
        var dto = new UpdateTransactionDto(TransactionType.Income, "Changed", 1m, March14);

        var response = await _factory.CreateClientAs(alan.Id)
            .PutAsJsonAsync($"/api/transactions/{id}", dto, CustomWebApplicationFactory.JsonOptions);
        var check = await _factory.CreateClientAs(ada.Id).GetAsync($"/api/transactions/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var transaction = await ReadAsync<TransactionResponseDto>(check);
        Assert.NotNull(transaction);
        Assert.Equal("Rent", transaction.Description);
        Assert.Equal(900m, transaction.Amount);
    }

    [Fact]
    public async Task Delete_AsOwner_Returns204AndTransactionIsGone()
    {
        var ada = await _factory.AddUserAsync("ada");
        var id = await _factory.AddTransactionAsync(ada.Id, TransactionType.Expense, 900m, March14);
        var client = _factory.CreateClientAs(ada.Id);

        var deleteResponse = await client.DeleteAsync($"/api/transactions/{id}");
        var getResponse = await client.GetAsync($"/api/transactions/{id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_AsAnotherUser_Returns404AndTransactionStillExists()
    {
        var ada = await _factory.AddUserAsync("ada");
        var alan = await _factory.AddUserAsync("alan");
        var id = await _factory.AddTransactionAsync(ada.Id, TransactionType.Expense, 900m, March14);

        var deleteResponse = await _factory.CreateClientAs(alan.Id).DeleteAsync($"/api/transactions/{id}");
        var getResponse = await _factory.CreateClientAs(ada.Id).GetAsync($"/api/transactions/{id}");

        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }
}
