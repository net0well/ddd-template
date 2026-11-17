using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Infrastructure.Authentication;

internal static class ClaimsPrincipalExtensions
{
    // Inbound claim mapping is off, so the JWT "sub" claim keeps its name.
    public static bool TryGetUserId(this ClaimsPrincipal? principal, out Guid userId)
    {
        userId = Guid.Empty;

        return principal?.Identity?.IsAuthenticated == true &&
               Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
    }
}
