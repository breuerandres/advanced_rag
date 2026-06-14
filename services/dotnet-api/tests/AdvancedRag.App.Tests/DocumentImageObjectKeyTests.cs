using AdvancedRag.App.DocumentImages;
using FluentAssertions;

namespace AdvancedRag.App.Tests;

public sealed class DocumentImageObjectKeyTests
{
    [Theory]
    [InlineData("image/png", ".png")]
    [InlineData("image/jpeg", ".jpg")]
    [InlineData("image/jpg", ".jpg")]
    [InlineData("image/webp", ".webp")]
    [InlineData("image/gif", ".gif")]
    public void ExtensionFor_KnownTypes_ReturnsExtension(string contentType, string expected)
    {
        DocumentImageObjectKey.TryGetExtension(contentType, out string? extension).Should().BeTrue();
        extension.Should().Be(expected);
    }

    [Fact]
    public void ExtensionFor_UnknownType_ReturnsFalse()
    {
        DocumentImageObjectKey.TryGetExtension("image/tiff", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_ComposesStableKey()
    {
        Guid documentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid imageId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        string key = DocumentImageObjectKey.Build(documentId, imageId, "abc123", ".png");

        key.Should().Be(
            "documents/11111111-1111-1111-1111-111111111111/images/22222222-2222-2222-2222-222222222222/abc123.png");
    }
}
