namespace Axrone.Numeric;

using System.Buffers;

public interface IReadOnlyRect<TSelf>
    where TSelf : struct, IReadOnlyRect<TSelf>
{
    float Left { get; }
    float Top { get; }
    float Right { get; }
    float Bottom { get; }
    Vec2 Location { get; }
    Vec2 Size { get; }
    Vec2 Center { get; }
    float Area { get; }
    bool IsEmpty { get; }
    bool IsAllFinite { get; }
    bool Contains(Vec2 point);
    bool Contains(TSelf other);
    bool Intersects(TSelf other);
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Rect :
    IEquatable<Rect>,
    IEqualityOperators<Rect, Rect, bool>,
    IReadOnlyRect<Rect>,
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable,
    IParsable<Rect>,
    ISpanParsable<Rect>,
    IUtf8SpanParsable<Rect>
{
    private static readonly SearchValues<char> Separators = SearchValues.Create(" ,;\t\r\n{}[]()");
    private static readonly SearchValues<byte> SeparatorsUtf8 = SearchValues.Create(" ,;\t\r\n{}[]()"u8);
    public const float MachineEpsilon = 1.1920929E-07F;

    public const float DefaultTolerance = MachineEpsilon * 8F;

    public float X;
    public float Y;
    public float Width;
    public float Height;

    public static Rect Empty => default;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Rect(float x, float y, float width, float height)
    {
        X = x; Y = y; Width = width; Height = height;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Rect(Vec2 location, Vec2 size)
    {
        X = location.X; Y = location.Y; Width = size.X; Height = size.Y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Rect(ReadOnlySpan<float> values)
    {
        if (values.Length < 4)
        {
            NumericThrowHelper.ThrowArgumentException(nameof(values), "Source span must contain at least 4 elements.");
        }
        ref float src = ref MemoryMarshal.GetReference(values);
        this = Unsafe.As<float, Rect>(ref src);
    }

    public readonly float Left => X;

    public readonly float Top => Y;

    public readonly float Right => X + Width;

    public readonly float Bottom => Y + Height;

    public Vec2 Location
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => new(X, Y);
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set { X = value.X; Y = value.Y; }
    }

    public Vec2 Size
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        readonly get => new(Width, Height);
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        set { Width = value.X; Height = value.Y; }
    }

    public readonly Vec2 Center => new(X + (Width * 0.5f), Y + (Height * 0.5f));

    public readonly float Area => Width * Height;

    public readonly bool IsEmpty => Width <= 0.0f || Height <= 0.0f;

    public readonly bool IsAllFinite =>
        float.IsFinite(X) && float.IsFinite(Y) &&
        float.IsFinite(Width) && float.IsFinite(Height);

    public readonly bool IsAnyNaN =>
        float.IsNaN(X) || float.IsNaN(Y) ||
        float.IsNaN(Width) || float.IsNaN(Height);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly ReadOnlySpan<float> AsSpan() =>
        MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in X), 4);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Span<float> AsWritableSpan() =>
        MemoryMarshal.CreateSpan(ref X, 4);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly void CopyTo(Span<float> destination)
    {
        if (destination.Length < 4)
        {
            NumericThrowHelper.ThrowArgumentException(nameof(destination), "Destination span must contain at least 4 elements.");
        }
        ref float dst = ref MemoryMarshal.GetReference(destination);
        Unsafe.As<float, Rect>(ref dst) = this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool TryCopyTo(Span<float> destination)
    {
        if (destination.Length >= 4)
        {
            ref float dst = ref MemoryMarshal.GetReference(destination);
            Unsafe.As<float, Rect>(ref dst) = this;
            return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool Contains(float x, float y) =>
        x >= X && x < (X + Width) && y >= Y && y < (Y + Height);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool Contains(Vec2 point) => Contains(point.X, point.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool Contains(Rect other) =>
        other.X >= X && other.Y >= Y &&
        other.Right <= Right && other.Bottom <= Bottom;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool Intersects(Rect other) =>
        other.X < Right && X < other.Right &&
        other.Y < Bottom && Y < other.Bottom;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool Intersects(Rect other, out Rect result)
    {
        float left = MathF.Max(X, other.X);
        float top = MathF.Max(Y, other.Y);
        float right = MathF.Min(Right, other.Right);
        float bottom = MathF.Min(Bottom, other.Bottom);

        if (right > left && bottom > top)
        {
            result = new Rect(left, top, right - left, bottom - top);
            return true;
        }

        result = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rect Union(Rect a, Rect b)
    {
        float left = MathF.Min(a.X, b.X);
        float top = MathF.Min(a.Y, b.Y);
        float right = MathF.Max(a.Right, b.Right);
        float bottom = MathF.Max(a.Bottom, b.Bottom);
        return new Rect(left, top, right - left, bottom - top);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rect Union(Rect value, Vec2 point)
    {
        float left = MathF.Min(value.X, point.X);
        float top = MathF.Min(value.Y, point.Y);
        float right = MathF.Max(value.Right, point.X);
        float bottom = MathF.Max(value.Bottom, point.Y);
        return new Rect(left, top, right - left, bottom - top);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Inflate(float horizontalAmount, float verticalAmount)
    {
        X -= horizontalAmount;
        Y -= verticalAmount;
        Width += horizontalAmount * 2.0f;
        Height += verticalAmount * 2.0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rect Inflate(Rect value, float horizontalAmount, float verticalAmount)
    {
        value.Inflate(horizontalAmount, verticalAmount);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Offset(float offsetX, float offsetY)
    {
        X += offsetX;
        Y += offsetY;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Offset(Vec2 amount) => Offset(amount.X, amount.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rect Offset(Rect value, float offsetX, float offsetY)
    {
        value.Offset(offsetX, offsetY);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rect Offset(Rect value, Vec2 amount)
    {
        value.Offset(amount);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rect Scale(Rect value, Vec2 factor) =>
        new(
            value.X * factor.X,
            value.Y * factor.Y,
            value.Width * factor.X,
            value.Height * factor.Y
        );

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Rect Scale(Rect value, float factor) => Scale(value, new Vec2(factor));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool Equals(Rect other, float tolerance = DefaultTolerance) =>
        MathF.Abs(X - other.X) <= tolerance &&
        MathF.Abs(Y - other.Y) <= tolerance &&
        MathF.Abs(Width - other.Width) <= tolerance &&
        MathF.Abs(Height - other.Height) <= tolerance;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool Equals(Rect left, Rect right, float tolerance = DefaultTolerance) =>
        left.Equals(right, tolerance);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool BitEquals(Rect other) =>
        BitConverter.SingleToUInt32Bits(X) == BitConverter.SingleToUInt32Bits(other.X) &&
        BitConverter.SingleToUInt32Bits(Y) == BitConverter.SingleToUInt32Bits(other.Y) &&
        BitConverter.SingleToUInt32Bits(Width) == BitConverter.SingleToUInt32Bits(other.Width) &&
        BitConverter.SingleToUInt32Bits(Height) == BitConverter.SingleToUInt32Bits(other.Height);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(Rect left, Rect right)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            ref float l = ref Unsafe.AsRef(in left.X);
            ref float r = ref Unsafe.AsRef(in right.X);
            return Vector128.EqualsAll(Vector128.LoadUnsafe(ref l), Vector128.LoadUnsafe(ref r));
        }

        return left.X == right.X && left.Y == right.Y &&
            left.Width == right.Width && left.Height == right.Height;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(Rect left, Rect right) => !(left == right);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly bool Equals(Rect other) => this == other;

    public override readonly bool Equals(object? obj) =>
        obj is Rect other && Equals(other);

    public override readonly int GetHashCode()
    {
        HashCode hash = default;
        ref float self = ref Unsafe.AsRef(in X);
        for (nuint i = 0; i < 4; i++)
        {
            hash.Add(Unsafe.Add(ref self, i));
        }
        return hash.ToHashCode();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly void Deconstruct(out float x, out float y, out float width, out float height)
    {
        x = X; y = Y; width = Width; height = Height;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public readonly void Deconstruct(out Vec2 location, out Vec2 size)
    {
        location = Location; size = Size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator Rect((float X, float Y, float Width, float Height) value) =>
        new(value.X, value.Y, value.Width, value.Height);

    public override readonly string ToString() => ToString(null, CultureInfo.InvariantCulture);

    public readonly string ToString(string? format) => ToString(format, CultureInfo.InvariantCulture);

    public readonly string ToString(string? format, IFormatProvider? formatProvider)
    {
        Span<char> buffer = stackalloc char[192];
        if (TryFormat(buffer, out int charsWritten, format, formatProvider))
        {
            return new string(buffer.Slice(0, charsWritten));
        }
        return "{ ... }";
    }

    public readonly bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null)
    {
        charsWritten = 0;
        Span<char> temp = stackalloc char[192];
        int written = 0;

        temp[written++] = '{';
        temp[written++] = ' ';

        NumberFormatInfo nfi = NumberFormatInfo.GetInstance(provider);
        ReadOnlySpan<char> separator = nfi.NumberDecimalSeparator == "," ? "; " : ", ";

        ref float baseRef = ref Unsafe.AsRef(in X);
        for (nuint i = 0; i < 4; i++)
        {
            if (i > 0)
            {
                if (temp.Length - written < separator.Length)
                {
                    return false;
                }
                separator.CopyTo(temp.Slice(written));
                written += separator.Length;
            }

            if (!Unsafe.Add(ref baseRef, i).TryFormat(temp.Slice(written), out int w, format, provider))
            {
                return false;
            }
            written += w;
        }

        if (temp.Length - written < 2)
        {
            return false;
        }
        temp[written++] = ' ';
        temp[written++] = '}';

        if (destination.Length < written)
        {
            return false;
        }

        temp.Slice(0, written).CopyTo(destination);
        charsWritten = written;
        return true;
    }

    public readonly bool TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = null)
    {
        bytesWritten = 0;
        Span<byte> temp = stackalloc byte[192];
        int written = 0;

        temp[written++] = (byte)'{';
        temp[written++] = (byte)' ';

        NumberFormatInfo nfi = NumberFormatInfo.GetInstance(provider);
        ReadOnlySpan<byte> separator = nfi.NumberDecimalSeparator == "," ? "; "u8 : ", "u8;

        ref float baseRef = ref Unsafe.AsRef(in X);
        for (nuint i = 0; i < 4; i++)
        {
            if (i > 0)
            {
                if (temp.Length - written < separator.Length)
                {
                    return false;
                }
                separator.CopyTo(temp.Slice(written));
                written += separator.Length;
            }

            if (!Unsafe.Add(ref baseRef, i).TryFormat(temp.Slice(written), out int w, format, provider))
            {
                return false;
            }
            written += w;
        }

        if (temp.Length - written < 2)
        {
            return false;
        }
        temp[written++] = (byte)' ';
        temp[written++] = (byte)'}';

        if (utf8Destination.Length < written)
        {
            return false;
        }

        temp.Slice(0, written).CopyTo(utf8Destination);
        bytesWritten = written;
        return true;
    }

    public static Rect Parse(string s, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan(), provider);
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Rect result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }
        return TryParse(s.AsSpan(), provider, out result);
    }

    public static Rect Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out Rect result))
        {
            NumericThrowHelper.ThrowFormatException("Invalid Rect format string.");
        }
        return result;
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Rect result)
    {
        result = default;
        ReadOnlySpan<char> remaining = s;
        Unsafe.SkipInit(out Rect m);
        ref float baseRef = ref m.X;
        int count = 0;

        while (!remaining.IsEmpty && count < 4)
        {
            int tokenStart = remaining.IndexOfAnyExcept(Separators);
            if (tokenStart < 0)
            {
                break;
            }
            remaining = remaining.Slice(tokenStart);

            int tokenEnd = remaining.IndexOfAny(Separators);
            ReadOnlySpan<char> token = tokenEnd < 0 ? remaining : remaining.Slice(0, tokenEnd);
            remaining = tokenEnd < 0 ? default : remaining.Slice(tokenEnd);

            if (!float.TryParse(token, NumberStyles.Float, provider, out Unsafe.Add(ref baseRef, (nuint)count)))
            {
                return false;
            }
            count++;
        }

        if (count == 4)
        {
            result = m;
            return true;
        }

        return false;
    }

    public static Rect Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider)
    {
        if (!TryParse(utf8Text, provider, out Rect result))
        {
            NumericThrowHelper.ThrowFormatException("Invalid Rect UTF-8 format stream.");
        }
        return result;
    }

    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Rect result)
    {
        result = default;
        ReadOnlySpan<byte> remaining = utf8Text;
        Unsafe.SkipInit(out Rect m);
        ref float baseRef = ref m.X;
        int count = 0;

        while (!remaining.IsEmpty && count < 4)
        {
            int tokenStart = remaining.IndexOfAnyExcept(SeparatorsUtf8);
            if (tokenStart < 0)
            {
                break;
            }
            remaining = remaining.Slice(tokenStart);

            int tokenEnd = remaining.IndexOfAny(SeparatorsUtf8);
            ReadOnlySpan<byte> token = tokenEnd < 0 ? remaining : remaining.Slice(0, tokenEnd);
            remaining = tokenEnd < 0 ? default : remaining.Slice(tokenEnd);

            if (!float.TryParse(token, NumberStyles.Float, provider, out Unsafe.Add(ref baseRef, (nuint)count)))
            {
                return false;
            }
            count++;
        }

        if (count == 4)
        {
            result = m;
            return true;
        }

        return false;
    }
}
