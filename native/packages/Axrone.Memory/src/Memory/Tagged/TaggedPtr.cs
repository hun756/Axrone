namespace Axrone.Memory;

/// <summary>
/// Pointer with a policy-packed tag in a single word: ABA counters and flags travel with the
/// address through one compare-exchange.
/// </summary>
/// <typeparam name="T">Pointee type.</typeparam>
/// <typeparam name="TTag">Tag value type.</typeparam>
/// <typeparam name="TPolicy">Bit layout policy.</typeparam>
public readonly struct TaggedPtr<T, TTag, TPolicy> : IEquatable<TaggedPtr<T, TTag, TPolicy>>, IComparable<TaggedPtr<T, TTag, TPolicy>>
    where T : unmanaged
    where TTag : unmanaged, IEquatable<TTag>
    where TPolicy : struct, ITagPolicy<TTag>
{
    private readonly nuint _raw;

    /// <summary>Packs a pointer and tag; throws when the pointer violates the policy alignment.</summary>
    public unsafe TaggedPtr(T* pointer, TTag tag)
    {
        nuint address = (nuint)pointer;
        if ((address & (TPolicy.AlignmentRequirement - 1)) != 0)
        {
            ThrowHelper.ThrowUnalignedPointer(address, TPolicy.AlignmentRequirement);
        }

        _raw = (address & TPolicy.PointerMask) | (TPolicy.Pack(tag) & TPolicy.TagMask);
    }

    /// <summary>Rewraps a packed word without validation (tagged values are intentionally unaligned).</summary>
    public TaggedPtr(nuint raw) => _raw = raw;

    /// <summary>Address with tag bits cleared.</summary>
    public unsafe T* Pointer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (T*)(_raw & TPolicy.PointerMask);
    }

    /// <summary>Unpacked tag value.</summary>
    public TTag Tag
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => TPolicy.Unpack(_raw);
    }

    /// <summary>Packed word for single-word compare-exchange.</summary>
    public nuint Raw
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _raw;
    }

    /// <summary>Whether the address is null.</summary>
    public unsafe bool IsNull
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Pointer == null;
    }

    /// <summary>Derives a pointer with a new tag; alignment is revalidated.</summary>
    public unsafe TaggedPtr<T, TTag, TPolicy> WithTag(TTag tag) => new(Pointer, tag);

    /// <summary>Derives a pointer to a new address; alignment is revalidated.</summary>
    public unsafe TaggedPtr<T, TTag, TPolicy> WithPointer(T* pointer) => new(pointer, Tag);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(TaggedPtr<T, TTag, TPolicy> other) => _raw == other._raw;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TaggedPtr<T, TTag, TPolicy> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => _raw.GetHashCode();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(TaggedPtr<T, TTag, TPolicy> other) => _raw.CompareTo(other._raw);

    public static bool operator ==(TaggedPtr<T, TTag, TPolicy> left, TaggedPtr<T, TTag, TPolicy> right) => left.Equals(right);

    public static bool operator !=(TaggedPtr<T, TTag, TPolicy> left, TaggedPtr<T, TTag, TPolicy> right) => !left.Equals(right);

    public static bool operator <(TaggedPtr<T, TTag, TPolicy> left, TaggedPtr<T, TTag, TPolicy> right) => left._raw < right._raw;

    public static bool operator <=(TaggedPtr<T, TTag, TPolicy> left, TaggedPtr<T, TTag, TPolicy> right) => left._raw <= right._raw;

    public static bool operator >(TaggedPtr<T, TTag, TPolicy> left, TaggedPtr<T, TTag, TPolicy> right) => left._raw > right._raw;

    public static bool operator >=(TaggedPtr<T, TTag, TPolicy> left, TaggedPtr<T, TTag, TPolicy> right) => left._raw >= right._raw;

    /// <summary>Extracts the untagged address.</summary>
    public static unsafe explicit operator void*(TaggedPtr<T, TTag, TPolicy> pointer) => pointer.Pointer;

    /// <summary>Extracts the packed word.</summary>
    public static explicit operator nuint(TaggedPtr<T, TTag, TPolicy> pointer) => pointer._raw;

    /// <inheritdoc/>
    public override unsafe string ToString() =>
        $"TaggedPtr<{typeof(T).Name}, {typeof(TTag).Name}>(Ptr=0x{(nuint)Pointer:X16}, Tag={Tag})";
}
