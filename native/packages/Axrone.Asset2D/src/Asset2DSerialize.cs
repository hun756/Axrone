namespace Axrone.Asset2D;

/// <summary>Capability identity for the 2D asset package.</summary>
public static class Asset2DCapabilities
{
    /// <summary>Stable cross-runtime capability id.</summary>
    public const string Id = "asset/2d";

    /// <summary>Owning native package.</summary>
    public const string Package = "Axrone.Asset2D";

    /// <summary>Core package this capability extends.</summary>
    public const string OwnerPackage = "Axrone.Asset";
}

/// <summary>Canonical JSON round-trip for sprite atlases (web-interop shape).</summary>
public static class SpriteAtlasSerializer
{
    /// <summary>Serializes an atlas to canonical JSON.</summary>
    public static string SerializeToJson<TCoordPolicy>(SpriteAtlas<TCoordPolicy> atlas)
        where TCoordPolicy : struct, ICoordinatePolicy
    {
        ArgumentNullException.ThrowIfNull(atlas);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("id", atlas.Id.Value);
            writer.WriteString("textureId", atlas.TextureId.Value);
            writer.WriteStartObject("textureSize");
            writer.WriteNumber("width", atlas.TextureSize.Width.Value);
            writer.WriteNumber("height", atlas.TextureSize.Height.Value);
            writer.WriteEndObject();

            writer.WriteStartArray("frames");
            foreach (ref readonly SpriteAtlasFrame frame in atlas)
            {
                writer.WriteStartObject();
                writer.WriteString("id", frame.Id.Value);
                writer.WriteStartObject("region");
                writer.WriteNumber("x", frame.Region.X.Value);
                writer.WriteNumber("y", frame.Region.Y.Value);
                writer.WriteNumber("width", frame.Region.Width.Value);
                writer.WriteNumber("height", frame.Region.Height.Value);
                writer.WriteEndObject();
                writer.WriteStartObject("sourceSize");
                writer.WriteNumber("width", frame.SourceSize.Width.Value);
                writer.WriteNumber("height", frame.SourceSize.Height.Value);
                writer.WriteEndObject();
                writer.WriteStartObject("pivot");
                writer.WriteNumber("x", frame.Pivot.X);
                writer.WriteNumber("y", frame.Pivot.Y);
                writer.WriteEndObject();
                if (frame.HasBorder)
                {
                    writer.WriteStartObject("sliceBorder");
                    writer.WriteNumber("left", frame.SliceBorder.Left.Value);
                    writer.WriteNumber("right", frame.SliceBorder.Right.Value);
                    writer.WriteNumber("top", frame.SliceBorder.Top.Value);
                    writer.WriteNumber("bottom", frame.SliceBorder.Bottom.Value);
                    writer.WriteEndObject();
                }

                if (frame.HasDuration)
                {
                    writer.WriteNumber("durationMs", frame.Duration.Value);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteStartArray("animations");
            foreach (SpriteAnimationClip clip in atlas.EnumerateAnimations())
            {
                writer.WriteStartObject();
                writer.WriteString("id", clip.Id.Value);
                writer.WriteBoolean("loop", clip.Loop);
                writer.WriteStartArray("frames");
                foreach (ref readonly SpriteAnimationFrame key in clip)
                {
                    writer.WriteStartObject();
                    writer.WriteString("frameId", key.Frame.Id.Value);
                    writer.WriteNumber("durationMs", key.Duration.Value);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>Deserializes canonical JSON through the atlas importer.</summary>
    public static Asset2DImportResult<TCoordPolicy> DeserializeFromJson<TCoordPolicy>(string path, string json)
        where TCoordPolicy : struct, ICoordinatePolicy
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(json);

        byte[] bytes = Encoding.UTF8.GetBytes(json);
        AssetImportContext context = new(path.AsSpan(), bytes);
        return SpriteAtlasJsonImporter<TCoordPolicy>.Import(in context);
    }
}
