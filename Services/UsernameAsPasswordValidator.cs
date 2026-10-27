using DulceAtardecer.Models;
using Microsoft.AspNetCore.Identity;

namespace DulceAtardecer.Services;

public class UsernameAsPasswordValidator : IPasswordValidator<ApplicationUser>
{
    public const string ErrorCode = "PasswordEqualsUserName";

    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        bool equalsUserName = string.Equals(password, user.UserName, StringComparison.OrdinalIgnoreCase);

        return Task.FromResult(equalsUserName
            ? IdentityResult.Failed(new IdentityError
            {
                Code = ErrorCode,
                Description = "La contraseña no puede ser igual al nombre de usuario",
            })
            : IdentityResult.Success);
    }
}
