using DulceAtardecer.Models;

namespace DulceAtardecer.Repository.IRepository;

public interface ISaleRepository
{
    Task<IReadOnlyList<Sale>> GetPendingAsync(CancellationToken cancellationToken);

    Task<Sale> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<(Sale Sale, bool Created)> CreateAsync(
        string cashierId,
        string idempotencyKey,
        string? customerDescription,
        decimal expectedTotal,
        IReadOnlyList<SaleLine> lines,
        CancellationToken cancellationToken);

    Task<Sale> UpdateAsync(
        Guid id,
        string userId,
        string? customerDescription,
        decimal expectedTotal,
        IReadOnlyList<SaleLine> lines,
        CancellationToken cancellationToken);

    Task<Sale> PayAsync(Guid id, string userId, PaymentMethod paymentMethod, CancellationToken cancellationToken);

    Task<Sale> CancelAsync(
        Guid id,
        string userId,
        string reason,
        bool canCancelPaid,
        CancellationToken cancellationToken);
}
