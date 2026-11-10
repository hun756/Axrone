namespace Axrone.Scene;

/// <summary>Lifecycle position of a managed scene behaviour.</summary>
public enum SceneBehaviourState : byte
{
    /// <summary>Constructed but not attached to an actor.</summary>
    Detached = 0,

    /// <summary>Awake has run.</summary>
    Awake = 1,

    /// <summary>Start has run, but the behaviour is not enabled.</summary>
    Started = 2,

    /// <summary>Enable has run; update phases are dispatched.</summary>
    Enabled = 3,

    /// <summary>Disable has run; update phases are skipped.</summary>
    Disabled = 4,

    /// <summary>Destroy has run; the instance must not be reused.</summary>
    Destroyed = 5
}

/// <summary>
/// Managed lifecycle contract. Data components intentionally do not implement
/// this: they are plain payloads consumed by systems.
/// </summary>
public interface ISceneBehaviour
{
    /// <summary>Owning actor, or <see cref="SceneActor.Null"/> while detached.</summary>
    SceneActor Actor { get; }

    /// <summary>Current lifecycle position.</summary>
    SceneBehaviourState State { get; }

    /// <summary>Whether update phases dispatch.</summary>
    bool Enabled { get; set; }

    /// <summary>Dispatch order; higher values run first.</summary>
    int Priority { get; }

    /// <summary>Runs once when the behaviour is attached to a started world.</summary>
    void Awake();

    /// <summary>Runs when the behaviour becomes enabled.</summary>
    void OnEnable();

    /// <summary>Runs once after Awake.</summary>
    void Start();

    /// <summary>Runs on every fixed step while enabled.</summary>
    void FixedUpdate(float deltaSeconds);

    /// <summary>Runs on every frame while enabled.</summary>
    void Update(float deltaSeconds);

    /// <summary>Runs after Update while enabled.</summary>
    void LateUpdate(float deltaSeconds);

    /// <summary>Runs when the behaviour becomes disabled.</summary>
    void OnDisable();

    /// <summary>Runs once when the behaviour is removed or destroyed.</summary>
    void OnDestroy();
}

/// <summary>
/// Base class for managed scene behaviours. Lifecycle transitions are driven
/// by the owning world through the internal methods, so user code cannot
/// reorder Awake/Start/Enable or resurrect a destroyed instance.
/// </summary>
public abstract class SceneBehaviour : ISceneBehaviour
{
    private SceneActor _actor = SceneActor.Null;
    private SceneBehaviourState _state = SceneBehaviourState.Detached;
    private bool _enabled = true;
    private bool _activeInHierarchy = true;
    private int _priority;
    /// <summary>Owning behaviour-table slot; <c>-1</c> while detached.</summary>
    internal int SlotIndex = -1;

    /// <summary>Journal used to record enable/disable transitions; set by the owning table.</summary>
    internal SceneChangeJournal? Changes;

    /// <summary>Registered behaviour type; set by the owning table.</summary>
    internal SceneBehaviourTypeId TypeId;

    private List<IDisposable>? _subscriptions;

