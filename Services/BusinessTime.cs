using DulceAtardecer.Models;

namespace DulceAtardecer.Services;

public static class BusinessTime
{
    public const string TimeZoneId = "America/Mexico_City";

    private static readonly TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    public static DateOnly Today(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), TimeZone).DateTime);

    public static (DateTimeOffset Start, DateTimeOffset End) ToUtcRange(ReportPeriod period) =>
        (StartOfDayUtc(period.From), StartOfDayUtc(period.To.AddDays(1)));

    private static DateTimeOffset StartOfDayUtc(DateOnly date) =>
        new(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), TimeZone));
}
