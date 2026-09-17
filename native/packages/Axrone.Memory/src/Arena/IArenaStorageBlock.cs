using Axrone.Utility.Alignment;

namespace Axrone.Memory.Arena;

public interface IArenaStorageBlock<T> : IDisposable where T : unmanaged
{
    MemoryTopology Topology { get; }

    int ElementCount { get; }

    ByteSize ByteSize { get; }

    Alignment BlockAlignment { get; }

    Span<T> Span { get; }

    unsafe T* Pointer { get; }

    bool IsDisposed { get; }
}
