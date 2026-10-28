namespace Axrone.Ecs;

using System.Runtime.InteropServices;

/// <summary>Execution phase, applied in declaration order.</summary>
public enum SystemPhase : byte
{
    /// <summary>Before simulation.</summary>
    PreUpdate = 0,

    /// <summary>Simulation.</summary>
    Update = 1,

    /// <summary>After simulation.</summary>
    PostUpdate = 2,

    /// <summary>Presentation preparation.</summary>
    Render = 3,
}

/// <summary>Stable system identity.</summary>
public readonly record struct SystemId(uint Value)
{
    /// <summary>Gets the invalid sentinel.</summary>
    public static SystemId Invalid => default;

    /// <summary>Gets whether the id names a system.</summary>
    public bool IsValid => Value != 0;
}

/// <summary>
/// System registration contract: phase, priority, declared component access and whether
/// the system mutates world structure. Access declarations drive conflict batching;
/// structural systems always run alone.
/// </summary>
public readonly record struct SystemDescriptor(
    SystemId Id,
    SystemPhase Phase,
    int Priority,
    ComponentMask Reads,
    ComponentMask Writes,
    bool Structural = false);

/// <summary>Per-execution context handed to every system.</summary>
public readonly struct SystemContext
{
    /// <summary>Creates a context.</summary>
    public SystemContext(int tick, long frame, float deltaTime, int sinceTick)
    {
        Tick = tick;
        Frame = frame;
        DeltaTime = deltaTime;
        SinceTick = sinceTick;
    }

    /// <summary>Current world change tick.</summary>
    public int Tick { get; }

    /// <summary>Current world frame count.</summary>
    public long Frame { get; }

    /// <summary>Frame delta in seconds.</summary>
    public float DeltaTime { get; }

    /// <summary>Last tick at which this system ran; use with chunk change ticks.</summary>
    public int SinceTick { get; }
}

/// <summary>Immutable context for system and frame execution callbacks.</summary>
public readonly struct SystemEventContext
{
    /// <summary>Creates an event context.</summary>
    public SystemEventContext(
        SystemId id,
        SystemPhase phase,
        int priority,
        int tick,
        long frame,
        float deltaTime)
    {
        Id = id;
        Phase = phase;
        Priority = priority;
        Tick = tick;
        Frame = frame;
        DeltaTime = deltaTime;
    }

    /// <summary>System identity.</summary>
    public SystemId Id { get; }

    /// <summary>System phase.</summary>
    public SystemPhase Phase { get; }

    /// <summary>System priority.</summary>
    public int Priority { get; }

    /// <summary>World change tick.</summary>
    public int Tick { get; }

    /// <summary>World frame count.</summary>
    public long Frame { get; }

    /// <summary>Frame delta in seconds.</summary>
    public float DeltaTime { get; }
}

/// <summary>
/// Optional scheduler observer. Callbacks may run on parallel workers, so implementations
/// must be thread-safe and must not perform structural world mutation.
/// </summary>
public interface ISystemEventListener
{
    /// <summary>Called before the first phase of a run.</summary>
    void OnFrameStarting(int tick, long frame, float deltaTime);

    /// <summary>Called after every phase completed, including on failure.</summary>
    void OnFrameCompleted(int tick, long frame, float deltaTime);

    /// <summary>Called before one system executes.</summary>
    void OnSystemStarting(in SystemEventContext context);

    /// <summary>Called after one system completed successfully.</summary>
    void OnSystemCompleted(in SystemEventContext context);

    /// <summary>Called when a system throws; the exception is rethrown afterwards.</summary>
    void OnSystemFaulted(in SystemEventContext context, Exception exception);
}

/// <summary>
/// Monomorphized system policy. Registration reads <see cref="Descriptor"/> once and
/// stores a generic runner, so execution performs one virtual call per system and no
/// reflection or delegate allocation.
/// </summary>
/// <typeparam name="TSelf">Concrete policy type.</typeparam>
public interface ISystemPolicy<TSelf> where TSelf : struct, ISystemPolicy<TSelf>
{
    /// <summary>System registration contract.</summary>
    static abstract SystemDescriptor Descriptor { get; }

