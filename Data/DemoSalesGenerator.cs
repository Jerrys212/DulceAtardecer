using DulceAtardecer.Models;
using DulceAtardecer.Services;

namespace DulceAtardecer.Data;

internal static class DemoSalesGenerator
{
    private const int HistoryDays = 730;
    private const int RandomSeed = 2305;
    private const decimal TaxRateFactor = 1.16m;
    private const string DrinksCategory = "Bebidas";
    private const int PendingSalesCount = 3;

    public static readonly (string UserName, string FullName)[] Cashiers =
    [
        ("demo.valeria", "Valeria Ruiz"),
        ("demo.diego", "Diego Hernández"),
        ("demo.sofia", "Sofía Martínez"),
    ];

    private static readonly (int Hour, int Weight)[] Hours =
    [
        (12, 3), (13, 5), (14, 6), (15, 5), (16, 6), (17, 9), (18, 12), (19, 13), (20, 11), (21, 6),
    ];

    private static readonly (int Lines, int Weight)[] LineCounts = [(1, 40), (2, 35), (3, 18), (4, 7)];

    private static readonly (int Quantity, int Weight)[] Quantities = [(1, 80), (2, 17), (3, 3)];

    private static readonly (PaymentMethod Method, int Weight)[] PaymentMethods =
    [
        (PaymentMethod.Cash, 55), (PaymentMethod.Card, 35), (PaymentMethod.Transfer, 10),
    ];

    private static readonly string[] Customers =
    [
        "Mesa 1", "Mesa 2", "Mesa 3", "Mesa 4", "Mesa 5", "Para llevar", "Ana", "Luis", "Carlos", "Fernanda", "Jorge", "Mariana",
    ];

    private static readonly string[] FoodNotes = ["Sin azúcar glass", "Bien dorada", "Cortada en triángulos", "Para llevar"];

    private static readonly string[] DrinkNotes = ["Sin crema batida", "Con leche deslactosada", "Poco hielo", "Para llevar"];

    private static readonly string[] UnpaidCancelReasons =
    [
        "El cliente ya no quiso el pedido", "Pedido duplicado por error", "El cliente se fue antes de pagar",
    ];

    private static readonly string[] PaidCancelReasons =
    [
        "El producto salió mal, se reembolsó", "Cobro con método de pago equivocado", "Error en el pedido, se devolvió el dinero",
    ];

    public static List<Sale> Generate(
        IReadOnlyList<Product> products,
        IReadOnlyList<Extra> extras,
        IReadOnlyList<string> cashierIds,
        string adminId,
        DateTimeOffset now)
    {
        Random rng = new(RandomSeed);
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(BusinessTime.TimeZoneId);
        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        (Product Value, int Weight)[] weightedProducts = products.Select(p => (p, rng.Next(1, 10))).ToArray();
        List<Sale> sales = [];

        for (int daysAgo = HistoryDays; daysAgo >= 0; daysAgo--)
        {
            DateOnly date = today.AddDays(-daysAgo);
            int count = SalesForDay(rng, date, daysAgo);

            for (int i = 0; i < count; i++)
            {
                DateTimeOffset createdAt = RandomTime(rng, zone, date);
                if (createdAt > now)
                {
                    continue;
                }

                Sale sale = BuildSale(rng, createdAt, cashierIds[rng.Next(cashierIds.Count)], weightedProducts, extras);
                sales.Add(sale);
            }
        }

        foreach (Sale sale in sales.SkipLast(PendingSalesCount))
        {
            ResolveStatus(rng, sale, adminId, now);
        }

        return sales;
    }

    private static int SalesForDay(Random rng, DateOnly date, int daysAgo)
    {
        int baseCount = daysAgo switch
        {
            <= 30 => rng.Next(3, 9),
            <= 180 => rng.Next(0, 5),
            _ => rng.Next(0, 2),
        };
        bool isWeekend = date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday;

        return isWeekend ? baseCount + rng.Next(0, 2) : baseCount;
    }

    private static DateTimeOffset RandomTime(Random rng, TimeZoneInfo zone, DateOnly date)
    {
        DateTime local = date.ToDateTime(new TimeOnly(Pick(rng, Hours), rng.Next(60), rng.Next(60)));

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
    }

