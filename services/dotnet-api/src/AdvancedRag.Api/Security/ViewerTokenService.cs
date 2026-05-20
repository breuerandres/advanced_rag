using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AdvancedRag.App.Viewer;
using Microsoft.IdentityModel.Tokens;

namespace AdvancedRag.Api.Security;

public sealed class ViewerTokenService : IViewerTokenService
{
    private readonly JwtSigningKeyStore _keys;
    private readonly IConfiguration _configuration;

    public ViewerTokenService(JwtSigningKeyStore keys, IConfiguration configuration)
    {
        _keys = keys;
        _configuration = configuration;
    }

    public IssuedViewerToken Issue(ViewerTokenIssueRequest request)
    {
        JwtSigningKey currentKey = _keys.GetCurrentSigningKey();
        string viewerTokenId = Guid.NewGuid().ToString("N");

        JwtHeader header = new(currentKey.Credentials);
        header["kid"] = currentKey.Kid;
        JwtPayload payload = new()
        {
            ["iss"] = _configuration["Jwt:Issuer"] ?? "advanced-rag-dotnet-api",
            ["aud"] = _configuration["Jwt:ViewerAudience"] ?? "advanced-rag-viewer",
            ["sub"] = request.UserId.ToString(),
            ["document_id"] = request.DocumentId.ToString(),
            ["purpose"] = request.Purpose,
            ["allowed_statuses"] = request.AllowedStatuses.ToArray(),
            ["jti"] = viewerTokenId,
            ["iat"] = EpochTime.GetIntDate(DateTimeOffset.UtcNow.UtcDateTime),
            ["exp"] = EpochTime.GetIntDate(request.ExpiresAt.UtcDateTime),
        };

        JwtSecurityToken token = new(header, payload);
        return new IssuedViewerToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            viewerTokenId,
            request.DocumentId,
            request.UserId,
            request.Purpose,
            request.ExpiresAt);
    }

    public ViewerTokenClaims Validate(string token)
    {
        try
        {
            ClaimsPrincipal principal = new JwtSecurityTokenHandler().ValidateToken(
                token,
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["Jwt:Issuer"] ?? "advanced-rag-dotnet-api",
                    ValidateAudience = true,
                    ValidAudience = _configuration["Jwt:ViewerAudience"] ?? "advanced-rag-viewer",
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = _keys.GetValidationKeys(),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                },
                out SecurityToken validatedToken);

            JwtSecurityToken jwt = validatedToken as JwtSecurityToken
                ?? throw new ViewerAccessException("AUTH_TOKEN_INVALID", 401, "Viewer token is invalid.");
            string viewerTokenId = principal.FindFirstValue(JwtRegisteredClaimNames.Jti)
                ?? throw new ViewerAccessException("AUTH_TOKEN_INVALID", 401, "Viewer token is invalid.");
            string userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? throw new ViewerAccessException("AUTH_TOKEN_INVALID", 401, "Viewer token is invalid.");
            string documentId = principal.FindFirstValue("document_id")
                ?? throw new ViewerAccessException("AUTH_TOKEN_INVALID", 401, "Viewer token is invalid.");
            string purpose = principal.FindFirstValue("purpose")
                ?? throw new ViewerAccessException("AUTH_TOKEN_INVALID", 401, "Viewer token is invalid.");
            IReadOnlyList<string> allowedStatuses = principal.FindAll("allowed_statuses")
                .Select(claim => claim.Value)
                .ToArray();

            return new ViewerTokenClaims(
                viewerTokenId,
                Guid.Parse(documentId),
                Guid.Parse(userId),
                purpose,
                allowedStatuses,
                new DateTimeOffset(DateTime.SpecifyKind(jwt.ValidTo, DateTimeKind.Utc)));
        }
        catch (ViewerAccessException)
        {
            throw;
        }
        catch (SecurityTokenExpiredException)
        {
            throw new ViewerAccessException("AUTH_TOKEN_EXPIRED", 401, "Viewer token has expired.");
        }
        catch (SecurityTokenException)
        {
            throw new ViewerAccessException("AUTH_TOKEN_INVALID", 401, "Viewer token is invalid.");
        }
        catch (Exception)
        {
            throw new ViewerAccessException("AUTH_TOKEN_INVALID", 401, "Viewer token is invalid.");
        }
    }
}
