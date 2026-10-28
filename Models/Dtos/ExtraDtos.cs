using System.ComponentModel.DataAnnotations;

namespace DulceAtardecer.Models.Dtos;

public record CreateExtraDto(
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [TrimmedLength(2, 50, ErrorMessage = "El nombre debe tener entre 2 y 50 caracteres")]
    string Name,
    [Required(ErrorMessage = "El precio es obligatorio")]
    [Range(typeof(decimal), "0", "9999.99", ParseLimitsInInvariantCulture = true, ErrorMessage = "El precio debe estar entre 0 y 9,999.99")]
    [MaxDecimalPlaces(2, ErrorMessage = "El precio no puede tener más de dos decimales")]
    decimal? Price);

public record UpdateExtraDto(
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [TrimmedLength(2, 50, ErrorMessage = "El nombre debe tener entre 2 y 50 caracteres")]
    string Name,
    [Required(ErrorMessage = "El precio es obligatorio")]
    [Range(typeof(decimal), "0", "9999.99", ParseLimitsInInvariantCulture = true, ErrorMessage = "El precio debe estar entre 0 y 9,999.99")]
    [MaxDecimalPlaces(2, ErrorMessage = "El precio no puede tener más de dos decimales")]
    decimal? Price);

public record ExtraDto(Guid Id, string Name, decimal Price, bool IsActive);

public record ActiveExtraDto(Guid Id, string Name, decimal Price);

public sealed class MaxDecimalPlacesAttribute(int decimalPlaces) : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is not decimal number || decimal.Round(number, decimalPlaces) == number;
}
