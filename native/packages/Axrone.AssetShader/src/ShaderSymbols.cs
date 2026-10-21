namespace Axrone.AssetShader;

/// <summary>
/// Strongly-typed shader symbol: 64-bit FNV-1a hash (via <c>Axrone.Hash</c>)
/// pairing an optional debug name. Hash values are deterministic within a
/// process and used for in-memory indexes only.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly struct ShaderSymbol : IEquatable<ShaderSymbol>, IComparable<ShaderSymbol>,
    IEqualityOperators<ShaderSymbol, ShaderSymbol, bool>,
    IComparisonOperators<ShaderSymbol, ShaderSymbol, bool>,
    IFormattable
{
    /// <summary>Symbol hash.</summary>
    public ulong Hash { get; }

    private readonly string? _debugName;

    /// <summary>Creates a symbol from a name.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ShaderSymbol(ReadOnlySpan<char> name)
    {
        Hash = Fnv1a64Algorithm.Hash(MemoryMarshal.AsBytes(name)).Value;
        _debugName = name.ToString();
    }

    /// <summary>Creates a symbol from a precomputed hash.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ShaderSymbol(ulong hash, string? debugName = null)
    {
        Hash = hash;
        _debugName = debugName;
    }

    /// <summary>Debug name, or hex hash when anonymous.</summary>
    public string Name => _debugName ?? $"0x{Hash:X16}";

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ShaderSymbol other) => Hash == other.Hash;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ShaderSymbol other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => (int)Hash ^ (int)(Hash >> 32);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(ShaderSymbol other) => Hash.CompareTo(other.Hash);

    /// <inheritdoc/>
    public override string ToString() => Name;

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        _debugName ?? Hash.ToString(format ?? "X16", formatProvider);

    /// <inheritdoc/>
    public static bool operator ==(ShaderSymbol left, ShaderSymbol right) => left.Hash == right.Hash;

    /// <inheritdoc/>
    public static bool operator !=(ShaderSymbol left, ShaderSymbol right) => left.Hash != right.Hash;

    /// <inheritdoc/>
    public static bool operator <(ShaderSymbol left, ShaderSymbol right) => left.Hash < right.Hash;

    /// <inheritdoc/>
    public static bool operator <=(ShaderSymbol left, ShaderSymbol right) => left.Hash <= right.Hash;

    /// <inheritdoc/>
    public static bool operator >(ShaderSymbol left, ShaderSymbol right) => left.Hash > right.Hash;

    /// <inheritdoc/>
    public static bool operator >=(ShaderSymbol left, ShaderSymbol right) => left.Hash >= right.Hash;

    /// <inheritdoc/>
    public static implicit operator ShaderSymbol(string name) => new(name.AsSpan());
}

/// <summary>Source position (1-based line, column).</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SourceLocation(uint Line, uint Column) :
    IComparable<SourceLocation>,
    IComparisonOperators<SourceLocation, SourceLocation, bool>
{
    /// <summary>Zero location.</summary>
    public static SourceLocation Zero => default;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(SourceLocation other)
    {
        int lineCmp = Line.CompareTo(other.Line);
        return lineCmp != 0 ? lineCmp : Column.CompareTo(other.Column);
    }

    /// <inheritdoc/>
    public override string ToString() => $"({Line}:{Column})";

    /// <inheritdoc/>
    public static bool operator <(SourceLocation left, SourceLocation right) => left.CompareTo(right) < 0;

    /// <inheritdoc/>
    public static bool operator <=(SourceLocation left, SourceLocation right) => left.CompareTo(right) <= 0;

    /// <inheritdoc/>
    public static bool operator >(SourceLocation left, SourceLocation right) => left.CompareTo(right) > 0;

    /// <inheritdoc/>
    public static bool operator >=(SourceLocation left, SourceLocation right) => left.CompareTo(right) >= 0;
}

/// <summary>128-bit variant identity: two folded FNV-1a streams.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct VariantKey(ulong Low, ulong High) :
    IComparable<VariantKey>,
    IComparisonOperators<VariantKey, VariantKey, bool>
{
    /// <summary>Default (empty selection) key.</summary>
    public static VariantKey Default => default;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(VariantKey other)
    {
        int cmp = Low.CompareTo(other.Low);
        return cmp != 0 ? cmp : High.CompareTo(other.High);
    }

    /// <inheritdoc/>
    public static bool operator <(VariantKey left, VariantKey right) => left.CompareTo(right) < 0;

    /// <inheritdoc/>
    public static bool operator <=(VariantKey left, VariantKey right) => left.CompareTo(right) <= 0;

    /// <inheritdoc/>
    public static bool operator >(VariantKey left, VariantKey right) => left.CompareTo(right) > 0;

    /// <inheritdoc/>
    public static bool operator >=(VariantKey left, VariantKey right) => left.CompareTo(right) >= 0;

    /// <inheritdoc/>
    public override string ToString() => $"{Low:X16}{High:X16}";
}

