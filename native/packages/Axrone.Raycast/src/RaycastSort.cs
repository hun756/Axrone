namespace Axrone.Raycast;

/// <summary>
/// In-place insertion sorts for collected contacts. Insertion sort is stable,
/// allocation-free and optimal for the small hit counts a ray routinely produces, so it
/// preserves the deterministic candidate order for equidistant contacts.
/// </summary>
internal static class RaycastSort
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Sort2D(Span<RaycastHit2D> hits)
    {
        for (var index = 1; index < hits.Length; index++)
        {
            RaycastHit2D key = hits[index];
            var position = index - 1;
            while (position >= 0 && hits[position].Distance > key.Distance)
            {
                hits[position + 1] = hits[position];
                position--;
            }

            hits[position + 1] = key;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void Sort3D(Span<RaycastHit3D> hits)
    {
        for (var index = 1; index < hits.Length; index++)
        {
            RaycastHit3D key = hits[index];
            var position = index - 1;
            while (position >= 0 && hits[position].Distance > key.Distance)
            {
                hits[position + 1] = hits[position];
                position--;
            }

            hits[position + 1] = key;
        }
    }
}
