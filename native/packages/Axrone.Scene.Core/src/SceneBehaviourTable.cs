namespace Axrone.Scene;

/// <summary>Host services the behaviour table needs from the owning world.</summary>
internal interface ISceneBehaviourHost
{
    /// <summary>Owning world, exposed to behaviours for sibling component access.</summary>
    SceneWorld World { get; }

    bool IsStarted { get; }

    bool IsActiveInHierarchy(SceneActor actor);

    void ReportBehaviourFault(SceneBehaviour behaviour, Exception exception);
}

/// <summary>One managed behaviour slot; <see cref="Next"/> links the actor list.</summary>
internal struct SceneBehaviourSlot
{
    public SceneBehaviour? Behaviour;
    public int ActorSlot;
    public int Next;
    public SceneBehaviourTypeId TypeId;
    public int Priority;
}

/// <summary>
/// Managed behaviour storage and priority-ordered phase dispatch. Slots are
/// dense and reused through a free list; the phase order array is rebuilt only
/// on structural changes.
/// </summary>
internal sealed class SceneBehaviourTable
{
    private const int MinimumCapacity = 16;

    private readonly SceneActorTable _actors;
    private readonly SceneComponentRegistry _registry;
    private readonly ISceneBehaviourHost _host;

    private SceneBehaviourSlot[] _slots;
    private int[] _order;
    private int _slotCount;
    private int _freeHead;
    private int _count;
    private int _orderCount;

    public SceneBehaviourTable(
        SceneActorTable actors,
        SceneComponentRegistry registry,
        ISceneBehaviourHost host,
        int initialCapacity = 32)
    {
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(host);

        if (initialCapacity <= 0)
        {
            SceneThrowHelper.ThrowInvalidCapacity(initialCapacity);
        }

        int capacity = Math.Max(initialCapacity, MinimumCapacity);
        _actors = actors;
        _registry = registry;
        _host = host;
        _slots = new SceneBehaviourSlot[capacity];
        _order = new int[capacity];
        _freeHead = -1;
        InitializeFreeSlots(0, capacity);
        _slotCount = capacity;
    }

