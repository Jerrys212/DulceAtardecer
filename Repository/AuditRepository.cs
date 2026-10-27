using System.Security.Claims;
using System.Text.Json;
using DulceAtardecer.Constants;
using DulceAtardecer.Data;
using DulceAtardecer.Models;
using DulceAtardecer.Repository.IRepository;

namespace DulceAtardecer.Repository;

public class AuditRepository(
    ApplicationDbContext db,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider) : IAuditRepository
{
    public async Task LogAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;

        db.AuditEvents.Add(new AuditEvent
        {
            OccurredAt = timeProvider.GetUtcNow(),
            UserId = entry.UserId ?? httpContext?.User.FindFirstValue(AppClaimTypes.Subject),
            Action = entry.Action,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            DetailsJson = entry.Details is null ? null : JsonSerializer.Serialize(entry.Details, JsonSerializerOptions.Web),
            IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
