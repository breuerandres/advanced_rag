using AdvancedRag.Infrastructure.Documents;
using FluentAssertions;
using SkiaSharp;

namespace AdvancedRag.Infrastructure.Tests;

public sealed class ImportImageNormalizerTests
{
    [Fact]
    public void Normalize_WebSafePngWithinCap_PassesThroughUnchanged()
    {
        byte[] png = CreatePng(64, 64);
        var normalizer = new ImportImageNormalizer(maxDimension: 2000, minDimension: 32, webpQuality: 80);

        NormalizedImage? result = normalizer.Normalize(png, "image/png");

        result.Should().NotBeNull();
        result!.ContentType.Should().Be("image/png");
        result.Content.Should().Equal(png);
    }

    [Fact]
    public void Normalize_OversizedImage_DownscalesAndReencodesToWebp()
    {
        byte[] png = CreatePng(4000, 100);
        var normalizer = new ImportImageNormalizer(maxDimension: 2000, minDimension: 32, webpQuality: 80);

        NormalizedImage? result = normalizer.Normalize(png, "image/png");

        result.Should().NotBeNull();
        result!.ContentType.Should().Be("image/webp");
        using SKBitmap decoded = SKBitmap.Decode(result.Content);
        decoded.Width.Should().Be(2000);
        decoded.Height.Should().Be(50);
    }

    [Fact]
    public void Normalize_TinyDecoration_IsSkipped()
    {
        byte[] png = CreatePng(16, 16);
        var normalizer = new ImportImageNormalizer(maxDimension: 2000, minDimension: 32, webpQuality: 80);

        normalizer.Normalize(png, "image/png").Should().BeNull();
    }

    [Fact]
    public void Normalize_UndecodableBytes_IsSkipped()
    {
        byte[] garbage = [0x01, 0x02, 0x03, 0x04, 0x05];
        var normalizer = new ImportImageNormalizer(maxDimension: 2000, minDimension: 32, webpQuality: 80);

        normalizer.Normalize(garbage, "image/x-emf").Should().BeNull();
    }

    private static byte[] CreatePng(int width, int height)
    {
        using SKBitmap bitmap = new(width, height);
        using (SKCanvas canvas = new(bitmap))
        {
            canvas.Clear(SKColors.CornflowerBlue);
        }

        using SKData data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
