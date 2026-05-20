using AdvancedRag.App.Documents;
using Ganss.Xss;

namespace AdvancedRag.Infrastructure.Documents;

public sealed class GanssDocumentHtmlSanitizer : IDocumentHtmlSanitizer
{
    private readonly HtmlSanitizer _sanitizer = new();

    public string Sanitize(string html)
    {
        return _sanitizer.Sanitize(html);
    }
}
