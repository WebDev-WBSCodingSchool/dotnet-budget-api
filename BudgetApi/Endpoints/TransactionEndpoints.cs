using System.ComponentModel.DataAnnotations;
using BudgetApi.Dtos.Transactions;
using BudgetApi.Models;
using BudgetApi.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BudgetApi.Endpoints;

public static class TransactionEndpoints
{
    public static RouteGroupBuilder MapTransactionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/transactions")
            .WithTags("Transactions");

        group.MapGet("/", GetAllTransactions)
            .WithName("GetAllTransactions")
            .WithSummary("List transactions, newest first, with optional filters and paging")
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetTransactionById)
            .WithName("GetTransactionById")
            .WithSummary("Get one transaction by id");

        group.MapPost("/", CreateTransaction)
            .WithName("CreateTransaction")
            .WithSummary("Add an income or expense")
            .ProducesValidationProblem();

        group.MapPut("/{id:guid}", UpdateTransaction)
            .WithName("UpdateTransaction")
            .WithSummary("Replace a transaction")
            .ProducesValidationProblem();

        group.MapDelete("/{id:guid}", DeleteTransaction)
            .WithName("DeleteTransaction")
            .WithSummary("Delete a transaction");

        return group;
    }

    // Every filter comes from the query string and is optional, for example
    // /api/transactions?type=Expense&from=2026-03-01&to=2026-03-31&search=rent&page=1&pageSize=20
    private static async Task<Ok<IReadOnlyList<TransactionResponseDto>>> GetAllTransactions(
        TransactionType? type,
        DateOnly? from,
        DateOnly? to,
        string? search,
        ITransactionService transactionService,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        var filter = new TransactionFilter(type, from, to, search, page, pageSize);
        return TypedResults.Ok(await transactionService.GetAllAsync(filter));
    }

    private static async Task<Results<Ok<TransactionResponseDto>, NotFound>> GetTransactionById(
        Guid id, ITransactionService transactionService)
    {
        var transaction = await transactionService.GetByIdAsync(id);
        return transaction is null ? TypedResults.NotFound() : TypedResults.Ok(transaction);
    }

    private static async Task<Created<TransactionResponseDto>> CreateTransaction(
        CreateTransactionDto dto, ITransactionService transactionService)
    {
        var transaction = await transactionService.CreateAsync(dto);
        return TypedResults.Created($"/api/transactions/{transaction.Id}", transaction);
    }

    private static async Task<Results<Ok<TransactionResponseDto>, NotFound>> UpdateTransaction(
        Guid id, UpdateTransactionDto dto, ITransactionService transactionService)
    {
        var transaction = await transactionService.UpdateAsync(id, dto);
        return transaction is null ? TypedResults.NotFound() : TypedResults.Ok(transaction);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteTransaction(
        Guid id, ITransactionService transactionService)
    {
        return await transactionService.DeleteAsync(id) ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
