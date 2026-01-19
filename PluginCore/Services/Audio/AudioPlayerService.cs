using BarRaider.SdTools;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SCStreamDeck.Services.Audio;

/// <summary>
///     Audio player service using WASAPI shared mode with mixing support.
///     Pre-initializes audio device for zero-latency playback.
/// </summary>
public sealed class AudioPlayerService : IAudioPlayerService
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
                Logger.Instance.LogMessage(TracingLevel.ERROR,
                    $"[{nameof(AudioPlayerService)}] Failed to play audio: {ex.Message}");
                throw;
            }
        }
    }

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

            _outputDevice?.Stop();
            _outputDevice?.Dispose();
            _deviceEnumerator?.Dispose();

            _disposed = true;
        }
    }

    private static AudioReaderAdapter CreateReaderAdapter(string filePath)
    {
        try
        {
            AudioFileReader reader = new(filePath);
            return new AudioReaderAdapter(reader);
        }
        catch (Exception)
        {
            MediaFoundationReader reader = new(filePath);
            ISampleProvider sampleProvider = reader.ToSampleProvider();
            return new AudioReaderAdapter(sampleProvider, reader);
        }
    }

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