/// <summary>Ordinal variant-key comparer.</summary>
public sealed class VariantKeyEqualityComparer : IEqualityComparer<VariantKey>
{
    /// <summary>Shared instance.</summary>
    public static readonly VariantKeyEqualityComparer Instance = new();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(VariantKey x, VariantKey y) => x.Low == y.Low && x.High == y.High;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetHashCode(VariantKey obj) => unchecked((int)(obj.Low ^ (obj.High * 397)));
}

/// <summary>Cache-aligned variant slot.</summary>
/// <typeparam name="TKey">Key type.</typeparam>
/// <typeparam name="TValue">Value type.</typeparam>
[StructLayout(LayoutKind.Explicit, Size = 128)]
public struct PaddedCacheSlot<TKey, TValue>
    where TKey : struct, IEquatable<TKey>
    where TValue : class?
{
    /// <summary>Slot key.</summary>
    [FieldOffset(0)]
    public TKey Key;

    /// <summary>Slot value.</summary>
    [FieldOffset(32)]
    public TValue? Value;

    /// <summary>Slot state.</summary>
    [FieldOffset(40)]
    public ulong State;
}

/// <summary>Shader stage tag.</summary>
public enum ShaderStageTag : byte
{
    /// <summary>Vertex stage.</summary>
    Vertex = 0,

    /// <summary>Fragment stage.</summary>
    Fragment = 1,

    /// <summary>Compute stage.</summary>
    Compute = 2,
}

/// <summary>Float precision qualifier.</summary>
public enum ShaderPrecision : byte
{
    /// <summary>No precision qualifier.</summary>
    None = 0,

    /// <summary>Low precision.</summary>
    Lowp = 1,

    /// <summary>Medium precision.</summary>
    Mediump = 2,

    /// <summary>High precision.</summary>
    Highp = 3,
}

/// <summary>Varying interpolation qualifier.</summary>
public enum ShaderInterpolation : byte
{
    /// <summary>Smooth interpolation.</summary>
    Smooth = 0,

    /// <summary>Flat interpolation.</summary>
    Flat = 1,

    /// <summary>No-perspective interpolation.</summary>
    NoPerspective = 2,
}

/// <summary>Shader property type.</summary>
public enum ShaderPropertyType : byte
{
    /// <summary>Scalar float.</summary>
    Float,

    /// <summary>Two-component float vector.</summary>
    Vec2,

    /// <summary>Three-component float vector.</summary>
    Vec3,

    /// <summary>Four-component float vector.</summary>
    Vec4,

    /// <summary>Scalar int.</summary>
    Int,

    /// <summary>Two-component int vector.</summary>
    Ivec2,

    /// <summary>Three-component int vector.</summary>
    Ivec3,

    /// <summary>Four-component int vector.</summary>
    Ivec4,

    /// <summary>Scalar uint.</summary>
    Uint,

    /// <summary>Two-component uint vector.</summary>
    Uvec2,

    /// <summary>Three-component uint vector.</summary>
    Uvec3,

    /// <summary>Four-component uint vector.</summary>
    Uvec4,

    /// <summary>Boolean scalar.</summary>
    Bool,

    /// <summary>Two-component boolean vector.</summary>
    Bvec2,

    /// <summary>Three-component boolean vector.</summary>
    Bvec3,

    /// <summary>Four-component boolean vector.</summary>
    Bvec4,

    /// <summary>3x3 matrix.</summary>
    Mat3,

    /// <summary>4x4 matrix.</summary>
    Mat4,

    /// <summary>2D sampler.</summary>
    Sampler2D,

    /// <summary>Cube sampler.</summary>
    SamplerCube,
}

/// <summary>Shader property scope.</summary>
public enum ShaderPropertyScope : byte
{
    /// <summary>Per-material property.</summary>
    Material = 0,

    /// <summary>Per-renderer property.</summary>
    Renderer = 1,
}

/// <summary>Shader property kind.</summary>
public enum ShaderPropertyKind : byte
{
    /// <summary>Regular property.</summary>
    Regular = 0,

    /// <summary>Ranged property.</summary>
    Range = 1,

    /// <summary>Toggle property.</summary>
    Toggle = 2,

    /// <summary>Enum property.</summary>
    Enum = 3,
}

/// <summary>Diagnostic severity.</summary>
public enum ShaderDiagnosticSeverity : byte
{
    /// <summary>Informational.</summary>
    Info = 0,

    /// <summary>Warning.</summary>
    Warning = 1,

    /// <summary>Error.</summary>
    Error = 2,
}
