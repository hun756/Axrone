namespace Axrone.Shapes2D;

/// <summary>Base paint with memoized content fingerprint.</summary>
public abstract class ShapePaint
{
    private string? _cachedFingerprint;

    /// <summary>Paint kind tag.</summary>
    public abstract PaintKind KindTag { get; }

    /// <summary>Paint kind name.</summary>
    public abstract string Kind { get; }

    /// <summary>Memoized fingerprint.</summary>
    public string GetFingerprint() => _cachedFingerprint ??= ComputeFingerprint();

    /// <summary>Computes the fingerprint.</summary>
    protected abstract string ComputeFingerprint();

    /// <summary>Hashes UTF-8 text with FNV-1a 32 via <c>Axrone.Hash</c>.</summary>
    protected static string HashUtf8(string payload)
    {
        using var hasher = HashEngine.CreateHasher(HashAlgorithmId.Fnv1a32);
        hasher.AppendUtf8(payload);
        return hasher.GetCurrentDigest().ToString();
    }
}

/// <summary>Solid color paint.</summary>
public sealed class SolidPaint : ShapePaint
{
    /// <summary>Fill color.</summary>
    public ResolvedColor Color { get; }

    /// <inheritdoc/>
    public override PaintKind KindTag => PaintKind.Solid;

    /// <inheritdoc/>
    public override string Kind => "solid";

    /// <summary>Creates a solid paint.</summary>
    public SolidPaint(ResolvedColor color)
    {
        Color = color.Clamp();
    }

    /// <inheritdoc/>
    protected override string ComputeFingerprint() =>
        FormattableString.Invariant($"solid:{Color.R}:{Color.G}:{Color.B}:{Color.A}");
}

/// <summary>Linear gradient paint.</summary>
public sealed class LinearGradientPaint : ShapePaint
{
    /// <summary>Gradient start.</summary>
    public Vec2 Start { get; }

    /// <summary>Gradient end.</summary>
    public Vec2 End { get; }

    /// <summary>Sorted stops.</summary>
    public ImmutableArray<GradientStop> Stops { get; }

    /// <summary>Spread.</summary>
    public GradientSpread Spread { get; }

    /// <summary>Color space.</summary>
    public GradientColorSpace ColorSpace { get; }

    /// <summary>Units.</summary>
    public GradientUnits Units { get; }

    /// <inheritdoc/>
    public override PaintKind KindTag => PaintKind.LinearGradient;

    /// <inheritdoc/>
    public override string Kind => "linear-gradient";

    /// <summary>Creates a linear gradient.</summary>
    public LinearGradientPaint(
        Vec2 start,
        Vec2 end,
        ReadOnlySpan<GradientStop> stops,
        GradientSpread spread = GradientSpread.Pad,
        GradientColorSpace colorSpace = GradientColorSpace.Srgb,
        GradientUnits units = GradientUnits.ShapeBounds)
    {
        if (stops.Length == 0)
        {
            ThrowHelper.ThrowPaintValidation("Gradient must have at least one stop.");
        }

        Start = start;
        End = end;
        Spread = spread;
        ColorSpace = colorSpace;
        Units = units;

        GradientStop[] sorted = stops.ToArray();
        Array.Sort(sorted, static (a, b) => a.Offset.CompareTo(b.Offset));
        Stops = ImmutableCollectionsMarshal.AsImmutableArray(sorted);
    }

    /// <inheritdoc/>
    protected override string ComputeFingerprint()
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"linear:{Start.X},{Start.Y}:{End.X},{End.Y}:{Units}:{Spread}:{ColorSpace}:");
        foreach (GradientStop stop in Stops)
        {
            sb.Append(CultureInfo.InvariantCulture, $"{stop.Offset}:{stop.Color.R}:{stop.Color.G}:{stop.Color.B}:{stop.Color.A}|");
        }

        return HashUtf8(sb.ToString());
    }
}

/// <summary>Radial gradient paint.</summary>
public sealed class RadialGradientPaint : ShapePaint
{
    /// <summary>Gradient center.</summary>
    public Vec2 Center { get; }

    /// <summary>Gradient radius.</summary>
    public float Radius
    {
        get => field;
        init => field = ThrowHelper.AssertPositive(value, nameof(Radius));
    }

