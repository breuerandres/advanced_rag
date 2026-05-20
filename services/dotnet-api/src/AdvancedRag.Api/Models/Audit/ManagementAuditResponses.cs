namespace AdvancedRag.Api.Models.Audit;

public sealed record ManagementAuditEventResponse(
    Guid Id,
    Guid? ActorUserId,
    string? ActorDisplayName,
    string EventType,
    string EventLabel,
    string EntityType,
    Guid? EntityId,
    IReadOnlyDictionary<string, object?> Details,
    string RequestId,
    DateTimeOffset CreatedAt);
