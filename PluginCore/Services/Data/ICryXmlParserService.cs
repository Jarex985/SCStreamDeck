namespace SCStreamDeck.SCCore.Services.Data;

/// <summary>
///     Service for parsing CryEngine binary XML files.
/// </summary>
public interface ICryXmlParserService
{
    /// <summary>
    ///     Converts CryEngine binary XML bytes to standard XML text.
    /// </summary>
    /// <param name="binaryXmlData">Binary XML data from P4K</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>XML text if successful, null if not CryXml or error</returns>
    Task<string?> ConvertCryXmlToTextAsync(byte[] binaryXmlData, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if data is CryEngine binary XML format.
    /// </summary>
    bool IsCryXml(ReadOnlySpan<byte> data);
}