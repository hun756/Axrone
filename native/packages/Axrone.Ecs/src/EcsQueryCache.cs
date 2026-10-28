namespace Axrone.Ecs;

/// <summary>
/// Cache key for one archetype query: every required bit and every excluded bit.
/// </summary>
internal readonly record struct QueryKey(ComponentMask All, ComponentMask None);

/// <summary>
/// Lock-free reader cache of archetype indices per query mask. The graph version is the
/// invalidation token: a new archetype publishes a new version, and the next query
/// rebuilds an immutable snapshot. Hits take no lock and allocate nothing.
/// </summary>
internal sealed class ArchetypeQueryCache
{
    private readonly Lock _gate = new();
    private CacheSnapshot _snapshot = new(-1, new Dictionary<QueryKey, int[]>());

    /// <summary>Resolves the matching archetype indices for a mask pair.</summary>
    public int[] Resolve(ComponentMask all, ComponentMask none, ArchetypeGraph graph)
    {
        int version = graph.Version;
        var key = new QueryKey(all, none);
        CacheSnapshot snapshot = Volatile.Read(ref _snapshot);
        if (snapshot.Version == version && snapshot.Entries.TryGetValue(key, out int[]? cached))
        {
            return cached;
        }

        lock (_gate)
        {
            snapshot = _snapshot;
            if (snapshot.Version != version)
            {
                snapshot = new CacheSnapshot(version, new Dictionary<QueryKey, int[]>());
            }
            else if (snapshot.Entries.TryGetValue(key, out cached))
            {
                return cached;
            }

            Dictionary<QueryKey, int[]> entries = new(snapshot.Entries)
            {
                [key] = Build(graph, in all, in none),
            };
            var next = new CacheSnapshot(version, entries);
            Volatile.Write(ref _snapshot, next);
            return next.Entries[key];
        }
    }

    private static int[] Build(ArchetypeGraph graph, in ComponentMask all, in ComponentMask none)
    {
        ReadOnlySpan<Archetype> archetypes = graph.AsSpan();
        var matches = new List<int>(archetypes.Length);
        for (int index = 0; index < archetypes.Length; index++)
        {
            Archetype archetype = archetypes[index];
            if (archetype.Mask.ContainsAll(in all) && !archetype.Mask.ContainsAny(in none))
            {
                matches.Add(index);
            }
        }

        return matches.ToArray();
    }

    private sealed class CacheSnapshot
    {
        public readonly int Version;
        public readonly Dictionary<QueryKey, int[]> Entries;

        public CacheSnapshot(int version, Dictionary<QueryKey, int[]> entries)
        {
            Version = version;
            Entries = entries;
        }
    }
}
