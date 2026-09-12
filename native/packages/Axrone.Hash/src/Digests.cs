namespace Axrone.Hash;

[StructLayout(LayoutKind.Sequential)]
public readonly struct Digest32 : IHashDigest<Digest32>
{
    public readonly uint Value;

    public Digest32(uint value) => Value = value;

    public static int ByteCount => sizeof(uint);

    [UnscopedRef]
    public ReadOnlySpan<byte> AsSpan() => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<uint, byte>(ref Unsafe.AsRef(in Value)), ByteCount);

    public void CopyTo(Span<byte> destination) => AsSpan().CopyTo(destination);

    public bool TryCopyTo(Span<byte> destination)
    {
        if (destination.Length >= ByteCount) { AsSpan().CopyTo(destination); return true; }
        return false;
    }

    public static Digest32 FromSpan(ReadOnlySpan<byte> span)
    {
        if (span.Length < ByteCount) throw new ArgumentOutOfRangeException(nameof(span), span.Length, "Span too short for Digest32.");
        return new Digest32(MemoryMarshal.Read<uint>(span));
    }

    public bool Equals(Digest32 other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is Digest32 other && Equals(other);
    public int CompareTo(Digest32 other) => AsSpan().SequenceCompareTo(other.AsSpan());
    public override int GetHashCode() => HashCode.Combine(Value);

    public static bool operator ==(Digest32 left, Digest32 right) => left.Value == right.Value;
    public static bool operator !=(Digest32 left, Digest32 right) => left.Value != right.Value;

    public override string ToString() => ToString(null, null);
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        string.Create(ByteCount * 2, (this, format), (span, state) => HexConverter.TryFormat(state.Item1.AsSpan(), span, out _, state.format));

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        HexConverter.TryFormat(AsSpan(), destination, out charsWritten, format);

    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        HexConverter.TryFormatUtf8(AsSpan(), utf8Destination, out bytesWritten, format);

    public static bool TryParse(ReadOnlySpan<char> chars, out Digest32 result)
    {
        Span<byte> buffer = stackalloc byte[ByteCount];
        if (HexConverter.TryParse(chars, buffer, out _) && buffer.Length == ByteCount) { result = FromSpan(buffer); return true; }
        result = default; return false;
    }

    public static bool TryParse(ReadOnlySpan<byte> utf8Chars, out Digest32 result)
    {
        Span<byte> buffer = stackalloc byte[ByteCount];
        if (HexConverter.TryParseUtf8(utf8Chars, buffer, out _) && buffer.Length == ByteCount) { result = FromSpan(buffer); return true; }
        result = default; return false;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct Digest64 : IHashDigest<Digest64>
{
    public readonly ulong Value;

    public Digest64(ulong value) => Value = value;

    public static int ByteCount => sizeof(ulong);

    [UnscopedRef]
    public ReadOnlySpan<byte> AsSpan() => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<ulong, byte>(ref Unsafe.AsRef(in Value)), ByteCount);

    public void CopyTo(Span<byte> destination) => AsSpan().CopyTo(destination);

    public bool TryCopyTo(Span<byte> destination)
    {
        if (destination.Length >= ByteCount) { AsSpan().CopyTo(destination); return true; }
        return false;
    }

    public static Digest64 FromSpan(ReadOnlySpan<byte> span)
    {
        if (span.Length < ByteCount) throw new ArgumentOutOfRangeException(nameof(span), span.Length, "Span too short for Digest64.");
        return new Digest64(MemoryMarshal.Read<ulong>(span));
    }

    public bool Equals(Digest64 other) => Value == other.Value;
    public override bool Equals(object? obj) => obj is Digest64 other && Equals(other);
    public int CompareTo(Digest64 other) => AsSpan().SequenceCompareTo(other.AsSpan());
    public override int GetHashCode() => HashCode.Combine(Value);

    public static bool operator ==(Digest64 left, Digest64 right) => left.Value == right.Value;
    public static bool operator !=(Digest64 left, Digest64 right) => left.Value != right.Value;

    public override string ToString() => ToString(null, null);
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        string.Create(ByteCount * 2, (this, format), (span, state) => HexConverter.TryFormat(state.Item1.AsSpan(), span, out _, state.format));

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        HexConverter.TryFormat(AsSpan(), destination, out charsWritten, format);

    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        HexConverter.TryFormatUtf8(AsSpan(), utf8Destination, out bytesWritten, format);

    public static bool TryParse(ReadOnlySpan<char> chars, out Digest64 result)
    {
        Span<byte> buffer = stackalloc byte[ByteCount];
        if (HexConverter.TryParse(chars, buffer, out _) && buffer.Length == ByteCount) { result = FromSpan(buffer); return true; }
        result = default; return false;
    }

    public static bool TryParse(ReadOnlySpan<byte> utf8Chars, out Digest64 result)
    {
        Span<byte> buffer = stackalloc byte[ByteCount];
        if (HexConverter.TryParseUtf8(utf8Chars, buffer, out _) && buffer.Length == ByteCount) { result = FromSpan(buffer); return true; }
        result = default; return false;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct Digest128 : IHashDigest<Digest128>
{
    public readonly ulong Part1;
    public readonly ulong Part2;

    public Digest128(ulong part1, ulong part2) { Part1 = part1; Part2 = part2; }

    public static int ByteCount => sizeof(ulong) * 2;

    [UnscopedRef]
    public ReadOnlySpan<byte> AsSpan() => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<ulong, byte>(ref Unsafe.AsRef(in Part1)), ByteCount);

    public void CopyTo(Span<byte> destination)
    {
        BinaryPrimitives.WriteUInt64BigEndian(destination, Part1);
        BinaryPrimitives.WriteUInt64BigEndian(destination[8..], Part2);
    }

    public bool TryCopyTo(Span<byte> destination)
    {
        if (destination.Length >= ByteCount) { CopyTo(destination); return true; }
        return false;
    }

    public static Digest128 FromSpan(ReadOnlySpan<byte> span)
    {
        if (span.Length < ByteCount) throw new ArgumentOutOfRangeException(nameof(span), span.Length, "Span too short for Digest128.");
        return new Digest128(MemoryMarshal.Read<ulong>(span), MemoryMarshal.Read<ulong>(span[8..]));
    }

    public bool Equals(Digest128 other) => CryptographicOperations.FixedTimeEquals(AsSpan(), other.AsSpan());
    public int CompareTo(Digest128 other) => AsSpan().SequenceCompareTo(other.AsSpan());
    public override int GetHashCode() => HashCode.Combine(Part1, Part2);

    public override string ToString() => ToString(null, null);
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        string.Create(ByteCount * 2, (this, format), (span, state) => HexConverter.TryFormat(state.Item1.AsSpan(), span, out _, state.format));

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        HexConverter.TryFormat(AsSpan(), destination, out charsWritten, format);

    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        HexConverter.TryFormatUtf8(AsSpan(), utf8Destination, out bytesWritten, format);

    public static bool TryParse(ReadOnlySpan<char> chars, out Digest128 result)
    {
        Span<byte> buffer = stackalloc byte[ByteCount];
        if (HexConverter.TryParse(chars, buffer, out _) && buffer.Length == ByteCount) { result = FromSpan(buffer); return true; }
        result = default; return false;
    }

    public static bool TryParse(ReadOnlySpan<byte> utf8Chars, out Digest128 result)
    {
        Span<byte> buffer = stackalloc byte[ByteCount];
        if (HexConverter.TryParseUtf8(utf8Chars, buffer, out _) && buffer.Length == ByteCount) { result = FromSpan(buffer); return true; }
        result = default; return false;
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct Digest256 : IHashDigest<Digest256>
{
    public static int ByteCount => 32;

    [InlineArray(32)]
    private struct Storage { private byte _b; }
    private readonly Storage _data;

    public Digest256(ReadOnlySpan<byte> span)
    {
        if (span.Length < ByteCount) throw new ArgumentOutOfRangeException(nameof(span), span.Length, "Span too short for Digest256.");
        _data = default;
        span[..ByteCount].CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in _data[0]), ByteCount));
    }

    [UnscopedRef]
    public ReadOnlySpan<byte> AsSpan() => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _data[0]), ByteCount);

    public void CopyTo(Span<byte> destination) => AsSpan().CopyTo(destination);
    public bool TryCopyTo(Span<byte> destination) => AsSpan().TryCopyTo(destination);
    public static Digest256 FromSpan(ReadOnlySpan<byte> span) => new(span);

    public bool Equals(Digest256 other) => CryptographicOperations.FixedTimeEquals(AsSpan(), other.AsSpan());
    public override bool Equals(object? obj) => obj is Digest256 other && Equals(other);
    public int CompareTo(Digest256 other) => AsSpan().SequenceCompareTo(other.AsSpan());

    public override int GetHashCode()
    {
        ReadOnlySpan<byte> span = AsSpan();
        return HashCode.Combine(
            BinaryPrimitives.ReadUInt64LittleEndian(span),
            BinaryPrimitives.ReadUInt64LittleEndian(span[8..]),
            BinaryPrimitives.ReadUInt64LittleEndian(span[16..]),
            BinaryPrimitives.ReadUInt64LittleEndian(span[24..]));
    }

    public override string ToString() => ToString(null, null);
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        string.Create(ByteCount * 2, (this, format), (span, state) => HexConverter.TryFormat(state.Item1.AsSpan(), span, out _, state.format));

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        HexConverter.TryFormat(AsSpan(), destination, out charsWritten, format);

    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        HexConverter.TryFormatUtf8(AsSpan(), utf8Destination, out bytesWritten, format);

    public static bool TryParse(ReadOnlySpan<char> chars, out Digest256 result)
    {
        Span<byte> buffer = stackalloc byte[ByteCount];
        if (HexConverter.TryParse(chars, buffer, out _) && buffer.Length == ByteCount) { result = new Digest256(buffer); return true; }
        result = default; return false;
    }

    public static bool TryParse(ReadOnlySpan<byte> utf8Chars, out Digest256 result)
    {
        Span<byte> buffer = stackalloc byte[ByteCount];
        if (HexConverter.TryParseUtf8(utf8Chars, buffer, out _) && buffer.Length == ByteCount) { result = new Digest256(buffer); return true; }
        result = default; return false;
    }

    public static bool operator ==(in Digest256 left, in Digest256 right) => left.Equals(right);
    public static bool operator !=(in Digest256 left, in Digest256 right) => !left.Equals(right);
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct Digest384 : IHashDigest<Digest384>
{
    public static int ByteCount => 48;

    [InlineArray(48)]
    private struct Storage { private byte _b; }
    private readonly Storage _data;

    public Digest384(ReadOnlySpan<byte> span)
    {
        if (span.Length < ByteCount) throw new ArgumentOutOfRangeException(nameof(span), span.Length, "Span too short for Digest384.");
        _data = default;
        span[..ByteCount].CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in _data[0]), ByteCount));
    }

    [UnscopedRef]
    public ReadOnlySpan<byte> AsSpan() => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _data[0]), ByteCount);

    public void CopyTo(Span<byte> destination) => AsSpan().CopyTo(destination);
    public bool TryCopyTo(Span<byte> destination) => AsSpan().TryCopyTo(destination);
    public static Digest384 FromSpan(ReadOnlySpan<byte> span) => new(span);

    public bool Equals(Digest384 other) => CryptographicOperations.FixedTimeEquals(AsSpan(), other.AsSpan());
    public override bool Equals(object? obj) => obj is Digest384 other && Equals(other);
    public int CompareTo(Digest384 other) => AsSpan().SequenceCompareTo(other.AsSpan());

    public override int GetHashCode()
    {
        ReadOnlySpan<byte> span = AsSpan();
        return HashCode.Combine(
            BinaryPrimitives.ReadUInt64LittleEndian(span),
            BinaryPrimitives.ReadUInt64LittleEndian(span[8..]),
            BinaryPrimitives.ReadUInt64LittleEndian(span[16..]),
            BinaryPrimitives.ReadUInt64LittleEndian(span[24..]),
            BinaryPrimitives.ReadUInt64LittleEndian(span[32..]),
            BinaryPrimitives.ReadUInt64LittleEndian(span[40..]));
    }

    public override string ToString() => ToString(null, null);
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        string.Create(ByteCount * 2, (this, format), (span, state) => HexConverter.TryFormat(state.Item1.AsSpan(), span, out _, state.format));

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        HexConverter.TryFormat(AsSpan(), destination, out charsWritten, format);

    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        HexConverter.TryFormatUtf8(AsSpan(), utf8Destination, out bytesWritten, format);

    public static bool TryParse(ReadOnlySpan<char> chars, out Digest384 result)
    {
        Span<byte> buffer = stackalloc byte[ByteCount];
        if (HexConverter.TryParse(chars, buffer, out _) && buffer.Length == ByteCount) { result = new Digest384(buffer); return true; }
        result = default; return false;
    }

    public static bool TryParse(ReadOnlySpan<byte> utf8Chars, out Digest384 result)
    {
        Span<byte> buffer = stackalloc byte[ByteCount];
        if (HexConverter.TryParseUtf8(utf8Chars, buffer, out _) && buffer.Length == ByteCount) { result = new Digest384(buffer); return true; }
        result = default; return false;
    }

    public static bool operator ==(in Digest384 left, in Digest384 right) => left.Equals(right);
    public static bool operator !=(in Digest384 left, in Digest384 right) => !left.Equals(right);
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct Digest512 : IHashDigest<Digest512>
{
    public static int ByteCount => 64;

    [InlineArray(64)]
    private struct Storage { private byte _b; }
    private readonly Storage _data;

    public Digest512(ReadOnlySpan<byte> span)
    {
        if (span.Length < ByteCount) throw new ArgumentOutOfRangeException(nameof(span), span.Length, "Span too short for Digest512.");
        _data = default;
        span[..ByteCount].CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in _data[0]), ByteCount));
    }

    [UnscopedRef]
    public ReadOnlySpan<byte> AsSpan() => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _data[0]), ByteCount);

    public void CopyTo(Span<byte> destination) => AsSpan().CopyTo(destination);
    public bool TryCopyTo(Span<byte> destination) => AsSpan().TryCopyTo(destination);
    public static Digest512 FromSpan(ReadOnlySpan<byte> span) => new(span);

    public bool Equals(Digest512 other) => CryptographicOperations.FixedTimeEquals(AsSpan(), other.AsSpan());
    public override bool Equals(object? obj) => obj is Digest512 other && Equals(other);
    public int CompareTo(Digest512 other) => AsSpan().SequenceCompareTo(other.AsSpan());

    public override int GetHashCode()
    {
        ReadOnlySpan<byte> span = AsSpan();
        return HashCode.Combine(
            BinaryPrimitives.ReadUInt64LittleEndian(span) ^ BinaryPrimitives.ReadUInt64LittleEndian(span[32..]),
            BinaryPrimitives.ReadUInt64LittleEndian(span[8..]) ^ BinaryPrimitives.ReadUInt64LittleEndian(span[40..]),
            BinaryPrimitives.ReadUInt64LittleEndian(span[16..]) ^ BinaryPrimitives.ReadUInt64LittleEndian(span[48..]),
            BinaryPrimitives.ReadUInt64LittleEndian(span[24..]) ^ BinaryPrimitives.ReadUInt64LittleEndian(span[56..]));
    }

    public override string ToString() => ToString(null, null);
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        string.Create(ByteCount * 2, (this, format), (span, state) => HexConverter.TryFormat(state.Item1.AsSpan(), span, out _, state.format));

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        HexConverter.TryFormat(AsSpan(), destination, out charsWritten, format);

    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        HexConverter.TryFormatUtf8(AsSpan(), utf8Destination, out bytesWritten, format);

    public static bool TryParse(ReadOnlySpan<char> chars, out Digest512 result)
    {
        Span<byte> buffer = stackalloc byte[ByteCount];
        if (HexConverter.TryParse(chars, buffer, out _) && buffer.Length == ByteCount) { result = new Digest512(buffer); return true; }
        result = default; return false;
    }

    public static bool TryParse(ReadOnlySpan<byte> utf8Chars, out Digest512 result)
    {
        Span<byte> buffer = stackalloc byte[ByteCount];
        if (HexConverter.TryParseUtf8(utf8Chars, buffer, out _) && buffer.Length == ByteCount) { result = new Digest512(buffer); return true; }
        result = default; return false;
    }

    public static bool operator ==(in Digest512 left, in Digest512 right) => left.Equals(right);
    public static bool operator !=(in Digest512 left, in Digest512 right) => !left.Equals(right);
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct HashDigest : IEquatable<HashDigest>, IComparable<HashDigest>, ISpanFormattable, IUtf8SpanFormattable
{
    [InlineArray(64)]
    private struct Storage { private byte _b; }
    private readonly Storage _data;
    private readonly byte _length;

    public int Length => _length;
    public bool IsEmpty => _length == 0;

    public HashDigest(ReadOnlySpan<byte> span)
    {
        if (span.Length > 64) throw new ArgumentOutOfRangeException(nameof(span), span.Length, "Digest cannot exceed 64 bytes.");
        _length = (byte)span.Length;
        _data = default;
        span.CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in _data[0]), _length));
    }

    [UnscopedRef]
    public ReadOnlySpan<byte> AsSpan() => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _data[0]), _length);
    public byte[] ToArray() => AsSpan().ToArray();
    public bool TryCopyTo(Span<byte> destination) => AsSpan().TryCopyTo(destination);

    public bool Equals(HashDigest other) => _length == other._length && CryptographicOperations.FixedTimeEquals(AsSpan(), other.AsSpan());
    public override bool Equals(object? obj) => obj is HashDigest other && Equals(other);
    public int CompareTo(HashDigest other) => AsSpan().SequenceCompareTo(other.AsSpan());

    public override int GetHashCode()
    {
        ReadOnlySpan<byte> span = AsSpan();
        if (span.Length >= 8) return HashCode.Combine(BinaryPrimitives.ReadUInt64LittleEndian(span), _length);
        if (span.Length >= 4) return HashCode.Combine(BinaryPrimitives.ReadUInt32LittleEndian(span), _length);
        int hash = _length;
        for (int i = 0; i < span.Length; i++) hash = HashCode.Combine(hash, span[i]);
        return hash;
    }

    public override string ToString() => ToString(null, null);
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        string.Create(_length * 2, (this, format), (span, state) => HexConverter.TryFormat(state.Item1.AsSpan(), span, out _, state.format));

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        HexConverter.TryFormat(AsSpan(), destination, out charsWritten, format);

    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) =>
        HexConverter.TryFormatUtf8(AsSpan(), utf8Destination, out bytesWritten, format);

    public static bool TryParse(ReadOnlySpan<char> chars, out HashDigest result)
    {
        Span<byte> buffer = stackalloc byte[64];
        if (HexConverter.TryParse(chars, buffer, out int written)) { result = new HashDigest(buffer[..written]); return true; }
        result = default; return false;
    }

    public static bool TryParse(ReadOnlySpan<byte> utf8Chars, out HashDigest result)
    {
        Span<byte> buffer = stackalloc byte[64];
        if (HexConverter.TryParseUtf8(utf8Chars, buffer, out int written)) { result = new HashDigest(buffer[..written]); return true; }
        result = default; return false;
    }

    public static bool operator ==(in HashDigest left, in HashDigest right) => left.Equals(right);
    public static bool operator !=(in HashDigest left, in HashDigest right) => !left.Equals(right);
    public static implicit operator ReadOnlySpan<byte>(in HashDigest digest) => digest.AsSpan();
}
