namespace Axrone.Memory.Lifetime;

/// <summary>Typestate marker for a builder that has not selected a factory yet.</summary>
public readonly struct Unconfigured;

/// <summary>Typestate marker for a builder that has selected a factory.</summary>
public readonly struct FactorySet;

/// <summary>Typestate marker for a builder that is ready to produce an engine.</summary>
public readonly struct Configured;

/// <summary>
/// Typestate-driven builder for lease-tracked singleton engines. The phase generic
/// parameter tracks configuration progress at compile time: <see cref="Unconfigured"/>
/// builders can select a factory, <see cref="FactorySet"/> builders can customize the
/// backoff policy and diagnostics sink, and <see cref="Configured"/> builders can be
/// materialized with <see cref="SingletonBuilderExtensions.Build{T, TFactory, TBackoff, TDiagnostics}"/>.
/// </summary>
/// <typeparam name="TPhase">The current typestate phase marker.</typeparam>
/// <typeparam name="T">The singleton instance type.</typeparam>
/// <typeparam name="TFactory">The factory struct that produces instances.</typeparam>
/// <typeparam name="TBackoff">The backoff policy struct used to space drain waits.</typeparam>
/// <typeparam name="TDiagnostics">The diagnostics sink struct receiving lifecycle callbacks.</typeparam>
public readonly struct SingletonBuilder<TPhase, T, TFactory, TBackoff, TDiagnostics>
    where T : class
    where TFactory : struct, ISingletonFactory<T>
    where TBackoff : struct, IBackoffPolicy
    where TDiagnostics : struct, ISingletonDiagnostics
{
    internal readonly TBackoff _backoff;
    internal readonly TDiagnostics _diagnostics;
    internal readonly SingletonOptions _options;

    internal SingletonBuilder(TBackoff backoff, TDiagnostics diagnostics, SingletonOptions options)
    {
        _backoff = backoff;
        _diagnostics = diagnostics;
        _options = options;
    }

    /// <summary>Selects the factory struct used to produce instances.</summary>
    /// <typeparam name="TNewFactory">The factory struct to use.</typeparam>
    /// <returns>A builder in the <see cref="FactorySet"/> phase.</returns>
    public SingletonBuilder<FactorySet, T, TNewFactory, TBackoff, TDiagnostics> WithFactory<TNewFactory>()
        where TNewFactory : struct, ISingletonFactory<T>
        => new(_backoff, _diagnostics, _options);

    /// <summary>Selects the backoff policy struct used to space drain waits.</summary>
    /// <typeparam name="TNewBackoff">The backoff policy struct to use.</typeparam>
    /// <param name="backoff">The backoff policy instance to use.</param>
    /// <returns>A builder carrying the new backoff policy.</returns>
    public SingletonBuilder<TPhase, T, TFactory, TNewBackoff, TDiagnostics> WithBackoff<TNewBackoff>(TNewBackoff backoff)
        where TNewBackoff : struct, IBackoffPolicy
        => new(backoff, _diagnostics, _options);

    /// <summary>Selects the diagnostics sink struct receiving lifecycle callbacks.</summary>
    /// <typeparam name="TNewDiagnostics">The diagnostics sink struct to use.</typeparam>
    /// <param name="diagnostics">The diagnostics sink instance to use.</param>
    /// <returns>A builder carrying the new diagnostics sink.</returns>
    public SingletonBuilder<TPhase, T, TFactory, TBackoff, TNewDiagnostics> WithDiagnostics<TNewDiagnostics>(TNewDiagnostics diagnostics)
        where TNewDiagnostics : struct, ISingletonDiagnostics
        => new(_backoff, diagnostics, _options);

    /// <summary>Applies configuration options to the builder.</summary>
    /// <param name="options">The options to apply.</param>
    /// <returns>A builder carrying the new options.</returns>
    public SingletonBuilder<TPhase, T, TFactory, TBackoff, TDiagnostics> WithOptions(SingletonOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new(_backoff, _diagnostics, options);
    }

    /// <summary>Produces the singleton engine described by the builder.</summary>
    /// <returns>The configured engine.</returns>
    internal Singleton<T, TFactory, TBackoff, TDiagnostics> Materialize()
        => new(_backoff, _diagnostics, _options);
}

