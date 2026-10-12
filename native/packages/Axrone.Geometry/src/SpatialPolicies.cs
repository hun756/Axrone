namespace Axrone.Geometry;

/// <summary>
/// Grows a stored box with slack so an item can creep without forcing a
/// structural update. The policy is a static-abstract contract rather than a
/// delegate so the insert path calls straight into the generic instantiation:
/// no closure, no virtual dispatch, no allocation per update.
/// </summary>
public interface IFatExpansionPolicy
{
    /// <summary>Grows <paramref name="bounds"/> by <paramref name="margin"/> on every side.</summary>
    static abstract Aabb3D Expand(in Aabb3D bounds, float margin);

    /// <summary>
    /// Grows <paramref name="bounds"/> by <paramref name="margin"/> and budgets
    /// for <paramref name="displacement"/> scaled by <paramref name="velocityMultiplier"/>,
    /// buying extra slack on the side the item travels toward.
    /// </summary>
    static abstract Aabb3D ExpandWithDisplacement(in Aabb3D bounds, Vec3 displacement, float margin, float velocityMultiplier);
}

/// <summary>
/// Scores insertion candidates by surface area. Grouping cost is the combined
/// area, inheritance is the area the new parent adds over the current node, and
/// descent weighs the growth each child would absorb. Minimising surface area
/// keeps traversals touching the fewest empty volumes.
/// </summary>
public interface ISurfaceAreaHeuristicPolicy
{
    /// <summary>Cost of grouping two boxes under one parent.</summary>
    static abstract float ComputeCombinedCost(float combinedArea);

    /// <summary>Area cost a new parent adds over the current node.</summary>
    static abstract float ComputeInheritanceCost(float combinedArea, float currentArea);

    /// <summary>Cost of descending into a child rather than stopping here.</summary>
    static abstract float ComputeDescentCost(float leafArea, float childArea, float combinedChildArea, bool isLeaf, float inheritanceCost);
}

/// <summary>
/// Uniform fattening: every side buys the same slack, and a moving item buys
/// extra slack proportional to its scaled travel on the side it moves toward.
/// </summary>
public readonly struct DefaultExpansionPolicy : IFatExpansionPolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Aabb3D Expand(in Aabb3D bounds, float margin) => bounds.Expanded(margin);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Aabb3D ExpandWithDisplacement(in Aabb3D bounds, Vec3 displacement, float margin, float velocityMultiplier)
    {
        Vec3 velocity = displacement * velocityMultiplier;
        Aabb3D fatBox = bounds.Expanded(margin);

        Vec3 min = fatBox.Min;
        Vec3 max = fatBox.Max;

        float minX = velocity.X < 0.0f ? min.X + velocity.X : min.X;
        float maxX = velocity.X < 0.0f ? max.X : max.X + velocity.X;
        float minY = velocity.Y < 0.0f ? min.Y + velocity.Y : min.Y;
        float maxY = velocity.Y < 0.0f ? max.Y : max.Y + velocity.Y;
        float minZ = velocity.Z < 0.0f ? min.Z + velocity.Z : min.Z;
        float maxZ = velocity.Z < 0.0f ? max.Z : max.Z + velocity.Z;

        return new Aabb3D(new Vec3(minX, minY, minZ), new Vec3(maxX, maxY, maxZ));
    }
}

/// <summary>Standard surface-area heuristic weights for tree insertion.</summary>
public readonly struct StandardSAHPolicy : ISurfaceAreaHeuristicPolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ComputeCombinedCost(float combinedArea) => 2.0f * combinedArea;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ComputeInheritanceCost(float combinedArea, float currentArea) => 2.0f * (combinedArea - currentArea);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ComputeDescentCost(float leafArea, float childArea, float combinedChildArea, bool isLeaf, float inheritanceCost)
    {
        if (isLeaf)
        {
            return combinedChildArea + inheritanceCost;
        }

        return (combinedChildArea - childArea) + inheritanceCost;
    }
}
