using System.Security.Claims;
using AdvancedRag.Api.Models.Documents;
using AdvancedRag.App.DocumentImages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdvancedRag.Api.Controllers;

[ApiController]
public sealed class DocumentImagesController : ApiControllerBase
{
    private readonly IDocumentImageService _images;

    public DocumentImagesController(IDocumentImageService images)
    {
        _images = images;
    }

    [HttpPost("api/documents/{documentId:guid}/images")]
    [Authorize(Roles = "Admin,DocumentEditor,DocumentPublisher")]
    [RequestSizeLimit(MaxImageUploadSizeBytes)]
    public async Task<IActionResult> UploadAsync(
        Guid documentId,
        IFormFile file,
        [FromForm] string? altText,
        CancellationToken ct)
    {
        if (file.Length > MaxImageUploadSizeBytes)
        {
            return Error(
                413,
                "DOCUMENT_IMAGE_TOO_LARGE",
                "Document image exceeds the configured size limit.",
                new Dictionary<string, object?> { ["maxBytes"] = MaxImageUploadSizeBytes });
        }

        await using Stream stream = file.OpenReadStream();
        try
        {
            DocumentImageUploadResult result = await _images.UploadAsync(
                new UploadDocumentImageCommand(
                    documentId,
                    file.FileName,
                    file.ContentType,
                    file.Length,
                    stream,
                    altText ?? string.Empty,
                    ActorUserId()),
                ct);
            return Ok(new DocumentImageUploadResponse(result.ImageId, result.Url, result.AltText));
        }
        catch (DocumentImageException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpGet("api/document-images/{imageId:guid}/content")]
    [Authorize]
    public async Task<IActionResult> GetContentAsync(Guid imageId, CancellationToken ct)
    {
        try
        {
            DocumentImageContentResult result = await _images.GetContentAsync(
                new GetDocumentImageContentCommand(imageId, ActorUserId(), ActorRoles()),
                ct);
            return File(result.Content, result.ContentType, enableRangeProcessing: true);
        }
        catch (DocumentImageException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    private const int MaxImageUploadSizeBytes = 5 * 1024 * 1024;

    private IReadOnlyList<string> ActorRoles()
    {
        return User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
    }
}
