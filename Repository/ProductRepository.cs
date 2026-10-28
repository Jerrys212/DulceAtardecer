using DulceAtardecer.Data;
using DulceAtardecer.Models;
using DulceAtardecer.Repository.IRepository;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DulceAtardecer.Repository;

public class ProductRepository(ApplicationDbContext db) : IProductRepository
{
    private const string NotFoundMessage = "Producto no encontrado";
    private const string DuplicateMessage = "Ya existe un producto con ese nombre";
    private const string SubcategoryMismatchMessage = "La subcategoría no pertenece a la categoría seleccionada";
    private const string SubcategoryUnavailableMessage = "La subcategoría seleccionada no está disponible";
    private const string LikeEscapeCharacter = @"\";

    public async Task<IReadOnlyList<Product>> GetAvailableAsync(
        Guid? categoryId,
        Guid? subcategoryId,
        string? search,
        CancellationToken cancellationToken) =>
        await Filter(WithSubcategory(), categoryId, subcategoryId, search)
            .Where(p => p.IsActive && p.Subcategory!.IsActive && p.Subcategory.Category!.IsActive)
            .OrderBy(p => p.Subcategory!.Category!.Name)
            .ThenBy(p => p.Subcategory!.Name)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Product>> GetAllAsync(
        Guid? categoryId,
        Guid? subcategoryId,
        string? search,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        IQueryable<Product> query = Filter(WithSubcategory(), categoryId, subcategoryId, search);

        if (isActive is not null)
        {
            query = query.Where(p => p.IsActive == isActive);
        }

        return await query
            .OrderBy(p => p.Subcategory!.Category!.Name)
            .ThenBy(p => p.Subcategory!.Name)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Product> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await WithSubcategory().SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
        ?? throw new ApiException(StatusCodes.Status404NotFound, NotFoundMessage);

    public async Task<Product> CreateAsync(
        string name,
        string? description,
        decimal price,
        Guid categoryId,
        Guid subcategoryId,
        CancellationToken cancellationToken)
    {
        string trimmedName = name.Trim();
        await EnsureSubcategoryAvailableAsync(categoryId, subcategoryId, cancellationToken);
        await EnsureNameAvailableAsync(trimmedName, null, cancellationToken);

        Product product = new()
        {
            Name = trimmedName,
            Description = NormalizeDescription(description),
            Price = price,
            SubcategoryId = subcategoryId,
        };
        db.Products.Add(product);
        await SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(product.Id, cancellationToken);
    }

    public async Task<Product> UpdateAsync(
        Guid id,
        string name,
        string? description,
        decimal price,
        Guid categoryId,
        Guid subcategoryId,
        CancellationToken cancellationToken)
    {
        string trimmedName = name.Trim();
        Product product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, NotFoundMessage);

        await EnsureSubcategoryAvailableAsync(categoryId, subcategoryId, cancellationToken);
        await EnsureNameAvailableAsync(trimmedName, id, cancellationToken);

        product.Name = trimmedName;
        product.Description = NormalizeDescription(description);
        product.Price = price;
        product.SubcategoryId = subcategoryId;
        await SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(product.Id, cancellationToken);
    }

    public async Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        Product product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, NotFoundMessage);

        if (product.IsActive == isActive)
        {
            return false;
        }

        product.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private IQueryable<Product> WithSubcategory() =>
        db.Products
            .AsNoTracking()
            .Include(p => p.Subcategory)
            .ThenInclude(s => s!.Category);

    private static IQueryable<Product> Filter(
        IQueryable<Product> query,
        Guid? categoryId,
        Guid? subcategoryId,
        string? search)
    {
        if (categoryId is not null)
        {
            query = query.Where(p => p.Subcategory!.CategoryId == categoryId);
        }

        if (subcategoryId is not null)
        {
            query = query.Where(p => p.SubcategoryId == subcategoryId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{EscapeLikePattern(search.Trim())}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, pattern, LikeEscapeCharacter));
        }

        return query;
    }

    private async Task EnsureSubcategoryAvailableAsync(
        Guid categoryId,
        Guid subcategoryId,
        CancellationToken cancellationToken)
    {
        var subcategory = await db.Subcategories
            .AsNoTracking()
            .Where(s => s.Id == subcategoryId && s.CategoryId == categoryId)
            .Select(s => new { IsAvailable = s.IsActive && s.Category!.IsActive })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ApiException(StatusCodes.Status400BadRequest, SubcategoryMismatchMessage);

        if (!subcategory.IsAvailable)
        {
            throw new ApiException(StatusCodes.Status400BadRequest, SubcategoryUnavailableMessage);
        }
    }

    private async Task EnsureNameAvailableAsync(string name, Guid? excludedId, CancellationToken cancellationToken)
    {
        if (await db.Products.AnyAsync(p => p.Name == name && p.Id != excludedId, cancellationToken))
        {
            throw new ApiException(StatusCodes.Status409Conflict, DuplicateMessage);
        }
    }

    // The pre-check misses concurrent requests; the unique index is the final guard.
    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ApiException(StatusCodes.Status409Conflict, DuplicateMessage);
        }
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static string EscapeLikePattern(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