    /// <summary>Sorted stops.</summary>
    public ImmutableArray<GradientStop> Stops { get; }

    /// <summary>Spread.</summary>
    public GradientSpread Spread { get; }

    /// <summary>Color space.</summary>
    public GradientColorSpace ColorSpace { get; }

    /// <summary>Units.</summary>
    public GradientUnits Units { get; }

    /// <inheritdoc/>
    public override PaintKind KindTag => PaintKind.RadialGradient;

    /// <inheritdoc/>
    public override string Kind => "radial-gradient";

    /// <summary>Creates a radial gradient.</summary>
    public RadialGradientPaint(
        Vec2 center,
        float radius,
        ReadOnlySpan<GradientStop> stops,
        GradientSpread spread = GradientSpread.Pad,
        GradientColorSpace colorSpace = GradientColorSpace.Srgb,
        GradientUnits units = GradientUnits.ShapeBounds)
    {
        if (stops.Length == 0)
        {
            ThrowHelper.ThrowPaintValidation("Gradient must have at least one stop.");
        }

        Center = center;
        Radius = ThrowHelper.AssertPositive(radius, nameof(radius));
        Spread = spread;
        ColorSpace = colorSpace;
        Units = units;

        GradientStop[] sorted = stops.ToArray();
        Array.Sort(sorted, static (a, b) => a.Offset.CompareTo(b.Offset));
        Stops = ImmutableCollectionsMarshal.AsImmutableArray(sorted);
    }

    /// <inheritdoc/>
    protected override string ComputeFingerprint()
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"radial:{Center.X},{Center.Y}:{Radius}:{Units}:{Spread}:{ColorSpace}:");
        foreach (GradientStop stop in Stops)
        {
            sb.Append(CultureInfo.InvariantCulture, $"{stop.Offset}:{stop.Color.R}:{stop.Color.G}:{stop.Color.B}:{stop.Color.A}|");
        }

        return HashUtf8(sb.ToString());
    }
}

/// <summary>Stroke: paint plus width and alignment.</summary>
public readonly record struct ShapeStroke
{
    /// <summary>Stroke paint.</summary>
    public ShapePaint Paint { get; }

    /// <summary>Stroke width.</summary>
    public float Width
    {
        get => field;
        init => field = ThrowHelper.AssertPositive(value, nameof(Width));
    }

    /// <summary>Stroke alignment.</summary>
    public ShapeStrokeAlignment Alignment { get; }

    /// <summary>Creates a stroke.</summary>
    public ShapeStroke(ShapePaint paint, float width, ShapeStrokeAlignment alignment = ShapeStrokeAlignment.Center)
    {
        Paint = paint ?? throw new ArgumentNullException(nameof(paint));
        Width = ThrowHelper.AssertPositive(width, "stroke.width");
        Alignment = alignment;
    }
}

/// <summary>Paint and stroke factory.</summary>
public static class ShapePaintFactory
{
    /// <summary>Creates a gradient stop.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GradientStop CreateGradientStop(float offset, ResolvedColor color)
    {
        ThrowHelper.AssertFinite(offset, nameof(offset));
        if (offset < 0f || offset > 1f)
        {
            ThrowHelper.ThrowPaintValidation("Gradient stop offset must be between 0 and 1.");
        }

        return new GradientStop(offset, color);
    }

    /// <summary>Creates a gradient stop from a color string.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GradientStop CreateGradientStop(float offset, string colorString) =>
        CreateGradientStop(offset, ColorParser.ParseColor(colorString));

    /// <summary>Creates a solid paint.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SolidPaint CreateSolidPaint(ResolvedColor color) => new(color);

    /// <summary>Creates a solid paint from a color string.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SolidPaint CreateSolidPaint(string colorString) => new(ColorParser.ParseColor(colorString));

    /// <summary>Creates a linear gradient.</summary>
    public static LinearGradientPaint CreateLinearGradient(
        Vec2 start,
        Vec2 end,
        ReadOnlySpan<GradientStop> stops,
        GradientSpread spread = GradientSpread.Pad,
        GradientColorSpace colorSpace = GradientColorSpace.Srgb,
        GradientUnits units = GradientUnits.ShapeBounds) =>
        new(start, end, stops, spread, colorSpace, units);

