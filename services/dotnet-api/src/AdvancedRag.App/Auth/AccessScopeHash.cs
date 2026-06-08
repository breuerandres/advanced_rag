using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AdvancedRag.App.Auth;

public static class AccessScopeHash
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Compute(
        string role,
        IEnumerable<Guid> groupIds,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        var sortedAttributes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in attributes ?? new Dictionary<string, string>())
        {
            sortedAttributes[key] = value;
        }

        var canonical = new SortedDictionary<string, object>(StringComparer.Ordinal)
        {
            ["attributes"] = sortedAttributes,
            ["groups"] = groupIds.Select(id => id.ToString()).Order(StringComparer.Ordinal).ToArray(),
            ["role"] = role,
            ["v"] = 1,
        };

        var serialized = JsonSerializer.Serialize(canonical, JsonOptions);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(serialized));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string ComputeV2(
        string role,
        bool isGlobalAdmin,
        Guid organizationalUnitId,
        IEnumerable<Guid> groupIds,
        long accessScopeVersion)
    {
        var canonical = new SortedDictionary<string, object>(StringComparer.Ordinal)
        {
            ["accessScopeVersion"] = accessScopeVersion,
            ["groups"] = groupIds.Select(id => id.ToString()).Order(StringComparer.Ordinal).ToArray(),
            ["isGlobalAdmin"] = isGlobalAdmin,
            ["organizationalUnitId"] = organizationalUnitId.ToString(),
            ["role"] = role,
            ["v"] = 2,
        };

        var serialized = JsonSerializer.Serialize(canonical, JsonOptions);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(serialized));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
