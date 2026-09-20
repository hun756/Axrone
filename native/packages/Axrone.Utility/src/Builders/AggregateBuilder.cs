namespace Axrone.Utility.Builders;

/// <summary>
/// Tier-2 derivable standard for state-based builders: a mutable accumulator
/// <typeparamref name="TState"/> plus a required-field mask, materialized into an immutable
/// <typeparamref name="TResult"/> via <see cref="IAggregateDefinition{TState, TResult}"/>.
/// </summary>
/// <remarks>
/// Two validation layers stay independent on purpose: the mask names <em>which</em> required
/// setter never ran (cheap, O(1)); <c>TryValidate</c> owns domain ranges and stays authoritative
/// even for state written outside the setters. Mask first, domain second.
/// </remarks>
/// <typeparam name="TSelf">Deriving builder type.</typeparam>
/// <typeparam name="TState">Mutable accumulator state.</typeparam>
/// <typeparam name="TResult">Immutable product type.</typeparam>
public abstract class AggregateBuilder<TSelf, TState, TResult>
    : BuilderBase<TSelf, TResult>, IResettable, IForkable<TSelf>
    where TSelf : AggregateBuilder<TSelf, TState, TResult>
    where TState : struct, IAggregateDefinition<TState, TResult>
{
    private TState _state;
    private PropertyBitmask64 _mutationMask;

    /// <summary>Accumulated configuration; by-ref so setters mutate in place.</summary>
    protected ref TState State => ref _state;

    /// <summary>Which required slots were written through the setters.</summary>
    protected PropertyBitmask64 MutationMask => _mutationMask;

    /// <summary>Mask of slots that must be set before <see cref="TryBuild"/> can succeed.</summary>
    protected abstract PropertyBitmask64 RequiredMask { get; }

    /// <inheritdoc/>
    public override bool TryBuild([MaybeNullWhen(false)] out TResult result, out BuilderDiagnostic diagnostic)
    {
        if (!MutationMask.ContainsAll(RequiredMask))
        {
            ulong missing = MutationMask.ComputeMissing(RequiredMask);
            result = default!;
            diagnostic = BuilderDiagnostic.Fail(
                BuilderStatusCode.MissingRequiredField,
                $"Missing required fields (mask 0x{missing:X16}).");
            return false;
        }

        if (!TState.TryValidate(in State, out diagnostic))
        {
            result = default!;
            return false;
        }

        result = TState.Materialize(in State);
        diagnostic = BuilderDiagnostic.Ok;
        return true;
    }

    /// <summary>Marks a required slot as written; call from every required setter.</summary>
    /// <param name="slot">Per-builder slot constant, 0..63.</param>
    protected void MarkSet(int slot) => _mutationMask = _mutationMask.SetBit(slot);

    /// <inheritdoc/>
    public virtual void Reset()
    {
        _state = default;
        _mutationMask = PropertyBitmask64.None;
    }

    /// <inheritdoc/>
    public abstract TSelf Fork();

    /// <summary>Copies accumulator and mask into a fresh clone; call from <see cref="Fork"/>.</summary>
    protected TSelf CopyTo(TSelf clone)
    {
        clone._state = _state;
        clone._mutationMask = _mutationMask;
        return clone;
    }
}
