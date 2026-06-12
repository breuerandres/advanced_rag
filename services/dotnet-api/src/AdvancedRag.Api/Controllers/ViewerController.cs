using AdvancedRag.Api.Models.Auth;
using AdvancedRag.Api.Models.Viewer;
using AdvancedRag.App.Auth;
using AdvancedRag.App.Viewer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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
                    request.Purpose,
                    HttpContext.TraceIdentifier),
                ct);
            return Ok(ViewerLinkResponse.FromResult(result));
        }
        catch (ViewerAccessException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpPost("session-handoff")]
    [AllowAnonymous]
    public async Task<IActionResult> ConsumeSessionHandoffAsync(
        [FromBody] ConsumeViewerSessionHandoffRequest request,
        CancellationToken ct)
    {
        ViewerSessionHandoffResult handoff;
        try
        {
            handoff = await _viewer.ConsumeHandoffAsync(
                new ConsumeViewerSessionHandoffCommand(request.HandoffCode, request.DocumentId),
                ct);
        }
        catch (ViewerAccessException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }

        AuthenticatedUser? user = await _auth.GetActiveUserAsync(handoff.UserId, ct);
        if (user is null)
        {
            return Error(StatusCodes.Status401Unauthorized, "AUTH_REQUIRED", "Authentication required.");
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            CreatePrincipal(user),
            new AuthenticationProperties
            {
                IsPersistent = false,
                IssuedUtc = DateTimeOffset.UtcNow,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
            });

        return Ok(SessionResponse.FromUser(user));
    }

    [HttpGet("document")]
    [Authorize]
    public async Task<IActionResult> GetDocumentAsync([FromQuery] Guid documentId, CancellationToken ct)
    {
        AuthenticatedUser? user = await ResolveCurrentUserAsync(_auth, ct);
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
        AuthenticatedUser? user = await ResolveCurrentUserAsync(_auth, ct);
        if (user is null)
        {
            return Error(StatusCodes.Status401Unauthorized, "AUTH_REQUIRED", "Authentication required.");
        }

        ViewerDocumentCatalog catalog = await _catalog.ListAsync(user, ct);
        return Ok(ViewerDocumentCatalogResponse.FromCatalog(catalog));
    }

}
