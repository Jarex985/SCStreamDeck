namespace SCStreamDeck.Services.Data;

public sealed record CryXmlConversionResult
{
    public bool IsSuccess { get; private init; }
    public string? Xml { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static CryXmlConversionResult Success(string xml) =>
        new() { IsSuccess = true, Xml = xml };

    public static CryXmlConversionResult Failure(string errorMessage) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage };
}
