using Axrone.Utility.Alignment;

namespace Axrone.Memory.Arena;

internal unsafe interface IArenaStorageBlock<T> : IDisposable where T : unmanaged
{
    T* BasePointer { get; }
    nuint Capacity { get; }
    Span<T> AsSpan();
}
