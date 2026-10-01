using System.ComponentModel.DataAnnotations;

namespace BudgetApi.Dtos.Auth;

public record LoginDto(
    [Required, EmailAddress] string Email,
    [Required] string Password);
