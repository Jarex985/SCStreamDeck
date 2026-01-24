namespace SCStreamDeck.Services.Data;

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
    /// <returns>
    ///     A result containing XML text on success, or an error message on failure.
    /// </returns>
    Task<CryXmlConversionResult> ConvertCryXmlToTextAsync(
        byte[] binaryXmlData,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if data is CryEngine binary XML format.
    /// </summary>
    /// <param name="data">Data to check.</param>
    /// <returns>True if data matches CryXml signature, false otherwise.</returns>
    bool IsCryXml(ReadOnlySpan<byte> data);
}
