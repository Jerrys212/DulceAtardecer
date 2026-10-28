using System.ComponentModel.DataAnnotations;

namespace DulceAtardecer.Models.Dtos;

public record CreateCategoryDto(
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [TrimmedLength(2, 50, ErrorMessage = "El nombre debe tener entre 2 y 50 caracteres")]
    string Name,
    [MaxLength(50, ErrorMessage = "No se pueden crear más de 50 subcategorías a la vez")]
    [TrimmedLength(2, 50, ErrorMessage = "Cada subcategoría debe tener entre 2 y 50 caracteres")]
    List<string>? Subcategories);

public record UpdateCategoryDto(
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [TrimmedLength(2, 50, ErrorMessage = "El nombre debe tener entre 2 y 50 caracteres")]
    string Name);

public record CreateSubcategoryDto(
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [TrimmedLength(2, 50, ErrorMessage = "El nombre debe tener entre 2 y 50 caracteres")]
    string Name);

public record UpdateSubcategoryDto(
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [TrimmedLength(2, 50, ErrorMessage = "El nombre debe tener entre 2 y 50 caracteres")]
    string Name);

public record CategoryDto(Guid Id, string Name, bool IsActive, List<SubcategoryDto> Subcategories);

public record SubcategoryDto(Guid Id, string Name, bool IsActive);

public record ActiveCategoryDto(Guid Id, string Name, List<ActiveSubcategoryDto> Subcategories);

public record ActiveSubcategoryDto(Guid Id, string Name);

public sealed class TrimmedLengthAttribute(int minimumLength, int maximumLength) : ValidationAttribute
{
    public override bool IsValid(object? value) => value switch
    {
        null => true,
        string text => HasValidLength(text),
        IEnumerable<string?> items => items.All(item => item is not null && HasValidLength(item)),
        _ => false,
    };

    private bool HasValidLength(string text)
    {
        int length = text.Trim().Length;
        return length >= minimumLength && length <= maximumLength;
    }
}
