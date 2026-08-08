namespace Axrone.Utility.Singleton.Internal;

/// <summary>
/// Reads and caches <see cref="SingletonAttribute"/> metadata once per closed generic type.
/// All reflection happens at first access; subsequent reads are volatile field reads.
/// </summary>
[SkipLocalsInit]
internal static class SingletonAttributeCache<T> where T : class
{
    private static int _read;
    private static bool _initialized;
    private static SingletonAttribute? _attribute;
    private static SingletonLifetime _lifetime;
    private static SingletonThreadSafety _threadSafety;
    private static bool _pinned;
    private static bool _monitored;
    private static bool _isEager;

    /// <summary>The raw attribute, or <see langword="null"/> if none is applied.</summary>
    public static SingletonAttribute? Attribute
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { EnsureRead(); return _attribute; }
    }

    /// <summary>Cached lifetime — defaults to <see cref="SingletonLifetime.Singleton"/>.</summary>
    public static SingletonLifetime Lifetime
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { EnsureRead(); return _lifetime; }
    }

    /// <summary>Cached thread-safety strategy.</summary>
    public static SingletonThreadSafety ThreadSafety
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { EnsureRead(); return _threadSafety; }
    }

    /// <summary>Whether the singleton should be pinned via <see cref="GCHandle"/>.</summary>
    public static bool Pinned
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { EnsureRead(); return _pinned; }
    }

    /// <summary>Whether diagnostics tracking is enabled.</summary>
    public static bool Monitored
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { EnsureRead(); return _monitored; }
    }

    /// <summary>Whether the singleton should be eagerly initialized at startup.</summary>
    public static bool IsEager
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get { EnsureRead(); return _isEager; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void EnsureRead()
    {
        if (_initialized) return;

        var state = Volatile.Read(ref _read);
        if (state == 2)
        {
            _initialized = true;
            return;
        }

        if (state == 0 && Interlocked.CompareExchange(ref _read, 1, 0) == 0)
        {
            var rawAttrs = typeof(T).GetCustomAttributes(typeof(SingletonAttribute), inherit: false);
            var attr = rawAttrs.Length > 0 ? (SingletonAttribute)rawAttrs[0] : null;

            _attribute = attr;
            _lifetime = attr?.Lifetime ?? SingletonLifetime.Singleton;
            _threadSafety = attr?.ThreadSafety ?? SingletonThreadSafety.ExecutionAndPublication;
            _pinned = attr?.Pinned ?? false;
            _monitored = attr?.Monitored ?? false;

            if (attr is not null && typeof(ISingletonLifecycle).IsAssignableFrom(typeof(T)))
            {
                _isEager = attr.Lifetime == SingletonLifetime.Singleton;
            }

            Volatile.Write(ref _read, 2);
            _initialized = true;
            return;
        }

        SpinWait spinner = default;
        while (Volatile.Read(ref _read) != 2)
            spinner.SpinOnce();

        _initialized = true;
    }
}