/// <summary>Entry points for the singleton builder typestate pipeline.</summary>
public static class SingletonBuilder
{
    /// <summary>
    /// Starts a builder for the specified instance type using the default factory,
    /// backoff policy, and diagnostics sink.
    /// </summary>
    /// <typeparam name="T">The singleton instance type; must have a parameterless constructor.</typeparam>
    /// <returns>A builder in the <see cref="Unconfigured"/> phase.</returns>
    public static SingletonBuilder<Unconfigured, T, DefaultActivatorFactory<T>, AdaptiveBackoff, NullDiagnostics> For<T>()
        where T : class, new()
        => new(new AdaptiveBackoff(), new NullDiagnostics(), new SingletonOptions());
}

/// <summary>
/// Extension methods for transitioning and materializing builders.
/// </summary>
public static class SingletonBuilderExtensions
{
    /// <summary>Transitions any builder phase to the <see cref="Configured"/> phase.</summary>
    /// <typeparam name="TPhase">The current typestate phase marker.</typeparam>
    /// <typeparam name="T">The singleton instance type.</typeparam>
    /// <typeparam name="TFactory">The factory struct that produces instances.</typeparam>
    /// <typeparam name="TBackoff">The backoff policy struct used to space drain waits.</typeparam>
    /// <typeparam name="TDiagnostics">The diagnostics sink struct receiving lifecycle callbacks.</typeparam>
    /// <param name="builder">The builder to prepare.</param>
    /// <returns>A builder in the <see cref="Configured"/> phase.</returns>
    public static SingletonBuilder<Configured, T, TFactory, TBackoff, TDiagnostics> Prepare<TPhase, T, TFactory, TBackoff, TDiagnostics>(
        this SingletonBuilder<TPhase, T, TFactory, TBackoff, TDiagnostics> builder)
        where T : class
        where TFactory : struct, ISingletonFactory<T>
        where TBackoff : struct, IBackoffPolicy
        where TDiagnostics : struct, ISingletonDiagnostics
        => new(builder._backoff, builder._diagnostics, builder._options);

    /// <summary>
    /// Produces the singleton engine described by a <see cref="Configured"/> builder.
    /// </summary>
    /// <typeparam name="T">The singleton instance type.</typeparam>
    /// <typeparam name="TFactory">The factory struct that produces instances.</typeparam>
    /// <typeparam name="TBackoff">The backoff policy struct used to space drain waits.</typeparam>
    /// <typeparam name="TDiagnostics">The diagnostics sink struct receiving lifecycle callbacks.</typeparam>
    /// <param name="builder">The configured builder.</param>
    /// <returns>The configured engine.</returns>
    public static Singleton<T, TFactory, TBackoff, TDiagnostics> Build<T, TFactory, TBackoff, TDiagnostics>(
        this SingletonBuilder<Configured, T, TFactory, TBackoff, TDiagnostics> builder)
        where T : class
        where TFactory : struct, ISingletonFactory<T>
        where TBackoff : struct, IBackoffPolicy
        where TDiagnostics : struct, ISingletonDiagnostics
        => builder.Materialize();
}

/// <summary>
/// Arity-two convenience subclass that fixes the backoff policy to
/// <see cref="AdaptiveBackoff"/> and the diagnostics sink to <see cref="NullDiagnostics"/>
/// so callers only specify the instance and factory types.
/// </summary>
/// <typeparam name="T">The singleton instance type.</typeparam>
/// <typeparam name="TFactory">The factory struct that produces instances.</typeparam>
public sealed class Singleton<T, TFactory> : Singleton<T, TFactory, AdaptiveBackoff, NullDiagnostics>
    where T : class
    where TFactory : struct, ISingletonFactory<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Singleton{T, TFactory}"/> class.
    /// </summary>
    /// <param name="options">The engine configuration; defaults are used when null.</param>
    public Singleton(SingletonOptions? options = null)
        : base(options)
    {
    }
}
