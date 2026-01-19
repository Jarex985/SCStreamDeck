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
    /// <returns>XML text if successful, null if not CryXml or error</returns>
    /// <exception cref="ArgumentNullException">Thrown when binaryXmlData is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    Task<string?> ConvertCryXmlToTextAsync(byte[] binaryXmlData, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if data is CryEngine binary XML format.
    /// </summary>
    /// <param name="data">Data to check.</param>
    /// <returns>True if data matches CryXml signature, false otherwise.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when data is too short to contain signature.</exception>
    bool IsCryXml(ReadOnlySpan<byte> data);
}
