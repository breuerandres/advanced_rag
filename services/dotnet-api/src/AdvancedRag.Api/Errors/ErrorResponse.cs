namespace AdvancedRag.Api.Errors;

public sealed record ErrorResponse(ErrorBody Error)
{
    public static ErrorResponse NotFound(string requestId)
    {
        return Create("NOT_FOUND", "Resource not found.", requestId);
    }

    public static ErrorResponse InternalError(string requestId)
    {
        return Create("INTERNAL_ERROR", "An internal error occurred.", requestId);
    }

    public static ErrorResponse Create(
        string code,
        string message,
        string requestId,
        IReadOnlyDictionary<string, object?>? details = null)
    {
        return new ErrorResponse(
            new ErrorBody(
                code,
                message,
                details ?? new Dictionary<string, object?>(),
                requestId));
    }
}

public sealed record ErrorBody(
    string Code,
    string Message,
    IReadOnlyDictionary<string, object?> Details,
    string RequestId);
