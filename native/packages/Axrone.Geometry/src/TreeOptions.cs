namespace Axrone.Geometry;

/// <summary>
/// Tuning knobs of a spatial tree: how much slack every stored box carries, how that
/// slack scales with the speed an item moves at, and how many slots a leaf reserves.
/// The guards live in the properties themselves, so an invalid combination is rejected at
/// the assignment rather than at the first traversal that trips over it.
/// </summary>
public readonly struct TreeOptions
{
    /// <summary>
    /// Creates the default tuning set: a tenth of a unit of slack per side, doubled for
    /// moving items, and 256 slots per node.
    /// </summary>
    public TreeOptions()
    {
    }

    /// <summary>Creates a fully specified tuning set.</summary>
    /// <param name="fatteningMargin">Per-side slack grown onto a stored box.</param>
    /// <param name="velocityMultiplier">Scale applied to the margin for moving items.</param>
    /// <param name="initialCapacity">Slots reserved per leaf node.</param>
    public TreeOptions(float fatteningMargin, float velocityMultiplier, TreeCapacity initialCapacity)
    {
        FatteningMargin = fatteningMargin;
        VelocityMultiplier = velocityMultiplier;
        InitialCapacity = initialCapacity;
    }

    /// <summary>
    /// Slack added to every side of a stored box, so an item can creep without forcing
    /// a structural update. Zero is allowed and means boxes are stored tight.
    /// </summary>
    public float FatteningMargin
    {
        get => field;
        init => field = value >= 0f ? value : ThrowHelper.ThrowNegativeMargin();
    } = 0.1f;

    /// <summary>
    /// Scale applied to <see cref="FatteningMargin"/> using the speed of the item, so a
    /// fast body buys enough slack to cover several frames of travel. Zero is allowed
    /// and disables the speed-scaled term.
    /// </summary>
    public float VelocityMultiplier
    {
        get => field;
        init => field = value >= 0f ? value : ThrowHelper.ThrowNegativeMultiplier();
    } = 2.0f;

    /// <summary>Slots each leaf node reserves before the node is split.</summary>
    public TreeCapacity InitialCapacity
    {
        get => field;
        init => field = value;
    } = new(256);
}
