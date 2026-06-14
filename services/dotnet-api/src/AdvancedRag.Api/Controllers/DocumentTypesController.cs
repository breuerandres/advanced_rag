using AdvancedRag.Api.Models.DocumentTypes;
using AdvancedRag.App.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdvancedRag.Api.Controllers;

[ApiController]
[Route("api/document-types")]
[Authorize(Roles = "Admin,DocumentEditor,DocumentPublisher")]
public sealed class DocumentTypesController : ApiControllerBase
{
    private readonly IDocumentTypeService _documentTypes;

    public DocumentTypesController(IDocumentTypeService documentTypes)
    {
        _documentTypes = documentTypes;
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync([FromQuery] bool includeInactive, CancellationToken ct)
    {
        bool effectiveIncludeInactive = includeInactive && User.IsInRole("Admin");
        IReadOnlyList<DocumentTypeRecord> types = await _documentTypes.ListAsync(effectiveIncludeInactive, ct);
        return Ok(types.Select(DocumentTypeResponse.FromRecord).ToArray());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateAsync([FromBody] CreateDocumentTypeRequest request, CancellationToken ct)
    {
        try
        {
            DocumentTypeRecord type = await _documentTypes.CreateAsync(
                new CreateDocumentTypeCommand(request.Name, request.SortOrder, ActorUserId()),
                ct);
            return Created($"/api/document-types/{type.Id}", DocumentTypeResponse.FromRecord(type));
        }
        catch (DocumentTypeException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateDocumentTypeRequest request,
        CancellationToken ct)
    {
        try
        {
            DocumentTypeRecord type = await _documentTypes.UpdateAsync(
                new UpdateDocumentTypeCommand(id, request.Name, request.IsActive, request.SortOrder, ActorUserId()),
                ct);
            return Ok(DocumentTypeResponse.FromRecord(type));
        }
        catch (DocumentTypeException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        try
        {
            await _documentTypes.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (DocumentTypeException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }
}
