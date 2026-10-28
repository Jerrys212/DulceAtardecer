using DulceAtardecer.Data;
using DulceAtardecer.Models;
using DulceAtardecer.Repository.IRepository;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DulceAtardecer.Repository;

public class ExtraRepository(ApplicationDbContext db) : IExtraRepository
{
    private const string NotFoundMessage = "Extra no encontrado";
    private const string DuplicateMessage = "Ya existe un extra con ese nombre";

    public async Task<IReadOnlyList<Extra>> GetActiveAsync(CancellationToken cancellationToken) =>
        await db.Extras
            .AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Extra>> GetAllAsync(CancellationToken cancellationToken) =>
        await db.Extras
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken);

    public async Task<Extra> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Extras.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, cancellationToken)
        ?? throw new ApiException(StatusCodes.Status404NotFound, NotFoundMessage);

    public async Task<Extra> CreateAsync(string name, decimal price, CancellationToken cancellationToken)
    {
        string trimmedName = name.Trim();
        await EnsureNameAvailableAsync(trimmedName, null, cancellationToken);

        Extra extra = new() { Name = trimmedName, Price = price };
        db.Extras.Add(extra);
        await SaveChangesAsync(cancellationToken);

        return extra;
    }

    public async Task<Extra> UpdateAsync(Guid id, string name, decimal price, CancellationToken cancellationToken)
    {
        string trimmedName = name.Trim();
        Extra extra = await db.Extras.SingleOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, NotFoundMessage);

        await EnsureNameAvailableAsync(trimmedName, id, cancellationToken);

        extra.Name = trimmedName;
        extra.Price = price;
        await SaveChangesAsync(cancellationToken);

        return extra;
    }

    public async Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        Extra extra = await db.Extras.SingleOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new ApiException(StatusCodes.Status404NotFound, NotFoundMessage);

        if (extra.IsActive == isActive)
        {
            return false;
        }

        extra.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task EnsureNameAvailableAsync(string name, Guid? excludedId, CancellationToken cancellationToken)
    {
        if (await db.Extras.AnyAsync(e => e.Name == name && e.Id != excludedId, cancellationToken))
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
}
