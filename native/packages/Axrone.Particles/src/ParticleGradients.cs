namespace Axrone.Particles;

/// <summary>1024-entry gradient lookup table over aligned native memory.</summary>
public unsafe struct GradientLUT : IDisposable
{
    /// <summary>Table size.</summary>
    public const int TableSize = 1024;

    private Vec4* _table;
    private int _isAllocated;

    /// <summary>Allocates the table once.</summary>
    public void Allocate()
    {
        if (Interlocked.Exchange(ref _isAllocated, 1) == 0)
        {
            _table = (Vec4*)NativeMemory.AlignedAlloc((nuint)(TableSize * sizeof(Vec4)), 64);
            if (_table == null)
            {
                ParticleThrowHelper.MemoryAllocationFailed((nuint)(TableSize * sizeof(Vec4)));
            }

            new Span<Vec4>(_table, TableSize).Clear();
        }
    }

    /// <summary>Evaluates with linear interpolation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly Vec4 Evaluate(float normalizedTime)
    {
        if (_table == null)
        {
            return Vec4.One;
        }

        float clamped = Math.Clamp(normalizedTime, 0.0f, 1.0f);
        float scaled = clamped * (TableSize - 1);
        int idx = (int)scaled;
        float frac = scaled - idx;

        if (idx >= TableSize - 1)
        {
            return _table[TableSize - 1];
        }

        return Vec4.Lerp(_table[idx], _table[idx + 1], frac);
    }

    /// <summary>Table as a span.</summary>
    public readonly Span<Vec4> AsSpan() => new(_table, TableSize);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isAllocated, 0) == 1)
        {
            if (_table != null)
            {
                NativeMemory.AlignedFree(_table);
                _table = null;
            }
        }
    }
}

/// <summary>Color gradient with baked lookup tables.</summary>
public sealed class Gradient : IDisposable
{
    /// <summary>Gradient mode.</summary>
    public GradientMode Mode { get; set; }

    /// <summary>Minimum color.</summary>
    public Vec4 ColorMin { get; set; } = Vec4.One;

    /// <summary>Maximum color.</summary>
    public Vec4 ColorMax { get; set; } = Vec4.One;

    /// <summary>Color space.</summary>
    public ColorSpace ColorSpace { get; set; } = ColorSpace.Rgb;

    /// <summary>Blend mode.</summary>
    public BlendMode BlendMode { get; set; } = BlendMode.Normal;

    private GradientLUT _lutPrimary;
    private GradientLUT _lutSecondary;
    private bool _disposed;

    /// <summary>Creates a gradient.</summary>
    public Gradient(GradientMode mode = GradientMode.Color)
    {
        Mode = mode;
        _lutPrimary.Allocate();
        _lutSecondary.Allocate();
        BakeSolid(Vec4.One);
    }

    /// <summary>Bakes a solid color.</summary>
    public void BakeSolid(Vec4 color, bool isSecondary = false)
    {
        ref GradientLUT lut = ref isSecondary ? ref _lutSecondary : ref _lutPrimary;
        lut.AsSpan().Fill(color);
    }

    /// <summary>Bakes a linear gradient.</summary>
    public void BakeLinearGradient(Vec4 start, Vec4 end, bool isSecondary = false)
    {
        ref GradientLUT lut = ref isSecondary ? ref _lutSecondary : ref _lutPrimary;
        Span<Vec4> span = lut.AsSpan();
        for (int i = 0; i < GradientLUT.TableSize; i++)
        {
            float t = (float)i / (GradientLUT.TableSize - 1);
            span[i] = Vec4.Lerp(start, end, t);
        }
    }

    /// <summary>Evaluates the gradient.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Vec4 Evaluate(float normalizedTime, float randomRatio = 0.0f)
    {
        return Mode switch
        {
            GradientMode.Color => ColorMin,
            GradientMode.TwoColors => Vec4.Lerp(ColorMin, ColorMax, randomRatio),
            GradientMode.Gradient => _lutPrimary.Evaluate(normalizedTime),
            GradientMode.TwoGradients => Vec4.Lerp(_lutPrimary.Evaluate(normalizedTime), _lutSecondary.Evaluate(normalizedTime), randomRatio),
            GradientMode.RandomColor => EvaluateRandomHue(randomRatio),
            _ => ColorMin,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static Vec4 EvaluateRandomHue(float ratio)
    {
        float hue = ratio * 6.0f;
        int hi = (int)hue % 6;
        float f = hue - MathF.Floor(hue);
        float q = 1.0f - f;

        return hi switch
        {
            0 => new Vec4(1.0f, f, 0.0f, 1.0f),
            1 => new Vec4(q, 1.0f, 0.0f, 1.0f),
            2 => new Vec4(0.0f, 1.0f, f, 1.0f),
            3 => new Vec4(0.0f, q, 1.0f, 1.0f),
            4 => new Vec4(f, 0.0f, 1.0f, 1.0f),
            _ => new Vec4(1.0f, 0.0f, q, 1.0f),
        };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _lutPrimary.Dispose();
            _lutSecondary.Dispose();
            _disposed = true;
        }
    }

    /// <summary>Finalizer.</summary>
    ~Gradient() => Dispose(false);
}
