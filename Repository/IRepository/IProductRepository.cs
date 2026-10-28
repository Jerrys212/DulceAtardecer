using DulceAtardecer.Models.Dtos;

namespace DulceAtardecer.Repository.IRepository;

public interface IProductRepository
{
    Task<IReadOnlyList<AvailableProductDto>> GetAvailableAsync(
        Guid? categoryId,
        Guid? subcategoryId,
        string? search,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductDto>> GetAllAsync(
        Guid? categoryId,
        Guid? subcategoryId,
        string? search,
        bool? isActive,
        CancellationToken cancellationToken);

    Task<ProductDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ProductDto> CreateAsync(
        string name,
        string? description,
        decimal price,
        Guid categoryId,
        Guid subcategoryId,
        CancellationToken cancellationToken);

    Task<ProductDto> UpdateAsync(
        Guid id,
        string name,
        string? description,
        decimal price,
        Guid categoryId,
        Guid subcategoryId,
        CancellationToken cancellationToken);

    Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken);
}
