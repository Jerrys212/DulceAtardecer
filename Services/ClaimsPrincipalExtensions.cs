using System.Security.Claims;
using DulceAtardecer.Constants;
using DulceAtardecer.Models;

namespace DulceAtardecer.Services;

public static class ClaimsPrincipalExtensions
{
    public static string GetUserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(AppClaimTypes.Subject)
        ?? throw new ApiException(StatusCodes.Status401Unauthorized);

    public static Guid GetSessionId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(AppClaimTypes.SessionId), out Guid sessionId)
            ? sessionId
            : throw new ApiException(StatusCodes.Status401Unauthorized);
}
