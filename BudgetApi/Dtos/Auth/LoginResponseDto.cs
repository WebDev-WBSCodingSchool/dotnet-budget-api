namespace BudgetApi.Dtos.Auth;

public record LoginResponseDto(string Token, DateTimeOffset ExpiresAt);