    /// <summary>Executes one frame.</summary>
    static abstract void Execute(World world, in SystemContext context);
}

/// <summary>
/// Chunk-parallel system policy. The scheduler forces such systems into a structural,
/// solo batch; the runner partitions matching chunks across the world worker pool and
/// replays one command buffer per worker in worker order.
/// </summary>
/// <typeparam name="TSelf">Concrete policy type.</typeparam>
/// <typeparam name="T1">Component type.</typeparam>
public interface IChunkSystemPolicy<TSelf, T1>
    where TSelf : struct, IChunkSystemPolicy<TSelf, T1>
    where T1 : unmanaged
{
    /// <summary>System registration contract.</summary>
    static abstract SystemDescriptor Descriptor { get; }

    /// <summary>Executes one row.</summary>
    static abstract void Execute(Entity entity, ref T1 c1, ref EntityCommandBuffer commands);
}

/// <summary>
/// Phase-ordered system scheduler with a deterministic dependency graph and
/// access-conflict batches. Independent systems in one batch run on the world worker
/// pool; structural systems and conflicting systems run alone.
/// </summary>
public sealed class SystemScheduler : IDisposable
{
    private readonly World _world;
    private readonly List<SystemRegistration> _registrations = new();
    private readonly Dictionary<SystemId, int> _byId = new();
    private readonly PhasePlan?[] _plans = new PhasePlan?[4];
    private readonly Lock _gate = new();
    private readonly BatchState _batchState;
    private readonly GCHandle _batchStateHandle;
    private ISystemEventListener? _listener;
    private bool _dirty = true;
    private bool _enabled = true;
    private bool _disposed;

    /// <summary>Creates a scheduler bound to a world.</summary>
    public SystemScheduler(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        _world = world;
        _batchState = new BatchState(this);
        _batchStateHandle = GCHandle.Alloc(_batchState);
    }

    /// <summary>Gets or sets whether the scheduler executes.</summary>
    public bool Enabled
    {
        get => Volatile.Read(ref _enabled);
        set => Volatile.Write(ref _enabled, value);
    }

    /// <summary>Gets the number of registered systems.</summary>
    public int SystemCount => _registrations.Count;

    /// <summary>Installs or clears the optional system/frame observer.</summary>
    public void SetListener(ISystemEventListener? listener) =>
        Volatile.Write(ref _listener, listener);

    /// <summary>Registers a monomorphized system policy.</summary>
    public SystemScheduler Add<TPolicy>(ReadOnlySpan<SystemId> dependencies = default)
        where TPolicy : struct, ISystemPolicy<TPolicy>
    {
        AddCore(TPolicy.Descriptor, dependencies, new SystemRunner<TPolicy>());
        return this;
    }

    /// <summary>Registers a chunk-parallel system policy.</summary>
    public SystemScheduler AddChunkSystem<TPolicy, T1>(ReadOnlySpan<SystemId> dependencies = default)
        where TPolicy : struct, IChunkSystemPolicy<TPolicy, T1>
        where T1 : unmanaged
    {
        SystemDescriptor descriptor = TPolicy.Descriptor with
        {
            Structural = true,
            Writes = TPolicy.Descriptor.Writes | ComponentType<T1>.Mask,
        };
        var buffers = new EntityCommandBuffer[_world.ParallelWorkerCount];
        for (int index = 0; index < buffers.Length; index++)
        {
            buffers[index] = new EntityCommandBuffer();
        }

        AddCore(descriptor, dependencies, new ChunkSystemRunner<TPolicy, T1>(buffers));
        return this;
    }

    /// <summary>Removes a system.</summary>
    public bool Remove(SystemId id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_byId.Remove(id, out int removedIndex))
        {
            return false;
        }