    private static Sale BuildSale(
        Random rng,
        DateTimeOffset createdAt,
        string cashierId,
        (Product Value, int Weight)[] products,
        IReadOnlyList<Extra> extras)
    {
        Guid id = Guid.CreateVersion7(createdAt);
        Sale sale = new()
        {
            Id = id,
            CashierId = cashierId,
            IdempotencyKey = $"demo-{id}",
            CustomerDescription = rng.NextDouble() < 0.5 ? Customers[rng.Next(Customers.Length)] : null,
            CreatedAt = createdAt,
            CreatedById = cashierId,
        };

        int lineCount = Pick(rng, LineCounts);
        for (int position = 0; position < lineCount; position++)
        {
            sale.Items.Add(BuildItem(rng, sale.Id, position, Pick(rng, products), extras));
        }

        sale.Total = sale.Items.Sum(i => i.Amount);
        sale.Subtotal = Math.Round(sale.Total / TaxRateFactor, 2, MidpointRounding.AwayFromZero);
        sale.Tax = sale.Total - sale.Subtotal;

        return sale;
    }

    private static SaleItem BuildItem(Random rng, Guid saleId, int position, Product product, IReadOnlyList<Extra> extras)
    {
        string categoryName = product.Subcategory!.Category!.Name;
        bool isDrink = categoryName == DrinksCategory;
        List<SaleItemExtra> itemExtras = isDrink ? [] : PickExtras(rng, extras);
        decimal unitPrice = product.Price + itemExtras.Sum(e => e.Price * e.Quantity);
        int quantity = Pick(rng, Quantities);
        string[] notes = isDrink ? DrinkNotes : FoodNotes;

        return new SaleItem
        {
            SaleId = saleId,
            Position = position,
            ProductId = product.Id,
            ProductName = product.Name,
            CategoryName = categoryName,
            SubcategoryName = product.Subcategory.Name,
            ProductPrice = product.Price,
            UnitPrice = unitPrice,
            Quantity = quantity,
            Amount = unitPrice * quantity,
            Note = rng.NextDouble() < 0.1 ? notes[rng.Next(notes.Length)] : null,
            Extras = itemExtras,
        };
    }

    private static List<SaleItemExtra> PickExtras(Random rng, IReadOnlyList<Extra> extras)
    {
        double roll = rng.NextDouble();
        int count = roll < 0.08 ? 2 : roll < 0.35 ? 1 : 0;

        return extras
            .OrderBy(_ => rng.Next())
            .Take(count)
            .Select((extra, index) => new SaleItemExtra
            {
                Position = index,
                ExtraId = extra.Id,
                Name = extra.Name,
                Price = extra.Price,
                Quantity = 1,
            })
            .ToList();
    }

    private static void ResolveStatus(Random rng, Sale sale, string adminId, DateTimeOffset now)
    {
        if (sale.CreatedAt.AddMinutes(30) > now)
        {
            return;
        }

        double roll = rng.NextDouble();

        if (roll < 0.04)
        {
            sale.Status = SaleStatus.Cancelled;
            sale.CancelReason = UnpaidCancelReasons[rng.Next(UnpaidCancelReasons.Length)];
            sale.CancelledAt = sale.CreatedAt.AddMinutes(rng.Next(2, 16));
            sale.CancelledById = sale.CashierId;
            return;
        }

        sale.Status = SaleStatus.Paid;
        sale.PaymentMethod = Pick(rng, PaymentMethods);
        sale.PaidAt = sale.CreatedAt.AddMinutes(rng.Next(5, 26));
        sale.PaidById = sale.CashierId;

        DateTimeOffset cancelledAt = sale.PaidAt.Value.AddMinutes(rng.Next(5, 61));

        if (roll < 0.07 && cancelledAt <= now)
        {
            sale.Status = SaleStatus.Cancelled;
            sale.CancelReason = PaidCancelReasons[rng.Next(PaidCancelReasons.Length)];
            sale.CancelledAt = cancelledAt;
            sale.CancelledById = adminId;
        }
    }

    private static T Pick<T>(Random rng, IReadOnlyList<(T Value, int Weight)> options)
    {
        int roll = rng.Next(options.Sum(o => o.Weight));

        foreach ((T value, int weight) in options)
        {
            if (roll < weight)
            {
                return value;
            }

            roll -= weight;
        }

        return options[^1].Value;
    }
}
