namespace Axrone.Utility.Builders;

/// <summary>Exceptionless build attempt reporting a structured diagnostic.</summary>
/// <typeparam name="TResult">Product type.</typeparam>
public interface ITryBuilder<TResult>
{
    /// <summary>Attempts to build, returning false with a diagnostic instead of throwing.</summary>
    bool TryBuild([MaybeNullWhen(false)] out TResult result, out BuilderDiagnostic diagnostic);
}
