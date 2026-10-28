using DulceAtardecer.Data;
using DulceAtardecer.Models;
using DulceAtardecer.Models.Dtos;
using DulceAtardecer.Repository.IRepository;
using DulceAtardecer.Services;
using Microsoft.EntityFrameworkCore;

namespace DulceAtardecer.Repository;

public class ReportRepository(ApplicationDbContext db) : IReportRepository
{
    private static readonly DayOfWeek[] WeekFromMonday =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday,
        DayOfWeek.Sunday,
    ];

    public async Task<SalesSummaryDto> GetSummaryAsync(ReportPeriod period, CancellationToken cancellationToken)
    {
        (DateTimeOffset start, DateTimeOffset end) = BusinessTime.ToUtcRange(period);
        PaidTotals current = await GetPaidTotalsAsync(period, cancellationToken);
        PaidTotals previous = await GetPaidTotalsAsync(period.Previous, cancellationToken);
        int unitsSold = await PaidSales(period).SelectMany(s => s.Items).SumAsync(i => i.Quantity, cancellationToken);
        IQueryable<Sale> cancelled = db.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Cancelled && s.CancelledAt >= start && s.CancelledAt < end);

        return new SalesSummaryDto(
            period.From,
            period.To,
            current.Revenue,
            current.Subtotal,
            current.Tax,
            current.SalesCount,
            current.AverageTicket,
            unitsSold,
            await cancelled.CountAsync(cancellationToken),
            await cancelled.SumAsync(s => s.Total, cancellationToken),
            new PeriodComparisonDto(
                period.Previous.From,
                period.Previous.To,
                previous.Revenue,
                previous.SalesCount,
                previous.AverageTicket,
                ChangePercentage(current.Revenue, previous.Revenue),
                ChangePercentage(current.SalesCount, previous.SalesCount),
                ChangePercentage(current.AverageTicket, previous.AverageTicket)));
    }

    public async Task<IReadOnlyList<SalesIntervalDto>> GetSalesOverTimeAsync(
        ReportPeriod period,
        ReportGrouping groupBy,
        CancellationToken cancellationToken)
    {
        List<HourlySales> hourly = await GetHourlySalesAsync(period, cancellationToken);
        Dictionary<DateTime, (int SalesCount, decimal Revenue)> buckets = hourly
            .GroupBy(h => IntervalStart(h.Date, h.Hour, groupBy))
            .ToDictionary(g => g.Key, g => (g.Sum(h => h.SalesCount), g.Sum(h => h.Revenue)));

        IEnumerable<DateTime> intervals = groupBy == ReportGrouping.Hour
            ? period.Dates.SelectMany(date => Enumerable.Range(0, 24).Select(hour => IntervalStart(date, hour, groupBy)))
            : period.Dates.Select(date => IntervalStart(date, 0, groupBy)).Distinct();

        return intervals
            .Select(start =>
            {
                (int salesCount, decimal revenue) = buckets.GetValueOrDefault(start);
                return new SalesIntervalDto(start, revenue, salesCount, AverageTicket(revenue, salesCount));
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ProductSalesDto>> GetTopProductsAsync(
        ReportPeriod period,
        int limit,
        ProductRanking orderBy,
        CancellationToken cancellationToken)
    {
        List<ProductSalesRow> rows = await GetProductSalesAsync(period, cancellationToken);
        decimal totalRevenue = rows.Sum(r => r.Revenue);

        IEnumerable<ProductSalesDto> products = rows
            .GroupBy(r => r.ProductId)
            .Select(g =>
            {
                ProductSalesRow latest = g.MaxBy(r => r.LastSoldAt)!;
                decimal revenue = g.Sum(r => r.Revenue);
                return new ProductSalesDto(
                    g.Key,
                    latest.ProductName,
                    latest.CategoryName,
                    latest.SubcategoryName,
                    g.Sum(r => r.Units),
                    revenue,
                    Percentage(revenue, totalRevenue));
            });

        IOrderedEnumerable<ProductSalesDto> ordered = orderBy == ProductRanking.Revenue
            ? products.OrderByDescending(p => p.Revenue).ThenByDescending(p => p.Units)
            : products.OrderByDescending(p => p.Units).ThenByDescending(p => p.Revenue);

        return ordered.ThenBy(p => p.Name).Take(limit).ToList();
    }

    public async Task<IReadOnlyList<ProductSalesDto>> GetBottomProductsAsync(
        ReportPeriod period,
        int limit,
        CancellationToken cancellationToken)
    {
        List<ProductSalesRow> rows = await GetProductSalesAsync(period, cancellationToken);
        decimal totalRevenue = rows.Sum(r => r.Revenue);
        Dictionary<Guid, (int Units, decimal Revenue)> sold = rows
            .GroupBy(r => r.ProductId)
            .ToDictionary(g => g.Key, g => (g.Sum(r => r.Units), g.Sum(r => r.Revenue)));

        List<Product> activeProducts = await db.Products
            .AsNoTracking()
            .Include(p => p.Subcategory)
            .ThenInclude(s => s!.Category)
            .Where(p => p.IsActive)
            .ToListAsync(cancellationToken);

        return activeProducts
            .Select(p =>
            {
                (int units, decimal revenue) = sold.GetValueOrDefault(p.Id);
                return new ProductSalesDto(
                    p.Id,
                    p.Name,
                    p.Subcategory!.Category!.Name,
                    p.Subcategory.Name,
                    units,
                    revenue,
                    Percentage(revenue, totalRevenue));
            })
            .OrderBy(p => p.Units)
            .ThenBy(p => p.Revenue)
            .ThenBy(p => p.Name)
            .Take(limit)
            .ToList();
    }

    public async Task<IReadOnlyList<CategorySalesDto>> GetSalesByCategoryAsync(
        ReportPeriod period,
        CancellationToken cancellationToken)
    {
        var rows = await PaidSales(period)
            .SelectMany(s => s.Items)
            .GroupBy(i => new { i.CategoryName, i.SubcategoryName })
            .Select(g => new
            {
                g.Key.CategoryName,
                g.Key.SubcategoryName,
                Units = g.Sum(i => i.Quantity),
                Revenue = g.Sum(i => i.Amount),
            })
            .ToListAsync(cancellationToken);
        decimal totalRevenue = rows.Sum(r => r.Revenue);

        return rows
            .GroupBy(r => r.CategoryName)
            .Select(g =>
            {
                decimal revenue = g.Sum(r => r.Revenue);
                return new CategorySalesDto(
                    g.Key,
                    g.Sum(r => r.Units),
                    revenue,
                    Percentage(revenue, totalRevenue),
                    g.OrderByDescending(r => r.Revenue)
                        .ThenBy(r => r.SubcategoryName)
                        .Select(r => new SubcategorySalesDto(
                            r.SubcategoryName,
                            r.Units,
                            r.Revenue,
                            Percentage(r.Revenue, totalRevenue)))
                        .ToList());
            })
            .OrderByDescending(c => c.Revenue)
            .ThenBy(c => c.Name)
            .ToList();
    }

    public async Task<IReadOnlyList<ExtraSalesDto>> GetTopExtrasAsync(
        ReportPeriod period,
        int limit,
        CancellationToken cancellationToken)
    {
        int salesCount = await PaidSales(period).CountAsync(cancellationToken);
        var rows = await (
                from sale in PaidSales(period)
                from item in sale.Items
                from extra in item.Extras
                select new
                {
                    SaleId = sale.Id,
                    sale.PaidAt,
                    extra.ExtraId,
                    extra.Name,
                    TimesSold = extra.Quantity * item.Quantity,
                    Revenue = extra.Price * extra.Quantity * item.Quantity,
                })
            .GroupBy(x => new { x.ExtraId, x.Name })
            .Select(g => new
            {
                g.Key.ExtraId,
                g.Key.Name,
                TimesSold = g.Sum(x => x.TimesSold),
                Revenue = g.Sum(x => x.Revenue),
                SalesCount = g.Select(x => x.SaleId).Distinct().Count(),
                LastSoldAt = g.Max(x => x.PaidAt),
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.ExtraId)
            .Select(g => new ExtraSalesDto(
                g.Key,
                g.MaxBy(r => r.LastSoldAt)!.Name,
                g.Sum(r => r.TimesSold),
                g.Sum(r => r.Revenue),
                Percentage(g.Sum(r => r.SalesCount), salesCount)))
            .OrderByDescending(e => e.TimesSold)
            .ThenByDescending(e => e.Revenue)
            .ThenBy(e => e.Name)
            .Take(limit)
            .ToList();
    }

    public async Task<IReadOnlyList<PaymentMethodSalesDto>> GetPaymentMethodsAsync(
        ReportPeriod period,
        CancellationToken cancellationToken)
    {
        List<PaymentMethodAmountDto> methods = ToPaymentMethods(await GetPaymentMethodRowsAsync(
            PaidSales(period),
            cancellationToken));
        decimal totalAmount = methods.Sum(m => m.Amount);

        return methods
            .Select(m => new PaymentMethodSalesDto(m.Method, m.SalesCount, m.Amount, Percentage(m.Amount, totalAmount)))
            .ToList();
    }

    public async Task<IReadOnlyList<HeatmapCellDto>> GetHeatmapAsync(
        ReportPeriod period,
        CancellationToken cancellationToken)
    {
        List<HourlySales> hourly = await GetHourlySalesAsync(period, cancellationToken);
        Dictionary<(DayOfWeek, int), (int SalesCount, decimal Revenue)> cells = hourly
            .GroupBy(h => (h.Date.DayOfWeek, h.Hour))
            .ToDictionary(g => g.Key, g => (g.Sum(h => h.SalesCount), g.Sum(h => h.Revenue)));

        return WeekFromMonday
            .SelectMany(day => Enumerable.Range(0, 24).Select(hour =>
            {
                (int salesCount, decimal revenue) = cells.GetValueOrDefault((day, hour));
                return new HeatmapCellDto(day, hour, salesCount, revenue);
            }))
            .ToList();
    }

    public async Task<IReadOnlyList<CashierPerformanceDto>> GetCashiersAsync(
        ReportPeriod period,
        CancellationToken cancellationToken)
    {
        (DateTimeOffset start, DateTimeOffset end) = BusinessTime.ToUtcRange(period);

        var paid = await PaidSales(period)
            .GroupBy(s => new { s.PaidById, s.PaymentMethod })
            .Select(g => new { UserId = g.Key.PaidById!, Method = g.Key.PaymentMethod!.Value, Count = g.Count(), Amount = g.Sum(s => s.Total) })
            .ToListAsync(cancellationToken);
        var cancelled = await db.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Cancelled && s.CancelledAt >= start && s.CancelledAt < end)
            .GroupBy(s => s.CancelledById)
            .Select(g => new { UserId = g.Key!, Count = g.Count(), Amount = g.Sum(s => s.Total) })
            .ToDictionaryAsync(x => x.UserId, cancellationToken);
        var reductions = await db.SaleModifications
            .AsNoTracking()
            .Where(m => m.ModifiedAt >= start && m.ModifiedAt < end && m.NewTotal < m.PreviousTotal)
            .GroupBy(m => m.ModifiedById)
            .Select(g => new { UserId = g.Key, Count = g.Count(), Amount = g.Sum(m => m.PreviousTotal - m.NewTotal) })
            .ToDictionaryAsync(x => x.UserId, cancellationToken);

        List<string> userIds = paid.Select(p => p.UserId).Concat(cancelled.Keys).Concat(reductions.Keys).Distinct().ToList();
        Dictionary<string, string> names = await db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return userIds
            .Select(userId =>
            {
                List<PaymentMethodAmountDto> methods = ToPaymentMethods(paid
                    .Where(p => p.UserId == userId)
                    .Select(p => new PaymentMethodAmountDto(p.Method, p.Count, p.Amount)));
                int salesCount = methods.Sum(m => m.SalesCount);
                decimal revenue = methods.Sum(m => m.Amount);
                var userCancelled = cancelled.GetValueOrDefault(userId);
                var userReductions = reductions.GetValueOrDefault(userId);

                return new CashierPerformanceDto(
                    userId,
                    names.GetValueOrDefault(userId, string.Empty),
                    salesCount,
                    revenue,
                    AverageTicket(revenue, salesCount),
                    methods,
                    userCancelled?.Count ?? 0,
                    userCancelled?.Amount ?? 0,
                    userReductions?.Count ?? 0,
                    userReductions?.Amount ?? 0);
            })
            .OrderByDescending(c => c.Revenue)
            .ThenBy(c => c.CashierName)
            .ToList();
    }

    public async Task<PagedResult<CancellationDto>> GetCancellationsAsync(
        ReportPeriod period,
        string? cashierId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        (DateTimeOffset start, DateTimeOffset end) = BusinessTime.ToUtcRange(period);
        IQueryable<Sale> query = db.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Cancelled && s.CancelledAt >= start && s.CancelledAt < end);

        if (!string.IsNullOrWhiteSpace(cashierId))
        {
            query = query.Where(s => s.CashierId == cashierId);
        }

        int totalCount = await query.CountAsync(cancellationToken);
        List<CancellationDto> items = await (
                from sale in query
                join canceller in db.Users on sale.CancelledById equals canceller.Id
                orderby sale.CancelledAt descending, sale.Id descending
                select new CancellationDto(
                    sale.Id,
                    sale.CancelledAt!.Value,
                    sale.Cashier!.FullName,
                    canceller.FullName,
                    sale.PaidAt != null ? SaleStatus.Paid : SaleStatus.Preparing,
                    sale.Total,
                    sale.CancelReason!))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CancellationDto>(items, page, pageSize, totalCount);
    }

    public async Task<MyDayDto> GetMyDayAsync(string userId, DateOnly today, CancellationToken cancellationToken)
    {
        ReportPeriod period = new(today, today);
        (DateTimeOffset start, DateTimeOffset end) = BusinessTime.ToUtcRange(period);

        List<PaymentMethodAmountDto> methods = ToPaymentMethods(await GetPaymentMethodRowsAsync(
            PaidSales(period).Where(s => s.PaidById == userId),
            cancellationToken));
        IQueryable<Sale> preparing = db.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Preparing
                && s.CashierId == userId
                && s.CreatedAt >= start
                && s.CreatedAt < end);

        return new MyDayDto(
            today,
            methods.Sum(m => m.SalesCount),
            methods.Sum(m => m.Amount),
            methods,
            await preparing.CountAsync(cancellationToken),
            await preparing.SumAsync(s => s.Total, cancellationToken));
    }

    private IQueryable<Sale> PaidSales(ReportPeriod period)
    {
        (DateTimeOffset start, DateTimeOffset end) = BusinessTime.ToUtcRange(period);

        return db.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Paid && s.PaidAt >= start && s.PaidAt < end);
    }

    private async Task<PaidTotals> GetPaidTotalsAsync(ReportPeriod period, CancellationToken cancellationToken)
    {
        IQueryable<Sale> paid = PaidSales(period);
        int salesCount = await paid.CountAsync(cancellationToken);
        decimal revenue = await paid.SumAsync(s => s.Total, cancellationToken);

        return new PaidTotals(
            revenue,
            await paid.SumAsync(s => s.Subtotal, cancellationToken),
            await paid.SumAsync(s => s.Tax, cancellationToken),
            salesCount,
            AverageTicket(revenue, salesCount));
    }

    // Local day and hour are computed in PostgreSQL so a sale charged at 11:30 p.m. stays on its local day.
    private async Task<List<HourlySales>> GetHourlySalesAsync(ReportPeriod period, CancellationToken cancellationToken)
    {
        (DateTimeOffset start, DateTimeOffset end) = BusinessTime.ToUtcRange(period);

        return await db.Database
            .SqlQuery<HourlySales>($"""
                SELECT ("PaidAt" AT TIME ZONE {BusinessTime.TimeZoneId})::date AS "Date",
                       EXTRACT(HOUR FROM "PaidAt" AT TIME ZONE {BusinessTime.TimeZoneId})::int AS "Hour",
                       COUNT(*)::int AS "SalesCount",
                       SUM("Total") AS "Revenue"
                FROM "Sales"
                WHERE "Status" = 'Paid' AND "PaidAt" >= {start} AND "PaidAt" < {end}
                GROUP BY 1, 2
                """)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<ProductSalesRow>> GetProductSalesAsync(
        ReportPeriod period,
        CancellationToken cancellationToken) =>
        await PaidSales(period)
            .SelectMany(s => s.Items, (sale, item) => new { sale.PaidAt, item })
            .GroupBy(x => new { x.item.ProductId, x.item.ProductName, x.item.CategoryName, x.item.SubcategoryName })
            .Select(g => new ProductSalesRow(
                g.Key.ProductId,
                g.Key.ProductName,
                g.Key.CategoryName,
                g.Key.SubcategoryName,
                g.Sum(x => x.item.Quantity),
                g.Sum(x => x.item.Amount),
                g.Max(x => x.PaidAt)))
            .ToListAsync(cancellationToken);

    private static async Task<List<PaymentMethodAmountDto>> GetPaymentMethodRowsAsync(
        IQueryable<Sale> paidSales,
        CancellationToken cancellationToken) =>
        await paidSales
            .GroupBy(s => s.PaymentMethod)
            .Select(g => new PaymentMethodAmountDto(g.Key!.Value, g.Count(), g.Sum(s => s.Total)))
            .ToListAsync(cancellationToken);

    private static List<PaymentMethodAmountDto> ToPaymentMethods(IEnumerable<PaymentMethodAmountDto> rows)
    {
        Dictionary<PaymentMethod, PaymentMethodAmountDto> byMethod = rows.ToDictionary(r => r.Method);

        return Enum.GetValues<PaymentMethod>()
            .Select(method => byMethod.GetValueOrDefault(method) ?? new PaymentMethodAmountDto(method, 0, 0))
            .ToList();
    }

    private static DateTime IntervalStart(DateOnly date, int hour, ReportGrouping groupBy) => groupBy switch
    {
        ReportGrouping.Hour => date.ToDateTime(new TimeOnly(hour, 0)),
        ReportGrouping.Week => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)).ToDateTime(TimeOnly.MinValue),
        ReportGrouping.Month => new DateTime(date.Year, date.Month, 1),
        _ => date.ToDateTime(TimeOnly.MinValue),
    };

    private static decimal AverageTicket(decimal revenue, int salesCount) =>
        salesCount == 0 ? 0 : Math.Round(revenue / salesCount, 2, MidpointRounding.AwayFromZero);

    private static decimal Percentage(decimal part, decimal total) =>
        total == 0 ? 0 : Math.Round(part * 100 / total, 2, MidpointRounding.AwayFromZero);

    private static decimal? ChangePercentage(decimal current, decimal previous) =>
        previous == 0 ? null : Math.Round((current - previous) * 100 / previous, 2, MidpointRounding.AwayFromZero);

    private sealed record PaidTotals(decimal Revenue, decimal Subtotal, decimal Tax, int SalesCount, decimal AverageTicket);

    private sealed record ProductSalesRow(
        Guid ProductId,
        string ProductName,
        string CategoryName,
        string SubcategoryName,
        int Units,
        decimal Revenue,
        DateTimeOffset? LastSoldAt);

    private sealed class HourlySales
    {
        public DateOnly Date { get; init; }
        public int Hour { get; init; }
        public int SalesCount { get; init; }
        public decimal Revenue { get; init; }
    }
}
