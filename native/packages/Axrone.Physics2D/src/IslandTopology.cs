namespace Axrone.Physics2D;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

/// <summary>Flags defining topological status within the disjoint set forest.</summary>
[Flags]
internal enum IslandFlags : uint
{
    /// <summary>No status.</summary>
    None = 0,

    /// <summary>Member is a dynamic body.</summary>
    Dynamic = 1 << 0,

    /// <summary>Sleep blocked by kinematics or configuration.</summary>
    BlockedFromSleep = 1 << 1,

    /// <summary>Member allows sleep.</summary>
    AllowSleep = 1 << 2,
}

/// <summary>
/// Packed 16-byte disjoint set node: union-find links, island sleep minimum
/// and status. Four nodes share one 64-byte cache line.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 16)]
internal struct DisjointSetNode
{
    /// <summary>Parent slot, self for roots, -1 when untracked.</summary>
    [FieldOffset(0)]
    public int Parent;

    /// <summary>Member count of the root.</summary>
    [FieldOffset(4)]
    public int Size;

    /// <summary>Minimum quiet time of the root.</summary>
    [FieldOffset(8)]
    public float MinSleep;

    /// <summary>Status flags of the root.</summary>
    [FieldOffset(12)]
    public IslandFlags Flags;

    /// <summary>Whether the node tracks a dynamic body.</summary>
    public readonly bool IsDynamic
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Flags & IslandFlags.Dynamic) != 0;
    }

    /// <summary>Whether the root is blocked from sleep.</summary>
    public readonly bool IsBlocked
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (Flags & IslandFlags.BlockedFromSleep) != 0;
    }

    /// <summary>Blocks the root from sleep.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MarkBlocked() => Flags |= IslandFlags.BlockedFromSleep;
}

/// <summary>Discriminated outcome of an island partition resolution.</summary>
public enum IslandEvaluationStatus : byte
{
    /// <summary>Island qualifies for sleep.</summary>
    Sleeping = 0,

    /// <summary>Island stays awake on kinetic activity.</summary>
    AwakeActive = 1,

    /// <summary>Sleep prevented by an active kinematic body.</summary>
    BlockedByKinematic = 2,

    /// <summary>Sleep disallowed by a member disabling AllowSleep.</summary>
    DisallowedSleep = 3,
}

/// <summary>Zero-allocation flat summary of one island analysis.</summary>
[StructLayout(LayoutKind.Explicit, Size = 16)]
public readonly record struct IslandEvaluationSummary
{
    /// <summary>Resolution outcome.</summary>
    [FieldOffset(0)]
    public readonly IslandEvaluationStatus Status;

    [FieldOffset(1)]
    private readonly byte _pad0;

    [FieldOffset(2)]
    private readonly ushort _pad1;

    /// <summary>Member count of the root.</summary>
    [FieldOffset(4)]
    public readonly int BodyCount;

    /// <summary>Minimum quiet time of the root.</summary>
    [FieldOffset(8)]
    public readonly float MinSleepTime;

    /// <summary>Root slot.</summary>
    [FieldOffset(12)]
    public readonly SlotIndex RootSlot;

    /// <summary>Builds a summary.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IslandEvaluationSummary(
        IslandEvaluationStatus status,
        int bodyCount,
        float minSleepTime,
        SlotIndex rootSlot)
    {
        _pad0 = 0;
        _pad1 = 0;
        Status = status;
        BodyCount = bodyCount;
        MinSleepTime = minSleepTime;
        RootSlot = rootSlot;
    }

    /// <summary>Whether the island resolved to sleep.</summary>
    public bool IsSleeping
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Status == IslandEvaluationStatus.Sleeping;
    }

    /// <summary>Exhaustive human description of the outcome; every arm is handled.</summary>
    public string DescribeOutcome() => Status switch
    {
        IslandEvaluationStatus.Sleeping => "Island qualifies for sleep and dynamic bodies put to rest.",
        IslandEvaluationStatus.AwakeActive => "Island retained in awake state due to persistent kinetic activity.",
        IslandEvaluationStatus.BlockedByKinematic => "Island sleep prevented by contact with an active kinematic body.",
        IslandEvaluationStatus.DisallowedSleep => "Island sleep disallowed by a dynamic member disabling AllowSleep.",
        _ => throw new UnreachableException($"Invalid island status discriminant: {Status}"),
    };
}

/// <summary>Cache-line isolated atomic solver counters without false sharing.</summary>
[StructLayout(LayoutKind.Explicit, Size = 128)]
public struct IslandSolverMetrics
{
    [FieldOffset(0)]
    private long _totalIslandsProcessed;