    /// <summary>Creates a radial gradient.</summary>
    public static RadialGradientPaint CreateRadialGradient(
        Vec2 center,
        float radius,
        ReadOnlySpan<GradientStop> stops,
        GradientSpread spread = GradientSpread.Pad,
        GradientColorSpace colorSpace = GradientColorSpace.Srgb,
        GradientUnits units = GradientUnits.ShapeBounds) =>
        new(center, radius, stops, spread, colorSpace, units);

    /// <summary>Creates a stroke.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ShapeStroke CreateStroke(ShapePaint paint, float width, ShapeStrokeAlignment alignment = ShapeStrokeAlignment.Center) =>
        new(paint, width, alignment);
}

/// <summary>Gradient lookup-table builder and point sampler.</summary>
public static class PaintSampler
{
    private static readonly ConditionalWeakTable<ShapePaint, ConcurrentDictionary<int, float[]>> s_lutCache = new();

    /// <summary>Gets or builds a cached RGBA lookup table.</summary>
    public static float[] GetOrCreateLut(ShapePaint paint, int size = ShapeConstants.DefaultGradientLookupSize)
    {
        if (paint is SolidPaint)
        {
            ThrowHelper.ThrowPaintValidation("Cannot create LUT for SolidPaint.");
        }

        ConcurrentDictionary<int, float[]> dict = s_lutCache.GetOrCreateValue(paint);
        return dict.GetOrAdd(size, static (sz, p) => BuildLut(p, sz), paint);
    }

