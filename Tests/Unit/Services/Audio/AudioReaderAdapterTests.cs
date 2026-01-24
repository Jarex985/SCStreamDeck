using FluentAssertions;
using NAudio.Wave;
using SCStreamDeck.Services.Audio;

namespace Tests.Unit.Services.Audio;

public sealed class AudioReaderAdapterTests
{
    [Fact]
    public void Read_DisposesSourceWhenPlaybackCompletes_AndStopsReadingAfterwards()
    {
        FakeSampleProvider source = new(5, 0, 7);
        AudioReaderAdapter adapter = new(source);
        float[] buffer = new float[16];

        adapter.Read(buffer, 0, buffer.Length).Should().Be(5);
        source.DisposeCalls.Should().Be(0);

        adapter.Read(buffer, 0, buffer.Length).Should().Be(0);
        source.DisposeCalls.Should().Be(1);

        // Once disposed, adapter should not read from source again.
        adapter.Read(buffer, 0, buffer.Length).Should().Be(0);
        source.ReadCalls.Should().Be(2);
        source.DisposeCalls.Should().Be(1);
    }

    private sealed class FakeSampleProvider : ISampleProvider, IDisposable
    {
        private readonly int[] _sequence;
        private int _index;

        public FakeSampleProvider(params int[] sequence)
        {
            _sequence = sequence;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        }

        public int ReadCalls { get; private set; }
        public int DisposeCalls { get; private set; }

        public void Dispose() => DisposeCalls++;

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            ReadCalls++;
            if (_index >= _sequence.Length)
            {
                return 0;
            }

            int value = _sequence[_index++];
            return value;
        }
    }
}
