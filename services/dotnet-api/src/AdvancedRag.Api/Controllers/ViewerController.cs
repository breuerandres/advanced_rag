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
    public const string ViewerTokenCookieName = "__Host-viewer-token";

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

    [HttpPost("exchange")]
    [AllowAnonymous]
    public async Task<IActionResult> ExchangeAsync(
        [FromBody] ExchangeViewerCodeRequest request,
        CancellationToken ct)
    {
        try
        {
            ViewerExchangeResult result = await _viewer.ExchangeCodeAsync(
                new ExchangeViewerCodeCommand(request.Code),
                ct);
            Response.Cookies.Append(
                ViewerTokenCookieName,
                result.Token,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Path = "/",
                    Expires = result.ExpiresAt,
                });
            return Ok(ViewerExchangeResponse.FromResult(result));
        }
        catch (ViewerAccessException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpGet("document")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDocumentAsync(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(ViewerTokenCookieName, out string? token)
            || string.IsNullOrWhiteSpace(token))
        {
            return Error(401, "AUTH_REQUIRED", "Viewer token is required.");
        }

        try
        {
            ViewerDocumentResult result = await _viewer.GetDocumentAsync(new GetViewerDocumentCommand(token), ct);
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
