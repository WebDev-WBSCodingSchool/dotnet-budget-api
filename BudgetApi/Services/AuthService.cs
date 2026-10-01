using System.Security.Claims;
using System.Text;
using BudgetApi.Dtos.Auth;
using BudgetApi.Dtos.Users;
using BudgetApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BudgetApi.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly IConfiguration _configuration;

    public AuthService(UserManager<User> userManager, IConfiguration configuration)
    {
        _userManager = userManager;
        _configuration = configuration;
    }

    public async Task<(IdentityResult Result, UserResponseDto? User)> RegisterAsync(RegisterDto dto)
    {
        var user = new User
        {
            Name = dto.Name,
            UserName = dto.Email,
            Email = dto.Email,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // UserManager checks the password rules, hashes the password and saves the user.
        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            return (result, null);
        }

        return (result, ToDto(user));
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, dto.Password))
        {
            // Same answer for an unknown email and a wrong password.
            return null;
        }

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_configuration.GetValue("Jwt:ExpiryMinutes", 60));
        return new LoginResponseDto(CreateToken(user, expiresAt), expiresAt);
    }

    public async Task<UserResponseDto?> GetUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user is null ? null : ToDto(user);
    }

    private string CreateToken(User user, DateTimeOffset expiresAt)
    {
        var key = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");

        var descriptor = new SecurityTokenDescriptor
        {
            // "sub" (subject) holds the user id. The endpoints read it to find the current user.
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Name, user.Name)
            ]),
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static UserResponseDto ToDto(User user) =>
        new(user.Id, user.Name, user.Email ?? string.Empty, user.CreatedAt);
}
