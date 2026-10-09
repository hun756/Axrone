using System.Globalization;

namespace Axrone.Render.Core;

/// <summary>
/// Element type of an attachment clear value. Selects which of the three
/// overlapping payload views of <see cref="ClearColorValue"/> is authoritative,
/// and therefore which <c>glClearBuffer*</c> entry point may consume it.
/// </summary>
public enum ClearColorFormat : byte
{
    /// <summary>Four IEEE-754 single-precision lanes; consumed by <c>glClearBufferfv</c>.</summary>
    Float32 = 0,

    /// <summary>Four signed 32-bit lanes; consumed by <c>glClearBufferiv</c>.</summary>
    Int32 = 1,

    /// <summary>Four unsigned 32-bit lanes; consumed by <c>glClearBufferuiv</c>.</summary>
    Uint32 = 2
}

/// <summary>
/// 20-byte tagged union carrying the clear value of a single render-target
/// attachment: a 16-byte payload exposed as three overlapping lane views plus a
/// <see cref="ClearColorFormat"/> discriminator at offset 16.
/// </summary>
/// <remarks>
/// <para>
/// The payload is written through exactly one view and may be read through any of
/// them, so a value built by <see cref="FromInt32"/> can still be handed to the
/// float span view as raw bits. The discriminator — never the caller — decides
/// which view is authoritative: it is only ever assigned by the constructors of
/// this type, which is why the tag cannot be forged from outside.
/// </para>
/// <para>
/// The default instance (zero payload, <see cref="ClearColorFormat.Float32"/>, which
/// is the zero enum member) therefore equals
/// <c>new ClearColorValue(0, 0, 0, 0)</c>, so a
/// <c>default(ClearColorValue)</c>-initialized field is always a valid, comparable
/// clear value and never needs a separate "unset" sentinel.
/// </para>
/// <para>
/// Equality is bitwise over the whole 16-byte payload plus the tag rather than a
/// float comparison: identical bits are equal even when they differ in the sign of
/// zero or in NaN payload, which is what makes the type usable as a frame-graph
/// cache key. Values carrying identical bits under different tags hash the same
/// but are not equal — see <see cref="GetHashCode"/>.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Explicit, Size = 20)]
public readonly struct ClearColorValue : IEquatable<ClearColorValue>
{
    /// <summary>Red lane of the float view (offset 0, see <see cref="R"/>).</summary>
    [FieldOffset(0)]
    private readonly float _f0;

    /// <summary>Green lane of the float view (offset 4, see <see cref="G"/>).</summary>
    [FieldOffset(4)]
    private readonly float _f1;

    /// <summary>Blue lane of the float view (offset 8, see <see cref="B"/>).</summary>
    [FieldOffset(8)]
    private readonly float _f2;

    /// <summary>Alpha lane of the float view (offset 12, see <see cref="A"/>).</summary>
    [FieldOffset(12)]
    private readonly float _f3;

    /// <summary>Red lane of the signed integer view (offset 0, see <see cref="IR"/>).</summary>
    [FieldOffset(0)]
    private readonly int _i0;

    /// <summary>Green lane of the signed integer view (offset 4, see <see cref="IG"/>).</summary>
    [FieldOffset(4)]
    private readonly int _i1;

    /// <summary>Blue lane of the signed integer view (offset 8, see <see cref="IB"/>).</summary>
    [FieldOffset(8)]
    private readonly int _i2;

    /// <summary>Alpha lane of the signed integer view (offset 12, see <see cref="IA"/>).</summary>
    [FieldOffset(12)]
    private readonly int _i3;

    /// <summary>Red lane of the unsigned integer view (offset 0, see <see cref="UR"/>).</summary>
    [FieldOffset(0)]
    private readonly uint _u0;

    /// <summary>Green lane of the unsigned integer view (offset 4, see <see cref="UG"/>).</summary>
    [FieldOffset(4)]
    private readonly uint _u1;

    /// <summary>Blue lane of the unsigned integer view (offset 8, see <see cref="UB"/>).</summary>
    [FieldOffset(8)]
    private readonly uint _u2;

    /// <summary>Alpha lane of the unsigned integer view (offset 12, see <see cref="UA"/>).</summary>
    [FieldOffset(12)]
    private readonly uint _u3;

    /// <summary>Payload view discriminator (offset 16, see <see cref="Format"/>).</summary>
    [FieldOffset(16)]
    private readonly ClearColorFormat _format;

    /// <summary>Gets the payload view that is authoritative for this value.</summary>
    public ClearColorFormat Format
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _format;
    }

    /// <summary>Gets the red lane reinterpreted as a float (raw bits, no conversion).</summary>
    public float R
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _f0;
    }

    /// <summary>Gets the green lane reinterpreted as a float (raw bits, no conversion).</summary>
    public float G
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _f1;
    }

    /// <summary>Gets the blue lane reinterpreted as a float (raw bits, no conversion).</summary>
    public float B
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _f2;
    }

    /// <summary>Gets the alpha lane reinterpreted as a float (raw bits, no conversion).</summary>
    public float A
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _f3;
    }

    /// <summary>Gets the red lane reinterpreted as a signed integer (raw bits, no conversion).</summary>
    public int IR
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _i0;
    }

    /// <summary>Gets the green lane reinterpreted as a signed integer (raw bits, no conversion).</summary>
    public int IG
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _i1;
    }

    /// <summary>Gets the blue lane reinterpreted as a signed integer (raw bits, no conversion).</summary>
    public int IB
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _i2;
    }

    /// <summary>Gets the alpha lane reinterpreted as a signed integer (raw bits, no conversion).</summary>
    public int IA
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _i3;
    }

    /// <summary>Gets the red lane reinterpreted as an unsigned integer (raw bits, no conversion).</summary>
    public uint UR
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _u0;
    }

    /// <summary>Gets the green lane reinterpreted as an unsigned integer (raw bits, no conversion).</summary>
    public uint UG
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _u1;
    }

    /// <summary>Gets the blue lane reinterpreted as an unsigned integer (raw bits, no conversion).</summary>
    public uint UB
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _u2;
    }

    /// <summary>Gets the alpha lane reinterpreted as an unsigned integer (raw bits, no conversion).</summary>
    public uint UA
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _u3;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClearColorValue"/> struct from four
    /// floating-point lanes, tagged <see cref="ClearColorFormat.Float32"/>.
    /// </summary>
    /// <param name="r">Red lane.</param>
    /// <param name="g">Green lane.</param>
    /// <param name="b">Blue lane.</param>
    /// <param name="a">Alpha lane.</param>
    /// <remarks>
    /// Lanes are sanitized here, once, so that no call site has to: NaN becomes
    /// <c>0</c>, positive infinity becomes <see cref="float.MaxValue"/> and negative
    /// infinity becomes <c>-<see cref="float.MaxValue"/></c>. Every finite value —
    /// signed zero and subnormals included — keeps its exact bits.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ClearColorValue(float r, float g, float b, float a)
        : this(r, g, b, a, ClearColorFormat.Float32)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClearColorValue"/> struct from a vector,
    /// tagged <see cref="ClearColorFormat.Float32"/>.
    /// </summary>
    /// <param name="value">RGBA lanes, in that order.</param>
    /// <remarks>Lanes are sanitized exactly as in the four-float constructor.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ClearColorValue(Vec4 value)
        : this(value.X, value.Y, value.Z, value.W)
    {
    }

    /// <summary>
    /// Creates a clear value from four signed integer lanes, tagged
    /// <see cref="ClearColorFormat.Int32"/>.
    /// </summary>
    /// <param name="r">Red lane.</param>
    /// <param name="g">Green lane.</param>
    /// <param name="b">Blue lane.</param>
    /// <param name="a">Alpha lane.</param>
    /// <returns>An <see cref="ClearColorFormat.Int32"/> clear value carrying the exact lane bits.</returns>
    /// <remarks>
    /// Integer lanes are never range-checked and never sanitized: only an integer
    /// attachment format can consume them, and that format decides how the bits are
    /// read. <see cref="FromUInt32"/> exists for the (still legal) negative clear value.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ClearColorValue FromInt32(int r, int g, int b, int a) => new(r, g, b, a, ClearColorFormat.Int32);

    /// <summary>
    /// Creates a clear value from four unsigned integer lanes, tagged
    /// <see cref="ClearColorFormat.Uint32"/>.
    /// </summary>
    /// <param name="r">Red lane.</param>
    /// <param name="g">Green lane.</param>
    /// <param name="b">Blue lane.</param>
    /// <param name="a">Alpha lane.</param>
    /// <returns>A <see cref="ClearColorFormat.Uint32"/> clear value carrying the exact lane bits.</returns>
    /// <remarks>Integer lanes are never range-checked and never sanitized.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ClearColorValue FromUInt32(uint r, uint g, uint b, uint a) => new(r, g, b, a, ClearColorFormat.Uint32);

    /// <summary>
    /// Gets the four payload lanes as floats, pointing at this value's own storage.
    /// </summary>
    /// <returns>A four-element span aliasing the payload; invalidated if this value moves.</returns>
    /// <remarks>
    /// Allocation-free view for <c>glClearBufferfv</c>-style calls. The lanes are raw
    /// bits and say nothing about the <see cref="Format"/> tag. <c>Unsafe.AsRef</c> is
    /// only what lifts the readonly-struct field into a span; the span itself is
    /// read-only, so the value's immutability still holds.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<float> AsFloatSpan() => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _f0), 4);

    /// <summary>
    /// Gets the four payload lanes as signed integers, pointing at this value's own storage.
    /// </summary>
    /// <returns>A four-element span aliasing the payload; invalidated if this value moves.</returns>
    /// <remarks>Allocation-free view for <c>glClearBufferiv</c>-style calls; raw bits.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<int> AsInt32Span() => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _i0), 4);

    /// <summary>
    /// Gets the four payload lanes as unsigned integers, pointing at this value's own storage.
    /// </summary>
    /// <returns>A four-element span aliasing the payload; invalidated if this value moves.</returns>
    /// <remarks>Allocation-free view for <c>glClearBufferuiv</c>-style calls; raw bits.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<uint> AsUInt32Span() => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _u0), 4);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ClearColorValue other) =>
        _format == other._format &&
        _u0 == other._u0 &&
        _u1 == other._u1 &&
        _u2 == other._u2 &&
        _u3 == other._u3;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is ClearColorValue other && Equals(other);

    /// <inheritdoc/>
    /// <remarks>
    /// The hash covers the 16 payload bytes only, deliberately leaving the
    /// <see cref="Format"/> tag out: equal values always hash equal, while values
    /// with identical bits under different tags (say <c>FromInt32(1, 2, 3, 4)</c> and
    /// <c>FromUInt32(1, 2, 3, 4)</c>) collide by design and are still told apart by
    /// <see cref="Equals(ClearColorValue)"/>. Including the tag would only spread
    /// hashes further; the bit-comparative equality is what the frame graph relies on.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(_u0, _u1, _u2, _u3);

    /// <summary>
    /// Equality operator. Two values are equal when their tags match and all 16 payload
    /// bytes match.
    /// </summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> when both the tag and the payload bits match.</returns>
    public static bool operator ==(ClearColorValue left, ClearColorValue right) => left.Equals(right);

    /// <summary>
    /// Inequality operator. Negation of <see cref="op_Equality"/>.
    /// </summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> when the tag or any payload byte differs.</returns>
    public static bool operator !=(ClearColorValue left, ClearColorValue right) => !left.Equals(right);

    /// <summary>
    /// Renders the value as <c>Format(lane0, lane1, lane2, lane3)</c> using the lane view
    /// named by <see cref="Format"/>, formatted with the invariant culture.
    /// </summary>
    /// <returns>A diagnostic string such as <c>Float32(0.1, 0.2, 0.3, 0.4)</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => _format switch
    {
        ClearColorFormat.Float32 => string.Create(CultureInfo.InvariantCulture, $"Float32({R}, {G}, {B}, {A})"),
        ClearColorFormat.Int32 => string.Create(CultureInfo.InvariantCulture, $"Int32({IR}, {IG}, {IB}, {IA})"),
        _ => string.Create(CultureInfo.InvariantCulture, $"Uint32({UR}, {UG}, {UB}, {UA})")
    };

    // The tag is private, so the three private constructors below are the only writers.
    // A struct's fields are all readonly here, hence the tag travels as a parameter
    // instead of being assigned by the public factories.

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ClearColorValue(float r, float g, float b, float a, ClearColorFormat format)
    {
        // Explicit layout: SkipInit covers the 8 aliasing views this constructor does
        // not write, because they share the 16 payload bytes it does.
        Unsafe.SkipInit(out this);
        _f0 = SanitizeLane(r);
        _f1 = SanitizeLane(g);
        _f2 = SanitizeLane(b);
        _f3 = SanitizeLane(a);
        _format = format;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ClearColorValue(int r, int g, int b, int a, ClearColorFormat format)
    {
        Unsafe.SkipInit(out this);
        _i0 = r;
        _i1 = g;
        _i2 = b;
        _i3 = a;
        _format = format;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ClearColorValue(uint r, uint g, uint b, uint a, ClearColorFormat format)
    {
        Unsafe.SkipInit(out this);
        _u0 = r;
        _u1 = g;
        _u2 = b;
        _u3 = a;
        _format = format;
    }

    /// <summary>
    /// Collapses non-finite lanes to a representable magnitude, leaving every finite
    /// lane bit-identical. It lives in the type so that no call site re-implements it.
    /// </summary>
    /// <param name="value">The candidate lane.</param>
    /// <returns>The sanitized lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static float SanitizeLane(float value) =>
        float.IsFinite(value)
            ? value
            : float.IsNaN(value)
                ? 0f
                : value > 0f ? float.MaxValue : -float.MaxValue;
}
