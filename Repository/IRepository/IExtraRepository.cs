using DulceAtardecer.Models;

namespace DulceAtardecer.Repository.IRepository;

public interface IExtraRepository
{
    Task<IReadOnlyList<Extra>> GetActiveAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Extra>> GetAllAsync(CancellationToken cancellationToken);

    Task<Extra> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Extra> CreateAsync(string name, decimal price, CancellationToken cancellationToken);

    Task<Extra> UpdateAsync(Guid id, string name, decimal price, CancellationToken cancellationToken);

    Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken);
}
