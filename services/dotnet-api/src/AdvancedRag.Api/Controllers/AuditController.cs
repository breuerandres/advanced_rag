using AdvancedRag.Api.Models.Audit;
using AdvancedRag.App.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdvancedRag.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,DocumentManager")]
[Route("api/audit")]
public sealed class AuditController : ApiControllerBase
{
    private readonly IManagementAuditService _audit;

    public AuditController(IManagementAuditService audit)
    {
        _audit = audit;
    }

    [HttpGet("events")]
    public async Task<ActionResult<IReadOnlyList<ManagementAuditEventResponse>>> ListEvents(
        [FromQuery] string? eventType,
        [FromQuery] string? entityType,
        [FromQuery] Guid? actorUserId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        ManagementAuditQuery query = new(
            Normalize(eventType),
            Normalize(entityType),
            actorUserId,
            from,
            to,
            Math.Clamp(limit ?? 100, 1, 200));
        IReadOnlyList<ManagementAuditEvent> events = await _audit.ListEventsAsync(query, ct);
        return Ok(events.Select(ToResponse).ToList());
    }

    private static ManagementAuditEventResponse ToResponse(ManagementAuditEvent item)
    {
        return new ManagementAuditEventResponse(
            item.Id,
            item.ActorUserId,
            item.ActorDisplayName,
            item.EventType,
            EventLabel(item.EventType),
            item.EntityType,
            item.EntityId,
            item.Details,
            item.RequestId,
            item.CreatedAt);
    }

    private static string? Normalize(string? value)
    {
        string? normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized == "all" ? null : normalized;
    }

    private static string EventLabel(string eventType)
    {
        return eventType switch
        {
            "instruction.created" => "Documento creado",
            "instruction.draft_updated" => "Borrador actualizado",
            "instruction.send_to_review" => "Documento enviado a revision",
            "instruction.return_to_draft" => "Documento devuelto a borrador",
            "instruction.publish_requested" => "Publicacion solicitada",
            "instruction.indexing_failed" => "Indexacion fallida",
            "instruction.published" => "Documento publicado",
            "instruction.archived" => "Documento archivado",
            "instruction.restored" => "Documento restaurado",
            "user.created" => "Usuario creado",
            "user.deactivated" => "Usuario dado de baja",
            "user.reactivated" => "Usuario reactivado",
            "user.ai_budget_updated" => "Presupuesto actualizado",
            "group.created" => "Grupo creado",
            _ => eventType,
        };
    }
}
