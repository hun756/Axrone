namespace Axrone.Audio.OpenAL.Tests;

internal sealed class FakeAudioDriver : IAudioDriver
{
    public bool IsOpen { get; private set; }
    public int OpenCalls;
    public int CloseCalls;
    public int BufferDataCalls;
    public int QueueCalls;
    public int PlayCalls;
    public int StopCalls;
    public readonly List<short> Uploaded = new();
    public readonly Queue<uint> Processed = new();
    public AudioDriverSourceState State = AudioDriverSourceState.Playing;
    private uint _nextBuffer = 1;

    public void Open(string? deviceName, int frequency)
    {
        OpenCalls++;
        IsOpen = true;
        _ = deviceName;
        _ = frequency;
    }

    public void Close()
    {
        CloseCalls++;
        IsOpen = false;
    }

    public uint[] GenBuffers(int count)
    {
        uint[] buffers = new uint[count];
        for (int i = 0; i < count; i++)
        {
            buffers[i] = _nextBuffer++;
        }

        return buffers;
    }

    public uint GenSource() => 100U;

    public void DeleteBuffers(uint[] buffers) => _ = buffers;

    public void DeleteSource(uint source) => _ = source;

    public void BufferDataShort(uint buffer, ReadOnlySpan<short> interleavedStereo, int frequency)
    {
        _ = buffer;
        _ = frequency;
        BufferDataCalls++;
        foreach (short sample in interleavedStereo)
        {
            Uploaded.Add(sample);
        }
    }

    public void QueueBuffers(uint source, uint[] buffers)
    {
        _ = source;
        QueueCalls++;
        _ = buffers;
    }

    public int UnqueueProcessed(uint source, uint[] destination)
    {
        _ = source;
        int count = 0;
        while (count < destination.Length && Processed.Count > 0)
        {
            destination[count++] = Processed.Dequeue();
        }

        return count;
    }

    public AudioDriverSourceState GetSourceState(uint source)
    {
        _ = source;
        return State;
    }

    public void Play(uint source)
    {
        _ = source;
        PlayCalls++;
        State = AudioDriverSourceState.Playing;
    }

    public void StopPlayback(uint source)
    {
        _ = source;
        StopCalls++;
        State = AudioDriverSourceState.Stopped;
    }

    public void Dispose()
    {
    }
}

public sealed class OpenALOutputBackendTests : IDisposable
{
    private readonly AudioSystem _system = new();
    private readonly FakeAudioDriver _driver = new();
    private readonly OpenALOutputBackend _backend;

    public OpenALOutputBackendTests()
    {
        _backend = new OpenALOutputBackend(_system, _driver, blockFrames: 64, bufferCount: 3);
    }

    public void Dispose()
    {
        _backend.Dispose();
        _driver.Dispose();
        _system.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Start_PrimesQueue_AndPlays()
    {
        _backend.Start();
        _backend.IsRunning.Should().BeTrue();
        _driver.OpenCalls.Should().Be(1);
        _driver.BufferDataCalls.Should().Be(3);
        _driver.PlayCalls.Should().Be(1);
        _backend.Stop();
        _backend.IsRunning.Should().BeFalse();
        _driver.CloseCalls.Should().Be(1);
    }

    [Fact]
    public void PumpOnce_Refills_ProcessedBuffers()
    {
        _backend.Start();
        int before = _driver.BufferDataCalls;
        _driver.Processed.Enqueue(1U);
        _driver.Processed.Enqueue(2U);
        _backend.PumpOnce();
        _driver.BufferDataCalls.Should().Be(before + 2);
        _driver.QueueCalls.Should().BeGreaterThan(1);
        _backend.Stop();
    }

    [Fact]
    public void PumpOnce_Restarts_StalledSource()
    {
        _backend.Start();
        _driver.State = AudioDriverSourceState.Stopped;
        int plays = _driver.PlayCalls;
        _backend.PumpOnce();
        _driver.PlayCalls.Should().Be(plays + 1);
        _backend.Stop();
    }

    [Fact]
    public void Stop_IsIdempotent_AndStartTwice_IsSingle()
    {
        _backend.Start();
        _backend.Start();
        _driver.OpenCalls.Should().Be(1);
        _backend.Stop();
        _backend.Stop();
        _driver.CloseCalls.Should().Be(1);
    }
}
