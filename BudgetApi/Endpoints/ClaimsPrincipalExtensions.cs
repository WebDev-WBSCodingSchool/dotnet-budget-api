using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace BudgetApi.Endpoints;

public static class ClaimsPrincipalExtensions
{
    // Reads the user id from the "sub" claim of the token.
    // Program.cs sets MapInboundClaims = false, so the claim keeps its JWT name "sub"
    // and is not renamed to ClaimTypes.NameIdentifier.
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out var id)
            ? id
            : throw new InvalidOperationException("The token has no valid 'sub' claim.");
    }
}
