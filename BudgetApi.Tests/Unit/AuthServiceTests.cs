using System.Diagnostics.Metrics;
using BudgetApi.Dtos.Auth;
using BudgetApi.Models;
using BudgetApi.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Moq;

namespace BudgetApi.Tests.Unit;

public class AuthServiceTests
{
    private readonly Mock<UserManager<User>> _userManager;
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        // UserManager is a class with a large constructor. Moq can fake it when we pass
        // a fake user store and null for the other dependencies, which these tests never use.
        var store = new Mock<IUserStore<User>>();
        _userManager = new Mock<UserManager<User>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        // A real configuration object filled from a dictionary is simpler than mocking IConfiguration.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "unit-test-signing-key-that-is-longer-than-32-bytes",
                ["Jwt:Issuer"] = "BudgetApi",
                ["Jwt:Audience"] = "BudgetApi",
                ["Jwt:ExpiryMinutes"] = "60"
            })
            .Build();

        var meterFactory = new ServiceCollection()
            .AddMetrics()
            .BuildServiceProvider()
            .GetRequiredService<IMeterFactory>();

        _service = new AuthService(
            _userManager.Object, configuration, NullLogger<AuthService>.Instance, new BudgetMetrics(meterFactory));
    }

    [Fact]
    public async Task RegisterAsync_WhenIdentityAcceptsUser_ReturnsUser()
    {
        // Arrange: CreateAsync succeeds for any user and password
        _userManager
            .Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var dto = new RegisterDto("Ada", "ada@example.com", "Passw0rd!");

        // Act
        var (result, user) = await _service.RegisterAsync(dto);

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(user);
        Assert.Equal("ada@example.com", user.Email);

        // The email is also used as the user name, and the password is passed on unchanged.
        _userManager.Verify(m => m.CreateAsync(
            It.Is<User>(u => u.UserName == "ada@example.com" && u.Name == "Ada"),
            "Passw0rd!"), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WhenIdentityRejectsUser_ReturnsErrorsAndNoUser()
    {
        var error = new IdentityError { Code = "PasswordRequiresDigit", Description = "Needs a digit." };
        _userManager
            .Setup(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(error));

        var (result, user) = await _service.RegisterAsync(new RegisterDto("Ada", "ada@example.com", "password"));

        Assert.False(result.Succeeded);
        Assert.Null(user);
        Assert.Contains(result.Errors, e => e.Code == "PasswordRequiresDigit");
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokenForTheUser()
    {
        var ada = new User { Id = Guid.NewGuid(), Name = "Ada", Email = "ada@example.com", UserName = "ada@example.com" };
        _userManager.Setup(m => m.FindByEmailAsync("ada@example.com")).ReturnsAsync(ada);
        _userManager.Setup(m => m.CheckPasswordAsync(ada, "Passw0rd!")).ReturnsAsync(true);

        var response = await _service.LoginAsync(new LoginDto("ada@example.com", "Passw0rd!"));

        Assert.NotNull(response);
        Assert.True(response.ExpiresAt > DateTimeOffset.UtcNow);

        // Read the token back and check that "sub" holds the user id.
        var token = new JsonWebTokenHandler().ReadJsonWebToken(response.Token);
        Assert.Equal(ada.Id.ToString(), token.Subject);
        Assert.Equal("BudgetApi", token.Issuer);
    }

    [Fact]
    public async Task LoginAsync_WithUnknownEmail_ReturnsNullWithoutCheckingPassword()
    {
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        var response = await _service.LoginAsync(new LoginDto("nobody@example.com", "Passw0rd!"));

        Assert.Null(response);
        _userManager.Verify(m => m.CheckPasswordAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsNull()
    {
        var ada = new User { Id = Guid.NewGuid(), Name = "Ada", Email = "ada@example.com" };
        _userManager.Setup(m => m.FindByEmailAsync("ada@example.com")).ReturnsAsync(ada);
        _userManager.Setup(m => m.CheckPasswordAsync(ada, It.IsAny<string>())).ReturnsAsync(false);

        var response = await _service.LoginAsync(new LoginDto("ada@example.com", "wrong"));

        Assert.Null(response);
    }
}
