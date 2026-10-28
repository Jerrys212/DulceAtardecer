using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DulceAtardecer.Models.Dtos;

public record ReportPeriodQueryDto
{
    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }
}

public record SalesOverTimeQueryDto : ReportPeriodQueryDto
{
    [EnumDataType(typeof(ReportGrouping), ErrorMessage = "La agrupación debe ser hour, day, week o month")]
    public ReportGrouping GroupBy { get; init; } = ReportGrouping.Day;
}

public record ReportLimitQueryDto : ReportPeriodQueryDto
{
    [Range(1, 50, ErrorMessage = "El límite debe estar entre 1 y 50")]
    public int Limit { get; init; } = 10;
}

public record TopProductsQueryDto : ReportLimitQueryDto
{
    [EnumDataType(typeof(ProductRanking), ErrorMessage = "El orden debe ser quantity o revenue")]
    public ProductRanking OrderBy { get; init; } = ProductRanking.Quantity;
}

public record CancellationsQueryDto : ReportPeriodQueryDto
{
    [MaxLength(450, ErrorMessage = "El cajero no es válido")]
    public string? CashierId { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "La página debe ser mayor o igual a 1")]
    public int Page { get; init; } = 1;

    [Range(1, 50, ErrorMessage = "El tamaño de página debe estar entre 1 y 50")]
    public int PageSize { get; init; } = 10;
}

public record SalesSummaryDto(
    DateOnly From,
    DateOnly To,
    decimal Revenue,
    decimal Subtotal,
    decimal Tax,
    int SalesCount,
    decimal AverageTicket,
    int UnitsSold,
    int CancelledCount,
    decimal CancelledAmount,
    PeriodComparisonDto Previous);

public record PeriodComparisonDto(
    DateOnly From,
    DateOnly To,
    decimal Revenue,
    int SalesCount,
    decimal AverageTicket,
    decimal? RevenueChangePercentage,
    decimal? SalesCountChangePercentage,
    decimal? AverageTicketChangePercentage);

public record SalesIntervalDto(DateTime Start, decimal Revenue, int SalesCount, decimal AverageTicket);

public record ProductSalesDto(
    Guid ProductId,
    string Name,
    string CategoryName,
    string SubcategoryName,
    int Units,
    decimal Revenue,
    decimal RevenuePercentage);

public record CategorySalesDto(
    string Name,
    int Units,
    decimal Revenue,
    decimal RevenuePercentage,
    List<SubcategorySalesDto> Subcategories);

public record SubcategorySalesDto(string Name, int Units, decimal Revenue, decimal RevenuePercentage);

public record ExtraSalesDto(Guid ExtraId, string Name, int TimesSold, decimal Revenue, decimal SalesPercentage);

public record PaymentMethodSalesDto(PaymentMethod Method, int SalesCount, decimal Amount, decimal Percentage);

public record HeatmapCellDto(
    [property: JsonConverter(typeof(JsonStringEnumConverter<DayOfWeek>))]
    DayOfWeek DayOfWeek,
    int Hour,
    int SalesCount,
    decimal Revenue);

public record PaymentMethodAmountDto(PaymentMethod Method, int SalesCount, decimal Amount);

public record CashierPerformanceDto(
    string CashierId,
    string CashierName,
    int SalesCount,
    decimal Revenue,
    decimal AverageTicket,
    List<PaymentMethodAmountDto> PaymentMethods,
    int CancelledCount,
    decimal CancelledAmount,
    int ReductionsCount,
    decimal ReducedAmount);

public record CancellationDto(
    Guid SaleId,
    DateTimeOffset CancelledAt,
    string CashierName,
    string CancelledByName,
    SaleStatus PreviousStatus,
    decimal Amount,
    string Reason);

public record MyDayDto(
    DateOnly Date,
    int SalesCount,
    decimal Total,
    List<PaymentMethodAmountDto> PaymentMethods,
    int PreparingCount,
    decimal PreparingAmount);
