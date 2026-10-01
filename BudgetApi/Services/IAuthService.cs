using BudgetApi.Dtos.Auth;
using BudgetApi.Dtos.Users;
using Microsoft.AspNetCore.Identity;

namespace BudgetApi.Services;

public interface IAuthService
{
    // Result tells whether Identity accepted the user. User is set only when it did.
    Task<(IdentityResult Result, UserResponseDto? User)> RegisterAsync(RegisterDto dto);

    // Returns null when the email or password is wrong.
    Task<LoginResponseDto?> LoginAsync(LoginDto dto);

    // Returns null when the user no longer exists.
    Task<UserResponseDto?> GetUserAsync(Guid userId);
}
