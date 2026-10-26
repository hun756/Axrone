namespace Axrone.Scene.Prefab;

using System.Buffers;

/// <summary>
/// Builds prefab definitions from a live scene world. Node ids are actor slots,
/// which keeps snapshots stable for the lifetime of one world.
/// </summary>
public static class ScenePrefabFactory
{
    /// <summary>Creates a prefab from every live actor.</summary>
    public static ScenePrefabDefinition Create(
        SceneWorld world,
        SceneCodecRegistry codecs,
        string id,
        ScenePrefabMetadata? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(codecs);

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.Invalid,
                "Prefab definitions require a non-empty id.");
        }

        var actors = new List<SceneActorSnapshot>(world.ActorCount);

        foreach (SceneActor actor in world.Actors)
        {
            string nodeId = actor.Slot.ToString(CultureInfo.InvariantCulture);
            SceneActor parent = world.Hierarchy.ParentOf(actor);
            string? parentNodeId = parent.IsValid
                ? parent.Slot.ToString(CultureInfo.InvariantCulture)
                : null;

            SceneComponentMask mask = world.GetComponentMask(actor);
            var components = new List<SceneComponentSnapshot>(codecs.Count);
            ReadOnlySpan<SceneCodecRegistration> registrations = codecs.Registrations;

            for (int i = 0; i < registrations.Length; i++)
            {
                ref readonly SceneCodecRegistration registration = ref registrations[i];
                if (!mask.IsSet(registration.Descriptor.TypeId))
                {
                    continue;
                }

                string typeName = world.Names.Resolve(registration.Descriptor.Name);
                JsonElement data = CaptureComponent(world, actor, registration.Codec);
                components.Add(new SceneComponentSnapshot(typeName, data));
            }

            components.Sort(static (left, right) =>
                string.CompareOrdinal(left.Type, right.Type));

            actors.Add(new SceneActorSnapshot(
                nodeId,
                world.GetName(actor),
                world.LayerOf(actor),
                world.GetTag(actor),
                world.IsActiveSelf(actor),
                world.IsPersistent(actor),
                world.IsPooled(actor),
                components,
                parentNodeId));
        }

        return new ScenePrefabDefinition(id, actors, ScenePrefabKind.Prefab, metadata);
    }

    private static JsonElement CaptureComponent(
        SceneWorld world,
        SceneActor actor,
        ISceneDataCodec codec)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            codec.Write(world, actor, writer);
            writer.WriteEndObject();
        }

        using JsonDocument document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}
