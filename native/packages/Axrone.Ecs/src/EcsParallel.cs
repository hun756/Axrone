namespace Axrone.Ecs;

using System.Runtime.InteropServices;

/// <summary>
/// Deferred job policy that may enqueue structural commands into a per-worker buffer.
/// </summary>
/// <typeparam name="T1">Component type.</typeparam>
public interface ICommandJobPolicy<T1> where T1 : unmanaged
{
    /// <summary>Executes over one row.</summary>
    static abstract void Execute(Entity entity, ref T1 c1, ref EntityCommandBuffer commands);
}

/// <summary>
/// Chunk-partitioned world execution. The world's persistent worker pool splits matching
/// chunks into contiguous ranges; each row belongs to exactly one worker, so component
/// writes are deterministic, and each worker owns one command buffer whose playback
/// follows worker index order.
/// </summary>
public sealed partial class World
{
    private const int ParallelChunkSize = 64;

    private readonly int _requestedWorkerCount;
    private readonly Lock _parallelGate = new();
    private readonly Lock _poolGate = new();
    private ChunkDescriptor[] _parallelChunks = [];
    private ParallelChunkState? _chunkState;
    private GCHandle _chunkStateHandle;
    private bool _chunkStateAllocated;
    private EcsWorkerPool? _workerPool;

    /// <summary>Total parallel workers, including the calling thread.</summary>
    public int ParallelWorkerCount => _requestedWorkerCount;

    /// <summary>Runs a one-component job over every matching chunk on the worker pool.</summary>
    public void RunParallelChunks<TJob, T1>()
        where TJob : struct, IJobPolicy<T1>
        where T1 : unmanaged =>
        RunParallelChunks<TJob, T1>(ComponentType<T1>.Mask, ComponentMask.Empty);

    /// <summary>Runs a one-component job over matching chunks with explicit filters.</summary>
    public unsafe void RunParallelChunks<TJob, T1>(ComponentMask all, ComponentMask none)
        where TJob : struct, IJobPolicy<T1>
        where T1 : unmanaged
    {
        lock (_parallelGate)
        {
            int count = BuildChunks(all, none);
            if (count == 0)
            {
                return;
            }

            EnsureChunkState(count);
            EcsWorkerPool? pool = GetWorkerPool();
            if (pool == null)
            {
                RunChunkWork<TJob, T1>((nint)_chunkStateHandle, 0, 0, count);
                return;
            }

            if (EcsWorkerPool.IsInWorker)
            {
                EcsThrowHelper.ThrowParallelReentrancy();
            }

            pool.RunPartitioned((nint)_chunkStateHandle, &RunChunkWork<TJob, T1>, count);
        }
    }

    /// <summary>
    /// Runs a command-enqueuing job over matching chunks. Each worker writes one buffer;
    /// buffers are played back in worker-index order, then cleared.
    /// </summary>
    public void RunParallelCommands<TJob, T1>(EntityCommandBuffer[] buffers)
        where TJob : struct, ICommandJobPolicy<T1>
        where T1 : unmanaged =>
        RunParallelCommands<TJob, T1>(ComponentType<T1>.Mask, ComponentMask.Empty, buffers);

    /// <summary>
    /// Runs a command-enqueuing job over matching chunks with explicit filters.
    /// </summary>
    public unsafe void RunParallelCommands<TJob, T1>(
        ComponentMask all,
        ComponentMask none,
        EntityCommandBuffer[] buffers)
        where TJob : struct, ICommandJobPolicy<T1>
        where T1 : unmanaged
    {
        ArgumentNullException.ThrowIfNull(buffers);
        int workers = GetWorkerPool()?.WorkerCount ?? 1;
        if (buffers.Length < workers)
        {
            EcsThrowHelper.ThrowInvalidCommandBufferCount(buffers.Length, workers);
        }

        lock (_parallelGate)
        {
            int count = BuildChunks(all, none);
            if (count == 0)
            {
                return;
            }

            ParallelChunkState state = EnsureChunkState(count);
            state.Buffers = buffers;
            EcsWorkerPool? pool = GetWorkerPool();
            if (pool == null)
            {
                RunChunkCommandWork<TJob, T1>((nint)_chunkStateHandle, 0, 0, count);
                buffers[0].Playback(this);
            }
            else
            {
                if (EcsWorkerPool.IsInWorker)
                {
                    EcsThrowHelper.ThrowParallelReentrancy();
                }

                pool.RunPartitioned((nint)_chunkStateHandle, &RunChunkCommandWork<TJob, T1>, count);
                for (int index = 0; index < pool.WorkerCount; index++)
                {
                    buffers[index].Playback(this);
                }
            }

            state.Buffers = null;
        }
    }

    internal EcsWorkerPool? GetWorkerPool()
    {
        if (_requestedWorkerCount <= 1)
        {
            return null;
        }

        lock (_poolGate)
        {
            return _workerPool ??= new EcsWorkerPool(_requestedWorkerCount);
        }
    }

