using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
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
            .WithTags("Transactions")
            // Every endpoint in the group needs a valid token. Without one the response is 401.
            .RequireAuthorization();

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
        ClaimsPrincipal user,
        ITransactionService transactionService,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, 100)] int pageSize = 20)
    {
        var filter = new TransactionFilter(type, from, to, search, page, pageSize);
        return TypedResults.Ok(await transactionService.GetAllAsync(user.GetUserId(), filter));
    }

    private static async Task<Results<Ok<TransactionResponseDto>, NotFound>> GetTransactionById(
        Guid id, ClaimsPrincipal user, ITransactionService transactionService)
    {
        // Another user's transaction is reported as missing (404), so ids of other users' data stay private.
        var transaction = await transactionService.GetByIdAsync(user.GetUserId(), id);
        return transaction is null ? TypedResults.NotFound() : TypedResults.Ok(transaction);
    }

    private static async Task<Created<TransactionResponseDto>> CreateTransaction(
        CreateTransactionDto dto, ClaimsPrincipal user, ITransactionService transactionService)
    {
        var transaction = await transactionService.CreateAsync(user.GetUserId(), dto);
        return TypedResults.Created($"/api/transactions/{transaction.Id}", transaction);
    }

    private static async Task<Results<Ok<TransactionResponseDto>, NotFound>> UpdateTransaction(
        Guid id, UpdateTransactionDto dto, ClaimsPrincipal user, ITransactionService transactionService)
    {
        var transaction = await transactionService.UpdateAsync(user.GetUserId(), id, dto);
        return transaction is null ? TypedResults.NotFound() : TypedResults.Ok(transaction);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteTransaction(
        Guid id, ClaimsPrincipal user, ITransactionService transactionService)
    {
        return await transactionService.DeleteAsync(user.GetUserId(), id) ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
