namespace Axrone.Physics2D;

using System.Runtime.CompilerServices;

/// <summary>
/// Zero-copy adapter exposing the class body store through the island role
/// contract. Stack-only; the store outlives every pass.
/// </summary>
internal readonly struct BodyStoreAdapter : IBodyStore
{
    private readonly BodyStore _bodies;

    /// <summary>Wraps a body store.</summary>
    /// <param name="bodies">The store.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BodyStoreAdapter(BodyStore bodies)
    {
        _bodies = bodies;
    }

    /// <inheritdoc/>
    public int SlotCapacity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _bodies.SlotCapacity;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsSlotLive(SlotIndex slot) => _bodies.IsSlotLive(slot.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref BodyState GetSlot(SlotIndex slot) => ref _bodies.Slot(new BodyId((uint)slot.Value + 1u));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetAwake(BodyId id, bool awake) => _bodies.SetAwake(id, awake);
}

/// <summary>Phase 1: bodies prepared and quiet timers updated.</summary>
/// <typeparam name="TPolicy">The sleep policy.</typeparam>
internal readonly unsafe ref struct IslandPipelineInitialized<TPolicy>
    where TPolicy : struct, ISleepPolicy<TPolicy>
{
    private readonly DisjointSetNode* _nodes;
    private readonly int _capacity;
    private readonly SimulationParameters _parameters;

    /// <summary>Binds forest memory and parameters.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IslandPipelineInitialized(DisjointSetNode* nodes, int capacity, SimulationParameters parameters)
    {
        _nodes = nodes;
        _capacity = capacity;
        _parameters = parameters;
    }

    /// <summary>Unions contact-linked bodies sharing live islands.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IslandPipelineInitialized<TPolicy> WireContacts(ReadOnlySpan<ContactPair> contacts)
    {
        for (int i = 0; i < contacts.Length; i++)
        {
            ref readonly ContactPair contact = ref contacts[i];
            int slotA = contact.BodyA.ToSlotIndex();
            int slotB = contact.BodyB.ToSlotIndex();

            if ((uint)slotA < (uint)_capacity && (uint)slotB < (uint)_capacity &&
                _nodes[slotA].Parent >= 0 && _nodes[slotB].Parent >= 0)
            {
                NativePartitionBuffer.UnionRoots(_nodes, slotA, slotB);
            }
        }

        return this;
    }

    /// <summary>Unions joint-linked bodies sharing live islands.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public IslandPipelineInitialized<TPolicy> WireJoints(ReadOnlySpan<JointPair> joints)
    {
        for (int i = 0; i < joints.Length; i++)
        {
            ref readonly JointPair joint = ref joints[i];
            int slotA = joint.BodyA.ToSlotIndex();
            int slotB = joint.BodyB.ToSlotIndex();

            if ((uint)slotA < (uint)_capacity && (uint)slotB < (uint)_capacity &&
                _nodes[slotA].Parent >= 0 && _nodes[slotB].Parent >= 0)
            {
                NativePartitionBuffer.UnionRoots(_nodes, slotA, slotB);
            }
        }

        return this;
    }

    /// <summary>Seals graph wiring and advances to evaluation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IslandPipelineWired<TPolicy> CompleteGraphWiring() =>
        new(_nodes, _capacity, _parameters);
}

/// <summary>Phase 2: graph wired; ready for root property aggregation.</summary>
/// <typeparam name="TPolicy">The sleep policy.</typeparam>
internal readonly unsafe ref struct IslandPipelineWired<TPolicy>
    where TPolicy : struct, ISleepPolicy<TPolicy>
{
    private readonly DisjointSetNode* _nodes;
    private readonly int _capacity;
    private readonly SimulationParameters _parameters;

    /// <summary>Binds forest memory and parameters.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IslandPipelineWired(DisjointSetNode* nodes, int capacity, SimulationParameters parameters)
    {
        _nodes = nodes;
        _capacity = capacity;
        _parameters = parameters;
    }

    /// <summary>Aggregates sleep timers and kinematic blocks per root.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public IslandPipelineEvaluated<TPolicy> EvaluateSleepRoots<TBodyStore>(
        ref TBodyStore bodies,
        ReadOnlySpan<ContactPair> contacts)
        where TBodyStore : struct, IBodyStore
    {
        for (int slot = 0; slot < _capacity; slot++)
        {
            if (_nodes[slot].Parent < 0)
            {
                continue;
            }

            int root = NativePartitionBuffer.FindRoot(_nodes, slot);
            _nodes[root].MinSleep = float.PositiveInfinity;
            _nodes[root].Flags &= ~IslandFlags.BlockedFromSleep;
        }

        for (int slot = 0; slot < _capacity; slot++)
        {
            if (_nodes[slot].Parent < 0)
            {
                continue;
            }

            SlotIndex slotIndex = (SlotIndex)slot;
            ref readonly BodyState body = ref bodies.GetSlot(slotIndex);
            int root = NativePartitionBuffer.FindRoot(_nodes, slot);

            if ((body.Flags & BodyFlags.AllowSleep) == 0)
            {
                _nodes[root].MarkBlocked();
            }

            if (body.SleepTime < _nodes[root].MinSleep)
            {
                _nodes[root].MinSleep = body.SleepTime;
            }
        }

        float linTolSq = _parameters.LinearToleranceSquared;
        float angTolSq = _parameters.AngularToleranceSquared;

        for (int i = 0; i < contacts.Length; i++)
        {
            ref readonly ContactPair contact = ref contacts[i];
            int slotA = contact.BodyA.ToSlotIndex();
            int slotB = contact.BodyB.ToSlotIndex();

            BlockKinematicSide(ref bodies, slotA, slotB, linTolSq, angTolSq);
            BlockKinematicSide(ref bodies, slotB, slotA, linTolSq, angTolSq);
        }

        return new IslandPipelineEvaluated<TPolicy>(_nodes, _capacity, _parameters);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BlockKinematicSide<TBodyStore>(
        ref TBodyStore bodies,
        int kinematicSlot,
        int otherSlot,
        float linTolSq,
        float angTolSq)
        where TBodyStore : struct, IBodyStore
    {
        if ((uint)kinematicSlot >= (uint)_capacity || _nodes[kinematicSlot].Parent >= 0)
        {
            return;
        }

        SlotIndex index = (SlotIndex)kinematicSlot;
        if (!bodies.IsSlotLive(index))
        {
            return;
        }

        ref readonly BodyState body = ref bodies.GetSlot(index);
        if (body.Type != BodyType.Kinematic)
        {
            return;
        }

        bool moving = TPolicy.IsKinematicMoving(body.LinearVelocity, body.AngularVelocity, linTolSq, angTolSq);
        if (moving && (uint)otherSlot < (uint)_capacity && _nodes[otherSlot].Parent >= 0)
        {
            int root = NativePartitionBuffer.FindRoot(_nodes, otherSlot);
            _nodes[root].MarkBlocked();
        }
    }
}

/// <summary>Phase 3: sleep eligibility evaluated; ready to commit rest.</summary>
/// <typeparam name="TPolicy">The sleep policy.</typeparam>
internal readonly unsafe ref struct IslandPipelineEvaluated<TPolicy>
    where TPolicy : struct, ISleepPolicy<TPolicy>
{
    private readonly DisjointSetNode* _nodes;
    private readonly int _capacity;
    private readonly SimulationParameters _parameters;

    /// <summary>Binds forest memory and parameters.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IslandPipelineEvaluated(DisjointSetNode* nodes, int capacity, SimulationParameters parameters)
    {
        _nodes = nodes;
        _capacity = capacity;
        _parameters = parameters;
    }

    /// <summary>Puts settled islands to rest and counts the rested bodies.</summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public IslandPipelineCommitted<TPolicy> CommitSleep<TBodyStore>(ref TBodyStore bodies)
        where TBodyStore : struct, IBodyStore
    {
        int sleptCount = 0;

        for (int slot = 0; slot < _capacity; slot++)
        {
            if (_nodes[slot].Parent < 0)
            {
                continue;
            }

            int root = NativePartitionBuffer.FindRoot(_nodes, slot);
            bool shouldSleep = TPolicy.ShouldSleep(
                _nodes[root].MinSleep,
                _parameters.TimeToSleep,
                _nodes[root].IsBlocked);

            if (shouldSleep)
            {
                SlotIndex slotIndex = (SlotIndex)slot;
                ref readonly BodyState body = ref bodies.GetSlot(slotIndex);
                if ((body.Flags & BodyFlags.Awake) != 0)
                {
                    bodies.SetAwake(BodyId.FromSlotIndex(slotIndex), awake: false);
                    sleptCount++;
                }
            }
        }

        return new IslandPipelineCommitted<TPolicy>(_nodes, _capacity, sleptCount, _parameters);
    }
}

/// <summary>Phase 4: sleep committed; exposes traversal and diagnostics.</summary>
/// <typeparam name="TPolicy">The sleep policy.</typeparam>
internal readonly unsafe ref struct IslandPipelineCommitted<TPolicy>
    where TPolicy : struct, ISleepPolicy<TPolicy>
{
    private readonly DisjointSetNode* _nodes;
    private readonly int _capacity;
    private readonly SimulationParameters _parameters;

    /// <summary>Bodies put to rest by the commit.</summary>
    public int SleptBodiesCount { get; }

    /// <summary>Binds forest memory, the rested count and parameters.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IslandPipelineCommitted(
        DisjointSetNode* nodes,
        int capacity,
        int sleptBodiesCount,
        SimulationParameters parameters)
    {
        _nodes = nodes;
        _capacity = capacity;
        SleptBodiesCount = sleptBodiesCount;
        _parameters = parameters;
    }

    /// <summary>Enumerates the dynamic roots.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IslandRootsEnumerable EnumerateRoots() => new(_nodes, _capacity);

    /// <summary>Enumerates the members of a root.</summary>
    /// <param name="root">The root slot.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IslandMembersEnumerable EnumerateMembers(SlotIndex root) => new(_nodes, _capacity, root.Value);

    /// <summary>Summarizes one root's resolution.</summary>
    /// <param name="rootSlot">The queried slot.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IslandEvaluationSummary Summarize(SlotIndex rootSlot)
    {
        int root = rootSlot.Value;
        if ((uint)root >= (uint)_capacity || _nodes[root].Parent < 0)
        {
            return new IslandEvaluationSummary(IslandEvaluationStatus.AwakeActive, 0, 0.0f, rootSlot);
        }

        int trueRoot = NativePartitionBuffer.FindRoot(_nodes, root);
        ref readonly DisjointSetNode node = ref _nodes[trueRoot];

        IslandEvaluationStatus status;
        if (node.IsBlocked)
        {
            status = (node.Flags & IslandFlags.AllowSleep) == 0
                ? IslandEvaluationStatus.DisallowedSleep
                : IslandEvaluationStatus.BlockedByKinematic;
        }
        else if (node.MinSleep >= _parameters.TimeToSleep)
        {
            status = IslandEvaluationStatus.Sleeping;
        }
        else
        {
            status = IslandEvaluationStatus.AwakeActive;
        }

        return new IslandEvaluationSummary(status, node.Size, node.MinSleep, (SlotIndex)trueRoot);
    }
}
