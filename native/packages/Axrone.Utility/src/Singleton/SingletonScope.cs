using Axrone.Utility.Singleton.Internal;

namespace Axrone.Utility.Singleton;

/// <summary>
/// Scoped singleton container with <see cref="AsyncLocal{T}"/> ambient context.
/// Supports both string-keyed and type-keyed registrations, parent-child hierarchy,
/// and automatic disposal on scope exit.
/// </summary>
/// <remarks>
/// String-keyed entries fall back to parent scope for unresolved keys.
/// Type-keyed entries (<c>Register&lt;T&gt;()</c>, <see cref="Resolve{T}()"/>) use the type itself as key.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public sealed class SingletonScope : IDisposable
{
    private static readonly AsyncLocal<SingletonScope?> _current = new();

    private readonly Dictionary<string, object> _instances = new();
    private readonly Dictionary<string, Func<object>> _factories = new();
    private readonly Dictionary<Type, object> _typedInstances = new();
    private readonly Dictionary<Type, Func<object>> _typedFactories = new();
    private readonly List<Action> _disposers = [];
    private readonly SingletonScope? _parent;
    private readonly object _lock = new();
    private int _disposed;

    /// <summary>Scope name for diagnostics.</summary>
    public string Name { get; }

    /// <summary>The currently active ambient scope, or null if none.</summary>
    public static SingletonScope? Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => _current.Value;
    }

    /// <summary>Global root scope — always available as fallback.</summary>
    public static SingletonScope Global { get; } = new("global");

    public SingletonScope(string name) => Name = name;

    private SingletonScope(string name, SingletonScope parent)
    {
        Name = name;
        _parent = parent;
    }

    // ── String-keyed API ─────────────────────────────────────────────

    /// <summary>Register a string-keyed factory with optional disposer.</summary>
    public void Register<T>(string key, Func<T> factory, Action<T>? disposer = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);
        lock (_lock)
        {
            _factories[key] = () => factory();
            if (disposer is not null)
                _disposers.Add(() =>
                {
                    if (_instances.TryGetValue(key, out var val) && val is T typed)
                        disposer(typed);
                });
        }
    }

    /// <summary>Resolve a string-keyed instance. Falls back to parent scope if not found locally.</summary>
    public T Resolve<T>(string key) where T : class
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_lock)
        {
            if (_instances.TryGetValue(key, out var existing))
                return (T)existing;

            if (!_factories.TryGetValue(key, out var factory))
            {
                if (_parent is not null)
                    return _parent.Resolve<T>(key);
                throw new KeyNotFoundException(
                    $"No registration found for key '{key}' in scope '{Name}'.");
            }

            var instance = (T)factory();
            _instances[key] = instance;
            return instance;
        }
    }

    /// <summary>Non-throwing string-keyed resolve variant.</summary>
    public bool TryResolve<T>(string key, [NotNullWhen(true)] out T? instance) where T : class
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_lock)
        {
            if (_instances.TryGetValue(key, out var existing))
            {
                instance = (T)existing;
                return true;
            }

            if (!_factories.TryGetValue(key, out var factory))
            {
                if (_parent is not null)
                    return _parent.TryResolve(key, out instance);

                instance = default;
                return false;
            }

            var created = (T)factory();
            _instances[key] = created;
            instance = created;
            return true;
        }
    }

    // ── Type-keyed API ───────────────────────────────────────────────

    /// <summary>Register a type-keyed factory. Resolved via <see cref="Resolve{T}()"/> without a string key.</summary>
    public void Register<T>(Func<T> factory) where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        lock (_lock)
        {
            _typedFactories[typeof(T)] = () => factory();
        }
    }

    /// <summary>
    /// Resolve a type-keyed instance. Checks the current ambient scope first,
    /// then falls back to <see cref="Global"/>.
    /// </summary>
    public static T Resolve<T>() where T : class
    {
        var scope = _current.Value ?? Global;
        return scope.ResolveTyped<T>();
    }

    /// <summary>Try to resolve a type-keyed instance from the ambient scope chain.</summary>
    public static bool TryResolve<T>([NotNullWhen(true)] out T? instance) where T : class
    {
        var scope = _current.Value ?? Global;
        return scope.TryResolveTyped(out instance);
    }

    private T ResolveTyped<T>() where T : class
    {
        lock (_lock)
        {
            if (_typedInstances.TryGetValue(typeof(T), out var existing))
                return (T)existing;

            if (!_typedFactories.TryGetValue(typeof(T), out var factory))
            {
                if (_parent is not null)
                    return _parent.ResolveTyped<T>();
                throw new KeyNotFoundException(
                    $"No typed registration found for {typeof(T).Name} in scope '{Name}'.");
            }

            var instance = (T)factory();
            _typedInstances[typeof(T)] = instance;
            return instance;
        }
    }

    private bool TryResolveTyped<T>([NotNullWhen(true)] out T? instance) where T : class
    {
        lock (_lock)
        {
            if (_typedInstances.TryGetValue(typeof(T), out var existing))
            {
                instance = (T)existing;
                return true;
            }

            if (!_typedFactories.TryGetValue(typeof(T), out var factory))
            {
                if (_parent is not null)
                    return _parent.TryResolveTyped(out instance);

                instance = default;
                return false;
            }

            var created = (T)factory();
            _typedInstances[typeof(T)] = created;
            instance = created;
            return true;
        }
    }

    // ── Scope management ─────────────────────────────────────────────

    /// <summary>Create a child scope with this scope as parent.</summary>
    public SingletonScope CreateChild(string childName) => new(childName, this);

    /// <summary>Set this scope as the ambient <see cref="Current"/> for the calling async context.</summary>
    public ScopeActivation Activate()
    {
        var previous = _current.Value;
        _current.Value = this;
        return new ScopeActivation(this, previous);
    }

    /// <summary>RAII activation — restores previous ambient scope on dispose.</summary>
    [SuppressMessage("Design", "CA1034:Nested types should not be visible",
        Justification = "RAII struct for scope activation.")]
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ScopeActivation : IDisposable
    {
        private readonly SingletonScope _scope;
        private readonly SingletonScope? _previous;

        internal ScopeActivation(SingletonScope scope, SingletonScope? previous)
        {
            _scope = scope;
            _previous = previous;
        }

        public void Dispose()
        {
            _current.Value = _previous;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        lock (_lock)
        {
            for (var i = _disposers.Count - 1; i >= 0; i--)
                _disposers[i]();

            _disposers.Clear();
            _instances.Clear();
            _factories.Clear();
            _typedInstances.Clear();
            _typedFactories.Clear();
        }
    }
}
