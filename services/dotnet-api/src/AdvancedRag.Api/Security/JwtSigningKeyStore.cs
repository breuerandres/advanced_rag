using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.IdentityModel.Tokens;

namespace AdvancedRag.Api.Security;

public sealed class JwtSigningKeyStore
{
    private readonly IReadOnlyList<JwtSigningKey> _keys;

    public JwtSigningKeyStore(IConfiguration configuration)
    {
        var json = SecretConfiguration.Read(configuration, "Jwt:SigningKeysJson", "Jwt:SigningKeysFile");
        var documents = JsonSerializer.Deserialize<List<JwtSigningKeyDocument>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("JWT signing key configuration is empty.");

        _keys = documents.Select(JwtSigningKey.FromDocument).ToArray();
    }

    public JwtSigningKey GetCurrentSigningKey()
    {
        return _keys.SingleOrDefault(key => key.Status.Equals("current", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("No current JWT signing key is configured.");
    }

    public object GetJwks()
    {
        return new
        {
            keys = _keys.Select(key => key.ToJwk()).ToArray(),
        };
    }
}

public sealed class JwtSigningKey
{
    private JwtSigningKey(
        string kid,
        string status,
        RSA privateKey,
        RSA publicKey)
    {
        Kid = kid;
        Status = status;
        Credentials = new SigningCredentials(
            new RsaSecurityKey(privateKey) { KeyId = kid },
            SecurityAlgorithms.RsaSha256);
        PublicKey = publicKey;
    }

    public string Kid { get; }

    public string Status { get; }

    public SigningCredentials Credentials { get; }

    private RSA PublicKey { get; }

    public static JwtSigningKey FromDocument(JwtSigningKeyDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.Kid)
            || string.IsNullOrWhiteSpace(document.Status)
            || string.IsNullOrWhiteSpace(document.Private)
            || string.IsNullOrWhiteSpace(document.Public))
        {
            throw new InvalidOperationException("JWT signing key document is incomplete.");
        }

        var privateKey = RSA.Create();
        privateKey.ImportFromPem(document.Private);
        var publicKey = RSA.Create();
        publicKey.ImportFromPem(document.Public);
        return new JwtSigningKey(document.Kid, document.Status, privateKey, publicKey);
    }

    public object ToJwk()
    {
        var parameters = PublicKey.ExportParameters(false);
        return new
        {
            kty = "RSA",
            use = "sig",
            kid = Kid,
            alg = SecurityAlgorithms.RsaSha256,
            n = Base64UrlEncoder.Encode(parameters.Modulus),
            e = Base64UrlEncoder.Encode(parameters.Exponent),
        };
    }
}

public sealed record JwtSigningKeyDocument(
    string Kid,
    string Status,
    [property: JsonPropertyName("private")] string Private,
    [property: JsonPropertyName("public")] string Public);
