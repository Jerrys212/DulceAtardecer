using Microsoft.AspNetCore.Identity;

namespace DulceAtardecer.Services;

public class SpanishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        Error(nameof(DefaultError), "Ocurrió un error inesperado");

    public override IdentityError ConcurrencyFailure() =>
        Error(nameof(ConcurrencyFailure), "El registro fue modificado por otra operación, intenta de nuevo");

    public override IdentityError PasswordMismatch() =>
        Error(nameof(PasswordMismatch), "La contraseña actual es incorrecta");

    public override IdentityError InvalidUserName(string? userName) =>
        Error(nameof(InvalidUserName), "El nombre de usuario solo puede contener letras, números y los caracteres - . _ @ +");

    public override IdentityError DuplicateUserName(string userName) =>
        Error(nameof(DuplicateUserName), $"El nombre de usuario '{userName}' ya está en uso");

    public override IdentityError PasswordTooShort(int length) =>
        Error(nameof(PasswordTooShort), $"La contraseña debe tener al menos {length} caracteres");

    public override IdentityError UserAlreadyInRole(string role) =>
        Error(nameof(UserAlreadyInRole), $"El usuario ya tiene el rol '{role}'");

    private static IdentityError Error(string code, string description) =>
        new() { Code = code, Description = description };
}
