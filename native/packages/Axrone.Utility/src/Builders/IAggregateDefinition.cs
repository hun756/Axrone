namespace Axrone.Utility.Builders;

/// <summary>
/// Unifies factory creation and invariant validation into a single static contract over a
/// mutable accumulator state. Implemented by state structs, consumed by
/// <see cref="AggregateBuilder{TSelf, TState, TResult}"/>.
/// </summary>
/// <typeparam name="TState">Mutable accumulator state.</typeparam>
/// <typeparam name="TResult">Immutable materialized product.</typeparam>
public interface IAggregateDefinition<TState, TResult>
    where TState : struct
{
    /// <summary>Materializes the immutable product from validated state.</summary>
    static abstract TResult Materialize(in TState state);

    /// <summary>Validates state independent of any mutation tracking.</summary>
    static abstract bool TryValidate(in TState state, out BuilderDiagnostic diagnostic);
}
