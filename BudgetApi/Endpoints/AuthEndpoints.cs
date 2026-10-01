using System.Security.Claims;
using BudgetApi.Dtos.Auth;
using BudgetApi.Dtos.Users;
using BudgetApi.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BudgetApi.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Auth");

        group.MapPost("/register", Register)
            .WithName("Register")
            .WithSummary("Create a user account with a password");

        group.MapPost("/login", Login)
            .WithName("Login")
            .WithSummary("Exchange an email and password for a JWT")
            .ProducesValidationProblem();

        group.MapGet("/me", GetCurrentUser)
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("Get the user that owns the token")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return group;
    }

    private static async Task<Results<Created<UserResponseDto>, ValidationProblem>> Register(
        RegisterDto dto, IAuthService authService)
    {
        var (result, user) = await authService.RegisterAsync(dto);

        if (!result.Succeeded || user is null)
        {
            // Turn Identity's errors (weak password, email already taken, ...) into a 400 ValidationProblem.
            var errors = result.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

            return TypedResults.ValidationProblem(errors);
        }

        return TypedResults.Created("/auth/me", user);
    }

    private static async Task<Results<Ok<LoginResponseDto>, UnauthorizedHttpResult>> Login(
        LoginDto dto, IAuthService authService)
    {
        var response = await authService.LoginAsync(dto);
        return response is null ? TypedResults.Unauthorized() : TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<UserResponseDto>, NotFound>> GetCurrentUser(
        ClaimsPrincipal user, IAuthService authService)
    {
        var currentUser = await authService.GetUserAsync(user.GetUserId());
        return currentUser is null ? TypedResults.NotFound() : TypedResults.Ok(currentUser);
    }
}
