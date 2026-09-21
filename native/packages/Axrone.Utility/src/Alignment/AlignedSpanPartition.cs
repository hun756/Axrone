#pragma warning disable CA1051 // Visible instance fields are intentional for ref struct data carriers

namespace Axrone.Utility.Alignment;

public readonly ref struct AlignedSpanPartition<T>
{
    public readonly Span<T> Prefix;
    public readonly Span<T> AlignedBody;
    public readonly Span<T> Postfix;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AlignedSpanPartition(Span<T> prefix, Span<T> alignedBody, Span<T> postfix)
    {
        Prefix = prefix;
        AlignedBody = alignedBody;
        Postfix = postfix;
    }

    public bool HasPrefix
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => !Prefix.IsEmpty;
    }

    public bool HasAlignedBody
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => !AlignedBody.IsEmpty;
    }

    public bool HasPostfix
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => !Postfix.IsEmpty;
    }

    public int TotalLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Prefix.Length + AlignedBody.Length + Postfix.Length;
    }
}

public readonly ref struct ReadOnlyAlignedSpanPartition<T>
{
    public readonly ReadOnlySpan<T> Prefix;
    public readonly ReadOnlySpan<T> AlignedBody;
    public readonly ReadOnlySpan<T> Postfix;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyAlignedSpanPartition(ReadOnlySpan<T> prefix, ReadOnlySpan<T> alignedBody, ReadOnlySpan<T> postfix)
    {
        Prefix = prefix;
        AlignedBody = alignedBody;
        Postfix = postfix;
    }

    public bool HasPrefix
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => !Prefix.IsEmpty;
    }

    public bool HasAlignedBody
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => !AlignedBody.IsEmpty;
    }

    public bool HasPostfix
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => !Postfix.IsEmpty;
    }

    public int TotalLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Prefix.Length + AlignedBody.Length + Postfix.Length;
    }
}
