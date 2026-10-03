namespace Axrone.Numeric;

public interface INormalizationStrategy
{
    static abstract float ReciprocalSqrt(float lengthSquared);
}

public readonly struct StrictIeeeStrategy : INormalizationStrategy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReciprocalSqrt(float lengthSquared) => 1.0f / MathF.Sqrt(lengthSquared);
}

public readonly struct FastApproximationStrategy : INormalizationStrategy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReciprocalSqrt(float lengthSquared)
    {
        float estimate = MathF.ReciprocalSqrtEstimate(lengthSquared);
        return estimate * MathF.FusedMultiplyAdd(-0.5f * lengthSquared, estimate * estimate, 1.5f);
    }
}

public interface IVectorTransformer<TState>
    where TState : allows ref struct
{
    static abstract vec3 Transform(vec3 value, scoped ref TState state);
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct vec3 :
    IEquatable<vec3>,
    IAdditionOperators<vec3, vec3, vec3>,
    IAdditiveIdentity<vec3, vec3>,
    ISubtractionOperators<vec3, vec3, vec3>,
    IMultiplyOperators<vec3, vec3, vec3>,
    IMultiplicativeIdentity<vec3, vec3>,
    IDivisionOperators<vec3, vec3, vec3>,
    IUnaryNegationOperators<vec3, vec3>,
    IUnaryPlusOperators<vec3, vec3>,
    IBitwiseOperators<vec3, vec3, vec3>,
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IParsable<vec3>,
    ISpanParsable<vec3>,
    IUtf8SpanParsable<vec3>
{
    public const float MachineEpsilon = 1.1920929E-07F;

    public const float DefaultTolerance = MachineEpsilon * 8F;

    private const int StackTextCapacity = 256;

    private static readonly float s_allBitsSet = BitConverter.UInt32BitsToSingle(uint.MaxValue);

    public static readonly vec3 Zero = new(0F, 0F, 0F);

    public static readonly vec3 One = new(1F, 1F, 1F);

    public static readonly vec3 UnitX = new(1F, 0F, 0F);

    public static readonly vec3 UnitY = new(0F, 1F, 0F);

    public static readonly vec3 UnitZ = new(0F, 0F, 1F);

    public static readonly vec3 NegativeOne = new(-1F, -1F, -1F);

    public static readonly vec3 NegativeUnitX = new(-1F, 0F, 0F);

    public static readonly vec3 NegativeUnitY = new(0F, -1F, 0F);

    public static readonly vec3 NegativeUnitZ = new(0F, 0F, -1F);

    public static readonly vec3 NegativeZero = new(-0F, -0F, -0F);

    public static readonly vec3 PositiveInfinity = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

    public static readonly vec3 NegativeInfinity = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

    public static readonly vec3 NaN = new(float.NaN, float.NaN, float.NaN);

    public static readonly vec3 Epsilon = new(float.Epsilon, float.Epsilon, float.Epsilon);

    public static readonly vec3 Pi = new(MathF.PI, MathF.PI, MathF.PI);

    public static readonly vec3 Tau = new(MathF.Tau, MathF.Tau, MathF.Tau);

    public static readonly vec3 E = new(MathF.E, MathF.E, MathF.E);

    public static readonly vec3 AllBitsSet = new(s_allBitsSet, s_allBitsSet, s_allBitsSet);

    public float X;

    public float Y;

    public float Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public vec3(float value) => X = Y = Z = value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public vec3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public vec3(ReadOnlySpan<float> values)
    {
        if (values.Length < 3)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(values));
        }

        X = values[0];
        Y = values[1];
        Z = values[2];
    }

    // vec2 slice: vec3(vec2 xy, float z) — deferred until Axrone.Numeric.vec2 exists.

    public float this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= 3U)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
            }

            return Unsafe.Add(ref X, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if ((uint)index >= 3U)
            {
                NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
            }

            Unsafe.Add(ref X, index) = value;
        }
    }

    public static vec3 AdditiveIdentity => Zero;

    public static vec3 MultiplicativeIdentity => One;

    public readonly bool IsAllZero => X == 0F && Y == 0F && Z == 0F;

    public readonly bool IsAllFinite => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);

    public readonly bool IsAnyNaN => float.IsNaN(X) || float.IsNaN(Y) || float.IsNaN(Z);

    public readonly bool IsAnyInfinity => float.IsInfinity(X) || float.IsInfinity(Y) || float.IsInfinity(Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Deconstruct(out float x, out float y, out float z)
    {
        x = X;
        y = Y;
        z = Z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<float> AsSpan() => MemoryMarshal.CreateSpan(ref X, 3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<float> AsReadOnlySpan() => MemoryMarshal.CreateReadOnlySpan(ref X, 3);

    public readonly void CopyTo(Span<float> destination)
    {
        if (destination.Length < 3)
        {
            NumericThrowHelper.ThrowArgumentException("Destination must hold at least three components.");
        }

        destination[0] = X;
        destination[1] = Y;
        destination[2] = Z;
    }

    public readonly void CopyTo(Span<byte> destination)
    {
        if (destination.Length < 12)
        {
            NumericThrowHelper.ThrowArgumentException("Destination must hold at least twelve bytes.");
        }

        float x = X;
        float y = Y;
        float z = Z;
        MemoryMarshal.Write(destination, in x);
        MemoryMarshal.Write(destination[4..], in y);
        MemoryMarshal.Write(destination[8..], in z);
    }

    public readonly void CopyTo(Span<vec3> destination)
    {
        if (destination.IsEmpty)
        {
            NumericThrowHelper.ThrowArgumentException("Destination must hold at least one vector.");
        }

        destination[0] = this;
    }

    public readonly void CopyTo(vec3[] destination, int index)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if ((uint)index >= (uint)destination.Length)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
        }

        destination[index] = this;
    }

    public readonly bool TryCopyTo(Span<float> destination)
    {
        if (destination.Length < 3)
        {
            return false;
        }

        destination[0] = X;
        destination[1] = Y;
        destination[2] = Z;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector128<float> AsVector128() => Vector128.Create(X, Y, Z, 0F);

    public static explicit operator Vector128<float>(vec3 value) => value.AsVector128();

    public static explicit operator vec3(Vector128<float> value) =>
        new(value.GetElement(0), value.GetElement(1), value.GetElement(2));

    public static implicit operator vec3((float X, float Y, float Z) value) => new(value.X, value.Y, value.Z);

    public static implicit operator (float X, float Y, float Z)(vec3 value) => (value.X, value.Y, value.Z);

    public static explicit operator System.Numerics.Vector3(vec3 value) => Unsafe.BitCast<vec3, System.Numerics.Vector3>(value);

    public static explicit operator vec3(System.Numerics.Vector3 value) => Unsafe.BitCast<System.Numerics.Vector3, vec3>(value);

    public readonly System.Numerics.Vector3 ToSystemNumerics() => (System.Numerics.Vector3)this;

    public static vec3 FromSystemNumerics(System.Numerics.Vector3 value) => (vec3)value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Create(float x, float y, float z) => new(x, y, z);

    // vec2 slice: Create(vec2 xy, float z) — deferred until Axrone.Numeric.vec2 exists.

    // vec2 slice: Transform(vec3 value, Matrix4x4 transform) — deferred to the mat4 slice.
    // vec2 slice: TransformNormal(vec3 value, Matrix4x4 transform) — deferred to the mat4 slice.
    // vec2 slice: Transform(vec3 value, Quaternion rotation) — deferred to the quat slice.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 CreateScalar(float value) => new(value, value, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe vec3 CreateScalarUnsafe(float value)
    {
        Unsafe.SkipInit(out vec3 result);
        float* destination = (float*)Unsafe.AsPointer(ref result);
        destination[0] = value;
        destination[1] = value;
        destination[2] = value;
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Load(ReadOnlySpan<float> source)
    {
        if (source.Length < 3)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(source));
        }

        return new vec3(source[0], source[1], source[2]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe vec3 LoadAligned(ref readonly float source)
    {
        Vector128<float> wide = Vector128.LoadAligned((float*)Unsafe.AsPointer(ref Unsafe.AsRef(in source)));
        return new vec3(wide.GetElement(0), wide.GetElement(1), wide.GetElement(2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe vec3 LoadAlignedNonTemporal(ref readonly float source)
    {
        Vector128<float> wide = Vector128.LoadAlignedNonTemporal((float*)Unsafe.AsPointer(ref Unsafe.AsRef(in source)));
        return new vec3(wide.GetElement(0), wide.GetElement(1), wide.GetElement(2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe vec3 LoadUnsafe(void* source)
    {
        float* components = (float*)source;
        return new vec3(components[0], components[1], components[2]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe vec3 LoadUnsafe(void* source, int offset)
    {
        if (offset < 0)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(offset));
        }

        float* components = (float*)source + offset;
        return new vec3(components[0], components[1], components[2]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Normalize<TStrategy>(vec3 value)
        where TStrategy : struct, INormalizationStrategy
    {
        float lengthSquared = value.X * value.X + value.Y * value.Y + value.Z * value.Z;
        if (lengthSquared > 0.0f)
        {
            float invLength = TStrategy.ReciprocalSqrt(lengthSquared);
            if (float.IsFinite(invLength) && invLength > 0.0f)
            {
                return new vec3(value.X * invLength, value.Y * invLength, value.Z * invLength);
            }
            float len = MathF.Sqrt(lengthSquared);
            if (len > 0.0f)
            {
                return new vec3(value.X / len, value.Y / len, value.Z / len);
            }
        }
        return Zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 operator +(vec3 left, vec3 right) =>
        new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 operator -(vec3 left, vec3 right) =>
        new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 operator *(vec3 left, vec3 right) =>
        new(left.X * right.X, left.Y * right.Y, left.Z * right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 operator *(vec3 left, float right) =>
        new(left.X * right, left.Y * right, left.Z * right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 operator *(float left, vec3 right) =>
        new(left * right.X, left * right.Y, left * right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 operator /(vec3 left, vec3 right) =>
        new(left.X / right.X, left.Y / right.Y, left.Z / right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 operator /(vec3 left, float right) =>
        new(left.X / right, left.Y / right, left.Z / right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 operator -(vec3 value) => new(-value.X, -value.Y, -value.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 operator +(vec3 value) => value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 operator ~(vec3 value) =>
        new(BitConverter.Int32BitsToSingle(~BitConverter.SingleToInt32Bits(value.X)),
            BitConverter.Int32BitsToSingle(~BitConverter.SingleToInt32Bits(value.Y)),
            BitConverter.Int32BitsToSingle(~BitConverter.SingleToInt32Bits(value.Z)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 operator &(vec3 left, vec3 right) =>
        new(BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(left.X) & BitConverter.SingleToInt32Bits(right.X)),
            BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(left.Y) & BitConverter.SingleToInt32Bits(right.Y)),
            BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(left.Z) & BitConverter.SingleToInt32Bits(right.Z)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 operator |(vec3 left, vec3 right) =>
        new(BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(left.X) | BitConverter.SingleToInt32Bits(right.X)),
            BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(left.Y) | BitConverter.SingleToInt32Bits(right.Y)),
            BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(left.Z) | BitConverter.SingleToInt32Bits(right.Z)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 operator ^(vec3 left, vec3 right) =>
        new(BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(left.X) ^ BitConverter.SingleToInt32Bits(right.X)),
            BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(left.Y) ^ BitConverter.SingleToInt32Bits(right.Y)),
            BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(left.Z) ^ BitConverter.SingleToInt32Bits(right.Z)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(vec3 left, vec3 right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(vec3 left, vec3 right) => !left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(vec3 other) => X == other.X && Y == other.Y && Z == other.Z;

    public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is vec3 other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override string ToString() => ToString(null, CultureInfo.InvariantCulture);

    public string ToString(string? format) => ToString(format, CultureInfo.InvariantCulture);

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        Span<char> buffer = stackalloc char[StackTextCapacity];
        if (TryFormat(buffer, out int charsWritten, format, formatProvider))
        {
            return buffer[..charsWritten].ToString();
        }

        return string.Join(", ", X.ToString(format, formatProvider), Y.ToString(format, formatProvider), Z.ToString(format, formatProvider));
    }

    public readonly bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        provider ??= CultureInfo.CurrentCulture;
        int offset = 0;

        if (!TryAppendComponent(destination, ref offset, X, format, provider) ||
            !TryAppendComponent(destination, ref offset, Y, format, provider) ||
            !TryAppendComponent(destination, ref offset, Z, format, provider))
        {
            charsWritten = 0;
            return false;
        }

        charsWritten = offset;
        return true;
    }

    public readonly bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        Span<char> characters = stackalloc char[StackTextCapacity];
        if (!TryFormat(characters, out int charsWritten, format, provider))
        {
            bytesWritten = 0;
            return false;
        }

        ReadOnlySpan<char> text = characters[..charsWritten];
        int required = System.Text.Encoding.UTF8.GetByteCount(text);
        if (required > utf8Destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        System.Text.Encoding.UTF8.GetBytes(text, utf8Destination);
        bytesWritten = required;
        return true;
    }

    public static vec3 Parse(string s, IFormatProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan(), provider);
    }

    public static vec3 Parse(ReadOnlySpan<char> s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out vec3 result))
        {
            NumericThrowHelper.ThrowFormatException("Expected three comma-separated floating-point components, for example \"1, 2, 3\".");
        }

        return result;
    }

    public static vec3 Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider = null)
    {
        if (!TryParse(utf8Text, provider, out vec3 result))
        {
            NumericThrowHelper.ThrowFormatException("Expected three comma-separated floating-point components, for example \"1, 2, 3\".");
        }

        return result;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out vec3 result) =>
        TryParse(s.AsSpan(), provider, out result);

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out vec3 result)
    {
        provider ??= CultureInfo.CurrentCulture;
        s = s.Trim();

        if (s.Length >= 2 && IsOpeningBracket(s[0]) && IsClosingBracket(s[^1]))
        {
            s = s[1..^1];
        }

        Span<float> components = stackalloc float[3];
        ReadOnlySpan<char> remaining = s;
        bool sawTrailingSeparator = false;

        for (int index = 0; index < 3; index++)
        {
            int separator = remaining.IndexOf(',');
            ReadOnlySpan<char> component = (separator < 0 ? remaining : remaining[..separator]).Trim();

            if (!float.TryParse(component, NumberStyles.Float, provider, out components[index]))
            {
                result = default;
                return false;
            }

            sawTrailingSeparator = separator >= 0;
            remaining = separator < 0 ? default : remaining[(separator + 1)..];
        }

        if (sawTrailingSeparator || !remaining.Trim().IsEmpty)
        {
            result = default;
            return false;
        }

        result = new vec3(components[0], components[1], components[2]);
        return true;
    }

    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out vec3 result)
    {
        int required = System.Text.Encoding.UTF8.GetCharCount(utf8Text);
        Span<char> characters = required <= StackTextCapacity ? stackalloc char[StackTextCapacity] : new char[required];
        int charsWritten = System.Text.Encoding.UTF8.GetChars(utf8Text, characters);
        return TryParse(characters[..charsWritten], provider, out result);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsOpeningBracket(char value) => value is '(' or '[' or '{';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsClosingBracket(char value) => value is ')' or ']' or '}';

    private static bool TryAppendComponent(Span<char> destination, ref int offset, float value, ReadOnlySpan<char> format, IFormatProvider provider)
    {
        Span<char> component = stackalloc char[64];

        if (!value.TryFormat(component, out int componentLength, format, provider))
        {
            return false;
        }

        if (offset > 0)
        {
            if (destination.Length <= offset)
            {
                return false;
            }

            destination[offset] = ',';
            offset++;
        }

        if (componentLength > destination.Length - offset)
        {
            return false;
        }

        component[..componentLength].CopyTo(destination[offset..]);
        offset += componentLength;
        return true;
    }
}
