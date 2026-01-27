using System.Diagnostics.CodeAnalysis;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SCStreamDeck.Logging;

namespace SCStreamDeck.Services.Audio;

/// <summary>
///     Audio player service using WASAPI shared mode with mixing support.
///     Pre-initializes the audio output to reduce first-play latency.
/// </summary>
public sealed class AudioPlayerService : IDisposable
{
    private readonly MMDeviceEnumerator _deviceEnumerator;
    private readonly object _lock = new();
    private readonly MixingSampleProvider _mixer;
    private readonly WasapiOut _outputDevice;
    private bool _disposed;

    public AudioPlayerService()
    {
        _deviceEnumerator = new MMDeviceEnumerator();

        MMDevice device = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        _outputDevice = new WasapiOut(device, AudioClientShareMode.Shared, false, 50);

        _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2)) { ReadFully = true };

        _outputDevice.Init(_mixer);
        _outputDevice.Play();
    }

    /// <summary>
    ///     Disposes audio resources.
    ///     Marked as [ExcludeFromCodeCoverage] because:
    ///     - Depends on NAudio WASAPI audio device (MMDeviceEnumerator, WasapiOut)
    ///     - Integration testing requires audio system initialization
    ///     - Resource cleanup pattern verified manually
    /// </summary>
    [ExcludeFromCodeCoverage]
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _outputDevice.Stop();
            _outputDevice.Dispose();
            _deviceEnumerator.Dispose();

            _disposed = true;
        }
    }

    public void Play(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            try
            {
                AudioReaderAdapter adapter = CreateReaderAdapter(filePath);
                ISampleProvider sampleProvider = ConvertToMixerFormat(adapter);
                _mixer.AddMixerInput(sampleProvider);
            }
            catch (Exception ex)
            {
                Log.Err($"[{nameof(AudioPlayerService)}] Failed to play audio: {ex.Message}", ex);
                throw;
            }
        }
    }

    private static AudioReaderAdapter CreateReaderAdapter(string filePath)
    {
        try
        {
            AudioFileReader reader = new(filePath);
            return new AudioReaderAdapter(reader);
        }
        catch (Exception ex)
        {
            Log.Debug(
                $"[{nameof(AudioPlayerService)}] AudioFileReader failed, falling back to MediaFoundationReader{Environment.NewLine}{ex}");
            MediaFoundationReader reader = new(filePath);
            ISampleProvider sampleProvider = reader.ToSampleProvider();
            return new AudioReaderAdapter(sampleProvider, reader);
        }
    }

    /// <summary>
    ///     Converts audio sample provider to mixer format (sample rate, channels).
    ///     Marked as [ExcludeFromCodeCoverage] because:
    ///     - Depends on NAudio sample providers and WASAPI output
    ///     - Integration testing requires audio device initialization
    ///     - Format conversion logic tested through Play() method integration
    /// </summary>
    [ExcludeFromCodeCoverage]
    private ISampleProvider ConvertToMixerFormat(AudioReaderAdapter source)
    {
        ISampleProvider sampleProvider = source;

        if (sampleProvider.WaveFormat.Channels == 1 && _mixer.WaveFormat.Channels == 2)
        {
            sampleProvider = new MonoToStereoSampleProvider(sampleProvider);
        }

        if (sampleProvider.WaveFormat.SampleRate != _mixer.WaveFormat.SampleRate)
        {
            sampleProvider = new WdlResamplingSampleProvider(sampleProvider, _mixer.WaveFormat.SampleRate);
        }

        return sampleProvider;
    }
}
