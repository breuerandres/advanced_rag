namespace AdvancedRag.Api.Models.Account;

public sealed record UpdateAccountEmailRequest(string Email);

public sealed record ChangeAccountPasswordRequest(string CurrentPassword, string NewPassword);