    /// <inheritdoc/>
    public SceneActor Actor
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _actor;
    }

    /// <inheritdoc/>
    public SceneBehaviourState State
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _state;
    }

    /// <summary>Whether the behaviour is attached to an actor.</summary>
    public bool IsAttached
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _actor.IsValid;
    }

    /// <summary>Owning world, or <see langword="null"/> while detached.</summary>
    public SceneWorld? World { get; private set; }

    /// <summary>Whether the behaviour has been destroyed.</summary>
    public bool IsDestroyed
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _state == SceneBehaviourState.Destroyed;
    }

    /// <summary>
    /// Enables or disables update dispatch. Detached behaviours only record the
    /// flag; the world applies it once the behaviour reaches Started.
    /// </summary>
    public bool Enabled
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;
            RefreshDispatchState();
        }
    }

    /// <inheritdoc/>
    public int Priority
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _priority;
    }

    /// <inheritdoc/>
    public virtual void Awake()
    {
    }

    /// <inheritdoc/>
    public virtual void OnEnable()
    {
    }

    /// <inheritdoc/>
    public virtual void Start()
    {
    }

    /// <inheritdoc/>
    public virtual void FixedUpdate(float deltaSeconds)
    {
    }

    /// <inheritdoc/>
    public virtual void Update(float deltaSeconds)
    {
    }

    /// <inheritdoc/>
    public virtual void LateUpdate(float deltaSeconds)
    {
    }

    /// <inheritdoc/>
    public virtual void OnDisable()
    {
    }

    /// <inheritdoc/>
    public virtual void OnDestroy()
    {
    }

    /// <summary>Attaches the instance to an actor and records its dispatch priority.</summary>
    internal void Attach(in SceneActor actor, int priority, SceneWorld world)
    {
        if (_actor.IsValid)
        {
            throw new SceneValidationException(
                SceneErrorCodes.ValidationInvalid,
                $"{GetType().Name} is already attached to {_actor}.");
        }

        if (_state == SceneBehaviourState.Destroyed)
        {
            throw new SceneValidationException(
                SceneErrorCodes.ValidationInvalid,
                $"{GetType().Name} has been destroyed and cannot be reattached.");
        }

        _actor = actor;
        _priority = priority;
        World = world;
        _state = SceneBehaviourState.Detached;
    }

    /// <summary>Clears the actor binding after destruction.</summary>
    internal void Detach()
    {
        _actor = SceneActor.Null;
        _priority = 0;
        World = null;
        SlotIndex = -1;
        Changes = null;
        TypeId = default;
    }

    /// <summary>
    /// Tracks a subscription whose lifetime is bound to this behaviour. The owning table disposes
    /// it when the behaviour is removed or the actor is destroyed.
    /// </summary>
    public void TrackSubscription(IDisposable subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        (_subscriptions ??= new List<IDisposable>()).Add(subscription);
    }

    /// <summary>Disposes every tracked subscription; called by the owning table on teardown.</summary>
    internal void DisposeSubscriptions()
    {
        List<IDisposable>? subscriptions = _subscriptions;
        if (subscriptions is null)
        {
            return;
        }

        _subscriptions = null;
        for (int i = subscriptions.Count - 1; i >= 0; i--)
        {
            try
            {
                subscriptions[i].Dispose();
            }
            catch (Exception)
            {
                // Teardown must finish even when a subscription's dispose misbehaves.
            }
        }
    }

    /// <summary>Runs Awake exactly once.</summary>
    internal void AwakeInternal()
    {
        if (_state != SceneBehaviourState.Detached)
        {
            return;
        }

        Awake();
        _state = SceneBehaviourState.Awake;
    }

    /// <summary>
    /// Reverts Started/Enabled/Disabled back to Awake so the next world Start
    /// re-runs Start and OnEnable without a fresh instance.
    /// </summary>
    internal void ResetForRestart()
    {
        if (_state == SceneBehaviourState.Enabled)
        {
            OnDisable();
        }

        if (_state is SceneBehaviourState.Started or
            SceneBehaviourState.Enabled or
            SceneBehaviourState.Disabled)
        {
            _state = SceneBehaviourState.Awake;
        }
    }

    /// <summary>Runs Start and applies the initial enabled and hierarchy state.</summary>
    internal void StartInternal()
    {
        if (_state != SceneBehaviourState.Awake)
        {
            return;
        }

        Start();
        _state = SceneBehaviourState.Started;

        if (_enabled && _activeInHierarchy)
        {
            _state = SceneBehaviourState.Enabled;
            Changes?.Write(SceneChange.BehaviourEnabled(Actor, TypeId));
            OnEnable();
        }
        else
        {
            _state = SceneBehaviourState.Disabled;
            Changes?.Write(SceneChange.BehaviourDisabled(Actor, TypeId));
        }
    }

    /// <summary>
    /// Applies hierarchy activation without touching the caller-controlled
    /// enabled flag, firing enable/disable only on effective transitions.
    /// </summary>
    internal void SetActiveInHierarchyInternal(bool active)
    {
        if (_activeInHierarchy == active)
        {
            if (active && _state == SceneBehaviourState.Disabled && _enabled)
            {
                _state = SceneBehaviourState.Enabled;
                Changes?.Write(SceneChange.BehaviourEnabled(Actor, TypeId));
                OnEnable();
            }

            return;
        }

        _activeInHierarchy = active;
        RefreshDispatchState();
    }

    private void RefreshDispatchState()
    {
        if (_state is not (SceneBehaviourState.Enabled or SceneBehaviourState.Disabled))
        {
            return;
        }

        bool shouldDispatch = _enabled && _activeInHierarchy;
        if (shouldDispatch && _state == SceneBehaviourState.Disabled)
        {
            _state = SceneBehaviourState.Enabled;
            Changes?.Write(SceneChange.BehaviourEnabled(Actor, TypeId));
            OnEnable();
        }
        else if (!shouldDispatch && _state == SceneBehaviourState.Enabled)
        {
            _state = SceneBehaviourState.Disabled;
            Changes?.Write(SceneChange.BehaviourDisabled(Actor, TypeId));
            OnDisable();
        }
    }

    /// <summary>Dispatches one fixed step when enabled.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void FixedUpdateInternal(float deltaSeconds)
    {
        if (_state == SceneBehaviourState.Enabled)
        {
            FixedUpdate(deltaSeconds);
        }
    }

    /// <summary>Dispatches one frame update when enabled.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void UpdateInternal(float deltaSeconds)
    {
        if (_state == SceneBehaviourState.Enabled)
        {
            Update(deltaSeconds);
        }
    }

    /// <summary>Dispatches one late update when enabled.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void LateUpdateInternal(float deltaSeconds)
    {
        if (_state == SceneBehaviourState.Enabled)
        {
            LateUpdate(deltaSeconds);
        }
    }

    /// <summary>Runs disable and destroy exactly once.</summary>
    internal void DestroyInternal()
    {
        if (_state == SceneBehaviourState.Destroyed)
        {
            return;
        }

        if (_state == SceneBehaviourState.Enabled)
        {
            OnDisable();
        }

        OnDestroy();
        _state = SceneBehaviourState.Destroyed;
    }
}
