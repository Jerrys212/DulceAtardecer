using System.ComponentModel.DataAnnotations;

namespace DulceAtardecer.Models.Dtos;

public record SalesQueryDto : ReportPeriodQueryDto
{
    [EnumDataType(typeof(SaleStatus), ErrorMessage = "El estado debe ser Preparing, Paid o Cancelled")]
    public SaleStatus? Status { get; init; }

    [EnumDataType(typeof(PaymentMethod), ErrorMessage = "El método de pago debe ser Cash, Card o Transfer")]
    public PaymentMethod? PaymentMethod { get; init; }

    [MaxLength(450, ErrorMessage = "El cajero no es válido")]
    public string? CashierId { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "La página debe ser mayor o igual a 1")]
    public int Page { get; init; } = 1;

    [Range(1, 50, ErrorMessage = "El tamaño de página debe estar entre 1 y 50")]
    public int PageSize { get; init; } = 10;
}

public record CreateSaleDto(
    [TrimmedLength(0, 100, ErrorMessage = "La descripción del cliente no puede superar los 100 caracteres")]
    string? CustomerDescription,
    [Required(ErrorMessage = "El total esperado es obligatorio")]
    [MaxDecimalPlaces(2, ErrorMessage = "El total esperado no puede tener más de dos decimales")]
    decimal? ExpectedTotal,
    [Required(ErrorMessage = "Los renglones son obligatorios")]
    [Length(1, 50, ErrorMessage = "La venta debe tener entre 1 y 50 renglones")]
    List<SaleItemRequestDto> Items);

public record UpdateSaleDto(
    [TrimmedLength(0, 100, ErrorMessage = "La descripción del cliente no puede superar los 100 caracteres")]
    string? CustomerDescription,
    [Required(ErrorMessage = "El total esperado es obligatorio")]
    [MaxDecimalPlaces(2, ErrorMessage = "El total esperado no puede tener más de dos decimales")]
    decimal? ExpectedTotal,
    [Required(ErrorMessage = "Los renglones son obligatorios")]
    [Length(1, 50, ErrorMessage = "La venta debe tener entre 1 y 50 renglones")]
    List<SaleItemRequestDto> Items);

public record SaleItemRequestDto(
    [Required(ErrorMessage = "El producto es obligatorio")]
    Guid? ProductId,
    [Required(ErrorMessage = "La cantidad es obligatoria")]
    [Range(1, 50, ErrorMessage = "La cantidad debe estar entre 1 y 50")]
    int? Quantity,
    [TrimmedLength(0, 200, ErrorMessage = "La nota no puede superar los 200 caracteres")]
    string? Note,
    List<SaleItemExtraRequestDto>? Extras) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        bool hasRepeatedExtra = (Extras ?? [])
            .Where(extra => extra.ExtraId is not null)
            .GroupBy(extra => extra.ExtraId)
            .Any(group => group.Count() > 1);

        if (hasRepeatedExtra)
        {
            yield return new ValidationResult(
                "Un extra no puede repetirse en el mismo renglón, usa su cantidad",
                [nameof(Extras)]);
        }
    }
}

public record SaleItemExtraRequestDto(
    [Required(ErrorMessage = "El extra es obligatorio")]
    Guid? ExtraId,
    [Required(ErrorMessage = "La cantidad del extra es obligatoria")]
    [Range(1, 10, ErrorMessage = "La cantidad de cada extra debe estar entre 1 y 10")]
    int? Quantity);

public record PaySaleDto(
    [Required(ErrorMessage = "El método de pago es obligatorio")]
    [EnumDataType(typeof(PaymentMethod), ErrorMessage = "El método de pago no es válido")]
    PaymentMethod? PaymentMethod);

public record CancelSaleDto(
    [Required(ErrorMessage = "El motivo es obligatorio")]
    [TrimmedLength(5, 200, ErrorMessage = "El motivo debe tener entre 5 y 200 caracteres")]
    string Reason);

public record SaleDto(
    Guid Id,
    SaleStatus Status,
    string? CustomerDescription,
    DateTimeOffset CreatedAt,
    string CashierName,
    List<SaleItemDto> Items,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    PaymentMethod? PaymentMethod,
    DateTimeOffset? PaidAt,
    string? CancelReason,
    DateTimeOffset? CancelledAt);

public record SaleItemDto(
    Guid ProductId,
    string ProductName,
    string CategoryName,
    string SubcategoryName,
    decimal UnitPrice,
    int Quantity,
    decimal Amount,
    string? Note,
    List<SaleItemExtraDto> Extras);

public record SaleItemExtraDto(Guid ExtraId, string Name, decimal Price, int Quantity);
