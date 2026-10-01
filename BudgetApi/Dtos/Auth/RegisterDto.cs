using System.ComponentModel.DataAnnotations;

namespace BudgetApi.Dtos.Auth;

// Identity's default password rules: at least 6 characters, with an uppercase letter,
// a lowercase letter, a digit and a non-alphanumeric character.
public record RegisterDto(
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, MinLength(8)] string Password);