    private static float[] BuildLut(ShapePaint paint, int size)
    {
        if (size < 2)
        {
            ThrowHelper.ThrowPaintValidation("LUT size must be at least 2.");
        }

        ReadOnlySpan<GradientStop> stops = paint switch
        {
            LinearGradientPaint l => l.Stops.AsSpan(),
            RadialGradientPaint r => r.Stops.AsSpan(),
            _ => throw new UnreachableException(),
        };

        GradientColorSpace cs = paint switch
        {
            LinearGradientPaint l => l.ColorSpace,
            RadialGradientPaint r => r.ColorSpace,
            _ => throw new UnreachableException(),
        };

        float[] table = GC.AllocateArray<float>(size * 4, pinned: false);

        switch (cs)
        {
            case GradientColorSpace.LinearSrgb:
                BuildLutInternal<LinearSrgbColorPolicy>(stops, table);
                break;
            case GradientColorSpace.Hsl:
                BuildLutInternal<HslColorPolicy>(stops, table);
                break;
            case GradientColorSpace.Lab:
                BuildLutInternal<LabColorPolicy>(stops, table);
                break;
            default:
                BuildLutInternal<SrgbColorPolicy>(stops, table);
                break;
        }

        return table;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void BuildLutInternal<TPolicy>(ReadOnlySpan<GradientStop> stops, Span<float> table)
        where TPolicy : struct, IColorSpacePolicy
    {
        int size = table.Length / 4;
        float invStep = 1f / (size - 1);

        for (int i = 0; i < size; i++)
        {
            float t = i * invStep;
            ResolvedColor c = SampleStops<TPolicy>(stops, t);
            int idx = i * 4;
            table[idx] = c.R;
            table[idx + 1] = c.G;
            table[idx + 2] = c.B;
            table[idx + 3] = c.A;
        }
    }

    /// <summary>Applies a spread policy with validation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ApplySpread(float value, GradientSpread spread)
    {
        if (!float.IsFinite(value))
        {
            ThrowHelper.ThrowPaintValidation("Gradient sample value must be finite.");
        }

        return spread switch
        {
            GradientSpread.Repeat => RepeatSpreadPolicy.Apply(value),
            GradientSpread.Reflect => ReflectSpreadPolicy.Apply(value),
            _ => PadSpreadPolicy.Apply(value),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static ResolvedColor SampleStops<TPolicy>(ReadOnlySpan<GradientStop> stops, float t)
        where TPolicy : struct, IColorSpacePolicy
    {
        if (stops.Length == 1 || t <= stops[0].Offset)
        {
            return stops[0].Color;
        }

        if (t >= stops[^1].Offset)
        {
            return stops[^1].Color;
        }

        nuint length = (nuint)stops.Length;
        ref GradientStop baseRef = ref MemoryMarshal.GetReference(stops);

        for (nuint i = 0; i < length - 1; i++)
        {
            ref readonly GradientStop left = ref Unsafe.Add(ref baseRef, i);
            ref readonly GradientStop right = ref Unsafe.Add(ref baseRef, i + 1);

            if (t >= left.Offset && t <= right.Offset)
            {
                float range = right.Offset - left.Offset;
                float factor = range <= ShapeConstants.Epsilon ? 0f : (t - left.Offset) / range;
                return TPolicy.Interpolate(left.Color, right.Color, factor);
            }
        }

        return stops[^1].Color;
    }

    /// <summary>Samples a paint at a point.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ResolvedColor Sample(ShapePaint paint, Vec2 point, ShapeBounds bounds = default, int lutSize = ShapeConstants.DefaultGradientLookupSize)
    {
        if (paint is SolidPaint solid)
        {
            return solid.Color;
        }

        float[] lut = GetOrCreateLut(paint, lutSize);

        if (paint is LinearGradientPaint linear)
        {
            Vec2 start = linear.Start;
            Vec2 end = linear.End;
            if (linear.Units == GradientUnits.ShapeBounds)
            {
                if (bounds.Width <= 0f && bounds.Height <= 0f)
                {
                    ThrowHelper.ThrowPaintValidation("Bounds are required when sampling a bounds-relative paint.");
                }

                start = new Vec2(bounds.MinX + bounds.Width * start.X, bounds.MinY + bounds.Height * start.Y);
                end = new Vec2(bounds.MinX + bounds.Width * end.X, bounds.MinY + bounds.Height * end.Y);
            }

            Vec2 dir = end - start;
            float lenSq = dir.LengthSquared();
            float rawT = lenSq <= ShapeConstants.Epsilon
                ? 0f
                : ((point.X - start.X) * dir.X + (point.Y - start.Y) * dir.Y) / lenSq;

            float t = ApplySpread(rawT, linear.Spread);
            return SampleTable(lut, t);
        }

        if (paint is RadialGradientPaint radial)
        {
            Vec2 center = radial.Center;
            float radius = radial.Radius;
            if (radial.Units == GradientUnits.ShapeBounds)
            {
                if (bounds.Width <= 0f && bounds.Height <= 0f)
                {
                    ThrowHelper.ThrowPaintValidation("Bounds are required when sampling a bounds-relative paint.");
                }

                center = new Vec2(bounds.MinX + bounds.Width * center.X, bounds.MinY + bounds.Height * center.Y);
                radius = MathF.Max(bounds.Width, bounds.Height) * radius;
            }

            float safeRadius = MathF.Max(radius, ShapeConstants.Epsilon);
            Vec2 delta = point - center;
            float dist = delta.Length();
            float rawT = dist / safeRadius;
            float t = ApplySpread(rawT, radial.Spread);
            return SampleTable(lut, t);
        }

        throw new UnreachableException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static ResolvedColor SampleTable(float[] table, float t)
    {
        int count = table.Length / 4;
        float pos = Math.Clamp(t, 0f, 1f) * (count - 1);
        int leftIdx = (int)MathF.Floor(pos);
        int rightIdx = Math.Min(leftIdx + 1, count - 1);
        float factor = pos - leftIdx;

        int lOff = leftIdx * 4;
        int rOff = rightIdx * 4;

        ref float baseRef = ref MemoryMarshal.GetArrayDataReference(table);

        float r = Unsafe.Add(ref baseRef, lOff) + (Unsafe.Add(ref baseRef, rOff) - Unsafe.Add(ref baseRef, lOff)) * factor;
        float g = Unsafe.Add(ref baseRef, lOff + 1) + (Unsafe.Add(ref baseRef, rOff + 1) - Unsafe.Add(ref baseRef, lOff + 1)) * factor;
        float b = Unsafe.Add(ref baseRef, lOff + 2) + (Unsafe.Add(ref baseRef, rOff + 2) - Unsafe.Add(ref baseRef, lOff + 2)) * factor;
        float a = Unsafe.Add(ref baseRef, lOff + 3) + (Unsafe.Add(ref baseRef, rOff + 3) - Unsafe.Add(ref baseRef, lOff + 3)) * factor;

        return new ResolvedColor(r, g, b, a);
    }
}
