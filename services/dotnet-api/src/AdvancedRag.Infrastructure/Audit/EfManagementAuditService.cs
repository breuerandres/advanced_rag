using System.Text.Json;
using AdvancedRag.App.Audit;
using AdvancedRag.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdvancedRag.Infrastructure.Audit;

public sealed class EfManagementAuditService : IManagementAuditService
{
    private readonly AppDbContext _db;

    public EfManagementAuditService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ManagementAuditEvent>> ListEventsAsync(
        ManagementAuditQuery query,
        CancellationToken ct)
    {
        IQueryable<AuditEvent> eventsQuery = _db.AuditEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            eventsQuery = eventsQuery.Where(audit => audit.EventType == query.EventType);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            eventsQuery = eventsQuery.Where(audit => audit.EntityType == query.EntityType);
        }

        if (query.ActorUserId is not null)
        {
            eventsQuery = eventsQuery.Where(audit => audit.ActorUserId == query.ActorUserId);
        }

        if (query.From is not null)
        {
            eventsQuery = eventsQuery.Where(audit => audit.CreatedAt >= query.From);
        }

        if (query.To is not null)
        {
            eventsQuery = eventsQuery.Where(audit => audit.CreatedAt < query.To);
        }

        int limit = Math.Clamp(query.Limit, 1, 200);
        List<AuditEventRow> rows = await (
                from audit in eventsQuery
                join actor in _db.Users.AsNoTracking()
                    on audit.ActorUserId equals (Guid?)actor.Id into actorRows
                from actor in actorRows.DefaultIfEmpty()
                orderby audit.CreatedAt descending
                select new AuditEventRow(
                    audit.Id,
                    audit.ActorUserId,
                    actor == null ? null : actor.DisplayName,
                    audit.EventType,
                    audit.EntityType,
                    audit.EntityId,
                    audit.DetailsJson,
                    audit.RequestId,
                    audit.CreatedAt))
            .Take(limit)
            .ToListAsync(ct);

        return rows
            .Select(row => new ManagementAuditEvent(
                row.Id,
                row.ActorUserId,
                row.ActorDisplayName,
                row.EventType,
                row.EntityType,
                row.EntityId,
                ParseDetails(row.DetailsJson),
                row.RequestId,
                row.CreatedAt))
            .ToList();
    }

    private static IReadOnlyDictionary<string, object?> ParseDetails(string detailsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(detailsJson)
                ?? new Dictionary<string, object?>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?> { ["raw"] = detailsJson };
        }
    }

    private sealed record AuditEventRow(
        Guid Id,
        Guid? ActorUserId,
        string? ActorDisplayName,
        string EventType,
        string EntityType,
        Guid? EntityId,
        string DetailsJson,
        string RequestId,
        DateTimeOffset CreatedAt);
}
