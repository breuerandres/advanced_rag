using AdvancedRag.Api.Models.Documents;
using AdvancedRag.App.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdvancedRag.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize(Roles = "Admin,DocumentEditor,DocumentPublisher")]
public sealed class DocumentsController : ApiControllerBase
{
    private readonly IDocumentLifecycleService _documents;
    private readonly IDocumentImportExtractionService _imports;
    private readonly IDocumentImportService _importService;

    public DocumentsController(
        IDocumentLifecycleService documents,
        IDocumentImportExtractionService imports,
        IDocumentImportService importService)
    {
        _documents = documents;
        _imports = imports;
        _importService = importService;
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        IReadOnlyList<DocumentSummary> documents = await _documents.ListAsync(ct);
        return Ok(documents.Select(DocumentSummaryResponse.FromSummary).ToArray());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken ct)
    {
        DocumentAggregate? document = await _documents.GetAsync(id, ct);
        return document is null
            ? Error(404, "NOT_FOUND", "Document not found.")
            : Ok(DocumentDetailResponse.FromAggregate(document));
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] SaveDocumentDraftRequest request, CancellationToken ct)
    {
        try
        {
            DocumentAggregate document = await _documents.CreateDraftAsync(
                new CreateDocumentCommand(
                    request.Title,
                    request.DocumentType,
                    request.Audience,
                    request.ContentHtml,
                    ToAccessRules(request),
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
            DocumentAggregate document = await _documents.UpdateDraftAsync(
                new UpdateDraftCommand(
                    id,
                    request.Title,
                    request.DocumentType,
                    request.Audience,
                    request.ContentHtml,
                    ToAccessRules(request),
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
            DocumentAggregate document = await _documents.SendToReviewAsync(
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
            DocumentAggregate document = await _documents.ReturnToDraftAsync(
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
    [Authorize(Roles = "Admin,DocumentPublisher")]
    public async Task<IActionResult> RequestPublishAsync(Guid id, CancellationToken ct)
    {
        try
        {
            DocumentAggregate document = await _documents.RequestPublishAsync(
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
            DocumentAggregate document = await _documents.ArchiveAsync(
                new ArchiveDocumentCommand(id, ActorUserId(), ActorRoles(), RequestId()),
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
            DocumentAggregate document = await _documents.RestoreAsync(
                new RestoreDocumentCommand(id, ActorUserId(), RequestId()),
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
            ImportExtractionResult result = await _imports.ExtractAsync(
                new ImportExtractionCommand(file.FileName, file.ContentType, memory.ToArray(), ActorUserId()),
                ct);
            return Ok(ImportExtractionResponse.FromResult(result));
        }
        catch (DocumentImportException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpPost("imports/docx")]
    [RequestSizeLimit(DocumentImportExtractionServiceMaxSize)]
    public async Task<IActionResult> ImportDocxAsync(IFormFile file, CancellationToken ct)
    {
        const string docxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        if (!string.Equals(file.ContentType, docxMime, StringComparison.Ordinal))
        {
            return Error(
                400,
                "VALIDATION_FAILED",
                "Image-aware import supports DOCX files only.",
                new Dictionary<string, object?> { ["field"] = "mimeType" });
        }

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
            DocumentAggregate document = await _importService.ImportDocxAsync(
                new ImportDocxCommand(file.FileName, file.ContentType, memory.ToArray(), ActorUserId(), RequestId()),
                ct);
            return Created($"/api/documents/{document.Id}", DocumentDetailResponse.FromAggregate(document));
        }
        catch (DocumentImportException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    private const int DocumentImportExtractionServiceMaxSize = 10 * 1024 * 1024;

    private static IReadOnlyList<DocumentAccessRuleDraft> ToAccessRules(SaveDocumentDraftRequest request)
    {
        if (request.AccessRules is { Count: > 0 })
        {
            return request.AccessRules
                .Select(rule => new DocumentAccessRuleDraft(
                    rule.OrganizationalUnitId,
                    (rule.GroupIds ?? []).Distinct().Order().ToArray()))
                .ToArray();
        }

        return ToLegacyGroupRules(request.AllowedGroupIds ?? []);
    }

    private static IReadOnlyList<DocumentAccessRuleDraft> ToLegacyGroupRules(IReadOnlyList<Guid> groupIds)
    {
        return groupIds
            .Distinct()
            .Order()
            .Select(groupId => new DocumentAccessRuleDraft(null, [groupId]))
            .ToArray();
    }
}
