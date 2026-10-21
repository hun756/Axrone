namespace Axrone.Particles;

/// <summary>512-entry curve lookup table over aligned native memory.</summary>
public unsafe struct CurveLUT : IDisposable
{
    /// <summary>Table size.</summary>
    public const int TableSize = 512;

    private float* _table;
    private int _isAllocated;

    /// <summary>Allocates the table once.</summary>
    public void Allocate()
    {
        if (Interlocked.Exchange(ref _isAllocated, 1) == 0)
        {
            _table = (float*)NativeMemory.AlignedAlloc(TableSize * sizeof(float), 64);
            if (_table == null)
            {
                ParticleThrowHelper.MemoryAllocationFailed((nuint)(TableSize * sizeof(float)));
            }

            new Span<float>(_table, TableSize).Clear();
        }
    }

    /// <summary>Evaluates with linear interpolation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly float Evaluate(float normalizedTime)
    {
        if (_table == null)
        {
            return 0f;
        }

        float clamped = Math.Clamp(normalizedTime, 0.0f, 1.0f);
        float scaled = clamped * (TableSize - 1);
        int idx = (int)scaled;
        float frac = scaled - idx;

        if (idx >= TableSize - 1)
        {
            return _table[TableSize - 1];
        }

        return MathF.FusedMultiplyAdd(frac, _table[idx + 1] - _table[idx], _table[idx]);
    }

    /// <summary>Table as a span.</summary>
    public readonly Span<float> AsSpan() => new(_table, TableSize);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isAllocated, 0) == 1)
        {
            if (_table != null)
            {
                NativeMemory.Free(_table);
                _table = null;
            }
        }
    }
}

/// <summary>Time-valued curve with Catmull-Rom baking.</summary>
public sealed class Curve : IDisposable
{
    /// <summary>Curve mode.</summary>
    public CurveMode Mode { get; set; }

    /// <summary>Constant value.</summary>
    public float Constant { get; set; }

    /// <summary>Random minimum.</summary>
    public float ConstantMin { get; set; }

    /// <summary>Random maximum.</summary>
    public float ConstantMax { get; set; }

    /// <summary>Scale multiplier.</summary>
    public float Multiplier { get; set; } = 1.0f;

    private CurveLUT _lutPrimary;
    private CurveLUT _lutSecondary;
    private bool _disposed;

    /// <summary>Creates a curve.</summary>
    public Curve(CurveMode mode = CurveMode.Constant, float constant = 1.0f)
    {
        Mode = mode;
        Constant = constant;
        ConstantMin = constant;
        ConstantMax = constant;
        _lutPrimary.Allocate();
        _lutSecondary.Allocate();
        BakeIdentity();
    }

    /// <summary>Bakes Catmull-Rom keys.</summary>
    public void BakeCatmullRom(ReadOnlySpan<float> keys, bool isSecondary = false)
    {
        ref CurveLUT lut = ref isSecondary ? ref _lutSecondary : ref _lutPrimary;
        Span<float> span = lut.AsSpan();
        int keyCount = keys.Length;
        if (keyCount < 2)
        {
            float val = keyCount == 1 ? keys[0] : 0f;
            span.Fill(val);
            return;
        }

        for (int i = 0; i < CurveLUT.TableSize; i++)
        {
            float t = (float)i / (CurveLUT.TableSize - 1);
            float p = t * (keyCount - 1);
            int i1 = (int)p;
            int i0 = Math.Max(i1 - 1, 0);
            int i2 = Math.Min(i1 + 1, keyCount - 1);
            int i3 = Math.Min(i1 + 2, keyCount - 1);
            float lt = p - i1;

            float p0 = keys[i0];
            float p1 = keys[i1];
            float p2 = keys[i2];
            float p3 = keys[i3];

            float val = 0.5f * (
                (2.0f * p1) +
                (-p0 + p2) * lt +
                ((2.0f * p0) - (5.0f * p1) + (4.0f * p2) - p3) * (lt * lt) +
                (-p0 + (3.0f * p1) - (3.0f * p2) + p3) * (lt * lt * lt));
            span[i] = val;
        }
    }

    private void BakeIdentity()
    {
        Span<float> p = _lutPrimary.AsSpan();
        Span<float> s = _lutSecondary.AsSpan();
        for (int i = 0; i < CurveLUT.TableSize; i++)
        {
            float v = (float)i / (CurveLUT.TableSize - 1);
            p[i] = v;
            s[i] = v;
        }
    }

    /// <summary>Evaluates the curve.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float Evaluate(float normalizedTime, float randomRatio = 0.0f)
    {
        return Mode switch
        {
            CurveMode.Constant => Constant * Multiplier,
            CurveMode.TwoConstants => MathF.FusedMultiplyAdd(ConstantMax - ConstantMin, randomRatio, ConstantMin) * Multiplier,
            CurveMode.Curve => _lutPrimary.Evaluate(normalizedTime) * Multiplier,
            CurveMode.TwoCurves => MathF.FusedMultiplyAdd(
                _lutSecondary.Evaluate(normalizedTime) - _lutPrimary.Evaluate(normalizedTime),
                randomRatio,
                _lutPrimary.Evaluate(normalizedTime)) * Multiplier,
            _ => Constant * Multiplier,
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
    ~Curve() => Dispose(false);
}
