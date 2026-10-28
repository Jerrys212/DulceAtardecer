using System.Text.Json.Serialization;

namespace DulceAtardecer.Models;

public class Sale : IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string CashierId { get; set; }
    public ApplicationUser? Cashier { get; set; }
    public required string IdempotencyKey { get; set; }
    public string? CustomerDescription { get; set; }
    public SaleStatus Status { get; set; } = SaleStatus.Preparing;
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public string? PaidById { get; set; }
    public string? CancelReason { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancelledById { get; set; }
    public uint Version { get; set; }
    public List<SaleItem> Items { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedById { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedById { get; set; }
}

public class SaleItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SaleId { get; set; }
    public int Position { get; set; }
    public Guid ProductId { get; set; }
    public required string ProductName { get; set; }
    public required string CategoryName { get; set; }
    public required string SubcategoryName { get; set; }
    public decimal ProductPrice { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    public List<SaleItemExtra> Extras { get; set; } = [];
}

public class SaleItemExtra
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SaleItemId { get; set; }
    public int Position { get; set; }
    public Guid ExtraId { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}

public class SaleModification
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SaleId { get; set; }
    public required string ModifiedById { get; set; }
    public DateTimeOffset ModifiedAt { get; set; }
    public decimal PreviousTotal { get; set; }
    public decimal NewTotal { get; set; }
}

public record SaleLine(Guid ProductId, int Quantity, string? Note, IReadOnlyList<SaleLineExtra>? Extras);

public record SaleLineExtra(Guid ExtraId, int Quantity);

[JsonConverter(typeof(JsonStringEnumConverter<SaleStatus>))]
public enum SaleStatus
{
    Preparing,
    Paid,
    Cancelled,
}

[JsonConverter(typeof(JsonStringEnumConverter<PaymentMethod>))]
public enum PaymentMethod
{
    Cash,
    Card,
    Transfer,
}
