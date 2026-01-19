namespace SCStreamDeck.Services.Audio;

/// <summary>
///     Service for playing audio files through the default audio device.
///     Uses WASAPI with mixing support for low-latency playback.
/// </summary>
public interface IAudioPlayerService : IDisposable
{
    /// <summary>
    ///     Plays an audio file asynchronously without blocking.
    /// </summary>
    /// <param name="filePath">Path to the audio file (.wav or .mp3).</param>
    /// <exception cref="ArgumentException">Thrown when filePath is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the audio file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when audio playback initialization fails.</exception>
    void Play(string filePath);
}
