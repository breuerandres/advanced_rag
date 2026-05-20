using AdvancedRag.Api.Models.Setup;
using AdvancedRag.App.Setup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdvancedRag.Api.Controllers;

[ApiController]
[Route("api/setup")]
public sealed class SetupController : ApiControllerBase
{
    private readonly ISetupService _setup;

    public SetupController(ISetupService setup)
    {
        _setup = setup;
    }

    [HttpGet("status")]
    [AllowAnonymous]
    public async Task<ActionResult<SetupStatusResponse>> GetStatusAsync(CancellationToken ct)
    {
        SetupStatus status = await _setup.GetStatusAsync(ct);
        return Ok(SetupStatusResponse.FromStatus(status));
    }

    [HttpPost("admin")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateFirstAdminAsync(
        [FromBody] CreateFirstAdminRequest request,
        CancellationToken ct)
    {
        try
        {
            FirstAdminResult result = await _setup.CreateFirstAdminAsync(
                new CreateFirstAdminCommand(
                    request.Email ?? string.Empty,
                    request.DisplayName ?? string.Empty,
                    request.Password ?? string.Empty),
                ct);

            return Created("/api/setup/admin", SetupAdminResponse.FromResult(result));
        }
        catch (SetupException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }
}