    /// <summary>Number of attached behaviours.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    /// <summary>Number of entries in the phase order.</summary>
    public int OrderCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _orderCount;
    }

    /// <summary>Creates and attaches a behaviour through its registered descriptor.</summary>
    public T Add<T>(SceneActor actor, T behaviour)
        where T : SceneBehaviour
    {
        ArgumentNullException.ThrowIfNull(behaviour);
        SceneBehaviourDescriptor descriptor = _registry.GetBehaviourByType(typeof(T));
        AddCore(actor, descriptor, behaviour);
        return behaviour;
    }

    /// <summary>Creates a behaviour from its descriptor and attaches it.</summary>
    public SceneBehaviour Add(SceneActor actor, SceneBehaviourDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        SceneBehaviour behaviour = descriptor.Create();
        AddCore(actor, descriptor, behaviour);
        return behaviour;
    }

    /// <summary>Creates and attaches a behaviour using its registered factory.</summary>
    public T Add<T>(SceneActor actor)
        where T : SceneBehaviour
    {
        SceneBehaviourDescriptor descriptor = _registry.GetBehaviourByType(typeof(T));
        if (descriptor.Create() is not T behaviour)
        {
            throw new SceneValidationException(
                SceneErrorCodes.ValidationInvalid,
                $"Behaviour factory for '{descriptor.Name}' did not produce {typeof(T).Name}.");
        }

        AddCore(actor, descriptor, behaviour);
        return behaviour;
    }

    /// <summary>Removes every behaviour attached to an actor.</summary>
    public void RemoveAllOnActor(SceneActor actor)
    {
        int actorSlot = _actors.Resolve(actor);
        int slot = _actors.BehaviourHeadAt(actorSlot);
        while (slot >= 0)
        {
            SceneBehaviour? behaviour = _slots[slot].Behaviour;
            int next = _slots[slot].Next;
            if (behaviour is not null)
            {
                Remove(behaviour);
            }

            slot = next;
        }
    }

    /// <summary>Returns the first behaviour of a type on an actor.</summary>
    public SceneBehaviour? Get(SceneActor actor, SceneBehaviourTypeId typeId)
    {
        int actorSlot = _actors.Resolve(actor);
        int slot = Find(actorSlot, typeId);
        return slot < 0 ? null : _slots[slot].Behaviour;
    }

    /// <summary>Returns the first behaviour of <typeparamref name="T"/> on an actor.</summary>
    public T? Get<T>(SceneActor actor)
        where T : SceneBehaviour
    {
        SceneBehaviourDescriptor descriptor = _registry.GetBehaviourByType(typeof(T));
        return Get(actor, descriptor.TypeId) as T;
    }

    /// <summary>Whether an actor carries a behaviour of <typeparamref name="T"/>.</summary>
    public bool Has<T>(SceneActor actor)
        where T : SceneBehaviour
    {
        SceneBehaviourDescriptor descriptor = _registry.GetBehaviourByType(typeof(T));
        int actorSlot = _actors.Resolve(actor);
        return Find(actorSlot, descriptor.TypeId) >= 0;
    }

    /// <summary>Removes a specific behaviour instance.</summary>
    public bool Remove(SceneBehaviour behaviour)
    {
        ArgumentNullException.ThrowIfNull(behaviour);

        int slot = behaviour.SlotIndex;
        if ((uint)slot >= (uint)_slotCount ||
            !ReferenceEquals(_slots[slot].Behaviour, behaviour))
        {
            return false;
        }

        int actorSlot = _slots[slot].ActorSlot;
        Unlink(actorSlot, slot);

        Exception? fault = null;
        try
        {
            behaviour.DestroyInternal();
        }
        catch (Exception exception)
        {
            fault = exception;
        }

        behaviour.Detach();
        ref SceneBehaviourSlot entry = ref _slots[slot];
        entry.Behaviour = null;
        entry.ActorSlot = -1;
        entry.Next = _freeHead;
        _freeHead = slot;
        _count--;
        RebuildOrder();

        if (fault is not null)
        {
            _host.ReportBehaviourFault(behaviour, fault);
        }

        return true;
    }

    /// <summary>Removes the first behaviour of <typeparamref name="T"/> from an actor.</summary>
    public bool Remove<T>(SceneActor actor)
        where T : SceneBehaviour
    {
        SceneBehaviourDescriptor descriptor = _registry.GetBehaviourByType(typeof(T));
        int actorSlot = _actors.Resolve(actor);
        int slot = Find(actorSlot, descriptor.TypeId);
        if (slot < 0)
        {
            return false;
        }

        return Remove(_slots[slot].Behaviour!);
    }

    /// <summary>Starts every behaviour still in the Awake state.</summary>
    public void StartAll()
    {
        for (int i = 0; i < _orderCount; i++)
        {
            SceneBehaviourSlot entry = _slots[_order[i]];
            if (entry.Behaviour is not { } behaviour)
            {
                continue;
            }

            try
            {
                behaviour.StartInternal();
            }
            catch (Exception exception)
            {
                _host.ReportBehaviourFault(behaviour, exception);
            }
        }
    }

    /// <summary>Reverts every started behaviour back to Awake so the next Start re-runs Start.</summary>
    public void ResetAll()
    {
        for (int i = 0; i < _orderCount; i++)
        {
            SceneBehaviourSlot entry = _slots[_order[i]];
            if (entry.Behaviour is not { } behaviour)
            {
                continue;
            }

            try
            {
                behaviour.ResetForRestart();
            }
            catch (Exception exception)
            {
                _host.ReportBehaviourFault(behaviour, exception);
            }
        }
    }

    /// <summary>Applies hierarchy activation to every behaviour on an actor.</summary>
    public void SetActiveInHierarchy(in SceneActor actor, bool active)
    {
        int actorSlot = _actors.Resolve(actor);
        int slot = _actors.BehaviourHeadAt(actorSlot);

        while (slot >= 0)
        {
            SceneBehaviourSlot entry = _slots[slot];
            int next = entry.Next;

            if (entry.Behaviour is { } behaviour)
            {
                try
                {
                    behaviour.SetActiveInHierarchyInternal(active);
                }
                catch (Exception exception)
                {
                    _host.ReportBehaviourFault(behaviour, exception);
                }
            }

            slot = next;
        }
    }

    /// <summary>Dispatches one fixed update in priority order.</summary>
    public void FixedUpdateAll(float deltaSeconds)
    {
        for (int i = 0; i < _orderCount; i++)
        {
            SceneBehaviourSlot entry = _slots[_order[i]];
            if (entry.Behaviour is not { } behaviour)
            {
                continue;
            }

            try
            {
                behaviour.FixedUpdateInternal(deltaSeconds);
            }
            catch (Exception exception)
            {
                _host.ReportBehaviourFault(behaviour, exception);
            }
        }
    }

    /// <summary>Dispatches one frame update in priority order.</summary>
    public void UpdateAll(float deltaSeconds)
    {
        for (int i = 0; i < _orderCount; i++)
        {
            SceneBehaviourSlot entry = _slots[_order[i]];
            if (entry.Behaviour is not { } behaviour)
            {
                continue;
            }

            try
            {
                behaviour.UpdateInternal(deltaSeconds);
            }
            catch (Exception exception)
            {
                _host.ReportBehaviourFault(behaviour, exception);
            }
        }
    }

    /// <summary>Dispatches one late update in priority order.</summary>
    public void LateUpdateAll(float deltaSeconds)
    {
        for (int i = 0; i < _orderCount; i++)
        {
            SceneBehaviourSlot entry = _slots[_order[i]];
            if (entry.Behaviour is not { } behaviour)
            {
                continue;
            }

            try
            {
                behaviour.LateUpdateInternal(deltaSeconds);
            }
            catch (Exception exception)
            {
                _host.ReportBehaviourFault(behaviour, exception);
            }
        }
    }

    /// <summary>Destroys every behaviour and resets the table.</summary>
    public void Clear()
    {
        for (int i = 0; i < _slotCount; i++)
        {
            SceneBehaviour? behaviour = _slots[i].Behaviour;
            if (behaviour is null)
            {
                continue;
            }

            Exception? fault = null;
            try
            {
                behaviour.DestroyInternal();
            }
            catch (Exception exception)
            {
                fault = exception;
            }

            behaviour.Detach();

            if (fault is not null)
            {
                _host.ReportBehaviourFault(behaviour, fault);
            }
        }

        _freeHead = -1;
        for (int i = _slotCount - 1; i >= 0; i--)
        {
            ref SceneBehaviourSlot entry = ref _slots[i];
            entry.Behaviour = null;
            entry.ActorSlot = -1;
            entry.TypeId = SceneBehaviourTypeId.Invalid;
            entry.Priority = 0;
            entry.Next = _freeHead;
            _freeHead = i;
        }

        _count = 0;
        _orderCount = 0;
    }

    private void AddCore(SceneActor actor, SceneBehaviourDescriptor descriptor, SceneBehaviour behaviour)
    {
        if (!descriptor.ComponentType.IsInstanceOfType(behaviour))
        {
            throw new SceneValidationException(
                SceneErrorCodes.ValidationInvalid,
                $"Behaviour {behaviour.GetType().Name} does not match descriptor '{descriptor.Name}'.");
        }

        int actorSlot = _actors.Resolve(actor);

        if (descriptor.IsSingleton && Find(actorSlot, descriptor.TypeId) >= 0)
        {
            SceneThrowHelper.ThrowComponentDuplicate(in actor, descriptor.ComponentType.Name);
        }

        int slot = AllocateSlot();
        ref SceneBehaviourSlot entry = ref _slots[slot];
        entry.Behaviour = behaviour;
        entry.ActorSlot = actorSlot;
        entry.TypeId = descriptor.TypeId;
        entry.Priority = descriptor.Priority;
        entry.Next = _actors.BehaviourHeadAt(actorSlot);
        _actors.BehaviourHeadAt(actorSlot) = slot;
        _count++;

        try
        {
            behaviour.Attach(actor, descriptor.Priority, _host.World);
            behaviour.SlotIndex = slot;
        }
        catch (Exception)
        {
            Unlink(actorSlot, slot);
            entry.Behaviour = null;
            entry.ActorSlot = -1;
            entry.Next = _freeHead;
            _freeHead = slot;
            _count--;
            throw;
        }

        try
        {
            // Hierarchy state is known before Start so attaching to an inactive
            // actor never fires a spurious OnEnable.
            behaviour.SetActiveInHierarchyInternal(_host.IsActiveInHierarchy(actor));
            behaviour.AwakeInternal();
            if (_host.IsStarted)
            {
                behaviour.StartInternal();
            }
        }
        catch (Exception)
        {
            Unlink(actorSlot, slot);
            try
            {
                behaviour.DestroyInternal();
            }
            catch (Exception destroyException)
            {
                _host.ReportBehaviourFault(behaviour, destroyException);
            }

            behaviour.Detach();
            entry.Behaviour = null;
            entry.ActorSlot = -1;
            entry.Next = _freeHead;
            _freeHead = slot;
            _count--;
            throw;
        }

        RebuildOrder();
    }

    private int AllocateSlot()
    {
        if (_freeHead < 0)
        {
            Grow();
        }

        int slot = _freeHead;
        _freeHead = _slots[slot].Next;
        return slot;
    }

    private void Grow()
    {
        int oldCapacity = _slots.Length;
        if (oldCapacity > int.MaxValue / 2)
        {
            throw new SceneConfigurationException("Behaviour table capacity overflow.");
        }

        int newCapacity = oldCapacity * 2;
        Array.Resize(ref _slots, newCapacity);
        Array.Resize(ref _order, newCapacity);
        InitializeFreeSlots(oldCapacity, newCapacity);
        _slotCount = newCapacity;
    }

    private void InitializeFreeSlots(int start, int end)
    {
        for (int slot = start; slot < end; slot++)
        {
            ref SceneBehaviourSlot entry = ref _slots[slot];
            entry.Behaviour = null;
            entry.ActorSlot = -1;
            entry.TypeId = SceneBehaviourTypeId.Invalid;
            entry.Priority = 0;
            entry.Next = _freeHead;
            _freeHead = slot;
        }
    }

    private int Find(int actorSlot, SceneBehaviourTypeId typeId)
    {
        for (int slot = _actors.BehaviourHeadAt(actorSlot); slot >= 0; slot = _slots[slot].Next)
        {
            if (_slots[slot].TypeId == typeId)
            {
                return slot;
            }
        }

        return -1;
    }

    private void Unlink(int actorSlot, int slot)
    {
        if ((uint)actorSlot >= (uint)_actors.Capacity)
        {
            return;
        }

        ref SceneActorRecord actor = ref _actors.RecordAt(actorSlot);
        if ((actor.Meta.Flags & SceneActorFlags.IsFree) != 0)
        {
            return;
        }

        ref int head = ref _actors.BehaviourHeadAt(actorSlot);
        if (head == slot)
        {
            head = _slots[slot].Next;
            return;
        }

        int current = head;
        while (current >= 0)
        {
            ref SceneBehaviourSlot entry = ref _slots[current];
            if (entry.Next == slot)
            {
                entry.Next = _slots[slot].Next;
                return;
            }

            current = entry.Next;
        }
    }

    private void RebuildOrder()
    {
        int count = 0;
        for (int slot = 0; slot < _slotCount; slot++)
        {
            if (_slots[slot].Behaviour is not null)
            {
                _order[count++] = slot;
            }
        }

        for (int i = 1; i < count; i++)
        {
            int key = _order[i];
            int keyPriority = _slots[key].Priority;
            int j = i - 1;

            while (j >= 0 &&
                (_slots[_order[j]].Priority < keyPriority ||
                 (_slots[_order[j]].Priority == keyPriority && _order[j] > key)))
            {
                _order[j + 1] = _order[j];
                j--;
            }

            _order[j + 1] = key;
        }

        _orderCount = count;
    }
}
