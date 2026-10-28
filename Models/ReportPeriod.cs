namespace DulceAtardecer.Models;

public record ReportPeriod(DateOnly From, DateOnly To)
{
    public const int MaxHourlyDays = 31;

    public int Days => To.DayNumber - From.DayNumber + 1;

    public ReportPeriod Previous => new(From.AddDays(-Days), From.AddDays(-1));

    public IEnumerable<DateOnly> Dates => Enumerable.Range(0, Days).Select(From.AddDays);

    public static ReportPeriod Resolve(DateOnly? from, DateOnly? to, DateOnly today)
    {
        ReportPeriod period = new(from ?? today, to ?? today);

        if (period.From > period.To)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "La fecha inicial no puede ser mayor que la final");
        }

        if (period.To >= period.From.AddYears(2))
        {
            throw new ApiException(StatusCodes.Status400BadRequest, "El periodo no puede superar 2 años");
        }

        return period;
    }
}

public enum ReportGrouping
{
    Hour,
    Day,
    Week,
    Month,
}

public enum ProductRanking
{
    Quantity,
    Revenue,
}
