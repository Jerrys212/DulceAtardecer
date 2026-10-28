using DulceAtardecer.Data;
using DulceAtardecer.Models;
using DulceAtardecer.Repository.IRepository;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DulceAtardecer.Repository;

public class SaleRepository(ApplicationDbContext db, TimeProvider timeProvider) : ISaleRepository
{
    private const string NotFoundMessage = "Venta no encontrada";
    private const string UnavailableItemsMessage = "Hay productos o extras que no están disponibles";
    private const string PricesChangedMessage = "Los precios cambiaron, revisa el pedido";
    private const string ModifyNotPreparingMessage = "Solo se pueden modificar ventas en preparación";
    private const string PayNotPreparingMessage = "Solo se pueden cobrar ventas en preparación";
    private const string AlreadyCancelledMessage = "La venta ya está cancelada";
    private const string CancelPaidForbiddenMessage = "Solo un administrador puede cancelar ventas cobradas";
    private const string ConcurrencyMessage = "La venta fue modificada por otro usuario, vuelve a cargarla";
    private const decimal TaxRateFactor = 1.16m;

    public async Task<IReadOnlyList<Sale>> GetPendingAsync(CancellationToken cancellationToken) =>
        await WithDetails()
            .Where(s => s.Status == SaleStatus.Preparing)
            .OrderBy(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);

    public async Task<Sale> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await WithDetails().SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
        ?? throw new ApiException(StatusCodes.Status404NotFound, NotFoundMessage);

    public async Task<(Sale Sale, bool Created)> CreateAsync(
        string cashierId,
        string idempotencyKey,
        string? customerDescription,
        decimal expectedTotal,
        IReadOnlyList<SaleLine> lines,
        CancellationToken cancellationToken)
    {
        string trimmedKey = idempotencyKey.Trim();
        Sale? existing = await FindByIdempotencyKeyAsync(cashierId, trimmedKey, cancellationToken);
        if (existing is not null)
        {
            return (existing, false);
        }

        Sale sale = new()
        {
            CashierId = cashierId,
            IdempotencyKey = trimmedKey,
            CustomerDescription = NormalizeText(customerDescription),
        };
        sale.Items = await PriceLinesAsync(sale, lines, expectedTotal, cancellationToken);
        db.Sales.Add(sale);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        // Two requests with the same key can both pass the lookup; the unique index keeps only one sale.
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            Sale duplicate = await FindByIdempotencyKeyAsync(cashierId, trimmedKey, cancellationToken)
                ?? throw new ApiException(StatusCodes.Status409Conflict);

            return (duplicate, false);
        }

