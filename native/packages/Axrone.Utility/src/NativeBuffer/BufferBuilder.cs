namespace Axrone.Utility.NativeBuffer;

using Axrone.Utility.Alignment;

/// <summary>Builder state before the length is chosen.</summary>
public readonly struct UnconfiguredPhase { }

/// <summary>Builder state after the length is chosen.</summary>
public readonly struct SizedPhase { }

/// <summary>
/// Compile-time typestate builder: <c>Build</c> is only reachable after the
/// length is chosen, so a zero-length buffer cannot be constructed.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct NativeBufferBuilder<T, TAllocator, TPhase>
    where T : unmanaged
    where TAllocator : struct, INativeAllocator
{
    /// <summary>Elements to commit.</summary>
    public ElementCount Length { get; }

    /// <summary>Grant alignment.</summary>
    public MemoryAlignment Alignment { get; }

    /// <summary>Whether storage starts zeroed.</summary>
    public bool ZeroInitialize { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal NativeBufferBuilder(ElementCount length, MemoryAlignment alignment, bool zeroInitialize)
    {
        Length = length;
        Alignment = alignment;
        ZeroInitialize = zeroInitialize;
    }

    /// <summary>Chooses the length; zero is rejected.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NativeBufferBuilder<T, TAllocator, SizedPhase> WithLength(ElementCount length)
    {
        if (length.Value == 0)
        {
            ThrowHelper.ThrowArgumentZero<NativeBufferBuilder<T, TAllocator, SizedPhase>>();
        }
        return new NativeBufferBuilder<T, TAllocator, SizedPhase>(length, Alignment, ZeroInitialize);
    }

    /// <summary>Overrides the alignment, keeping the current stage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NativeBufferBuilder<T, TAllocator, TPhase> WithAlignment(MemoryAlignment alignment) =>
        new(Length, alignment, ZeroInitialize);

    /// <summary>Overrides zero-initialization, keeping the current stage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NativeBufferBuilder<T, TAllocator, TPhase> WithZeroInitialization(bool zeroInitialize) =>
        new(Length, Alignment, zeroInitialize);
}

/// <summary>Entry points over the static <c>NativeBuffer</c> factory.</summary>
public static class NativeBuffer
{
    /// <summary>Starts a builder for the default allocator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NativeBufferBuilder<T, AlignedNativeAllocator, UnconfiguredPhase> Configure<T>()
        where T : unmanaged =>
        new(ElementCount.From(0), MemoryAlignment.CacheLine, true);

    /// <summary>Starts a builder for a custom allocator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NativeBufferBuilder<T, TAllocator, UnconfiguredPhase> ConfigureCustom<T, TAllocator>()
        where T : unmanaged
        where TAllocator : struct, INativeAllocator =>
        new(ElementCount.From(0), MemoryAlignment.CacheLine, true);

    /// <summary>Allocates directly with the default allocator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NativeBuffer<T, AlignedNativeAllocator> Allocate<T>(
        ElementCount length,
        MemoryAlignment? alignment = null,
        bool zeroInitialize = true)
        where T : unmanaged
    {
        return new NativeBuffer<T, AlignedNativeAllocator>(length, alignment ?? MemoryAlignment.CacheLine, zeroInitialize);
    }

    /// <summary>Allocates directly with a custom allocator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NativeBuffer<T, TAllocator> AllocateCustom<T, TAllocator>(
        ElementCount length,
        MemoryAlignment? alignment = null,
        bool zeroInitialize = true)
        where T : unmanaged
        where TAllocator : struct, INativeAllocator
    {
        return new NativeBuffer<T, TAllocator>(length, alignment ?? MemoryAlignment.CacheLine, zeroInitialize);
    }

    /// <summary>Allocates raw bytes with the default allocator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NativeBuffer<byte, AlignedNativeAllocator> AllocateBytes(
        ByteSize byteCount,
        MemoryAlignment? alignment = null,
        bool zeroInitialize = true)
    {
        return new NativeBuffer<byte, AlignedNativeAllocator>(ElementCount.From(byteCount.Value), alignment ?? MemoryAlignment.CacheLine, zeroInitialize);
    }
}

/// <summary>Build entry points enabled once the builder is sized.</summary>
public static class SizedNativeBufferBuilderExtensions
{
    /// <summary>Builds the buffer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NativeBuffer<T, TAllocator> Build<T, TAllocator>(
        this NativeBufferBuilder<T, TAllocator, SizedPhase> builder)
        where T : unmanaged
        where TAllocator : struct, INativeAllocator
    {
        return new NativeBuffer<T, TAllocator>(builder.Length, builder.Alignment, builder.ZeroInitialize);
    }

    /// <summary>Builds the buffer with a custom initialization policy.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NativeBuffer<T, TAllocator> BuildWithPolicy<T, TAllocator, TInitPolicy>(
        this NativeBufferBuilder<T, TAllocator, SizedPhase> builder)
        where T : unmanaged
        where TAllocator : struct, INativeAllocator
        where TInitPolicy : struct, IMemoryInitializationPolicy
    {
        return NativeBuffer<T, TAllocator>.AllocateCustomWithPolicy<TInitPolicy>(builder.Length, builder.Alignment);
    }
}
