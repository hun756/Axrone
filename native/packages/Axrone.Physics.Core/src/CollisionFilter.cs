namespace Axrone.Physics.Core;

/// <summary>
/// Collision filtering triple: shared group overrides, category/mask pairs
/// otherwise. Two shapes interact exactly when <see cref="ShouldCollide"/>
/// says so, so the broadphase can reject pairs before any geometry runs.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CollisionFilter(uint Category, uint Mask, int Group)
{
    /// <summary>Default filter: default category, collides with everything, no group.</summary>
    public static CollisionFilter Default => new((uint)CollisionCategory.Default, (uint)CollisionCategory.All, 0);

    /// <summary>Determines whether two filters interact.</summary>
    /// <param name="a">The first filter.</param>
    /// <param name="b">The second filter.</param>
    /// <returns>
    /// <see langword="true"/> when a positive shared group unites them, or when no
    /// negative group separates them and the category/mask pairs cross-match.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool ShouldCollide(in CollisionFilter a, in CollisionFilter b)
    {
        if (a.Group != 0 && a.Group == b.Group)
        {
            return a.Group > 0;
        }

        return (a.Mask & b.Category) != 0 && (b.Mask & a.Category) != 0;
    }
}
