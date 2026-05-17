namespace AdvancedRag.App.Documents;

public interface IInstructionHtmlSanitizer
{
    string Sanitize(string html);
}

public sealed class PassthroughInstructionHtmlSanitizer : IInstructionHtmlSanitizer
{
    public string Sanitize(string html)
    {
        return html;
    }
}
