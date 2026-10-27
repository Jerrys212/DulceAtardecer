using DulceAtardecer.Constants;
using DulceAtardecer.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DulceAtardecer.Middleware;

public class MustChangePasswordFilter : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        bool mustChangePassword = context.HttpContext.User.HasClaim(AppClaimTypes.MustChangePassword, "true");
        bool allowed = context.ActionDescriptor.EndpointMetadata.OfType<AllowWhenPasswordChangeRequiredAttribute>().Any();

        if (mustChangePassword && !allowed)
        {
            context.Result = new ObjectResult(ApiResponse.Fail(
                StatusCodes.Status403Forbidden,
                "Debes cambiar tu contraseña antes de continuar"))
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
        }
    }
}
