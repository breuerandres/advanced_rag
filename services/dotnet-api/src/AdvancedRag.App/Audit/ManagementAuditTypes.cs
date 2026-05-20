namespace AdvancedRag.App.Audit;

public sealed record ManagementAuditQuery(
    string? EventType,
    string? EntityType,
    Guid? ActorUserId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Limit);

public sealed record ManagementAuditEvent(
    Guid Id,
    Guid? ActorUserId,
    string? ActorDisplayName,
    string EventType,
    string EntityType,
    Guid? EntityId,
    IReadOnlyDictionary<string, object?> Details,
    string RequestId,
    DateTimeOffset CreatedAt);

public interface IManagementAuditService
{
    Task<IReadOnlyList<ManagementAuditEvent>> ListEventsAsync(
        ManagementAuditQuery query,
        CancellationToken ct);
}
