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
public struct vec3 :
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(vec3 left, vec3 right) =>
        MathF.FusedMultiplyAdd(left.X, right.X, MathF.FusedMultiplyAdd(left.Y, right.Y, left.Z * right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DotStrict(vec3 left, vec3 right) =>
        (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Cross(vec3 left, vec3 right) =>
        new(
            (left.Y * right.Z) - (left.Z * right.Y),
            (left.Z * right.X) - (left.X * right.Z),
            (left.X * right.Y) - (left.Y * right.X)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float LengthSquared() => Dot(this, this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly float Length() => MathF.Sqrt(LengthSquared());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceSquared(vec3 value1, vec3 value2)
    {
        float dx = value1.X - value2.X;
        float dy = value1.Y - value2.Y;
        float dz = value1.Z - value2.Z;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(vec3 value1, vec3 value2) =>
        MathF.Sqrt(DistanceSquared(value1, value2));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Normalize(vec3 value)
    {
        float lengthSquared = value.LengthSquared();
        if (lengthSquared > 0.0f)
        {
            float invLength = 1.0f / MathF.Sqrt(lengthSquared);
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
    public static bool TryNormalize(vec3 value, out vec3 result) =>
        TryNormalize(value, out result, 0.0f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryNormalize(vec3 value, out vec3 result, float tolerance)
    {
        float lengthSquared = value.LengthSquared();
        float tolSquared = tolerance > 0.0f ? tolerance * tolerance : 0.0f;
        if (lengthSquared > tolSquared && lengthSquared > 0.0f)
        {
            float invLength = 1.0f / MathF.Sqrt(lengthSquared);
            if (float.IsFinite(invLength) && invLength > 0.0f)
            {
                result = new vec3(value.X * invLength, value.Y * invLength, value.Z * invLength);
                return true;
            }

            float len = MathF.Sqrt(lengthSquared);
            if (len > 0.0f)
            {
                result = new vec3(value.X / len, value.Y / len, value.Z / len);
                return true;
            }
        }

        result = Zero;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Reflect(vec3 vector, vec3 normal)
    {
        float dot2 = Dot(vector, normal) * 2.0f;
        return new vec3(
            vector.X - (normal.X * dot2),
            vector.Y - (normal.Y * dot2),
            vector.Z - (normal.Z * dot2)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Project(vec3 vector, vec3 onNormal)
    {
        float sqrMag = Dot(onNormal, onNormal);
        if (sqrMag <= 0.0f)
        {
            return Zero;
        }

        float scale = Dot(vector, onNormal) / sqrMag;
        return onNormal * scale;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 ProjectOnPlane(vec3 vector, vec3 planeNormal) =>
        vector - Project(vector, planeNormal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Slide(vec3 vector, vec3 normal)
    {
        float normalSq = Dot(normal, normal);
        if (normalSq <= 0.0f)
        {
            return vector;
        }

        return vector - (normal * (Dot(vector, normal) / normalSq));
    }

    public static float Angle(vec3 from, vec3 to)
    {
        float maxA = MathF.Max(MathF.Abs(from.X), MathF.Max(MathF.Abs(from.Y), MathF.Abs(from.Z)));
        float maxB = MathF.Max(MathF.Abs(to.X), MathF.Max(MathF.Abs(to.Y), MathF.Abs(to.Z)));
        if (maxA <= 0.0f || maxB <= 0.0f)
        {
            return 0.0f;
        }

        vec3 a = from * (1.0f / maxA);
        vec3 b = to * (1.0f / maxB);

        float lenA = a.Length();
        float lenB = b.Length();
        if (lenA <= 0.0f || lenB <= 0.0f)
        {
            return 0.0f;
        }

        float cos = Dot(a, b) / (lenA * lenB);
        return MathF.Acos(Math.Clamp(cos, -1.0f, 1.0f));
    }

    public static float SignedAngle(vec3 from, vec3 to, vec3 axis)
    {
        float unsignedAngle = Angle(from, to);
        vec3 cross = Cross(from, to);
        float sign = Dot(axis, cross);
        return sign < 0.0f ? -unsignedAngle : unsignedAngle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Clamp(vec3 value, vec3 min, vec3 max)
    {
        float x = value.X;
        x = (x < min.X) ? min.X : x;
        x = (x > max.X) ? max.X : x;

        float y = value.Y;
        y = (y < min.Y) ? min.Y : y;
        y = (y > max.Y) ? max.Y : y;

        float z = value.Z;
        z = (z < min.Z) ? min.Z : z;
        z = (z > max.Z) ? max.Z : z;

        return new vec3(x, y, z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 ClampNative(vec3 value, vec3 min, vec3 max) => Clamp(value, min, max);

    public static vec3 ClampLength(vec3 value, float minLength, float maxLength)
    {
        float sqrMagnitude = value.LengthSquared();
        if (sqrMagnitude <= 0.0f)
        {
            return minLength > 0.0f ? new vec3(minLength, 0.0f, 0.0f) : Zero;
        }

        float magnitude = MathF.Sqrt(sqrMagnitude);
        if (magnitude < minLength)
        {
            return value * (minLength / magnitude);
        }

        if (magnitude > maxLength)
        {
            return value * (maxLength / magnitude);
        }

        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Min(vec3 left, vec3 right) =>
        new(MathF.Min(left.X, right.X), MathF.Min(left.Y, right.Y), MathF.Min(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Max(vec3 left, vec3 right) =>
        new(MathF.Max(left.X, right.X), MathF.Max(left.Y, right.Y), MathF.Max(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 MinNative(vec3 left, vec3 right) => Min(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 MaxNative(vec3 left, vec3 right) => Max(left, right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 MinNumber(vec3 left, vec3 right) =>
        new(float.MinNumber(left.X, right.X), float.MinNumber(left.Y, right.Y), float.MinNumber(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 MaxNumber(vec3 left, vec3 right) =>
        new(float.MaxNumber(left.X, right.X), float.MaxNumber(left.Y, right.Y), float.MaxNumber(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 MinMagnitude(vec3 left, vec3 right) =>
        new(float.MinMagnitude(left.X, right.X), float.MinMagnitude(left.Y, right.Y), float.MinMagnitude(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 MaxMagnitude(vec3 left, vec3 right) =>
        new(float.MaxMagnitude(left.X, right.X), float.MaxMagnitude(left.Y, right.Y), float.MaxMagnitude(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 MinMagnitudeNumber(vec3 left, vec3 right) =>
        new(float.MinMagnitudeNumber(left.X, right.X), float.MinMagnitudeNumber(left.Y, right.Y), float.MinMagnitudeNumber(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 MaxMagnitudeNumber(vec3 left, vec3 right) =>
        new(float.MaxMagnitudeNumber(left.X, right.X), float.MaxMagnitudeNumber(left.Y, right.Y), float.MaxMagnitudeNumber(left.Z, right.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 CopySign(vec3 value, vec3 sign) =>
        new(MathF.CopySign(value.X, sign.X), MathF.CopySign(value.Y, sign.Y), MathF.CopySign(value.Z, sign.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 FusedMultiplyAdd(vec3 left, vec3 right, vec3 addend) =>
        new(
            MathF.FusedMultiplyAdd(left.X, right.X, addend.X),
            MathF.FusedMultiplyAdd(left.Y, right.Y, addend.Y),
            MathF.FusedMultiplyAdd(left.Z, right.Z, addend.Z)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 MultiplyAddEstimate(vec3 left, vec3 right, vec3 addend) =>
        FusedMultiplyAdd(left, right, addend);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Abs(vec3 value) =>
        new(MathF.Abs(value.X), MathF.Abs(value.Y), MathF.Abs(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Sqrt(vec3 value) =>
        new(MathF.Sqrt(value.X), MathF.Sqrt(value.Y), MathF.Sqrt(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 SquareRoot(vec3 value) => Sqrt(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Sin(vec3 vector) =>
        new(MathF.Sin(vector.X), MathF.Sin(vector.Y), MathF.Sin(vector.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Cos(vec3 vector) =>
        new(MathF.Cos(vector.X), MathF.Cos(vector.Y), MathF.Cos(vector.Z));

    public static (vec3 Sin, vec3 Cos) SinCos(vec3 vector)
    {
        (float sX, float cX) = MathF.SinCos(vector.X);
        (float sY, float cY) = MathF.SinCos(vector.Y);
        (float sZ, float cZ) = MathF.SinCos(vector.Z);
        return (new vec3(sX, sY, sZ), new vec3(cX, cY, cZ));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Exp(vec3 vector) =>
        new(MathF.Exp(vector.X), MathF.Exp(vector.Y), MathF.Exp(vector.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Log(vec3 vector) =>
        new(MathF.Log(vector.X), MathF.Log(vector.Y), MathF.Log(vector.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Log2(vec3 vector) =>
        new(MathF.Log2(vector.X), MathF.Log2(vector.Y), MathF.Log2(vector.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Hypot(vec3 x, vec3 y) => Sqrt((x * x) + (y * y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 DegreesToRadians(vec3 degrees) => degrees * (MathF.PI / 180.0f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 RadiansToDegrees(vec3 radians) => radians * (180.0f / MathF.PI);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Floor(vec3 value) =>
        new(MathF.Floor(value.X), MathF.Floor(value.Y), MathF.Floor(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Ceiling(vec3 value) =>
        new(MathF.Ceiling(value.X), MathF.Ceiling(value.Y), MathF.Ceiling(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Round(vec3 value) =>
        new(MathF.Round(value.X), MathF.Round(value.Y), MathF.Round(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Truncate(vec3 value) =>
        new(MathF.Truncate(value.X), MathF.Truncate(value.Y), MathF.Truncate(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Lerp(vec3 a, vec3 b, float t) =>
        new(
            MathF.FusedMultiplyAdd(b.X - a.X, t, a.X),
            MathF.FusedMultiplyAdd(b.Y - a.Y, t, a.Y),
            MathF.FusedMultiplyAdd(b.Z - a.Z, t, a.Z)
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 LerpClamped(vec3 a, vec3 b, float t) =>
        Lerp(a, b, Math.Clamp(t, 0.0f, 1.0f));

    public static vec3 SmoothStep(vec3 from, vec3 to, float amount)
    {
        amount = Math.Clamp(amount, 0.0f, 1.0f);
        float factor = amount * amount * (3.0f - (2.0f * amount));
        return Lerp(from, to, factor);
    }

    public static vec3 Slerp(vec3 a, vec3 b, float t)
    {
        float lenA = a.Length();
        float lenB = b.Length();

        if (lenA <= 0.0f || lenB <= 0.0f)
        {
            return Lerp(a, b, t);
        }

        float targetLength = MathF.FusedMultiplyAdd(lenB - lenA, t, lenA);
        vec3 unitA = a / lenA;
        vec3 unitB = b / lenB;

        float dot = Math.Clamp(Dot(unitA, unitB), -1.0f, 1.0f);

        if (dot > 0.9995f)
        {
            vec3 result = Lerp(unitA, unitB, t);
            return Normalize(result) * targetLength;
        }

        if (dot < -0.9995f)
        {
            vec3 ortho = GetOrthogonal(unitA);
            float angle = MathF.PI * t;
            return ((unitA * MathF.Cos(angle)) + (ortho * MathF.Sin(angle))) * targetLength;
        }

        float theta = MathF.Acos(dot);
        float sinTheta = MathF.Sin(theta);
        float factorA = MathF.Sin((1.0f - t) * theta) / sinTheta;
        float factorB = MathF.Sin(t * theta) / sinTheta;

        return ((unitA * factorA) + (unitB * factorB)) * targetLength;
    }

    public static vec3 GetOrthogonal(vec3 v)
    {
        if (v.LengthSquared() <= 0.0f)
        {
            return UnitX;
        }

        vec3 abs = Abs(v);
        vec3 other = abs.X < abs.Y
            ? (abs.X < abs.Z ? UnitX : UnitZ)
            : (abs.Y < abs.Z ? UnitY : UnitZ);
        return Normalize(Cross(v, other));
    }

    public static vec3 MoveTowards(vec3 current, vec3 target, float maxDistanceDelta)
    {
        float dx = target.X - current.X;
        float dy = target.Y - current.Y;
        float dz = target.Z - current.Z;
        float distSq = (dx * dx) + (dy * dy) + (dz * dz);

        if (distSq == 0.0f || (maxDistanceDelta >= 0.0f && distSq <= maxDistanceDelta * maxDistanceDelta))
        {
            return target;
        }

        float dist = MathF.Sqrt(distSq);
        float ratio = maxDistanceDelta / dist;
        return new vec3(
            MathF.FusedMultiplyAdd(dx, ratio, current.X),
            MathF.FusedMultiplyAdd(dy, ratio, current.Y),
            MathF.FusedMultiplyAdd(dz, ratio, current.Z)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 TransformCustom<TTransformer, TState>(vec3 value, scoped ref TState state)
        where TTransformer : IVectorTransformer<TState>
        where TState : allows ref struct =>
        TTransformer.Transform(value, ref state);

    public static vec3 SumAll(params ReadOnlySpan<vec3> vectors)
    {
        vec3 accumulator = Zero;
        for (int i = 0; i < vectors.Length; i++)
        {
            accumulator += vectors[i];
        }

        return accumulator;
    }

    public static vec3 Average(params ReadOnlySpan<vec3> vectors)
    {
        if (vectors.IsEmpty)
        {
            NumericThrowHelper.ThrowArgumentException("Average requires at least one vector.");
        }

        return SumAll(vectors) / (float)vectors.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sum(vec3 vector) => vector.X + vector.Y + vector.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool All(vec3 vector) => vector.X != 0.0f && vector.Y != 0.0f && vector.Z != 0.0f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AllWhereAllBitsSet(vec3 vector) =>
        BitConverter.SingleToUInt32Bits(vector.X) == 0xFFFF_FFFF &&
        BitConverter.SingleToUInt32Bits(vector.Y) == 0xFFFF_FFFF &&
        BitConverter.SingleToUInt32Bits(vector.Z) == 0xFFFF_FFFF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Any(vec3 vector) => vector.X != 0.0f || vector.Y != 0.0f || vector.Z != 0.0f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AnyWhereAllBitsSet(vec3 vector) =>
        BitConverter.SingleToUInt32Bits(vector.X) == 0xFFFF_FFFF ||
        BitConverter.SingleToUInt32Bits(vector.Y) == 0xFFFF_FFFF ||
        BitConverter.SingleToUInt32Bits(vector.Z) == 0xFFFF_FFFF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool None(vec3 vector) => !Any(vector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NoneWhereAllBitsSet(vec3 vector) => !AnyWhereAllBitsSet(vector);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Count(vec3 vector)
    {
        int count = 0;
        if (vector.X != 0.0f)
        {
            count++;
        }

        if (vector.Y != 0.0f)
        {
            count++;
        }

        if (vector.Z != 0.0f)
        {
            count++;
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountWhereAllBitsSet(vec3 vector)
    {
        int count = 0;
        if (BitConverter.SingleToUInt32Bits(vector.X) == 0xFFFF_FFFF)
        {
            count++;
        }

        if (BitConverter.SingleToUInt32Bits(vector.Y) == 0xFFFF_FFFF)
        {
            count++;
        }

        if (BitConverter.SingleToUInt32Bits(vector.Z) == 0xFFFF_FFFF)
        {
            count++;
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsAll(vec3 left, vec3 right) => left == right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsAny(vec3 left, vec3 right) =>
        left.X == right.X || left.Y == right.Y || left.Z == right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(vec3 other, float tolerance) =>
        MathF.Abs(X - other.X) <= tolerance &&
        MathF.Abs(Y - other.Y) <= tolerance &&
        MathF.Abs(Z - other.Z) <= tolerance;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Equals(vec3 left, vec3 right, float tolerance = DefaultTolerance) =>
        left.Equals(right, tolerance);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool BitEquals(vec3 other) =>
        BitConverter.SingleToUInt32Bits(X) == BitConverter.SingleToUInt32Bits(other.X) &&
        BitConverter.SingleToUInt32Bits(Y) == BitConverter.SingleToUInt32Bits(other.Y) &&
        BitConverter.SingleToUInt32Bits(Z) == BitConverter.SingleToUInt32Bits(other.Z);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Add(vec3 left, vec3 right) => left + right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Subtract(vec3 left, vec3 right) => left - right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Multiply(vec3 left, vec3 right) => left * right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Multiply(vec3 value, float scalar) => value * scalar;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Multiply(float scalar, vec3 value) => scalar * value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Divide(vec3 left, vec3 right) => left / right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Divide(vec3 value, float scalar) => value / scalar;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Negate(vec3 value) => -value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 BitwiseAnd(vec3 left, vec3 right) => left & right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 BitwiseOr(vec3 left, vec3 right) => left | right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Xor(vec3 left, vec3 right) => left ^ right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 OnesComplement(vec3 value) => ~value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 AndNot(vec3 left, vec3 right) => left & ~right;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static vec3 Mask(bool x, bool y, bool z) => new(
        x ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFFu) : 0F,
        y ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFFu) : 0F,
        z ? BitConverter.UInt32BitsToSingle(0xFFFF_FFFFu) : 0F);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsNaN(vec3 value) => Mask(float.IsNaN(value.X), float.IsNaN(value.Y), float.IsNaN(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsFinite(vec3 value) => Mask(float.IsFinite(value.X), float.IsFinite(value.Y), float.IsFinite(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsInfinity(vec3 value) => Mask(float.IsInfinity(value.X), float.IsInfinity(value.Y), float.IsInfinity(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsNormal(vec3 value) => Mask(float.IsNormal(value.X), float.IsNormal(value.Y), float.IsNormal(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsSubnormal(vec3 value) => Mask(float.IsSubnormal(value.X), float.IsSubnormal(value.Y), float.IsSubnormal(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsZero(vec3 value) => Mask(value.X == 0F, value.Y == 0F, value.Z == 0F);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsNegative(vec3 value) => Mask(float.IsNegative(value.X), float.IsNegative(value.Y), float.IsNegative(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsPositive(vec3 value) => Mask(float.IsPositive(value.X), float.IsPositive(value.Y), float.IsPositive(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsNegativeInfinity(vec3 value) => Mask(float.IsNegativeInfinity(value.X), float.IsNegativeInfinity(value.Y), float.IsNegativeInfinity(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsPositiveInfinity(vec3 value) => Mask(float.IsPositiveInfinity(value.X), float.IsPositiveInfinity(value.Y), float.IsPositiveInfinity(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsInteger(vec3 value) => Mask(float.IsInteger(value.X), float.IsInteger(value.Y), float.IsInteger(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsEvenInteger(vec3 value) => Mask(float.IsEvenInteger(value.X), float.IsEvenInteger(value.Y), float.IsEvenInteger(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 IsOddInteger(vec3 value) => Mask(float.IsOddInteger(value.X), float.IsOddInteger(value.Y), float.IsOddInteger(value.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 GreaterThan(vec3 left, vec3 right) => Mask(left.X > right.X, left.Y > right.Y, left.Z > right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GreaterThanAll(vec3 left, vec3 right) => left.X > right.X && left.Y > right.Y && left.Z > right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GreaterThanAny(vec3 left, vec3 right) => left.X > right.X || left.Y > right.Y || left.Z > right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 GreaterThanOrEqual(vec3 left, vec3 right) => Mask(left.X >= right.X, left.Y >= right.Y, left.Z >= right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GreaterThanOrEqualAll(vec3 left, vec3 right) => left.X >= right.X && left.Y >= right.Y && left.Z >= right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GreaterThanOrEqualAny(vec3 left, vec3 right) => left.X >= right.X || left.Y >= right.Y || left.Z >= right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 LessThan(vec3 left, vec3 right) => Mask(left.X < right.X, left.Y < right.Y, left.Z < right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool LessThanAll(vec3 left, vec3 right) => left.X < right.X && left.Y < right.Y && left.Z < right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool LessThanAny(vec3 left, vec3 right) => left.X < right.X || left.Y < right.Y || left.Z < right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 LessThanOrEqual(vec3 left, vec3 right) => Mask(left.X <= right.X, left.Y <= right.Y, left.Z <= right.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool LessThanOrEqualAll(vec3 left, vec3 right) => left.X <= right.X && left.Y <= right.Y && left.Z <= right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool LessThanOrEqualAny(vec3 left, vec3 right) => left.X <= right.X || left.Y <= right.Y || left.Z <= right.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 ConditionalSelect(vec3 condition, vec3 trueValue, vec3 falseValue) =>
        (condition & trueValue) | (~condition & falseValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOf(vec3 value, vec3 target)
    {
        if (value.X == target.X)
        {
            return 0;
        }

        if (value.Y == target.Y)
        {
            return 1;
        }

        if (value.Z == target.Z)
        {
            return 2;
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LastIndexOf(vec3 value, vec3 target)
    {
        if (value.Z == target.Z)
        {
            return 2;
        }

        if (value.Y == target.Y)
        {
            return 1;
        }

        if (value.X == target.X)
        {
            return 0;
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfWhereAllBitsSet(vec3 value)
    {
        if (BitConverter.SingleToUInt32Bits(value.X) == 0xFFFF_FFFFu)
        {
            return 0;
        }

        if (BitConverter.SingleToUInt32Bits(value.Y) == 0xFFFF_FFFFu)
        {
            return 1;
        }

        if (BitConverter.SingleToUInt32Bits(value.Z) == 0xFFFF_FFFFu)
        {
            return 2;
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LastIndexOfWhereAllBitsSet(vec3 value)
    {
        if (BitConverter.SingleToUInt32Bits(value.Z) == 0xFFFF_FFFFu)
        {
            return 2;
        }

        if (BitConverter.SingleToUInt32Bits(value.Y) == 0xFFFF_FFFFu)
        {
            return 1;
        }

        if (BitConverter.SingleToUInt32Bits(value.X) == 0xFFFF_FFFFu)
        {
            return 0;
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static vec3 Shuffle(vec3 value, byte x, byte y, byte z)
    {
        float rx = x == 0 ? value.X : x == 1 ? value.Y : x == 2 ? value.Z : 0F;
        float ry = y == 0 ? value.X : y == 1 ? value.Y : y == 2 ? value.Z : 0F;
        float rz = z == 0 ? value.X : z == 1 ? value.Y : z == 2 ? value.Z : 0F;
        return new vec3(rx, ry, rz);
    }
}
