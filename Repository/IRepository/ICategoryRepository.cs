using DulceAtardecer.Models;

namespace DulceAtardecer.Repository.IRepository;

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> GetAvailableAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken);

    Task<Category> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Category> CreateAsync(string name, IReadOnlyList<string> subcategoryNames, CancellationToken cancellationToken);

    Task<Category> UpdateAsync(Guid id, string name, CancellationToken cancellationToken);

    Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken);

    Task<Subcategory> CreateSubcategoryAsync(Guid categoryId, string name, CancellationToken cancellationToken);

    Task<Subcategory> UpdateSubcategoryAsync(
        Guid categoryId,
        Guid subcategoryId,
        string name,
        CancellationToken cancellationToken);

    Task<bool> SetSubcategoryActiveAsync(
        Guid categoryId,
        Guid subcategoryId,
        bool isActive,
        CancellationToken cancellationToken);
}
