using System.Security.Claims;
using AdvancedRag.Api.Models.Viewer;
using AdvancedRag.App.Auth;
using AdvancedRag.App.Viewer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdvancedRag.Api.Controllers;

[ApiController]
[Route("api/viewer")]
public sealed class ViewerController : ApiControllerBase
{
    private readonly IViewerAccessService _viewer;
    private readonly IViewerDocumentCatalogService _catalog;
    private readonly IAuthService _auth;

    public ViewerController(
        IViewerAccessService viewer,
        IViewerDocumentCatalogService catalog,
        IAuthService auth)
    {
        _viewer = viewer;
        _catalog = catalog;
        _auth = auth;
    }

    [HttpPost("links")]
    [Authorize]
    public async Task<IActionResult> CreateLinkAsync(
        [FromBody] CreateViewerLinkRequest request,
        CancellationToken ct)
    {
        try
        {
            ViewerLinkResult result = await _viewer.CreateLinkAsync(
                new CreateViewerLinkCommand(
                    request.DocumentId,
                    ActorUserId(),
                    ActorRoles(),
                    request.Purpose),
                ct);
            return Ok(ViewerLinkResponse.FromResult(result));
        }
        catch (ViewerAccessException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpGet("document")]
    [Authorize]
    public async Task<IActionResult> GetDocumentAsync([FromQuery] Guid documentId, CancellationToken ct)
    {
        AuthenticatedUser? user = await ResolveCurrentUserAsync(ct);
        if (user is null)
        {
            return Error(StatusCodes.Status401Unauthorized, "AUTH_REQUIRED", "Authentication required.");
        }

        try
        {
            ViewerDocumentResult result = await _viewer.GetDocumentAsync(
                new GetViewerDocumentCommand(documentId, user.Id, user.Roles),
                ct);
            return Ok(ViewerDocumentResponse.FromResult(result));
        }
        catch (ViewerAccessException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpGet("documents")]
    [Authorize]
    public async Task<IActionResult> ListDocumentsAsync(CancellationToken ct)
    {
        AuthenticatedUser? user = await ResolveCurrentUserAsync(ct);
        if (user is null)
        {
            return Error(StatusCodes.Status401Unauthorized, "AUTH_REQUIRED", "Authentication required.");
        }

        ViewerDocumentCatalog catalog = await _catalog.ListAsync(user, ct);
        return Ok(ViewerDocumentCatalogResponse.FromCatalog(catalog));
    }

    private IReadOnlyList<string> ActorRoles()
    {
        return User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
    }

    private async Task<AuthenticatedUser?> ResolveCurrentUserAsync(CancellationToken ct)
    {
        string? userIdValue = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out var userId)
            ? await _auth.GetActiveUserAsync(userId, ct)
            : null;
    }
}
