using System.Security.Claims;
using AdvancedRag.Api.Models.Documents;
using AdvancedRag.App.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdvancedRag.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize(Roles = "Admin,DocumentManager")]
public sealed class DocumentsController : ApiControllerBase
{
    private readonly IDocumentLifecycleService _documents;
    private readonly IDocumentImportExtractionService _imports;

    public DocumentsController(
        IDocumentLifecycleService documents,
        IDocumentImportExtractionService imports)
    {
        _documents = documents;
        _imports = imports;
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var documents = await _documents.ListAsync(ct);
        return Ok(documents.Select(DocumentSummaryResponse.FromSummary).ToArray());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken ct)
    {
        var document = await _documents.GetAsync(id, ct);
        return document is null
            ? Error(404, "NOT_FOUND", "Instruction not found.")
            : Ok(DocumentDetailResponse.FromAggregate(document));
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] SaveDocumentDraftRequest request, CancellationToken ct)
    {
        try
        {
            var document = await _documents.CreateDraftAsync(
                new CreateDocumentCommand(
                    request.Title,
                    request.InstructionType,
                    request.Audience,
                    request.ContentHtml,
                    request.AllowedGroupIds ?? [],
                    ActorUserId(),
                    RequestId()),
                ct);
            return Created($"/api/documents/{document.Id}", DocumentDetailResponse.FromAggregate(document));
        }
        catch (DocumentLifecycleException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpPut("{id:guid}/draft")]
    public async Task<IActionResult> UpdateDraftAsync(
        Guid id,
        [FromBody] SaveDocumentDraftRequest request,
        CancellationToken ct)
    {
        try
        {
            var document = await _documents.UpdateDraftAsync(
                new UpdateDraftCommand(
                    id,
                    request.Title,
                    request.InstructionType,
                    request.Audience,
                    request.ContentHtml,
                    request.AllowedGroupIds ?? [],
                    ActorUserId(),
                    RequestId()),
                ct);
            return Ok(DocumentDetailResponse.FromAggregate(document));
        }
        catch (DocumentLifecycleException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpPost("{id:guid}/send-to-review")]
    public async Task<IActionResult> SendToReviewAsync(
        Guid id,
        [FromBody] ReviewCommentRequest request,
        CancellationToken ct)
    {
        try
        {
            var document = await _documents.SendToReviewAsync(
                new SendToReviewCommand(id, request.Comment, ActorUserId(), RequestId()),
                ct);
            return Ok(DocumentDetailResponse.FromAggregate(document));
        }
        catch (DocumentLifecycleException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpPost("{id:guid}/return-to-draft")]
    public async Task<IActionResult> ReturnToDraftAsync(
        Guid id,
        [FromBody] ReviewCommentRequest request,
        CancellationToken ct)
    {
        try
        {
            var document = await _documents.ReturnToDraftAsync(
                new ReturnToDraftCommand(id, request.Comment ?? string.Empty, ActorUserId(), RequestId()),
                ct);
            return Ok(DocumentDetailResponse.FromAggregate(document));
        }
        catch (DocumentLifecycleException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpPost("{id:guid}/request-publish")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RequestPublishAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var document = await _documents.RequestPublishAsync(
                new RequestPublishCommand(id, ActorUserId(), ActorRoles(), RequestId()),
                ct);
            return Ok(DocumentDetailResponse.FromAggregate(document));
        }
        catch (DocumentLifecycleException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> ArchiveAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var document = await _documents.ArchiveAsync(
                new ArchiveInstructionCommand(id, ActorUserId(), ActorRoles(), RequestId()),
                ct);
            return Ok(DocumentDetailResponse.FromAggregate(document));
        }
        catch (DocumentLifecycleException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> RestoreAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var document = await _documents.RestoreAsync(
                new RestoreInstructionCommand(id, ActorUserId(), RequestId()),
                ct);
            return Ok(DocumentDetailResponse.FromAggregate(document));
        }
        catch (DocumentLifecycleException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpPost("imports/extract")]
    [RequestSizeLimit(DocumentImportExtractionServiceMaxSize)]
    public async Task<IActionResult> ExtractImportAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length > DocumentImportExtractionServiceMaxSize)
        {
            return Error(
                413,
                "IMPORT_FILE_TOO_LARGE",
                "Uploaded import file exceeds the configured size limit.",
                new Dictionary<string, object?> { ["maxBytes"] = DocumentImportExtractionServiceMaxSize });
        }

        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, ct);

        try
        {
            var result = await _imports.ExtractAsync(
                new ImportExtractionCommand(file.FileName, file.ContentType, memory.ToArray(), ActorUserId()),
                ct);
            return Ok(ImportExtractionResponse.FromResult(result));
        }
        catch (DocumentImportException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    private const int DocumentImportExtractionServiceMaxSize = 10 * 1024 * 1024;

    private IReadOnlyList<string> ActorRoles()
    {
        return User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
    }
}
