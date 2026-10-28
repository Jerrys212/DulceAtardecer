using DulceAtardecer.Models;

namespace DulceAtardecer.Repository.IRepository;

public interface IAuditRepository
{
    Task LogAsync(AuditEntry entry, CancellationToken cancellationToken);
}
