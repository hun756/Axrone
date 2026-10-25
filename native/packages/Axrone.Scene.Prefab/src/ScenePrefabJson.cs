namespace Axrone.Scene.Prefab;

using System.Buffers;

/// <summary>
/// Deterministic JSON codec for prefab definitions. The format is versioned and
/// uses only explicit property names, so Native AOT never needs reflection.
/// </summary>
public static class ScenePrefabJson
{
    /// <summary>Current wire version.</summary>
    public const int Version = 1;

    /// <summary>Writes a prefab definition.</summary>
    public static void Write(Utf8JsonWriter writer, ScenePrefabDefinition prefab)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(prefab);

        writer.WriteStartObject();
        writer.WriteNumber("version", Version);
        writer.WriteString("id", prefab.Id);
        writer.WriteString("kind", KindToString(prefab.Kind));

        writer.WriteStartArray("actors");
        for (int i = 0; i < prefab.Actors.Count; i++)
        {
            WriteActor(writer, prefab.Actors[i]);
        }

        writer.WriteEndArray();

        if (prefab.Metadata is { } metadata)
        {
            WriteMetadata(writer, metadata);
        }

        writer.WriteEndObject();
    }

    /// <summary>Reads a prefab definition from a JSON object.</summary>
    public static ScenePrefabDefinition Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.Invalid,
                "Prefab JSON root must be an object.");
        }

        int version = root.TryGetProperty("version", out JsonElement versionElement)
            ? versionElement.GetInt32()
            : Version;

        if (version != Version)
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.Invalid,
                $"Unsupported prefab version {version}.");
        }

        string id = GetRequiredString(root, "id");
        ScenePrefabKind kind = root.TryGetProperty("kind", out JsonElement kindElement)
            ? ParseKind(GetString(kindElement, "kind"))
            : ScenePrefabKind.Prefab;

        if (!root.TryGetProperty("actors", out JsonElement actorsElement) ||
            actorsElement.ValueKind != JsonValueKind.Array)
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.Invalid,
                $"Prefab '{id}' requires an actors array.");
        }

        var actors = new List<SceneActorSnapshot>(actorsElement.GetArrayLength());
        foreach (JsonElement actorElement in actorsElement.EnumerateArray())
        {
            actors.Add(ReadActor(actorElement));
        }

        ScenePrefabMetadata? metadata = null;
        if (root.TryGetProperty("metadata", out JsonElement metadataElement) &&
            metadataElement.ValueKind == JsonValueKind.Object)
        {
            metadata = ReadMetadata(metadataElement);
        }

        return new ScenePrefabDefinition(id, actors, kind, metadata);
    }

    /// <summary>Serializes a prefab to UTF-8 JSON.</summary>
    public static byte[] Serialize(ScenePrefabDefinition prefab)
    {
        ArgumentNullException.ThrowIfNull(prefab);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            Write(writer, prefab);
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Deserializes a prefab from UTF-8 JSON.</summary>
    public static ScenePrefabDefinition Deserialize(ReadOnlySpan<byte> utf8)
    {
        if (utf8.IsEmpty)
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.Invalid,
                "Prefab JSON payload must not be empty.");
        }

        using JsonDocument document = JsonDocument.Parse(utf8.ToArray());
        return Read(document.RootElement);
    }

    private static void WriteActor(Utf8JsonWriter writer, SceneActorSnapshot actor)
    {
        writer.WriteStartObject();
        writer.WriteString("nodeId", actor.NodeId);

        if (actor.ParentNodeId is { } parentNodeId)
        {
            writer.WriteString("parentNodeId", parentNodeId);
        }
        else
        {
            writer.WriteNull("parentNodeId");
        }

        writer.WriteString("name", actor.Name);
        writer.WriteNumber("layer", actor.Layer);
        writer.WriteString("tag", actor.Tag);
        writer.WriteBoolean("active", actor.Active);
        writer.WriteBoolean("persistent", actor.Persistent);
        writer.WriteBoolean("pooled", actor.Pooled);

        writer.WriteStartArray("components");
        for (int i = 0; i < actor.Components.Count; i++)
        {
            WriteComponent(writer, actor.Components[i]);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteComponent(Utf8JsonWriter writer, SceneComponentSnapshot component)
    {
        writer.WriteStartObject();
        writer.WriteString("type", component.Type);

        if (component.Id is { } id)
        {
            writer.WriteString("id", id);
        }

        writer.WritePropertyName("data");
        component.Data.WriteTo(writer);
        writer.WriteEndObject();
    }

    private static void WriteMetadata(Utf8JsonWriter writer, ScenePrefabMetadata metadata)
    {
        writer.WriteStartObject("metadata");

        if (metadata.Revision is { } revision)
        {
            writer.WriteString("revision", revision);
        }

        if (metadata.Locale is { } locale)
        {
            writer.WriteString("locale", locale);
        }

        if (metadata.UpdatedAt is { } updatedAt)
        {
            writer.WriteString("updatedAt", updatedAt);
        }

        if (metadata.TimeZone is { } timeZone)
        {
            writer.WriteString("timeZone", timeZone);
        }

        if (metadata.Tags.Count > 0)
        {
            writer.WriteStartArray("tags");
            for (int i = 0; i < metadata.Tags.Count; i++)
            {
                writer.WriteStringValue(metadata.Tags[i]);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static SceneActorSnapshot ReadActor(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.Invalid,
                "Actor snapshots must be JSON objects.");
        }

        string nodeId = GetRequiredString(element, "nodeId");
        string? parentNodeId = GetOptionalString(element, "parentNodeId");
        string name = GetRequiredString(element, "name");
        ushort layer = GetOptionalUInt16(element, "layer");
        string tag = element.TryGetProperty("tag", out JsonElement tagElement)
            ? GetString(tagElement, "tag")
            : "Default";
        bool active = GetOptionalBoolean(element, "active", true);
        bool persistent = GetOptionalBoolean(element, "persistent", false);
        bool pooled = GetOptionalBoolean(element, "pooled", false);

        if (!element.TryGetProperty("components", out JsonElement componentsElement) ||
            componentsElement.ValueKind != JsonValueKind.Array)
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.Invalid,
                $"Actor '{nodeId}' requires a components array.",
                nodeId);
        }

        var components = new List<SceneComponentSnapshot>(componentsElement.GetArrayLength());
        foreach (JsonElement componentElement in componentsElement.EnumerateArray())
        {
            components.Add(ReadComponent(componentElement, nodeId));
        }

        return new SceneActorSnapshot(
            nodeId,
            name,
            layer,
            tag,
            active,
            persistent,
            pooled,
            components,
            parentNodeId);
    }

    private static SceneComponentSnapshot ReadComponent(JsonElement element, string nodeId)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.Invalid,
                "Component snapshots must be JSON objects.",
                nodeId);
        }

        string type = GetRequiredString(element, "type");
        if (!element.TryGetProperty("data", out JsonElement data) ||
            data.ValueKind != JsonValueKind.Object)
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.Invalid,
                $"Component '{type}' requires an object payload.",
                nodeId);
        }

        string? id = GetOptionalString(element, "id");
        return new SceneComponentSnapshot(type, data, id);
    }

    private static ScenePrefabMetadata ReadMetadata(JsonElement element) => new()
    {
        Revision = GetOptionalString(element, "revision"),
        Locale = GetOptionalString(element, "locale"),
        UpdatedAt = GetOptionalString(element, "updatedAt"),
        TimeZone = GetOptionalString(element, "timeZone"),
        Tags = ReadTags(element)
    };

    private static string[] ReadTags(JsonElement element)
    {
        if (!element.TryGetProperty("tags", out JsonElement tags) ||
            tags.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        string[] result = new string[tags.GetArrayLength()];
        int index = 0;
        foreach (JsonElement tag in tags.EnumerateArray())
        {
            result[index++] = tag.GetString() ?? string.Empty;
        }

        return result;
    }

    private static string GetRequiredString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.Invalid,
                $"Required property '{name}' is missing.");
        }

        return GetString(value, name);
    }

    private static string GetString(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.Invalid,
                $"Property '{name}' must be a string.");
        }

        return value.GetString() ?? string.Empty;
    }

    private static string? GetOptionalString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value) ||
            value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return GetString(value, name);
    }

    private static bool GetOptionalBoolean(JsonElement element, string name, bool fallback) =>
        element.TryGetProperty(name, out JsonElement value)
            ? value.GetBoolean()
            : fallback;

    private static ushort GetOptionalUInt16(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
        {
            return 0;
        }

        int number = value.GetInt32();
        if (number is < 0 or > ushort.MaxValue)
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.Invalid,
                $"Property '{name}' is outside the 16-bit layer range.");
        }

        return (ushort)number;
    }

    private static string KindToString(ScenePrefabKind kind) => kind switch
    {
        ScenePrefabKind.Variant => "variant",
        ScenePrefabKind.Resolved => "resolved",
        _ => "prefab"
    };

    private static ScenePrefabKind ParseKind(string value) => value switch
    {
        "variant" => ScenePrefabKind.Variant,
        "resolved" => ScenePrefabKind.Resolved,
        _ => ScenePrefabKind.Prefab
    };
}