        return (await GetByIdAsync(sale.Id, cancellationToken), true);
    }

    public async Task<Sale> UpdateAsync(
        Guid id,
        string userId,
        string? customerDescription,
        decimal expectedTotal,
        IReadOnlyList<SaleLine> lines,
        CancellationToken cancellationToken)
    {
        Sale sale = await db.Sales
            .Include(s => s.Items)
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, NotFoundMessage);

        if (sale.Status != SaleStatus.Preparing)
        {
            throw new ApiException(StatusCodes.Status409Conflict, ModifyNotPreparingMessage);
        }

        decimal previousTotal = sale.Total;
        List<SaleItem> items = await PriceLinesAsync(sale, lines, expectedTotal, cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();

        db.SaleItems.RemoveRange(sale.Items);
        db.SaleItems.AddRange(items);
        sale.CustomerDescription = NormalizeText(customerDescription);
        // Always touching the row makes its version check catch concurrent edits even when totals match.
        sale.UpdatedAt = now;
        db.SaleModifications.Add(new SaleModification
        {
            SaleId = sale.Id,
            ModifiedById = userId,
            ModifiedAt = now,
            PreviousTotal = previousTotal,
            NewTotal = sale.Total,
        });
        await SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(sale.Id, cancellationToken);
    }

    public async Task<Sale> PayAsync(
        Guid id,
        string userId,
        PaymentMethod paymentMethod,
        CancellationToken cancellationToken)
    {
        Sale sale = await FindTrackedAsync(id, cancellationToken);

        if (sale.Status != SaleStatus.Preparing)
        {
            throw new ApiException(StatusCodes.Status409Conflict, PayNotPreparingMessage);
        }

        sale.Status = SaleStatus.Paid;
        sale.PaymentMethod = paymentMethod;
        sale.PaidAt = timeProvider.GetUtcNow();
        sale.PaidById = userId;
        await SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(sale.Id, cancellationToken);
    }

    public async Task<Sale> CancelAsync(
        Guid id,
        string userId,
        string reason,
        bool canCancelPaid,
        CancellationToken cancellationToken)
    {
        Sale sale = await FindTrackedAsync(id, cancellationToken);

        if (sale.Status == SaleStatus.Cancelled)
        {
            throw new ApiException(StatusCodes.Status409Conflict, AlreadyCancelledMessage);
        }

        if (sale.Status == SaleStatus.Paid && !canCancelPaid)
        {
            throw new ApiException(StatusCodes.Status403Forbidden, CancelPaidForbiddenMessage);
        }

        sale.Status = SaleStatus.Cancelled;
        sale.CancelReason = reason.Trim();
        sale.CancelledAt = timeProvider.GetUtcNow();
        sale.CancelledById = userId;
        await SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(sale.Id, cancellationToken);
    }

    private IQueryable<Sale> WithDetails() =>
        db.Sales
            .AsNoTracking()
            .AsSplitQuery()
            .Include(s => s.Cashier)
            .Include(s => s.Items.OrderBy(i => i.Position))
            .ThenInclude(i => i.Extras.OrderBy(e => e.Position));

    private async Task<Sale?> FindByIdempotencyKeyAsync(
        string cashierId,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        await WithDetails().SingleOrDefaultAsync(
            s => s.CashierId == cashierId && s.IdempotencyKey == idempotencyKey,
            cancellationToken);

    private async Task<Sale> FindTrackedAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Sales.SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
        ?? throw new ApiException(StatusCodes.Status404NotFound, NotFoundMessage);

    private async Task<List<SaleItem>> PriceLinesAsync(
        Sale sale,
        IReadOnlyList<SaleLine> lines,
        decimal expectedTotal,
        CancellationToken cancellationToken)
    {
        List<Guid> productIds = lines.Select(l => l.ProductId).Distinct().ToList();
        List<Guid> extraIds = lines.SelectMany(l => l.Extras ?? []).Select(e => e.ExtraId).Distinct().ToList();

        Dictionary<Guid, Product> products = await db.Products
            .AsNoTracking()
            .Include(p => p.Subcategory)
            .ThenInclude(s => s!.Category)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
        Dictionary<Guid, Extra> extras = await db.Extras
            .AsNoTracking()
            .Where(e => extraIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        EnsureAvailable(productIds, products, extraIds, extras);

        List<SaleItem> items = lines.Select((line, index) =>
        {
            Product product = products[line.ProductId];
            List<SaleItemExtra> itemExtras = (line.Extras ?? []).Select((lineExtra, extraIndex) => new SaleItemExtra
            {
                Position = extraIndex,
                ExtraId = lineExtra.ExtraId,
                Name = extras[lineExtra.ExtraId].Name,
                Price = extras[lineExtra.ExtraId].Price,
                Quantity = lineExtra.Quantity,
            }).ToList();
            decimal unitPrice = product.Price + itemExtras.Sum(e => e.Price * e.Quantity);

            return new SaleItem
            {
                SaleId = sale.Id,
                Position = index,
                ProductId = product.Id,
                ProductName = product.Name,
                CategoryName = product.Subcategory!.Category!.Name,
                SubcategoryName = product.Subcategory.Name,
                ProductPrice = product.Price,
                UnitPrice = unitPrice,
                Quantity = line.Quantity,
                Amount = unitPrice * line.Quantity,
                Note = NormalizeText(line.Note),
                Extras = itemExtras,
            };
        }).ToList();

        decimal total = items.Sum(i => i.Amount);
        if (total != expectedTotal)
        {
            throw new ApiException(StatusCodes.Status409Conflict, PricesChangedMessage, responseData: total);
        }

        sale.Total = total;
        sale.Subtotal = Math.Round(total / TaxRateFactor, 2, MidpointRounding.AwayFromZero);
        sale.Tax = total - sale.Subtotal;

        return items;
    }

    private static void EnsureAvailable(
        List<Guid> productIds,
        Dictionary<Guid, Product> products,
        List<Guid> extraIds,
        Dictionary<Guid, Extra> extras)
    {
        List<string> errors = [];

        foreach (Guid productId in productIds)
        {
            if (!products.TryGetValue(productId, out Product? product))
            {
                errors.Add($"El producto {productId} no existe");
            }
            else if (!product.IsActive || !product.Subcategory!.IsActive || !product.Subcategory.Category!.IsActive)
            {
                errors.Add($"El producto '{product.Name}' no está disponible");
            }
        }

        foreach (Guid extraId in extraIds)
        {
            if (!extras.TryGetValue(extraId, out Extra? extra))
            {
                errors.Add($"El extra {extraId} no existe");
            }
            else if (!extra.IsActive)
            {
                errors.Add($"El extra '{extra.Name}' no está activo");
            }
        }

        if (errors.Count > 0)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, UnavailableItemsMessage, errors);
        }
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, ConcurrencyMessage);
        }
    }

    private static string? NormalizeText(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
