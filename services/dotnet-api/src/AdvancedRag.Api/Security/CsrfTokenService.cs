using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AdvancedRag.Api.Security;

public interface ICsrfTokenService
{
    string CreateToken();

    bool Validate(string headerToken, string cookieToken);
}

public sealed class CsrfTokenService : ICsrfTokenService
{
    public const string CookieName = "__Host-CSRF";
    public const string HeaderName = "X-CSRF-Token";

    private readonly byte[] _signingKey;

    public CsrfTokenService(IConfiguration configuration)
    {
        _signingKey = Encoding.UTF8.GetBytes(SecretConfiguration.Read(configuration, "Csrf:SigningKey", "Csrf:SigningKeyFile"));
    }

    public string CreateToken()
    {
        var nonce = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var payload = $"{nonce}.{issuedAt}";
        var signature = Sign(payload);
        return $"{payload}.{signature}";
    }

    public bool Validate(string headerToken, string cookieToken)
    {
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(headerToken),
                Encoding.UTF8.GetBytes(cookieToken)))
        {
            return false;
        }

        var parts = headerToken.Split('.');
        if (parts.Length != 3 || !long.TryParse(parts[1], out _))
        {
            return false;
        }

        var payload = $"{parts[0]}.{parts[1]}";
        var expectedSignature = Sign(payload);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(parts[2]));
    }

    private string Sign(string payload)
    {
        using var hmac = new HMACSHA256(_signingKey);
        return Base64UrlEncoder.Encode(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }
}
