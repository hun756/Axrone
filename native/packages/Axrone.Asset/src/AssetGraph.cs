namespace Axrone.Asset;

/// <summary>Dependency graph utilities for cascade deletes.</summary>
public static class AssetDependencyGraph
{
    /// <summary>Orders a deletion cascade, dependents before dependencies.</summary>
    public static void TopologicallySortForDelete(
        AssetId root,
        IReadOnlyDictionary<AssetId, HashSet<AssetId>> dependentsMap,
        ICollection<AssetId> executionOrder)
    {
        ArgumentNullException.ThrowIfNull(dependentsMap);
        ArgumentNullException.ThrowIfNull(executionOrder);

        HashSet<AssetId> visited = new();
        HashSet<AssetId> stack = new();
        Visit(root);

        void Visit(AssetId current)
        {
            if (stack.Contains(current))
            {
                ThrowHelper.ThrowConflictError(
                    "Cyclic dependency detected during cascade calculation.",
                    "dependency.cycle");
            }

            if (!visited.Add(current))
            {
                return;
            }

            stack.Add(current);
            if (dependentsMap.TryGetValue(current, out HashSet<AssetId>? dependents))
            {
                foreach (AssetId dependent in dependents)
                {
                    Visit(dependent);
                }
            }

            stack.Remove(current);
            executionOrder.Add(current);
        }
    }
}