    private void DisposeParallelInfrastructure()
    {
        _workerPool?.Dispose();
        _workerPool = null;
        if (_chunkStateAllocated)
        {
            _chunkStateHandle.Free();
            _chunkStateAllocated = false;
        }

        _chunkState = null;
        _parallelChunks = [];
    }

    private ParallelChunkState EnsureChunkState(int count)
    {
        if (_chunkState is null)
        {
            _chunkState = new ParallelChunkState(this);
            _chunkStateHandle = GCHandle.Alloc(_chunkState);
            _chunkStateAllocated = true;
        }

        _chunkState.Chunks = _parallelChunks;
        _chunkState.Count = count;
        return _chunkState;
    }

    private int BuildChunks(ComponentMask all, ComponentMask none)
    {
        int[] matches = _queryCache.Resolve(all, none, _archetypes);
        ReadOnlySpan<Archetype> archetypes = _archetypes.AsSpan();
        int needed = 0;
        for (int index = 0; index < matches.Length; index++)
        {
            int count = archetypes[matches[index]].Count;
            needed += (count + ParallelChunkSize - 1) / ParallelChunkSize;
        }

        EnsureChunkCapacity(needed);
        int cursor = 0;
        for (int index = 0; index < matches.Length; index++)
        {
            Archetype archetype = archetypes[matches[index]];
            for (int start = 0; start < archetype.Count; start += ParallelChunkSize)
            {
                int count = Math.Min(ParallelChunkSize, archetype.Count - start);
                _parallelChunks[cursor++] = new ChunkDescriptor(matches[index], start, count);
            }
        }

        return cursor;
    }

    private void EnsureChunkCapacity(int capacity)
    {
        if (_parallelChunks.Length >= capacity)
        {
            return;
        }

        int size = Math.Max(64, _parallelChunks.Length);
        while (size < capacity)
        {
            size *= 2;
        }

        ChunkDescriptor[] grown = GC.AllocateArray<ChunkDescriptor>(size, pinned: true);
        Array.Copy(_parallelChunks, grown, _parallelChunks.Length);
        _parallelChunks = grown;
    }

    private static unsafe void RunChunkWork<TJob, T1>(nint statePtr, int workerId, int start, int end)
        where TJob : struct, IJobPolicy<T1>
        where T1 : unmanaged
    {
        _ = workerId;
        var state = (ParallelChunkState)GCHandle.FromIntPtr(statePtr).Target!;
        ReadOnlySpan<Archetype> archetypes = state.World.Archetypes.AsSpan();
        for (int index = start; index < end; index++)
        {
            ChunkDescriptor chunk = state.Chunks[index];
            Archetype archetype = archetypes[chunk.ArchetypeIndex];
            Span<T1> column = archetype.GetColumn<T1>()!.AsSpan().Slice(chunk.Start, chunk.Count);
            ReadOnlySpan<Entity> entities = archetype.Entities.Slice(chunk.Start, chunk.Count);
            for (int row = 0; row < chunk.Count; row++)
            {
                TJob.Execute(entities[row], ref column[row]);
            }
        }
    }

    private static unsafe void RunChunkCommandWork<TJob, T1>(nint statePtr, int workerId, int start, int end)
        where TJob : struct, ICommandJobPolicy<T1>
        where T1 : unmanaged
    {
        var state = (ParallelChunkState)GCHandle.FromIntPtr(statePtr).Target!;
        EntityCommandBuffer[] buffers = state.Buffers!;
        ref EntityCommandBuffer commands = ref buffers[workerId];
        ReadOnlySpan<Archetype> archetypes = state.World.Archetypes.AsSpan();
        for (int index = start; index < end; index++)
        {
            ChunkDescriptor chunk = state.Chunks[index];
            Archetype archetype = archetypes[chunk.ArchetypeIndex];
            Span<T1> column = archetype.GetColumn<T1>()!.AsSpan().Slice(chunk.Start, chunk.Count);
            ReadOnlySpan<Entity> entities = archetype.Entities.Slice(chunk.Start, chunk.Count);
            for (int row = 0; row < chunk.Count; row++)
            {
                TJob.Execute(entities[row], ref column[row], ref commands);
            }
        }
    }

    private readonly struct ChunkDescriptor
    {
        public readonly int ArchetypeIndex;
        public readonly int Start;
        public readonly int Count;

        public ChunkDescriptor(int archetypeIndex, int start, int count)
        {
            ArchetypeIndex = archetypeIndex;
            Start = start;
            Count = count;
        }
    }

    private sealed class ParallelChunkState
    {
        public readonly World World;
        public ChunkDescriptor[] Chunks = [];
        public EntityCommandBuffer[]? Buffers;
        public int Count;

        public ParallelChunkState(World world)
        {
            World = world;
        }
    }
}
