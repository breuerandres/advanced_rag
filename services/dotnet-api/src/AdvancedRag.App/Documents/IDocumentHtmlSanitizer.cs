namespace AdvancedRag.App.Documents;

public interface IDocumentHtmlSanitizer
{
    string Sanitize(string html);
}

public sealed class PassthroughDocumentHtmlSanitizer : IDocumentHtmlSanitizer
{
    public string Sanitize(string html)
    {
        return html;
    }
}
