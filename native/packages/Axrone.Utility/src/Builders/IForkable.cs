namespace Axrone.Utility.Builders;

/// <summary>Produces an independent clone that can diverge without affecting the source.</summary>
/// <typeparam name="TSelf">Builder type.</typeparam>
public interface IForkable<out TSelf>
{
    /// <summary>Copies current builder state into a fresh builder.</summary>
    TSelf Fork();
}
