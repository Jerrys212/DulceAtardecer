using DulceAtardecer.Models;
using DulceAtardecer.Models.Dtos;

namespace DulceAtardecer.Repository.IRepository;

public interface IReportRepository
{
    Task<SalesSummaryDto> GetSummaryAsync(ReportPeriod period, CancellationToken cancellationToken);

    Task<IReadOnlyList<SalesIntervalDto>> GetSalesOverTimeAsync(
        ReportPeriod period,
        ReportGrouping groupBy,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductSalesDto>> GetTopProductsAsync(
        ReportPeriod period,
        int limit,
        ProductRanking orderBy,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductSalesDto>> GetBottomProductsAsync(
        ReportPeriod period,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CategorySalesDto>> GetSalesByCategoryAsync(ReportPeriod period, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExtraSalesDto>> GetTopExtrasAsync(
        ReportPeriod period,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PaymentMethodSalesDto>> GetPaymentMethodsAsync(
        ReportPeriod period,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<HeatmapCellDto>> GetHeatmapAsync(ReportPeriod period, CancellationToken cancellationToken);

    Task<IReadOnlyList<CashierPerformanceDto>> GetCashiersAsync(ReportPeriod period, CancellationToken cancellationToken);

    Task<PagedResult<CancellationDto>> GetCancellationsAsync(
        ReportPeriod period,
        string? cashierId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<MyDayDto> GetMyDayAsync(string userId, DateOnly today, CancellationToken cancellationToken);
}
