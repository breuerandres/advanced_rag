namespace AdvancedRag.Api.Security;

public static class SecretConfiguration
{
    public static string Read(IConfiguration configuration, string valueKey, string fileKey)
    {
        var directValue = configuration[valueKey];
        if (!string.IsNullOrWhiteSpace(directValue))
        {
            return directValue;
        }

        var filePath = configuration[fileKey];
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            return File.ReadAllText(filePath).Trim();
        }

        throw new InvalidOperationException($"Missing required secret configuration '{valueKey}' or '{fileKey}'.");
    }
}
