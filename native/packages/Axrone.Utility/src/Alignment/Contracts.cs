namespace Axrone.Utility.Alignment;

/// <summary>Defines a static alignment policy with compile-time alignment value and alignment operations.</summary>
public interface IAlignmentPolicy<TSelf> where TSelf : struct, IAlignmentPolicy<TSelf>
{
    static abstract Alignment TargetAlignment { get; }

    static abstract nuint AlignUp(nuint address);

    static abstract nuint AlignDown(nuint address);

    static abstract bool IsAligned(nuint address);
}

/// <summary>A block of aligned native memory with deterministic lifetime.</summary>
public interface IAlignedBlock : IDisposable
{
    unsafe void* BasePointer { get; }

    ByteSize ByteSize { get; }

    Alignment Alignment { get; }

    Span<byte> Span { get; }

    ReadOnlySpan<byte> ReadOnlySpan { get; }

    bool IsDisposed { get; }
}

/// <summary>Consumes aligned memory blocks with a caller-provided context.</summary>
public interface IAlignedMemoryConsumer<TContext> where TContext : allows ref struct
{
    void Consume(Span<byte> memory, scoped ref TContext context);
}

/// <summary>Transforms aligned memory in-place with a caller-provided context.</summary>
public interface IAlignedSpanTransformer<TContext> where TContext : allows ref struct
{
    void Transform(Span<byte> memory, scoped ref TContext context);
}

/// <summary>Allocates aligned memory blocks.</summary>
public interface IAlignedMemoryAllocator<out TBlock> where TBlock : IAlignedBlock
{
    TBlock Allocate(ByteSize byteSize, Alignment alignment, bool zeroInitialize = false);
}
