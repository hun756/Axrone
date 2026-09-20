namespace Axrone.Utility.Builders;

/// <summary>Restores an instance to a pristine state for reuse.</summary>
public interface IResettable
{
    /// <summary>Clears all accumulated state.</summary>
    void Reset();
}
