using Asp.Versioning;
using DulceAtardecer.Constants;
using DulceAtardecer.Models;
using DulceAtardecer.Models.Dtos;
using DulceAtardecer.Repository.IRepository;
using DulceAtardecer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DulceAtardecer.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
[ProducesResponseType<ApiResponse<object>>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ApiResponse<object>>(StatusCodes.Status403Forbidden)]
public class ReportsController(IReportRepository reportRepository, TimeProvider timeProvider) : ControllerBase
{
    [HttpGet("summary")]
    [Authorize(Roles = Permissions.ReportsView)]
    [ProducesResponseType<ApiResponse<SalesSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSummary([FromQuery] ReportPeriodQueryDto query, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await reportRepository.GetSummaryAsync(ResolvePeriod(query), cancellationToken)));

    [HttpGet("sales-over-time")]
    [Authorize(Roles = Permissions.ReportsView)]
    [ProducesResponseType<ApiResponse<List<SalesIntervalDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSalesOverTime([FromQuery] SalesOverTimeQueryDto query, CancellationToken cancellationToken)
    {
        ReportPeriod period = ResolvePeriod(query);

        if (query.GroupBy == ReportGrouping.Hour && period.Days > ReportPeriod.MaxHourlyDays)
        {
            throw new ApiException(
                StatusCodes.Status400BadRequest,
                $"La agrupación por hora solo admite periodos de hasta {ReportPeriod.MaxHourlyDays} días");
        }

        return Ok(ApiResponse.Ok(await reportRepository.GetSalesOverTimeAsync(period, query.GroupBy, cancellationToken)));
    }

    [HttpGet("top-products")]
    [Authorize(Roles = Permissions.ReportsView)]
    [ProducesResponseType<ApiResponse<List<ProductSalesDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTopProducts([FromQuery] TopProductsQueryDto query, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await reportRepository.GetTopProductsAsync(
            ResolvePeriod(query),
            query.Limit,
            query.OrderBy,
            cancellationToken)));

    [HttpGet("bottom-products")]
    [Authorize(Roles = Permissions.ReportsView)]
    [ProducesResponseType<ApiResponse<List<ProductSalesDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetBottomProducts([FromQuery] ReportLimitQueryDto query, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await reportRepository.GetBottomProductsAsync(ResolvePeriod(query), query.Limit, cancellationToken)));

    [HttpGet("sales-by-category")]
    [Authorize(Roles = Permissions.ReportsView)]
    [ProducesResponseType<ApiResponse<List<CategorySalesDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSalesByCategory([FromQuery] ReportPeriodQueryDto query, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await reportRepository.GetSalesByCategoryAsync(ResolvePeriod(query), cancellationToken)));

    [HttpGet("top-extras")]
    [Authorize(Roles = Permissions.ReportsView)]
    [ProducesResponseType<ApiResponse<List<ExtraSalesDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTopExtras([FromQuery] ReportLimitQueryDto query, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await reportRepository.GetTopExtrasAsync(ResolvePeriod(query), query.Limit, cancellationToken)));

    [HttpGet("payment-methods")]
    [Authorize(Roles = Permissions.ReportsView)]
    [ProducesResponseType<ApiResponse<List<PaymentMethodSalesDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPaymentMethods([FromQuery] ReportPeriodQueryDto query, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await reportRepository.GetPaymentMethodsAsync(ResolvePeriod(query), cancellationToken)));

    [HttpGet("heatmap")]
    [Authorize(Roles = Permissions.ReportsView)]
    [ProducesResponseType<ApiResponse<List<HeatmapCellDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetHeatmap([FromQuery] ReportPeriodQueryDto query, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await reportRepository.GetHeatmapAsync(ResolvePeriod(query), cancellationToken)));

    [HttpGet("cashiers")]
    [Authorize(Roles = Permissions.ReportsView)]
    [ProducesResponseType<ApiResponse<List<CashierPerformanceDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCashiers([FromQuery] ReportPeriodQueryDto query, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await reportRepository.GetCashiersAsync(ResolvePeriod(query), cancellationToken)));

    [HttpGet("cancellations")]
    [Authorize(Roles = Permissions.ReportsView)]
    [ProducesResponseType<ApiResponse<PagedResult<CancellationDto>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCancellations([FromQuery] CancellationsQueryDto query, CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await reportRepository.GetCancellationsAsync(
            ResolvePeriod(query),
            query.CashierId,
            query.Page,
            query.PageSize,
            cancellationToken)));

    [HttpGet("my-day")]
    [Authorize(Roles = Permissions.SalesCharge)]
    [ProducesResponseType<ApiResponse<MyDayDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyDay(CancellationToken cancellationToken) =>
        Ok(ApiResponse.Ok(await reportRepository.GetMyDayAsync(
            User.GetUserId(),
            BusinessTime.Today(timeProvider),
            cancellationToken)));

    private ReportPeriod ResolvePeriod(ReportPeriodQueryDto query) =>
        ReportPeriod.Resolve(query.From, query.To, BusinessTime.Today(timeProvider));
}
