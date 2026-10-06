namespace Axrone.Numeric;

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
    IReadOnlyRect<Rect>
{
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
}