    [FieldOffset(64)]
    private long _totalBodiesPutToSleep;

    /// <summary>Completed sleep passes.</summary>
    public long TotalIslandsProcessed => Volatile.Read(ref _totalIslandsProcessed);

    /// <summary>Bodies put to rest across all passes.</summary>
    public long TotalBodiesPutToSleep => Volatile.Read(ref _totalBodiesPutToSleep);

    /// <summary>Adds completed passes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementIslands(long count = 1) => Interlocked.Add(ref _totalIslandsProcessed, count);

    /// <summary>Adds rested bodies.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementSleptBodies(long count = 1) => Interlocked.Add(ref _totalBodiesPutToSleep, count);
}

/// <summary>
/// 64-byte aligned unmanaged backing store for the disjoint set forest.
/// Zero GC overhead across frames; grows geometrically on demand.
/// </summary>
internal sealed unsafe class NativePartitionBuffer : IDisposable
{
    private DisjointSetNode* _pointer;
    private int _capacity;
    private bool _disposed;
    private readonly Lock _allocationLock = new();

    /// <summary>Allocates aligned backing memory.</summary>
    /// <param name="initialCapacity">The initial node capacity.</param>
    public NativePartitionBuffer(int initialCapacity)
    {
        int capacity = Math.Max(16, initialCapacity);
        _pointer = (DisjointSetNode*)NativeMemory.AlignedAlloc((nuint)capacity * (nuint)sizeof(DisjointSetNode), 64);
        NativeMemory.Clear(_pointer, (nuint)capacity * (nuint)sizeof(DisjointSetNode));
        _capacity = capacity;
    }

    /// <summary>Node capacity.</summary>
    public int Capacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _capacity;
    }

    /// <summary>Live node pointer; disposed buffers throw.</summary>
    public DisjointSetNode* Pointer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_disposed)
            {
                ThrowHelper.ThrowObjectDisposed(nameof(NativePartitionBuffer));
            }

            return _pointer;
        }
    }

    /// <summary>Grows the buffer when the required capacity exceeds it.</summary>
    /// <param name="requiredCapacity">The required node capacity.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int requiredCapacity)
    {
        if (requiredCapacity <= _capacity)
        {
            return;
        }

        Grow(requiredCapacity);
    }

    /// <summary>Finds a root with path compression.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public static int FindRoot(DisjointSetNode* nodes, int slot)
    {
        int root = slot;
        while (nodes[root].Parent != root)
        {
            root = nodes[root].Parent;
        }

        int current = slot;
        while (current != root)
        {
            int next = nodes[current].Parent;
            nodes[current].Parent = root;
            current = next;
        }

        return root;
    }

    /// <summary>Unions two sets by size.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public static void UnionRoots(DisjointSetNode* nodes, int a, int b)
    {
        int rootA = FindRoot(nodes, a);
        int rootB = FindRoot(nodes, b);
        if (rootA == rootB)
        {
            return;
        }

        if (nodes[rootA].Size < nodes[rootB].Size)
        {
            (rootA, rootB) = (rootB, rootA);
        }

        nodes[rootB].Parent = rootA;
        nodes[rootA].Size += nodes[rootB].Size;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int requiredCapacity)
    {
        lock (_allocationLock)
        {
            if (_disposed)
            {
                ThrowHelper.ThrowObjectDisposed(nameof(NativePartitionBuffer));
            }

            if (requiredCapacity <= _capacity)
            {
                return;
            }

            int newCapacity = Math.Max(requiredCapacity, _capacity * 2);
            nuint byteCount = (nuint)newCapacity * (nuint)sizeof(DisjointSetNode);
            DisjointSetNode* next = (DisjointSetNode*)NativeMemory.AlignedAlloc(byteCount, 64);
            NativeMemory.Clear(next, byteCount);

            if (_pointer != null)
            {
                nuint oldByteCount = (nuint)_capacity * (nuint)sizeof(DisjointSetNode);
                Buffer.MemoryCopy(_pointer, next, byteCount, oldByteCount);
                NativeMemory.AlignedFree(_pointer);
            }

            _pointer = next;
            _capacity = newCapacity;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_allocationLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_pointer != null)
            {
                NativeMemory.AlignedFree(_pointer);
                _pointer = null;
            }

            _capacity = 0;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Finalizer releasing native memory on undisposed drops.</summary>
    ~NativePartitionBuffer()
    {
        if (_pointer != null)
        {
            NativeMemory.AlignedFree(_pointer);
            _pointer = null;
        }
    }
}
