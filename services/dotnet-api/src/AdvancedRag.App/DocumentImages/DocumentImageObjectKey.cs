namespace AdvancedRag.App.DocumentImages;

public static class DocumentImageObjectKey
{
    private static readonly IReadOnlyDictionary<string, string> ExtensionsByContentType =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/png"] = ".png",
            ["image/jpeg"] = ".jpg",
            ["image/jpg"] = ".jpg",
            ["image/webp"] = ".webp",
            ["image/gif"] = ".gif",
        };

    public static bool TryGetExtension(string contentType, out string? extension)
    {
        return ExtensionsByContentType.TryGetValue(NormalizeContentType(contentType), out extension);
    }

    public static string Build(Guid documentId, Guid imageId, string sha256, string extension)
    {
        return $"documents/{documentId:D}/images/{imageId:D}/{sha256}{extension}";
    }

    public static string NormalizeContentType(string contentType)
    {
        return contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
    }
}
