using DulceAtardecer.Models;

namespace DulceAtardecer.Repository.IRepository;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetAvailableAsync(
        Guid? categoryId,
        Guid? subcategoryId,
        string? search,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Product>> GetAllAsync(
        Guid? categoryId,
        Guid? subcategoryId,
        string? search,
        bool? isActive,
        CancellationToken cancellationToken);

    Task<Product> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Product> CreateAsync(
        string name,
        string? description,
        decimal price,
        Guid categoryId,
        Guid subcategoryId,
        CancellationToken cancellationToken);

    Task<Product> UpdateAsync(
        Guid id,
        string name,
        string? description,
        decimal price,
        Guid categoryId,
        Guid subcategoryId,
        CancellationToken cancellationToken);

    Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken);
}
