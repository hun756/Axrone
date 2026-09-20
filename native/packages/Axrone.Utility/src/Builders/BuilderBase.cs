namespace Axrone.Utility.Builders;

/// <summary>
/// Derivable standard for fluent builders: uniform <see cref="Build"/>/<see cref="TryBuild"/>
/// contract plus conditional configuration combinators.
/// </summary>
/// <remarks>
/// Validation stays per-builder — each product guards its own invariants. The base only
/// standardizes how outcomes surface: throwing <see cref="Build"/> for the hot path,
/// exceptionless <see cref="TryBuild"/> for configuration-time feedback.
/// </remarks>
/// <typeparam name="TSelf">Deriving builder type.</typeparam>
/// <typeparam name="TResult">Product type; struct or class.</typeparam>
public abstract class BuilderBase<TSelf, TResult> : IBuilder<TResult>, ITryBuilder<TResult>
    where TSelf : BuilderBase<TSelf, TResult>
{
    /// <summary>Downcast handle for fluent chaining.</summary>
    protected abstract TSelf Self { get; }

    /// <summary>Attempts to build, returning false with a diagnostic instead of throwing.</summary>
    public abstract bool TryBuild([MaybeNullWhen(false)] out TResult result, out BuilderDiagnostic diagnostic);

    /// <summary>
    /// Builds the product; throws <see cref="InvalidOperationException"/> carrying the diagnostic on failure.
    /// </summary>
    /// <remarks>
    /// Override to preserve a legacy exception contract (e.g. a product ctor that throws
    /// <see cref="ArgumentOutOfRangeException"/>); the override must stay a one-liner over the product.
    /// </remarks>
    public virtual TResult Build()
    {
        if (!TryBuild(out TResult? result, out BuilderDiagnostic diagnostic))
        {
            ThrowHelper.ThrowBuilderFailed(in diagnostic);
        }

        return result;
    }

    /// <summary>Applies configuration only when the condition holds.</summary>
    public TSelf When(bool condition, Action<TSelf> configure)
    {
        if (condition)
        {
            configure(Self);
        }

        return Self;
    }

    /// <summary>Applies configuration only when the condition does not hold.</summary>
    public TSelf Unless(bool condition, Action<TSelf> configure)
    {
        if (!condition)
        {
            configure(Self);
        }

        return Self;
    }

    /// <summary>Runs a side effect mid-chain without breaking fluency.</summary>
    public TSelf Tap(Action<TSelf> tap)
    {
        tap(Self);
        return Self;
    }

    /// <summary>
    /// Adapts a throwing factory into the <see cref="TryBuild"/> contract.
    /// </summary>
    /// <remarks>
    /// Single source of truth stays in the product ctor: the factory throws as today, the adapter
    /// only translates on the cold failure path. House CA1031 suppression covers this Try pattern.
    /// </remarks>
    protected static bool TryCreate(Func<TResult> factory, [MaybeNullWhen(false)] out TResult result, out BuilderDiagnostic diagnostic)
    {
        try
        {
            result = factory();
            diagnostic = BuilderDiagnostic.Ok;
            return true;
        }
        catch (Exception ex)
        {
            result = default!;
            diagnostic = BuilderDiagnostic.Fail(BuilderStatusCode.ValidationFailed, $"{ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}
