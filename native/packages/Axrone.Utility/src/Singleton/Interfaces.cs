namespace Axrone.Utility.Singleton;

/// <summary>Synchronous lifecycle hook — called once after singleton creation.</summary>
public interface IInitializable
{
    void Initialize();
}

/// <summary>Async lifecycle hook — called once after singleton creation.</summary>
public interface IAsyncInitializable
{
    Task InitializeAsync();
}

/// <summary>
/// Marker interface — types implementing this are eagerly initialized at module load time
/// via <c>[ModuleInitializer]</c>.
/// </summary>
/// <example>
/// <code>
/// public sealed class AudioSystem : SingletonBase&lt;AudioSystem&gt;, IEagerSingleton
/// {
///     protected override void OnCreated() { /* init audio device at startup */ }
/// }
/// </code>
/// </example>
[SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Marker interface for eager initialization dispatch.")]
public interface IEagerSingleton { }

/// <summary>Value-type lifecycle hook for <see cref="ValueSingleton{T}"/>.</summary>
public interface IValueInitializable
{
    void Initialize();
}

/// <summary>Keyed identity — for multiton types that receive their key at construction.</summary>
/// <typeparam name="TKey">The key type.</typeparam>
public interface IKeyed<TKey>
{
    TKey Key { get; set; }
}
