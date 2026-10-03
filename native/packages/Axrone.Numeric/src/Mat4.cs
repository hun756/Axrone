namespace Axrone.Numeric;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct RowIndex : IEquatable<RowIndex>, IComparable<RowIndex>
{
    public readonly byte Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private RowIndex(byte value) => Value = value;

    public static RowIndex R0 => new(0);

    public static RowIndex R1 => new(1);

    public static RowIndex R2 => new(2);

    public static RowIndex R3 => new(3);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static RowIndex From(int index)
    {
        if ((uint)index >= 4U)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
        }

        return new RowIndex((byte)index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator RowIndex(int index) => From(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator int(RowIndex index) => index.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator nuint(RowIndex index) => index.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(RowIndex other) => Value.CompareTo(other.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(RowIndex left, RowIndex right) => left.Value < right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(RowIndex left, RowIndex right) => left.Value > right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(RowIndex left, RowIndex right) => left.Value <= right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(RowIndex left, RowIndex right) => left.Value >= right.Value;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct ColumnIndex : IEquatable<ColumnIndex>, IComparable<ColumnIndex>
{
    public readonly byte Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private ColumnIndex(byte value) => Value = value;

    public static ColumnIndex C0 => new(0);

    public static ColumnIndex C1 => new(1);

    public static ColumnIndex C2 => new(2);

    public static ColumnIndex C3 => new(3);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ColumnIndex From(int index)
    {
        if ((uint)index >= 4U)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
        }

        return new ColumnIndex((byte)index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator ColumnIndex(int index) => From(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator int(ColumnIndex index) => index.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator nuint(ColumnIndex index) => index.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(ColumnIndex other) => Value.CompareTo(other.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(ColumnIndex left, ColumnIndex right) => left.Value < right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(ColumnIndex left, ColumnIndex right) => left.Value > right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(ColumnIndex left, ColumnIndex right) => left.Value <= right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(ColumnIndex left, ColumnIndex right) => left.Value >= right.Value;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct MatrixIndex : IEquatable<MatrixIndex>, IComparable<MatrixIndex>
{
    public readonly byte Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private MatrixIndex(byte value) => Value = value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static MatrixIndex From(int index)
    {
        if ((uint)index >= 16U)
        {
            NumericThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));
        }

        return new MatrixIndex((byte)index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static MatrixIndex From(RowIndex row, ColumnIndex column) =>
        new((byte)((row.Value << 2) | column.Value));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator MatrixIndex(int index) => From(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator int(MatrixIndex index) => index.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator nuint(MatrixIndex index) => index.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Deconstruct(out RowIndex row, out ColumnIndex column)
    {
        row = RowIndex.From(Value >> 2);
        column = ColumnIndex.From(Value & 3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(MatrixIndex other) => Value.CompareTo(other.Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <(MatrixIndex left, MatrixIndex right) => left.Value < right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >(MatrixIndex left, MatrixIndex right) => left.Value > right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator <=(MatrixIndex left, MatrixIndex right) => left.Value <= right.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator >=(MatrixIndex left, MatrixIndex right) => left.Value >= right.Value;
}

public interface IReadOnlyMatrix4x4<TSelf>
    where TSelf : struct, IReadOnlyMatrix4x4<TSelf>
{
    #pragma warning disable CA1043 // Nominal indices are the whole point: a 0..3 / 0..15 proof checked at construction.
    float this[RowIndex row, ColumnIndex column] { get; }
    float this[MatrixIndex index] { get; }
    #pragma warning restore CA1043
    Vec4 Row0 { get; }
    Vec4 Row1 { get; }
    Vec4 Row2 { get; }
    Vec4 Row3 { get; }
    Vec4 Column0 { get; }
    Vec4 Column1 { get; }
    Vec4 Column2 { get; }
    Vec4 Column3 { get; }
    float Determinant();
    TSelf Transpose();
    bool Invert(out TSelf result);
    bool IsIdentity { get; }
    bool IsAllZero { get; }
    bool IsAllFinite { get; }
}

public interface IAffineTransformable4x4<TSelf>
    where TSelf : struct, IAffineTransformable4x4<TSelf>
{
    static abstract TSelf CreateTranslation(Vec3 position);
    static abstract TSelf CreateScale(Vec3 scales);
    static abstract TSelf CreateFromQuaternion(Quat rotation);
    static abstract TSelf CreateFromAxisAngle(UnitAxis3 axis, AngleRadians angle);
}

public interface IMatrixStorage4x4<TSelf>
    where TSelf : struct, IMatrixStorage4x4<TSelf>
{
    ReadOnlySpan<float> AsSpan();
    void CopyTo(Span<float> destination);
    bool TryCopyTo(Span<float> destination);
}
