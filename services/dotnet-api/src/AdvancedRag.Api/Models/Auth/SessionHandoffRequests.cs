namespace AdvancedRag.Api.Models.Auth;

public sealed record CreateSessionHandoffRequest(string Target);

public sealed record SessionHandoffResponse(
    string Target,
    string HandoffCode,
    DateTimeOffset ExpiresAt);

public sealed record ConsumeSessionHandoffRequest(
    string HandoffCode,
    string Target);
