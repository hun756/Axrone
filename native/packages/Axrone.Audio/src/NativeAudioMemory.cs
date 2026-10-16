namespace Axrone.Audio;

/// <summary>
/// Cache-aligned native float buffer for decoded audio: interleaved frames,
/// up to 8 channels. Zeroed on allocation, freed on dispose.
/// </summary>
public sealed unsafe class NativeAudioMemory : IDisposable
{
    private float* _pointer;
    private readonly int _sampleCount;
    private readonly int _channelCount;
    private readonly int _sampleRate;
    private int _disposed;

    /// <summary>Native pointer.</summary>
    public float* Pointer => _pointer;

    /// <summary>Total float count (frames times channels).</summary>
    public int SampleCount => _sampleCount;

    /// <summary>Channel count in [1, 8].</summary>
    public int ChannelCount => _channelCount;

    /// <summary>Sample rate in Hz.</summary>
    public int SampleRate => _sampleRate;

    /// <summary>Allocates a zeroed buffer.</summary>
    public NativeAudioMemory(int sampleRate, int channelCount, int sampleCount)
    {
        if (sampleRate <= 0)
        {
            AudioThrowHelper.ThrowConfig("audio.invalid-sample-rate", "Sample rate must be positive.");
        }

        if (channelCount is <= 0 or > 8)
        {
            AudioThrowHelper.ThrowConfig("audio.invalid-channel-count", "Channel count must be in [1, 8].");
        }

        if (sampleCount <= 0)
        {
            AudioThrowHelper.ThrowConfig("audio.invalid-buffer", "Sample count must be positive.");
        }

        _sampleRate = sampleRate;
        _channelCount = channelCount;
        _sampleCount = sampleCount;

        nuint byteSize = (nuint)sampleCount * sizeof(float);
        _pointer = (float*)NativeMemory.AlignedAlloc(byteSize, 64);
        NativeMemory.Clear(_pointer, byteSize);
    }

    /// <summary>Views the buffer; throws after disposal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Span<float> AsSpan()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            AudioThrowHelper.ThrowDisposed(nameof(NativeAudioMemory));
        }

        return new Span<float>(_pointer, _sampleCount);
    }

    /// <summary>Views the buffer read-only; throws after disposal.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ReadOnlySpan<float> AsReadOnlySpan()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            AudioThrowHelper.ThrowDisposed(nameof(NativeAudioMemory));
        }

        return new ReadOnlySpan<float>(_pointer, _sampleCount);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_pointer != null)
        {
            NativeMemory.AlignedFree(_pointer);
            _pointer = null;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Finalizer frees native memory when Dispose was skipped.</summary>
    ~NativeAudioMemory() => Dispose();
}
