using AdvancedRag.Infrastructure.Documents;
using FluentAssertions;

namespace AdvancedRag.Infrastructure.Tests;

public sealed class DocumentHtmlSanitizerTests
{
    [Fact]
    public void Sanitize_PreservesApprovedEditorStylesAndRemovesUnsafeStyles()
    {
        var sanitizer = new GanssDocumentHtmlSanitizer();

        string sanitized = sanitizer.Sanitize(
            """
            <p><span style="color: #991b1b; position: fixed;">Critical notice</span></p>
            <h2 style="text-align: center; position: fixed;">Centered heading</h2>
            <p><mark>Important highlight</mark></p>
            <p><span style="color: url(javascript:alert(1)); background-image: url(javascript:alert(1));">Unsafe</span></p>
            """);

        sanitized.Should().Contain("color:");
        sanitized.Should().Contain("text-align:");
        sanitized.Should().Contain("Critical notice");
        sanitized.Should().Contain("<mark>Important highlight</mark>");
        string normalized = sanitized.ToLowerInvariant();
        normalized.Should().NotContain("position");
        normalized.Should().NotContain("javascript");
        normalized.Should().NotContain("background-image");
    }
}
