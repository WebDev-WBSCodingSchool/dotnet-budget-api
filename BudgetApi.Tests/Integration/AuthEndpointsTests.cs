using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BudgetApi.Dtos.Auth;
using BudgetApi.Dtos.Transactions;
using BudgetApi.Dtos.Users;
using BudgetApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace BudgetApi.Tests.Integration;

public class AuthEndpointsTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task RegisterLoginAndCallProtectedEndpoints_WithRealJwt_Succeeds()
    {
        var client = _factory.CreateClient();

        // Register
        var register = await client.PostAsJsonAsync("/auth/register",
            new RegisterDto("Grace", "grace@example.com", "Str0ng!pass"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        // Log in and read the token
        var login = await client.PostAsJsonAsync("/auth/login",
            new LoginDto("grace@example.com", "Str0ng!pass"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokenResponse = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(tokenResponse);

        // Send the token in the Authorization header
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenResponse.Token);

        var me = await client.GetFromJsonAsync<UserResponseDto>("/auth/me");
        Assert.Equal("grace@example.com", me?.Email);

        var create = await client.PostAsJsonAsync("/api/transactions",
            new CreateTransactionDto(TransactionType.Income, "Salary", 2500m, null),
            CustomWebApplicationFactory.JsonOptions);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
    }

    [Fact]
    public async Task Me_WithInvalidToken_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        var response = await client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/auth/register", new RegisterDto("Grace", "grace@example.com", "Str0ng!pass"));

        var response = await client.PostAsJsonAsync("/auth/login", new LoginDto("grace@example.com", "Wrong!pass1"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithWeakPassword_Returns400WithIdentityErrors()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/register",
            new RegisterDto("Grace", "grace@example.com", "password"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("PasswordRequiresDigit", problem.Errors.Keys);
    }

    [Fact]
    public async Task Register_WithTakenEmail_Returns400()
    {
        var client = _factory.CreateClient();
        var dto = new RegisterDto("Grace", "grace@example.com", "Str0ng!pass");
        await client.PostAsJsonAsync("/auth/register", dto);

        var response = await client.PostAsJsonAsync("/auth/register", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("DuplicateEmail", problem.Errors.Keys);
    }
}
