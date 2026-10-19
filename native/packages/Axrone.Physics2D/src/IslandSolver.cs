namespace Axrone.Physics2D;

using System.Runtime.CompilerServices;

/// <summary>
/// Fluent sleep tuning with range validation and zero reflection overhead.
/// </summary>
internal sealed class IslandSolverConfiguration
{
    /// <summary>Linear speed below which a body counts as quiet; never negative.</summary>
    public float LinearTolerance
    {
        get => field;
        set
        {
            if (value < 0.0f)
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(value));
            }

            field = value;
        }
    } = 0.005f;

    /// <summary>Angular speed below which a body counts as quiet; never negative.</summary>
    public float AngularTolerance
    {
        get => field;
        set
        {
            if (value < 0.0f)
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(value));
            }

            field = value;
        }
    } = (2.0f / 180.0f) * MathF.PI;

    /// <summary>Quiet seconds before an island sleeps; never negative.</summary>
    public float TimeToSleep
    {
        get => field;
        set
        {
            if (value < 0.0f)
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(value));
            }

            field = value;
        }
    } = 0.5f;

    /// <summary>Initial forest capacity; strictly positive.</summary>
    public int InitialCapacity
    {
        get => field;
        set
        {
            if (value <= 0)
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(value));
            }

            field = value;
        }
    } = 128;
}

/// <summary>
/// Zero-allocation island and sleep solver: union-find over contact and joint
/// edges, policy-driven quiet evaluation, typestate sleep pipeline over native
/// cache-aligned memory. Steady-state managed allocation is zero.
/// </summary>
/// <typeparam name="TPolicy">The sleep policy.</typeparam>
internal sealed unsafe class IslandSolver<TPolicy> : IDisposable
    where TPolicy : struct, ISleepPolicy<TPolicy>
{
    private readonly NativePartitionBuffer _buffer;
    private IslandSolverMetrics _metrics;
    private bool _disposed;

    /// <summary>Creates a solver with default capacity.</summary>
    /// <param name="initialCapacity">The initial forest capacity.</param>
    public IslandSolver(int initialCapacity = 128)
    {
        _buffer = new NativePartitionBuffer(initialCapacity);
        _metrics = default;
        _disposed = false;
    }

    /// <summary>Creates a solver from configuration.</summary>
    /// <param name="config">The tuning.</param>
    public IslandSolver(IslandSolverConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _buffer = new NativePartitionBuffer(config.InitialCapacity);
        _metrics = default;
        _disposed = false;
    }

    /// <summary>Atomic solver counters.</summary>
    public IslandSolverMetrics Metrics => _metrics;

    /// <summary>
    /// Runs one end-to-end sleep pass: prepare bodies, wire contact and joint
    /// edges, evaluate roots, commit rest. Steady-state allocation is zero.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public void UpdateSleep<TBodyStore>(
        ref TBodyStore bodies,
        ReadOnlySpan<ContactPair> contacts,
        ReadOnlySpan<JointPair> joints,
        in SimulationParameters parameters)
        where TBodyStore : struct, IBodyStore
    {
        ThrowIfDisposed();

        IslandPipelineInitialized<TPolicy> initialized = BeginStep(ref bodies, in parameters);
        IslandPipelineWired<TPolicy> wired = initialized
            .WireContacts(contacts)
            .WireJoints(joints)
            .CompleteGraphWiring();
        IslandPipelineEvaluated<TPolicy> evaluated = wired.EvaluateSleepRoots(ref bodies, contacts);
        IslandPipelineCommitted<TPolicy> committed = evaluated.CommitSleep(ref bodies);

        _metrics.IncrementSleptBodies(committed.SleptBodiesCount);
        _metrics.IncrementIslands();
    }

    /// <summary>
    /// Begins a typestate pipeline: prepares dynamic bodies and advances
    /// quiet timers under the sleep policy.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public IslandPipelineInitialized<TPolicy> BeginStep<TBodyStore>(
        ref TBodyStore bodies,
        in SimulationParameters parameters)
        where TBodyStore : struct, IBodyStore
    {
        ThrowIfDisposed();

        int capacity = bodies.SlotCapacity;
        _buffer.EnsureCapacity(capacity);
        DisjointSetNode* nodes = _buffer.Pointer;

        float dt = parameters.DeltaTime;
        float linTolSq = parameters.LinearToleranceSquared;
        float angTolSq = parameters.AngularToleranceSquared;

        for (int slot = 0; slot < capacity; slot++)
        {
            SlotIndex slotIndex = (SlotIndex)slot;
            if (!bodies.IsSlotLive(slotIndex))
            {
                nodes[slot] = new DisjointSetNode
                {
                    Parent = -1,
                    Size = 0,
                    MinSleep = 0.0f,
                    Flags = IslandFlags.None,
                };
                continue;
            }

            ref BodyState body = ref bodies.GetSlot(slotIndex);
            if (body.Type != BodyType.Dynamic)
            {
                nodes[slot] = new DisjointSetNode
                {
                    Parent = -1,
                    Size = 0,
                    MinSleep = 0.0f,
                    Flags = IslandFlags.None,
                };
                continue;
            }

            nodes[slot] = new DisjointSetNode
            {
                Parent = slot,
                Size = 1,
                MinSleep = float.PositiveInfinity,
                Flags = IslandFlags.Dynamic |
                    (((body.Flags & BodyFlags.AutoSleep) != 0) ? IslandFlags.AllowSleep : IslandFlags.None),
            };

            if ((body.Flags & BodyFlags.Awake) == 0)
            {
                continue;
            }

            bool quiet = TPolicy.IsQuiet(body.LinearVelocity, body.AngularVelocity, linTolSq, angTolSq);
            body.SleepTime = TPolicy.UpdateSleepTimer(body.SleepTime, dt, quiet);
        }

        return new IslandPipelineInitialized<TPolicy>(nodes, capacity, parameters);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            ThrowHelper.ThrowObjectDisposed(nameof(IslandSolver<TPolicy>));
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
        _buffer.Dispose();
        GC.SuppressFinalize(this);
    }
}
