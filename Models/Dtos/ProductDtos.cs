using System.ComponentModel.DataAnnotations;

namespace DulceAtardecer.Models.Dtos;

public record CreateProductDto(
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [TrimmedLength(2, 100, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres")]
    string Name,
    [TrimmedLength(0, 250, ErrorMessage = "La descripción no puede superar los 250 caracteres")]
    string? Description,
    [Required(ErrorMessage = "El precio es obligatorio")]
    [Range(typeof(decimal), "0.01", "9999.99", ParseLimitsInInvariantCulture = true, ErrorMessage = "El precio debe ser mayor a 0 y no superar 9,999.99")]
    [MaxDecimalPlaces(2, ErrorMessage = "El precio no puede tener más de dos decimales")]
    decimal? Price,
    [Required(ErrorMessage = "La categoría es obligatoria")]
    Guid? CategoryId,
    [Required(ErrorMessage = "La subcategoría es obligatoria")]
    Guid? SubcategoryId);

public record UpdateProductDto(
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [TrimmedLength(2, 100, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres")]
    string Name,
    [TrimmedLength(0, 250, ErrorMessage = "La descripción no puede superar los 250 caracteres")]
    string? Description,
    [Required(ErrorMessage = "El precio es obligatorio")]
    [Range(typeof(decimal), "0.01", "9999.99", ParseLimitsInInvariantCulture = true, ErrorMessage = "El precio debe ser mayor a 0 y no superar 9,999.99")]
    [MaxDecimalPlaces(2, ErrorMessage = "El precio no puede tener más de dos decimales")]
    decimal? Price,
    [Required(ErrorMessage = "La categoría es obligatoria")]
    Guid? CategoryId,
    [Required(ErrorMessage = "La subcategoría es obligatoria")]
    Guid? SubcategoryId);

public record ProductQueryDto
{
    public Guid? CategoryId { get; init; }

    public Guid? SubcategoryId { get; init; }

    [MaxLength(100, ErrorMessage = "La búsqueda no puede superar los 100 caracteres")]
    public string? Search { get; init; }
}

public record ProductAdminQueryDto : ProductQueryDto
{
    public bool? IsActive { get; init; }
}

public record ProductDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    Guid CategoryId,
    string CategoryName,
    Guid SubcategoryId,
    string SubcategoryName,
    bool IsActive);

public record AvailableProductDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    Guid CategoryId,
    string CategoryName,
    Guid SubcategoryId,
    string SubcategoryName);
