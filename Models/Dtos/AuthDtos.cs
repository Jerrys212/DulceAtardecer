using System.ComponentModel.DataAnnotations;

namespace DulceAtardecer.Models.Dtos;

public record LoginRequestDto(
    [Required(ErrorMessage = "El usuario es obligatorio")]
    [MaxLength(256, ErrorMessage = "El usuario no puede superar los 256 caracteres")]
    string Username,
    [Required(ErrorMessage = "La contraseña es obligatoria")]
    [MaxLength(128, ErrorMessage = "La contraseña no puede superar los 128 caracteres")]
    string Password);

public record ChangePasswordRequestDto(
    [Required(ErrorMessage = "La contraseña actual es obligatoria")]
    [MaxLength(128, ErrorMessage = "La contraseña no puede superar los 128 caracteres")]
    string CurrentPassword,
    [Required(ErrorMessage = "La nueva contraseña es obligatoria")]
    [MaxLength(128, ErrorMessage = "La contraseña no puede superar los 128 caracteres")]
    string NewPassword);

public record CurrentUserDto(
    string Id,
    string Username,
    string FullName,
    string Role,
    bool MustChangePassword);

public record LoginResponseDto(string AccessToken, DateTimeOffset ExpiresAt, CurrentUserDto User);

public record AccessTokenDto(string AccessToken, DateTimeOffset ExpiresAt);
