namespace Axrone.Scene.Prefab;

/// <summary>
/// Two-phase prefab instantiation: actors and hierarchy first, components
/// second. Any failure rolls the created actors back before a coded prefab
/// error is thrown.
/// </summary>
public static class ScenePrefabInstantiator
{
    /// <summary>Instantiates a prefab into a scene world.</summary>
    public static ScenePrefabInstantiationResult Instantiate(
        SceneWorld world,
        SceneCodecRegistry codecs,
        ScenePrefabDefinition prefab,
        ScenePrefabInstantiateOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(codecs);
        ArgumentNullException.ThrowIfNull(prefab);

        var created = new List<SceneActor>(prefab.Actors.Count);
        var nodeMap = new Dictionary<string, SceneActor>(prefab.Actors.Count, StringComparer.Ordinal);

        try
        {
            for (int i = 0; i < prefab.Actors.Count; i++)
            {
                SceneActorSnapshot snapshot = prefab.Actors[i];
                string name = options?.NamePrefix is { Length: > 0 } prefix
                    ? string.Concat(prefix, snapshot.Name)
                    : snapshot.Name;

                SceneActor actor = world.CreateActor(new SceneActorConfig(
                    name,
                    snapshot.Tag,
                    snapshot.Layer,
                    snapshot.Active,
                    snapshot.Persistent,
                    snapshot.Pooled));

                created.Add(actor);
                nodeMap.Add(snapshot.NodeId, actor);
            }

            for (int i = 0; i < prefab.Actors.Count; i++)
            {
                SceneActorSnapshot snapshot = prefab.Actors[i];
                if (snapshot.ParentNodeId is not { } parentNodeId)
                {
                    continue;
                }

                if (!nodeMap.TryGetValue(parentNodeId, out SceneActor parent))
                {
                    throw new ScenePrefabException(
                        ScenePrefabErrorCodes.MissingParent,
                        $"Parent node '{parentNodeId}' was not created.",
                        snapshot.NodeId);
                }

                world.Hierarchy.SetParent(nodeMap[snapshot.NodeId], parent);
            }

            for (int i = 0; i < prefab.Actors.Count; i++)
            {
                SceneActorSnapshot snapshot = prefab.Actors[i];
                SceneActor actor = nodeMap[snapshot.NodeId];

                for (int componentIndex = 0; componentIndex < snapshot.Components.Count; componentIndex++)
                {
                    SceneComponentSnapshot component = snapshot.Components[componentIndex];
                    if (!codecs.TryGet(component.Type, out ISceneDataCodec? codec))
                    {
                        throw new ScenePrefabException(
                            ScenePrefabErrorCodes.UnknownComponent,
                            $"No codec is registered for component '{component.Type}'.",
                            snapshot.NodeId);
                    }

                    codec.ReadInto(world, actor, component.Data);
                }
            }

            return new ScenePrefabInstantiationResult(created, nodeMap);
        }
        catch (Exception exception)
        {
            Rollback(world, created);

            if (exception is ScenePrefabException)
            {
                throw;
            }

            throw new ScenePrefabException(
                ScenePrefabErrorCodes.InstantiationFailed,
                "Prefab instantiation failed and was rolled back.",
                inner: exception);
        }
    }

    private static void Rollback(SceneWorld world, List<SceneActor> created)
    {
        for (int i = created.Count - 1; i >= 0; i--)
        {
            try
            {
                world.DestroyActor(created[i]);
            }
            catch
            {
                // Rollback must continue through every actor; the original
                // instantiation failure remains the reported cause.
            }
        }

        created.Clear();
    }
}
