namespace Axrone.Scene.Prefab;

/// <summary>Built-in core component codecs for prefab snapshots.</summary>
public static class ScenePrefabCodecs
{
    /// <summary>Registers codecs for the transform components present in a registry.</summary>
    public static void RegisterCore(SceneCodecRegistry codecs, SceneComponentRegistry components)
    {
        ArgumentNullException.ThrowIfNull(codecs);
        ArgumentNullException.ThrowIfNull(components);

        if (components.TryGetDataByType(typeof(SceneTransform3D), out _))
        {
            codecs.Register(new SceneTransform3DCodec());
        }

        if (components.TryGetDataByType(typeof(SceneTransform2D), out _))
        {
            codecs.Register(new SceneTransform2DCodec());
        }
    }
}

/// <summary>JSON codec for <see cref="SceneTransform3D"/>.</summary>
public sealed class SceneTransform3DCodec : SceneDataCodec<SceneTransform3D>
{
    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, in SceneTransform3D value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        WriteVec3(writer, "position", value.Position);
        WriteQuat(writer, "rotation", value.Rotation);
        WriteVec3(writer, "scale", value.Scale);
    }

    /// <inheritdoc/>
    public override SceneTransform3D Read(JsonElement data)
    {
        SceneTransform3D transform = SceneTransform3D.Identity;

        if (data.TryGetProperty("position", out JsonElement position))
        {
            transform.Position = ReadVec3(position, "position");
        }

        if (data.TryGetProperty("rotation", out JsonElement rotation))
        {
            transform.Rotation = ReadQuat(rotation);
        }

        if (data.TryGetProperty("scale", out JsonElement scale))
        {
            transform.Scale = ReadVec3(scale, "scale");
        }

        transform.MarkLocalDirty();
        return transform;
    }

    internal static void WriteVec3(Utf8JsonWriter writer, string name, in Vec3 value)
    {
        writer.WriteStartArray(name);
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteNumberValue(value.Z);
        writer.WriteEndArray();
    }

    internal static void WriteQuat(Utf8JsonWriter writer, string name, in Quat value)
    {
        writer.WriteStartArray(name);
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteNumberValue(value.Z);
        writer.WriteNumberValue(value.W);
        writer.WriteEndArray();
    }

    internal static Vec3 ReadVec3(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() < 3)
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.Invalid,
                $"Transform '{name}' must be a three-component array.");
        }

        return new Vec3(
            element[0].GetSingle(),
            element[1].GetSingle(),
            element[2].GetSingle());
    }

    internal static Quat ReadQuat(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() < 4)
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.Invalid,
                "Transform 'rotation' must be a four-component array.");
        }

        return new Quat(
            element[0].GetSingle(),
            element[1].GetSingle(),
            element[2].GetSingle(),
            element[3].GetSingle());
    }
}

/// <summary>JSON codec for <see cref="SceneTransform2D"/>.</summary>
public sealed class SceneTransform2DCodec : SceneDataCodec<SceneTransform2D>
{
    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, in SceneTransform2D value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStartArray("position");
        writer.WriteNumberValue(value.Position.X);
        writer.WriteNumberValue(value.Position.Y);
        writer.WriteEndArray();
        writer.WriteNumber("rotationRadians", value.RotationRadians);
        writer.WriteStartArray("scale");
        writer.WriteNumberValue(value.Scale.X);
        writer.WriteNumberValue(value.Scale.Y);
        writer.WriteEndArray();
    }

    /// <inheritdoc/>
    public override SceneTransform2D Read(JsonElement data)
    {
        SceneTransform2D transform = SceneTransform2D.Identity;

        if (data.TryGetProperty("position", out JsonElement position))
        {
            transform.Position = ReadVec2(position, "position");
        }

        if (data.TryGetProperty("rotationRadians", out JsonElement rotation))
        {
            transform.RotationRadians = rotation.GetSingle();
        }

        if (data.TryGetProperty("scale", out JsonElement scale))
        {
            transform.Scale = ReadVec2(scale, "scale");
        }

        transform.MarkLocalDirty();
        return transform;
    }

    private static Vec2 ReadVec2(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() < 2)
        {
            throw new ScenePrefabException(
                ScenePrefabErrorCodes.Invalid,
                $"Transform '{name}' must be a two-component array.");
        }

        return new Vec2(element[0].GetSingle(), element[1].GetSingle());
    }
}
