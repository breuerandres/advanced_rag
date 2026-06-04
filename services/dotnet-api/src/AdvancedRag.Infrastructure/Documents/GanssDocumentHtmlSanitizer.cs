using AdvancedRag.App.Documents;
using Ganss.Xss;

namespace AdvancedRag.Infrastructure.Documents;

public sealed class GanssDocumentHtmlSanitizer : IDocumentHtmlSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    public GanssDocumentHtmlSanitizer()
    {
        _sanitizer = new HtmlSanitizer();
        _sanitizer.AllowedTags.Add("mark");
        _sanitizer.AllowedCssProperties.Clear();
        _sanitizer.AllowedCssProperties.Add("color");
        _sanitizer.AllowedCssProperties.Add("text-align");
    }

    public string Sanitize(string html)
    {
        return _sanitizer.Sanitize(html);
    }
}
