using AdvancedRag.App.Auth;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace AdvancedRag.Api.Security;

public interface IChatTokenIssuer
{
    IssuedChatToken Issue(AuthenticatedUser user);
}

public sealed class ChatTokenIssuer : IChatTokenIssuer
{
    public const string CookieName = "__Host-chat-token";

    private readonly JwtSigningKeyStore _keys;
    private readonly IConfiguration _configuration;

    public ChatTokenIssuer(JwtSigningKeyStore keys, IConfiguration configuration)
    {
        _keys = keys;
        _configuration = configuration;
    }

    public IssuedChatToken Issue(AuthenticatedUser user)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(_configuration.GetValue("Jwt:ChatTokenTtlMinutes", 15));
        var currentKey = _keys.GetCurrentSigningKey();
        var groupIds = user.Groups.Select(group => group.Id).Order().ToArray();
        var accessScopeHash = AccessScopeHash.Compute(user.PrimaryRole, groupIds);

        var header = new JwtHeader(currentKey.Credentials);
        header["kid"] = currentKey.Kid;

        var payload = new JwtPayload
        {
            ["iss"] = _configuration["Jwt:Issuer"] ?? "advanced-rag-dotnet-api",
            ["aud"] = _configuration["Jwt:Audience"] ?? "advanced-rag-chat",
            ["sub"] = user.Id.ToString(),
            ["role"] = user.PrimaryRole,
            ["groups"] = groupIds.Select(id => id.ToString()).ToArray(),
            ["attributes"] = new Dictionary<string, string>(),
            ["access_scope_hash"] = accessScopeHash,
            ["corpus"] = "published",
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["iat"] = EpochTime.GetIntDate(now.UtcDateTime),
            ["exp"] = EpochTime.GetIntDate(expiresAt.UtcDateTime),
        };

        var token = new JwtSecurityToken(header, payload);
        return new IssuedChatToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

public sealed record IssuedChatToken(string Token, DateTimeOffset ExpiresAt);
