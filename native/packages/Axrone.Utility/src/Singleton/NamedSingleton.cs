using Axrone.Utility.Singleton.Internal;

namespace Axrone.Utility.Singleton;

/// <summary>
/// CRTP base for named singletons — one instance per string key.
/// </summary>
/// <typeparam name="TSelf">The concrete named singleton type.</typeparam>
/// <example>
/// <code>
/// public sealed class DatabaseConnection : NamedSingleton&lt;DatabaseConnection&gt;
/// {
///     protected override void Initialize(string name) { /* connect to "name" db */ }
/// }
///
/// // Usage:
/// var primary = DatabaseConnection.GetInstance("primary");
/// var replica = DatabaseConnection.GetInstance("replica");
/// </code>
/// </example>
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public abstract class NamedSingleton<TSelf> where TSelf : NamedSingleton<TSelf>, new()
{
    private static class InstanceHolder
    {
        public static readonly ConcurrentDictionary<string, TSelf> Instances =
            new(StringComparer.Ordinal);
    }

    /// <summary>Get or create a named instance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static TSelf GetInstance(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return InstanceHolder.Instances.GetOrAdd(name, n =>
        {
            var instance = new TSelf();
            try
            {
                using (NestingContext.EnterScope())
                {
                    instance.Initialize(n);
                    return instance;
                }
            }
            catch (Exception ex) when (ex is not SingletonCreationException)
            {
                throw new SingletonCreationException(
                    $"Failed to initialize named singleton '{n}' of type {typeof(TSelf).Name}",
                    typeof(TSelf), ex);
            }
        });
    }

    /// <summary>Try to get an existing named instance without creating.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryGet(string name, [NotNullWhen(true)] out TSelf? instance) =>
        InstanceHolder.Instances.TryGetValue(name, out instance);

    /// <summary>Check if a named instance exists.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool Contains(string name) => InstanceHolder.Instances.ContainsKey(name);

    /// <summary>Try to remove a named instance.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryRemove(string name, [NotNullWhen(true)] out TSelf? instance) =>
        InstanceHolder.Instances.TryRemove(name, out instance);

    /// <summary>Clear all named instances.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Clear() => InstanceHolder.Instances.Clear();

    /// <summary>Called once per named instance after construction.</summary>
    protected abstract void Initialize(string name);
}
