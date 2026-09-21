namespace Axrone.Utility.Builders;

/// <summary>Materializes a validated product; throws on invariant violation.</summary>
/// <typeparam name="TResult">Product type.</typeparam>
public interface IBuilder<out TResult>
{
    /// <summary>Builds the product.</summary>
    TResult Build();
}
