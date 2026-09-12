namespace Axrone.Utility.Alignment;

public interface IAlignmentPolicy<TSelf> where TSelf : struct, IAlignmentPolicy<TSelf>
{
    static abstract Alignment TargetAlignment { get; }

    static abstract nuint AlignUp(nuint address);

    static abstract nuint AlignDown(nuint address);

    static abstract bool IsAligned(nuint address);
}

public interface IAlignedBlock : IDisposable
{
    unsafe void* BasePointer { get; }

    ByteSize ByteSize { get; }

    Alignment Alignment { get; }

    Span<byte> Span { get; }

    ReadOnlySpan<byte> ReadOnlySpan { get; }

    bool IsDisposed { get; }
}

public interface IAlignedMemoryConsumer<TContext> where TContext : allows ref struct
{
    void Consume(Span<byte> memory, scoped ref TContext context);
}

public interface IAlignedSpanTransformer<TContext> where TContext : allows ref struct
{
    void Transform(Span<byte> memory, scoped ref TContext context);
}

public interface IAlignedMemoryAllocator<out TBlock> where TBlock : IAlignedBlock
{
    TBlock Allocate(ByteSize byteSize, Alignment alignment, bool zeroInitialize = false);
}
