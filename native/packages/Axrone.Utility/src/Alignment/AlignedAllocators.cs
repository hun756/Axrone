namespace Axrone.Utility.Alignment;

public sealed class NativeAlignedMemoryAllocator : IAlignedMemoryAllocator<NativeAlignedBlock>
{
    public static NativeAlignedMemoryAllocator Shared { get; } = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NativeAlignedBlock Allocate(ByteSize byteSize, Alignment alignment, bool zeroInitialize = false) =>
        new(byteSize, alignment, zeroInitialize);
}

public sealed class PinnedAlignedMemoryAllocator : IAlignedMemoryAllocator<PinnedAlignedBlock>
{
    public static PinnedAlignedMemoryAllocator Shared { get; } = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PinnedAlignedBlock Allocate(ByteSize byteSize, Alignment alignment, bool zeroInitialize = false) =>
        new(byteSize, alignment, zeroInitialize);
}
