using NAudio.Wave;

namespace SCStreamDeck.Services.Audio;

/// <summary>
///     Adapter that automatically disposes an audio reader when playback completes.
/// </summary>
internal sealed class AudioReaderAdapter(ISampleProvider source, IDisposable? disposeTarget = null) : ISampleProvider
{
    private readonly IDisposable? _disposeTarget = disposeTarget ?? source as IDisposable;
    private readonly ISampleProvider _source = source ?? throw new ArgumentNullException(nameof(source));
    private bool _disposed;

    public WaveFormat WaveFormat { get; } = source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        if (_disposed)
        {
            return 0;
        }

        int samplesRead = _source.Read(buffer, offset, count);

        if (samplesRead != 0)
        {
            return samplesRead;
        }

        _disposeTarget?.Dispose();
        _disposed = true;

        return samplesRead;
    }
}