        _registrations.RemoveAt(removedIndex);
        _byId.Clear();
        for (int index = 0; index < _registrations.Count; index++)
        {
            _byId[_registrations[index].Descriptor.Id] = index;
        }

        _dirty = true;
        return true;
    }

    /// <summary>Returns the deterministic execution order of one phase.</summary>
    public ReadOnlySpan<SystemId> GetOrder(SystemPhase phase)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsurePlans();
        PhasePlan? plan = _plans[(int)phase];
        return plan is null ? ReadOnlySpan<SystemId>.Empty : plan.Order;
    }

    /// <summary>Advances the world tick and executes every phase.</summary>
    public void Run(float deltaTime = 0f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Enabled)
        {
            return;
        }

        EnsurePlans();
        _world.Step();
        int tick = _world.Tick;
        long frame = _world.FrameCount;
        ISystemEventListener? listener = Volatile.Read(ref _listener);
        listener?.OnFrameStarting(tick, frame, deltaTime);
        try
        {
            for (int phase = 0; phase < _plans.Length; phase++)
            {
                PhasePlan? plan = _plans[phase];
                if (plan is not null)
                {
                    RunPlan(plan, tick, frame, deltaTime);
                }
            }
        }
        finally
        {
            listener?.OnFrameCompleted(tick, frame, deltaTime);
        }
    }

    /// <summary>Executes one phase at the current world tick without stepping.</summary>
    public void RunPhase(SystemPhase phase, float deltaTime = 0f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Enabled)
        {
            return;
        }

        EnsurePlans();
        PhasePlan? plan = _plans[(int)phase];
        if (plan is not null)
        {
            RunPlan(plan, _world.Tick, _world.FrameCount, deltaTime);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _registrations.Clear();
        _byId.Clear();
        Array.Clear(_plans);
        _batchStateHandle.Free();
        GC.SuppressFinalize(this);
    }

    private void AddCore(
        in SystemDescriptor descriptor,
        ReadOnlySpan<SystemId> dependencies,
        ISystemRunner runner)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!descriptor.Id.IsValid)
        {
            EcsThrowHelper.ThrowInvalidSystemId();
        }

        if (_byId.ContainsKey(descriptor.Id))
        {
            EcsThrowHelper.ThrowDuplicateSystem(descriptor.Id);
        }

        SystemId[] copiedDependencies = dependencies.ToArray();
        var registration = new SystemRegistration(
            descriptor,
            copiedDependencies,
            _registrations.Count,
            runner);
        _byId.Add(descriptor.Id, _registrations.Count);
        _registrations.Add(registration);
        _dirty = true;
    }

    private unsafe void RunPlan(PhasePlan plan, int tick, long frame, float deltaTime)
    {
        for (int batchIndex = 0; batchIndex < plan.Batches.Length; batchIndex++)
        {
            Batch batch = plan.Batches[batchIndex];
            if (batch.Indices.Length == 1)
            {
                RunRegistration(batch.Indices[0], tick, frame, deltaTime);
                continue;
            }

            EcsWorkerPool? pool = _world.GetWorkerPool();
            if (pool is null || EcsWorkerPool.IsInWorker)
            {
                for (int index = 0; index < batch.Indices.Length; index++)
                {
                    RunRegistration(batch.Indices[index], tick, frame, deltaTime);
                }

                continue;
            }

            _batchState.Indices = batch.Indices;
            _batchState.Tick = tick;
            _batchState.Frame = frame;
            _batchState.DeltaTime = deltaTime;
            pool.RunPartitioned((nint)_batchStateHandle, &RunSystemWork, batch.Indices.Length);
        }
    }

    private void RunRegistration(int index, int tick, long frame, float deltaTime)
    {
        SystemRegistration registration = _registrations[index];
        ISystemEventListener? listener = Volatile.Read(ref _listener);
        var eventContext = new SystemEventContext(
            registration.Descriptor.Id,
            registration.Descriptor.Phase,
            registration.Descriptor.Priority,
            tick,
            frame,
            deltaTime);
        listener?.OnSystemStarting(in eventContext);

        var context = new SystemContext(tick, frame, deltaTime, registration.LastTick);
        try
        {
            registration.Runner.Run(_world, in context);
        }
        catch (Exception error)
        {
            listener?.OnSystemFaulted(in eventContext, error);
            throw;
        }

        listener?.OnSystemCompleted(in eventContext);
        registration.LastTick = tick;
    }

    private static unsafe void RunSystemWork(nint statePtr, int workerId, int start, int end)
    {
        _ = workerId;
        var state = (BatchState)GCHandle.FromIntPtr(statePtr).Target!;
        for (int index = start; index < end; index++)
        {
            state.Scheduler.RunRegistration(state.Indices[index], state.Tick, state.Frame, state.DeltaTime);
        }
    }

    private void EnsurePlans()
    {
        if (!_dirty)
        {
            return;
        }

        lock (_gate)
        {
            if (!_dirty)
            {
                return;
            }

            BuildPlans();
            _dirty = false;
        }
    }

    private void BuildPlans()
    {
        var phaseLists = new List<int>[4];
        for (int phase = 0; phase < phaseLists.Length; phase++)
        {
            phaseLists[phase] = new List<int>();
        }

        for (int index = 0; index < _registrations.Count; index++)
        {
            phaseLists[(int)_registrations[index].Descriptor.Phase].Add(index);
        }

        var levels = new int[_registrations.Count];
        var states = new byte[_registrations.Count];
        for (int index = 0; index < _registrations.Count; index++)
        {
            ComputeLevel(index, levels, states);
        }

        for (int phase = 0; phase < phaseLists.Length; phase++)
        {
            List<int> phaseRegistrations = phaseLists[phase];
            if (phaseRegistrations.Count == 0)
            {
                _plans[phase] = null;
                continue;
            }

            phaseRegistrations.Sort((left, right) => CompareRegistrations(left, right, levels));
            Batch[] batches = BuildBatches(phaseRegistrations);
            SystemId[] order = new SystemId[phaseRegistrations.Count];
            for (int index = 0; index < phaseRegistrations.Count; index++)
            {
                order[index] = _registrations[phaseRegistrations[index]].Descriptor.Id;
            }

            _plans[phase] = new PhasePlan(order, batches);
        }
    }

    private int ComputeLevel(int index, int[] levels, byte[] states)
    {
        if (states[index] == 2)
        {
            return levels[index];
        }

        if (states[index] == 1)
        {
            EcsThrowHelper.ThrowSystemCycle(_registrations[index].Descriptor.Id);
        }

        states[index] = 1;
        SystemRegistration registration = _registrations[index];
        int level = 0;
        for (int dependencyIndex = 0; dependencyIndex < registration.Dependencies.Length; dependencyIndex++)
        {
            SystemId dependencyId = registration.Dependencies[dependencyIndex];
            if (!_byId.TryGetValue(dependencyId, out int resolved))
            {
                EcsThrowHelper.ThrowMissingSystemDependency(registration.Descriptor.Id, dependencyId);
            }

            SystemRegistration dependency = _registrations[resolved];
            if (dependency.Descriptor.Phase > registration.Descriptor.Phase)
            {
                EcsThrowHelper.ThrowSystemPhaseDependency(
                    registration.Descriptor.Id,
                    dependencyId);
            }

            if (dependency.Descriptor.Phase == registration.Descriptor.Phase)
            {
                level = Math.Max(level, ComputeLevel(resolved, levels, states) + 1);
            }
        }

        levels[index] = level;
        states[index] = 2;
        return level;
    }

    private int CompareRegistrations(int left, int right, int[] levels)
    {
        int levelComparison = levels[left].CompareTo(levels[right]);
        if (levelComparison != 0)
        {
            return levelComparison;
        }

        int priorityComparison = _registrations[right].Descriptor.Priority.CompareTo(
            _registrations[left].Descriptor.Priority);
        if (priorityComparison != 0)
        {
            return priorityComparison;
        }

        return _registrations[left].Order.CompareTo(_registrations[right].Order);
    }

    private Batch[] BuildBatches(List<int> ordered)
    {
        var batches = new List<Batch>();
        var current = new List<int>();
        for (int index = 0; index < ordered.Count; index++)
        {
            int registrationIndex = ordered[index];
            SystemRegistration registration = _registrations[registrationIndex];
            if (registration.Descriptor.Structural)
            {
                FlushBatch(batches, current);
                batches.Add(new Batch([registrationIndex]));
                continue;
            }

            if (ConflictsWithCurrent(registrationIndex, current))
            {
                FlushBatch(batches, current);
            }

            current.Add(registrationIndex);
        }

        FlushBatch(batches, current);
        return batches.ToArray();
    }

    private bool ConflictsWithCurrent(int candidate, List<int> current)
    {
        SystemDescriptor candidateDescriptor = _registrations[candidate].Descriptor;
        for (int index = 0; index < current.Count; index++)
        {
            if (Conflicts(candidateDescriptor, _registrations[current[index]].Descriptor))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Conflicts(in SystemDescriptor left, in SystemDescriptor right) =>
        left.Writes.ContainsAny(right.Reads) ||
        left.Writes.ContainsAny(right.Writes) ||
        right.Writes.ContainsAny(left.Reads);

    private static void FlushBatch(List<Batch> batches, List<int> current)
    {
        if (current.Count > 0)
        {
            batches.Add(new Batch(current.ToArray()));
            current.Clear();
        }
    }

    private interface ISystemRunner
    {
        void Run(World world, in SystemContext context);
    }

    private sealed class SystemRunner<TPolicy> : ISystemRunner
        where TPolicy : struct, ISystemPolicy<TPolicy>
    {
        public void Run(World world, in SystemContext context) => TPolicy.Execute(world, in context);
    }

    private sealed class ChunkSystemRunner<TPolicy, T1> : ISystemRunner
        where TPolicy : struct, IChunkSystemPolicy<TPolicy, T1>
        where T1 : unmanaged
    {
        private readonly EntityCommandBuffer[] _buffers;

        public ChunkSystemRunner(EntityCommandBuffer[] buffers)
        {
            _buffers = buffers;
        }

        public void Run(World world, in SystemContext context)
        {
            _ = context;
            world.RunParallelCommands<ChunkJobAdapter<TPolicy, T1>, T1>(_buffers);
        }
    }

    private struct ChunkJobAdapter<TPolicy, T1> : ICommandJobPolicy<T1>
        where TPolicy : struct, IChunkSystemPolicy<TPolicy, T1>
        where T1 : unmanaged
    {
        public static void Execute(Entity entity, ref T1 c1, ref EntityCommandBuffer commands) =>
            TPolicy.Execute(entity, ref c1, ref commands);
    }

    private sealed class SystemRegistration
    {
        public readonly SystemDescriptor Descriptor;
        public readonly SystemId[] Dependencies;
        public readonly int Order;
        public readonly ISystemRunner Runner;
        public int LastTick = -1;

        public SystemRegistration(
            in SystemDescriptor descriptor,
            SystemId[] dependencies,
            int order,
            ISystemRunner runner)
        {
            Descriptor = descriptor;
            Dependencies = dependencies;
            Order = order;
            Runner = runner;
        }
    }

    private sealed class PhasePlan
    {
        public readonly SystemId[] Order;
        public readonly Batch[] Batches;

        public PhasePlan(SystemId[] order, Batch[] batches)
        {
            Order = order;
            Batches = batches;
        }
    }

    private sealed class Batch
    {
        public readonly int[] Indices;

        public Batch(int[] indices)
        {
            Indices = indices;
        }
    }

    private sealed class BatchState
    {
        public readonly SystemScheduler Scheduler;
        public int[] Indices = [];
        public int Tick;
        public long Frame;
        public float DeltaTime;

        public BatchState(SystemScheduler scheduler)
        {
            Scheduler = scheduler;
        }
    }
}
