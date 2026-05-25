using AdvancedRag.Api.Models.Account;
using AdvancedRag.Api.Models.Auth;
using AdvancedRag.App.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdvancedRag.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account")]
public sealed class AccountController : ApiControllerBase
{
    private readonly IUserAccountService _account;

    public AccountController(IUserAccountService account)
    {
        _account = account;
    }

    [HttpPut("email")]
    public async Task<IActionResult> UpdateEmailAsync(
        [FromBody] UpdateAccountEmailRequest request,
        CancellationToken ct)
    {
        try
        {
            AuthenticatedUser updated = await _account.UpdateEmailAsync(
                new UpdateUserEmailCommand(ActorUserId(), request.Email),
                ct);
            return Ok(SessionResponse.FromUser(updated));
        }
        catch (UserAccountException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }

    [HttpPut("password")]
    public async Task<IActionResult> ChangePasswordAsync(
        [FromBody] ChangeAccountPasswordRequest request,
        CancellationToken ct)
    {
        try
        {
            await _account.ChangePasswordAsync(
                new ChangeUserPasswordCommand(ActorUserId(), request.CurrentPassword, request.NewPassword),
                ct);
            return Ok(new ChangeAccountPasswordResponse("ok"));
        }
        catch (UserAccountException exception)
        {
            return Error(exception.HttpStatus, exception.Code, exception.Message, exception.Details);
        }
    }
}
