using System.Security.Claims;
using AdvancedRag.Api.Errors;
using AdvancedRag.Api.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace AdvancedRag.Api.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult Error(
        int statusCode,
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? details = null)
    {
        return StatusCode(statusCode, ErrorResponse.Create(code, message, RequestId(), details));
    }

    protected string RequestId()
    {
        return HttpContext.Items.TryGetValue(RequestIdMiddleware.ContextItemKey, out var value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;
    }

    protected Guid ActorUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userId, out var parsed) ? parsed : Guid.Empty;
    }
}
