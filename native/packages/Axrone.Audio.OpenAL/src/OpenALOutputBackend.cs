namespace Axrone.Audio.OpenAL;

using Axrone.Audio;

/// <summary>
/// Streaming output backend: renders mixer blocks on a pump thread and feeds
/// them through a queued-buffer ring. Float samples convert to 16-bit with
/// clamping; suspends stream silence without stopping the device.
/// </summary>
public sealed class OpenALOutputBackend : IDisposable
{
    private readonly AudioSystem _system;
    private readonly IAudioDriver _driver;
    private readonly int _frequency;
    private readonly int _blockFrames;
    private readonly float[] _floatBlock;
    private readonly short[] _shortBlock;
    private readonly uint[] _buffers;
    private readonly uint[] _scratch;
    private readonly bool _preferFloat;
    private bool _useFloat;
    private uint _source;
    private Thread? _pump;
    private ManualResetEventSlim? _stop;
    private int _disposed;

    /// <summary>Whether the pump thread runs.</summary>
    public bool IsRunning => Volatile.Read(ref _disposed) == 0 && _pump is not null && _pump.IsAlive;

    /// <summary>Creates a backend.</summary>
    /// <param name="system">Mixer to render from.</param>
    /// <param name="driver">Output driver.</param>
    /// <param name="frequency">Output sample rate in Hz.</param>
    /// <param name="blockFrames">Frames per rendered block.</param>
    /// <param name="bufferCount">Queued buffers, at least two.</param>
    /// <param name="preferFloat">Probe AL_EXT_float32 and upload float when present.</param>
    public OpenALOutputBackend(
        AudioSystem system,
        IAudioDriver driver,
        int frequency = 48000,
        int blockFrames = 512,
        int bufferCount = 4,
        bool preferFloat = true)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frequency);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(blockFrames);
        ArgumentOutOfRangeException.ThrowIfLessThan(bufferCount, 2);

        _system = system;
        _driver = driver;
        _frequency = frequency;
        _blockFrames = blockFrames;
        _preferFloat = preferFloat;
        _floatBlock = new float[blockFrames * 2];
        _shortBlock = new short[blockFrames * 2];
        _buffers = new uint[bufferCount];
        _scratch = new uint[bufferCount];
    }

    /// <summary>Opens the device, primes the queue and starts the pump.</summary>
    /// <param name="deviceName">Device name, or default when null.</param>
    public void Start(string? deviceName = null)
    {
        ThrowIfDisposed();
        if (_pump is not null)
        {
            return;
        }

        _driver.Open(deviceName, _frequency);
        _useFloat = _preferFloat && _driver.IsExtensionPresent("AL_EXT_float32");

        uint[] handles = _driver.GenBuffers(_buffers.Length);
        Array.Copy(handles, _buffers, handles.Length);
        _source = _driver.GenSource();

        for (int i = 0; i < _buffers.Length; i++)
        {
            RenderInto(_buffers[i]);
        }

        _driver.QueueBuffers(_source, _buffers);
        _driver.Play(_source);

        _stop = new ManualResetEventSlim(false);
        _pump = new Thread(PumpLoop)
        {
            IsBackground = true,
            Name = "AxroneAudioPump",
        };
        _pump.Start();
    }

    /// <summary>Stops the pump and releases device resources.</summary>
    public void Stop()
    {
        _stop?.Set();
        _pump?.Join();
        _pump = null;
        _stop?.Dispose();
        _stop = null;

        if (!_driver.IsOpen)
        {
            return;
        }

        _driver.StopPlayback(_source);
        _driver.UnqueueProcessed(_source, _scratch);
        _driver.DeleteSource(_source);
        _driver.DeleteBuffers(_buffers);
        Array.Clear(_buffers);
        _source = 0;
        _driver.Close();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Stop();
        _driver.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Runs one refill iteration; the pump thread loops on this.</summary>
    internal void PumpOnce()
    {
        int count = _driver.UnqueueProcessed(_source, _scratch);
        for (int i = 0; i < count; i++)
        {
            RenderInto(_scratch[i]);
        }

        if (count > 0)
        {
            uint[] ready = _scratch;
            if (count != ready.Length)
            {
                ready = new uint[count];
                Array.Copy(_scratch, ready, count);
            }

            _driver.QueueBuffers(_source, ready);
        }

        if (_driver.GetSourceState(_source) != AudioDriverSourceState.Playing)
        {
            _driver.Play(_source);
        }
    }

    private void PumpLoop()
    {
        ManualResetEventSlim? stop = _stop;
        int intervalMs = Math.Max(1, (_blockFrames * 1000 / _frequency) / 2);
        while (stop is not null && !stop.IsSet)
        {
            PumpOnce();
            stop.Wait(intervalMs);
        }
    }

    private void RenderInto(uint buffer)
    {
        Span<float> block = _floatBlock;
        _system.ProcessAudioRenderBlock(block);

        if (_useFloat)
        {
            _driver.BufferDataFloat(buffer, block, _frequency);
            return;
        }

        for (int i = 0; i < block.Length; i++)
        {
            float clamped = Math.Clamp(block[i], -1.0f, 1.0f);
            _shortBlock[i] = (short)(clamped * 32767.0f);
        }

        _driver.BufferDataShort(buffer, _shortBlock, _frequency);
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new AudioDisposedException(nameof(OpenALOutputBackend));
        }
    }
}
