using System.ComponentModel.DataAnnotations;
using DulceAtardecer.Constants;

namespace DulceAtardecer.Models.Dtos;

public record UserDto(
    string Id,
    string Username,
    string FullName,
    string Role,
    bool IsActive,
    bool MustChangePassword,
    DateTimeOffset? DeactivatedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public record CreateUserDto(
    [Required(ErrorMessage = "El usuario es obligatorio")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "El usuario debe tener entre 3 y 50 caracteres")]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "El usuario solo puede contener letras, números y los caracteres . _ -")]
    string Username,
    [Required(ErrorMessage = "El nombre completo es obligatorio")]
    [MaxLength(100, ErrorMessage = "El nombre completo no puede superar los 100 caracteres")]
    string FullName,
    [Required(ErrorMessage = "El rol es obligatorio")]
    [AllowedValues(RoleNames.Admin, RoleNames.Cashier, RoleNames.Kitchen, ErrorMessage = "El rol debe ser Admin, Cashier o Kitchen")]
    string Role);

public record UpdateUserDto(
    [Required(ErrorMessage = "El nombre completo es obligatorio")]
    [MaxLength(100, ErrorMessage = "El nombre completo no puede superar los 100 caracteres")]
    string FullName,
    [Required(ErrorMessage = "El rol es obligatorio")]
    [AllowedValues(RoleNames.Admin, RoleNames.Cashier, RoleNames.Kitchen, ErrorMessage = "El rol debe ser Admin, Cashier o Kitchen")]
    string Role);

public record UserQueryDto
{
    public bool? IsActive { get; init; }

    [AllowedValues(null, RoleNames.Admin, RoleNames.Cashier, RoleNames.Kitchen, ErrorMessage = "El rol debe ser Admin, Cashier o Kitchen")]
    public string? Role { get; init; }

    [MaxLength(100, ErrorMessage = "La búsqueda no puede superar los 100 caracteres")]
    public string? Search { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "La página debe ser mayor o igual a 1")]
    public int Page { get; init; } = 1;

    [Range(1, 50, ErrorMessage = "El tamaño de página debe estar entre 1 y 50")]
    public int PageSize { get; init; } = 10;
}

public record CreatedUserDto(UserDto User, string TemporaryPassword);

public record TemporaryPasswordDto(string TemporaryPassword);

public record SessionDto(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string? UserAgent,
    string? IpAddress);
