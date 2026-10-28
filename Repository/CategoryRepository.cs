using DulceAtardecer.Data;
using DulceAtardecer.Models;
using DulceAtardecer.Repository.IRepository;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DulceAtardecer.Repository;

public class CategoryRepository(ApplicationDbContext db) : ICategoryRepository
{
    private const string CategoryNotFoundMessage = "Categoría no encontrada";
    private const string SubcategoryNotFoundMessage = "Subcategoría no encontrada";
    private const string DuplicateCategoryMessage = "Ya existe una categoría con ese nombre";
    private const string DuplicateSubcategoryMessage = "Ya existe una subcategoría con ese nombre en esta categoría";

    public async Task<IReadOnlyList<Category>> GetAvailableAsync(CancellationToken cancellationToken) =>
        await db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Include(c => c.Subcategories.Where(s => s.IsActive).OrderBy(s => s.Name))
            .OrderBy(c => c.Name)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken) =>
        await db.Categories
            .AsNoTracking()
            .Include(c => c.Subcategories.OrderBy(s => s.Name))
            .OrderBy(c => c.Name)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    public async Task<Category> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Categories
            .AsNoTracking()
            .Include(c => c.Subcategories.OrderBy(s => s.Name))
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
        ?? throw new ApiException(StatusCodes.Status404NotFound, CategoryNotFoundMessage);

    public async Task<Category> CreateAsync(
        string name,
        IReadOnlyList<string> subcategoryNames,
        CancellationToken cancellationToken)
    {
        string trimmedName = name.Trim();
        List<string> trimmedSubcategoryNames = [.. subcategoryNames.Select(n => n.Trim())];

        string[] repeatedNames = trimmedSubcategoryNames
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        if (repeatedNames.Length > 0)
        {
            throw new ApiException(
                StatusCodes.Status400BadRequest,
                "Hay subcategorías repetidas en la petición",
                repeatedNames.Select(n => $"La subcategoría '{n}' está repetida"));
        }

        await EnsureCategoryNameAvailableAsync(trimmedName, null, cancellationToken);

        Category category = new() { Name = trimmedName };
        category.Subcategories.AddRange(trimmedSubcategoryNames
            .Order(StringComparer.InvariantCultureIgnoreCase)
            .Select(n => new Subcategory { CategoryId = category.Id, Name = n }));

        db.Categories.Add(category);
        await SaveChangesAsync(DuplicateCategoryMessage, cancellationToken);

        return category;
    }

    public async Task<Category> UpdateAsync(Guid id, string name, CancellationToken cancellationToken)
    {
        string trimmedName = name.Trim();
        Category category = await db.Categories
            .Include(c => c.Subcategories.OrderBy(s => s.Name))
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, CategoryNotFoundMessage);

        await EnsureCategoryNameAvailableAsync(trimmedName, id, cancellationToken);

        category.Name = trimmedName;
        await SaveChangesAsync(DuplicateCategoryMessage, cancellationToken);

        return category;
    }

    public async Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        Category category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, CategoryNotFoundMessage);

        if (category.IsActive == isActive)
        {
            return false;
        }

        category.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<Subcategory> CreateSubcategoryAsync(Guid categoryId, string name, CancellationToken cancellationToken)
    {
        string trimmedName = name.Trim();

        if (!await db.Categories.AnyAsync(c => c.Id == categoryId, cancellationToken))
        {
            throw new ApiException(StatusCodes.Status404NotFound, CategoryNotFoundMessage);
        }

        await EnsureSubcategoryNameAvailableAsync(categoryId, trimmedName, null, cancellationToken);

        Subcategory subcategory = new() { CategoryId = categoryId, Name = trimmedName };
        db.Subcategories.Add(subcategory);
        await SaveChangesAsync(DuplicateSubcategoryMessage, cancellationToken);

        return subcategory;
    }

    public async Task<Subcategory> UpdateSubcategoryAsync(
        Guid categoryId,
        Guid subcategoryId,
        string name,
        CancellationToken cancellationToken)
    {
        string trimmedName = name.Trim();
        Subcategory subcategory = await FindSubcategoryAsync(categoryId, subcategoryId, cancellationToken);

        await EnsureSubcategoryNameAvailableAsync(categoryId, trimmedName, subcategoryId, cancellationToken);

        subcategory.Name = trimmedName;
        await SaveChangesAsync(DuplicateSubcategoryMessage, cancellationToken);

        return subcategory;
    }

    public async Task<bool> SetSubcategoryActiveAsync(
        Guid categoryId,
        Guid subcategoryId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        Subcategory subcategory = await FindSubcategoryAsync(categoryId, subcategoryId, cancellationToken);

        if (subcategory.IsActive == isActive)
        {
            return false;
        }

        subcategory.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<Subcategory> FindSubcategoryAsync(
        Guid categoryId,
        Guid subcategoryId,
        CancellationToken cancellationToken) =>
        await db.Subcategories.SingleOrDefaultAsync(
            s => s.Id == subcategoryId && s.CategoryId == categoryId,
            cancellationToken)
        ?? throw new ApiException(StatusCodes.Status404NotFound, SubcategoryNotFoundMessage);

    private async Task EnsureCategoryNameAvailableAsync(string name, Guid? excludedId, CancellationToken cancellationToken)
    {
        if (await db.Categories.AnyAsync(c => c.Name == name && c.Id != excludedId, cancellationToken))
        {
            throw new ApiException(StatusCodes.Status409Conflict, DuplicateCategoryMessage);
        }
    }

    private async Task EnsureSubcategoryNameAvailableAsync(
        Guid categoryId,
        string name,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        if (await db.Subcategories.AnyAsync(
                s => s.CategoryId == categoryId && s.Name == name && s.Id != excludedId,
                cancellationToken))
        {
            throw new ApiException(StatusCodes.Status409Conflict, DuplicateSubcategoryMessage);
        }
    }

    // The pre-checks miss concurrent requests; the unique index is the final guard.
    private async Task SaveChangesAsync(string conflictMessage, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ApiException(StatusCodes.Status409Conflict, conflictMessage);
        }
    }
}
