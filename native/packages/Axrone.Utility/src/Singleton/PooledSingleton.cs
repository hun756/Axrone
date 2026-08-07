using Axrone.Utility.Singleton.Internal;

namespace Axrone.Utility.Singleton;

/// <summary>
/// CRTP base for pooled singletons — instances are recycled through an <see cref="ObjectPool{T}"/>
/// instead of being permanently held. Call <see cref="Return"/> when done.
/// </summary>
/// <example>
/// <code>
/// public sealed class ParticleEffect : PooledSingleton&lt;ParticleEffect&gt;
/// {
///     protected override void Initialize() { /* reset particle state */ }
///     protected override void Reset() { /* clear for reuse */ }
/// }
///
/// // Usage:
/// var effect = ParticleEffect.GetInstance();
/// effect.Play();
/// ParticleEffect.Return(effect);
/// </code>
/// </example>
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public abstract class PooledSingleton<TSelf> where TSelf : PooledSingleton<TSelf>, new()
{
    private static readonly ObjectPool<TSelf> _pool = new(
        static () => new TSelf(),
        instance => instance.Initialize(),
        instance => instance.Reset());

    /// <summary>Get an instance from the pool (or create a new one).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static TSelf GetInstance() => _pool.Get();

    /// <summary>Return an instance to the pool for reuse.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Return(TSelf instance) => _pool.Return(instance);

    /// <summary>Called when an instance is obtained from the pool.</summary>
    protected virtual void Initialize() { }

    /// <summary>Called when an instance is returned to the pool.</summary>
    protected virtual void Reset() { }
}
