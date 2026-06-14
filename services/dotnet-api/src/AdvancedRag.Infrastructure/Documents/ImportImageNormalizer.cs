using SkiaSharp;

namespace AdvancedRag.Infrastructure.Documents;

public sealed record NormalizedImage(byte[] Content, string ContentType);

/// <summary>
/// Decodes an imported raster image, drops icons/decorations below the minimum
/// size, keeps web-safe formats within the dimension cap unchanged, and otherwise
/// downscales + re-encodes to WebP. Returns <c>null</c> when the image must be
/// skipped (undecodable vector/metafile, or too small).
/// </summary>
public sealed class ImportImageNormalizer
{
    private static readonly HashSet<string> WebSafeContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/png", "image/jpeg", "image/jpg", "image/gif", "image/webp" };

    private readonly int _maxDimension;
    private readonly int _minDimension;
    private readonly int _webpQuality;

    public ImportImageNormalizer(int maxDimension = 2000, int minDimension = 32, int webpQuality = 80)
    {
        _maxDimension = maxDimension;
        _minDimension = minDimension;
        _webpQuality = webpQuality;
    }

    public NormalizedImage? Normalize(byte[] source, string sourceContentType)
    {
        SKBitmap? bitmap = TryDecode(source);
        if (bitmap is null)
        {
            return null; // EMF/WMF/TIFF/unsupported -> caller drops the <img>
        }

        using (bitmap)
        {
            return NormalizeDecoded(bitmap, source, sourceContentType);
        }
    }

    private static SKBitmap? TryDecode(byte[] source)
    {
        try
        {
            return SKBitmap.Decode(source);
        }
        catch (Exception)
        {
            return null; // malformed bytes can throw on the native side
        }
    }

    private NormalizedImage? NormalizeDecoded(SKBitmap bitmap, byte[] source, string sourceContentType)
    {
        if (bitmap.Width < _minDimension && bitmap.Height < _minDimension)
        {
            return null; // bullets/icons
        }

        string contentType = NormalizeContentType(sourceContentType);
        bool withinCap = bitmap.Width <= _maxDimension && bitmap.Height <= _maxDimension;
        if (withinCap && WebSafeContentTypes.Contains(contentType))
        {
            return new NormalizedImage(source, contentType == "image/jpg" ? "image/jpeg" : contentType);
        }

        using SKBitmap scaled = Downscale(bitmap);
        using SKData data = scaled.Encode(SKEncodedImageFormat.Webp, _webpQuality);
        if (data is null || data.Size == 0)
        {
            return null;
        }

        return new NormalizedImage(data.ToArray(), "image/webp");
    }

    private SKBitmap Downscale(SKBitmap bitmap)
    {
        if (bitmap.Width <= _maxDimension && bitmap.Height <= _maxDimension)
        {
            return bitmap.Copy();
        }

        double scale = (double)_maxDimension / Math.Max(bitmap.Width, bitmap.Height);
        int width = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
        int height = Math.Max(1, (int)Math.Round(bitmap.Height * scale));
        SKBitmap target = new(new SKImageInfo(width, height, bitmap.ColorType, bitmap.AlphaType));
        bitmap.ScalePixels(target, SKFilterQuality.High);
        return target;
    }

    private static string NormalizeContentType(string contentType)
    {
        return contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
    }
}
