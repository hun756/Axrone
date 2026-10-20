namespace Axrone.Asset2D;

/// <summary>Zero-allocation import input: path plus content.</summary>
public readonly ref struct AssetImportContext
{
    /// <summary>Source path.</summary>
    public ReadOnlySpan<char> Path { get; }

    /// <summary>Source content.</summary>
    public ReadOnlySpan<byte> Content { get; }

    /// <summary>Creates a context.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AssetImportContext(ReadOnlySpan<char> path, ReadOnlySpan<byte> content)
    {
        Path = path;
        Content = content;
    }
}

/// <summary>Visitor over the import result union.</summary>
public interface IImportResultVisitor<TCoordPolicy, out T>
    where TCoordPolicy : struct, ICoordinatePolicy
{
    /// <summary>Visits a successful atlas.</summary>
    T OnAtlas(SpriteAtlas<TCoordPolicy> atlas);

    /// <summary>Visits a failure.</summary>
    T OnFailure(string message);
}

/// <summary>
/// Zero-allocation discriminated union representing polymorphic outcome of an asset import operation.
/// </summary>
/// <typeparam name="TCoordPolicy">UV coordinate policy.</typeparam>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Asset2DImportResult<TCoordPolicy>
    where TCoordPolicy : struct, ICoordinatePolicy
{
    /// <summary>Result discriminant.</summary>
    public enum ResultKind : byte
    {
        /// <summary>Import failed.</summary>
        Failure = 0,

        /// <summary>Atlas imported.</summary>
        Atlas = 1,
    }

    /// <summary>Discriminant.</summary>
    public ResultKind Kind { get; }

    /// <summary>Imported atlas.</summary>
    public SpriteAtlas<TCoordPolicy>? Atlas { get; }

    /// <summary>Failure message.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Whether the import succeeded.</summary>
    [MemberNotNullWhen(true, nameof(Atlas))]
    public bool IsSuccess => Kind == ResultKind.Atlas && Atlas is not null;

    private Asset2DImportResult(ResultKind kind, SpriteAtlas<TCoordPolicy>? atlas, string? errorMessage)
    {
        Kind = kind;
        Atlas = atlas;
        ErrorMessage = errorMessage;
    }

    /// <summary>Creates a success.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Asset2DImportResult<TCoordPolicy> Success(SpriteAtlas<TCoordPolicy> atlas) =>
        new(ResultKind.Atlas, atlas, null);

    /// <summary>Creates a failure.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Asset2DImportResult<TCoordPolicy> Failure(string message) =>
        new(ResultKind.Failure, null, message);

    /// <summary>Dispatches on the discriminant.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Match<T, TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IImportResultVisitor<TCoordPolicy, T>, allows ref struct
    {
        return Kind switch
        {
            ResultKind.Atlas when Atlas is not null => visitor.OnAtlas(Atlas),
            ResultKind.Failure => visitor.OnFailure(ErrorMessage ?? "Unknown failure"),
            _ => throw new InvalidOperationException($"Invalid discriminated union kind for {typeof(T).FullName}."),
        };
    }
}

/// <summary>Importer policy contract: match, then import.</summary>
public interface IAssetImporterPolicy<TCoordPolicy>
    where TCoordPolicy : struct, ICoordinatePolicy
{
    /// <summary>Importer identity.</summary>
    static abstract string ImporterId { get; }

    /// <summary>Priority; higher wins.</summary>
    static abstract int Priority { get; }

    /// <summary>Whether this importer accepts the source.</summary>
    static abstract bool CanImport(in AssetImportContext context);

    /// <summary>Imports the source.</summary>
    static abstract Asset2DImportResult<TCoordPolicy> Import(in AssetImportContext context);
}

/// <summary>
/// Monomorphized zero-vtable importer chain dispatcher.
/// </summary>
public readonly struct ImporterChain<TPrimary, TSecondary, TCoordPolicy>
    where TPrimary : struct, IAssetImporterPolicy<TCoordPolicy>
    where TSecondary : struct, IAssetImporterPolicy<TCoordPolicy>
    where TCoordPolicy : struct, ICoordinatePolicy
{
    /// <summary>Runs the chain: primary first, then secondary.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Asset2DImportResult<TCoordPolicy> Import(in AssetImportContext context)
    {
        if (TPrimary.CanImport(in context))
        {
            return TPrimary.Import(in context);
        }

        if (TSecondary.CanImport(in context))
        {
            return TSecondary.Import(in context);
        }

        return Asset2DImportResult<TCoordPolicy>.Failure(
            $"No registered importer could process asset at: '{context.Path.ToString()}'");
    }
}

/// <summary>
/// High-performance Utf8JsonReader canonical sprite atlas importer (Native AOT Safe, zero dynamic reflection).
/// </summary>
public readonly struct SpriteAtlasJsonImporter<TCoordPolicy> : IAssetImporterPolicy<TCoordPolicy>
    where TCoordPolicy : struct, ICoordinatePolicy
{
    /// <inheritdoc/>
    public static string ImporterId => "asset-2d.sprite-atlas.json";

    /// <inheritdoc/>
    public static int Priority => 20;

    /// <inheritdoc/>
    public static bool CanImport(in AssetImportContext context)
    {
        ReadOnlySpan<char> path = context.Path;
        if (path.EndsWith(".spriteatlas.json", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".atlas.json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return IsCanonicalJson(context.Content);
        }

        return false;
    }

    /// <inheritdoc/>
    public static Asset2DImportResult<TCoordPolicy> Import(in AssetImportContext context)
    {
        try
        {
            string? atlasId = null;
            string? textureId = null;
            Size2D? textureSize = null;
            var framesList = new List<SpriteFrameDefinition>(32);
            var animationsList = new List<SpriteClipDefinition>(8);

            var reader = new Utf8JsonReader(context.Content);

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    if (reader.ValueTextEquals("id"u8))
                    {
                        reader.Read();
                        atlasId = reader.GetString();
                    }
                    else if (reader.ValueTextEquals("textureId"u8))
                    {
                        reader.Read();
                        textureId = reader.GetString();
                    }
                    else if (reader.ValueTextEquals("textureSize"u8))
                    {
                        reader.Read();
                        float w = 0f;
                        float h = 0f;
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                        {
                            if (reader.TokenType == JsonTokenType.PropertyName)
                            {
                                if (reader.ValueTextEquals("width"u8) || reader.ValueTextEquals("w"u8))
                                {
                                    reader.Read();
                                    w = reader.GetSingle();
                                }
                                else if (reader.ValueTextEquals("height"u8) || reader.ValueTextEquals("h"u8))
                                {
                                    reader.Read();
                                    h = reader.GetSingle();
                                }
                                else
                                {
                                    reader.Skip();
                                }
                            }
                        }

                        textureSize = new Size2D(w, h);
                    }
                    else if (reader.ValueTextEquals("frames"u8))
                    {
                        reader.Read();
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {
                            if (reader.TokenType == JsonTokenType.StartObject)
                            {
                                framesList.Add(ParseFrame(ref reader));
                            }
                        }
                    }
                    else if (reader.ValueTextEquals("animations"u8))
                    {
                        reader.Read();
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {
                            if (reader.TokenType == JsonTokenType.StartObject)
                            {
                                animationsList.Add(ParseClip(ref reader));
                            }
                        }
                    }
                }
            }

            atlasId ??= new string(SpriteAtlasUtil.DeriveAtlasId(context.Path));
            textureId ??= $"{atlasId}.png";

            if (!textureSize.HasValue)
            {
                return Asset2DImportResult<TCoordPolicy>.Failure("Missing textureSize section in JSON.");
            }

            AtlasBuilder<AtlasBuildStage.Configured, TCoordPolicy> configured = AtlasBuilder.Create<TCoordPolicy>()
                .WithIdentity(new AtlasId(atlasId), new TextureId(textureId), textureSize.Value);

            if (framesList.Count == 0)
            {
                return Asset2DImportResult<TCoordPolicy>.Failure("Atlas contains no frames.");
            }

            Span<SpriteFrameDefinition> framesSpan = CollectionsMarshal.AsSpan(framesList);
            AtlasBuilder<AtlasBuildStage.FramesDefined, TCoordPolicy> builder = configured.AddFrame(in framesSpan[0]);
            for (int i = 1; i < framesSpan.Length; i++)
            {
                builder = builder.AddFrame(in framesSpan[i]);
            }

            Span<SpriteClipDefinition> animSpan = CollectionsMarshal.AsSpan(animationsList);
            for (int i = 0; i < animSpan.Length; i++)
            {
                builder = builder.AddAnimation(in animSpan[i]);
            }

            return Asset2DImportResult<TCoordPolicy>.Success(builder.Build());
        }
        catch (Exception ex)
        {
            return Asset2DImportResult<TCoordPolicy>.Failure(ex.Message);
        }
    }

    private static SpriteFrameDefinition ParseFrame(ref Utf8JsonReader reader)
    {
        string frameId = string.Empty;
        Rect2D region = Rect2D.Zero;
        Size2D? sourceSize = null;
        Vec2? pivot = null;
        Border2D? sliceBorder = null;
        Milliseconds? duration = null;
        bool rotated = false;
        Vec2? trimOffset = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals("id"u8))
                {
                    reader.Read();
                    frameId = reader.GetString() ?? string.Empty;
                }
                else if (reader.ValueTextEquals("region"u8))
                {
                    reader.Read();
                    region = ParseRect(ref reader);
                }
                else if (reader.ValueTextEquals("sourceSize"u8))
                {
                    reader.Read();
                    sourceSize = ParseSize(ref reader);
                }
                else if (reader.ValueTextEquals("pivot"u8))
                {
                    reader.Read();
                    pivot = ParseVec2(ref reader);
                }
                else if (reader.ValueTextEquals("sliceBorder"u8))
                {
                    reader.Read();
                    sliceBorder = ParseBorder(ref reader);
                }
                else if (reader.ValueTextEquals("durationMs"u8) || reader.ValueTextEquals("duration"u8))
                {
                    reader.Read();
                    duration = new Milliseconds(reader.GetSingle());
                }
                else if (reader.ValueTextEquals("rotated"u8))
                {
                    reader.Read();
                    rotated = reader.GetBoolean();
                }
                else if (reader.ValueTextEquals("trimOffset"u8))
                {
                    reader.Read();
                    trimOffset = ParseVec2(ref reader);
                }
                else
                {
                    reader.Skip();
                }
            }
        }

        return new SpriteFrameDefinition
        {
            Id = new FrameId(frameId),
            Region = region,
            SourceSize = sourceSize,
            Pivot = pivot,
            SliceBorder = sliceBorder,
            Duration = duration,
            Rotated = rotated,
            TrimOffset = trimOffset,
        };
    }

    private static SpriteClipDefinition ParseClip(ref Utf8JsonReader reader)
    {
        string clipId = string.Empty;
        ImmutableArray<SpriteClipRefDefinition>.Builder frameRefs = ImmutableArray.CreateBuilder<SpriteClipRefDefinition>();
        float? fps = null;
        bool? loop = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals("id"u8))
                {
                    reader.Read();
                    clipId = reader.GetString() ?? string.Empty;
                }
                else if (reader.ValueTextEquals("fps"u8))
                {
                    reader.Read();
                    fps = reader.GetSingle();
                }
                else if (reader.ValueTextEquals("loop"u8))
                {
                    reader.Read();
                    loop = reader.GetBoolean();
                }
                else if (reader.ValueTextEquals("frames"u8))
                {
                    reader.Read();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            frameRefs.Add(new SpriteClipRefDefinition(new FrameId(reader.GetString() ?? string.Empty)));
                        }
                        else if (reader.TokenType == JsonTokenType.StartObject)
                        {
                            string refId = string.Empty;
                            Milliseconds? dur = null;
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                            {
                                if (reader.TokenType == JsonTokenType.PropertyName)
                                {
                                    if (reader.ValueTextEquals("frameId"u8))
                                    {
                                        reader.Read();
                                        refId = reader.GetString() ?? string.Empty;
                                    }
                                    else if (reader.ValueTextEquals("durationMs"u8) || reader.ValueTextEquals("duration"u8))
                                    {
                                        reader.Read();
                                        dur = new Milliseconds(reader.GetSingle());
                                    }
                                    else
                                    {
                                        reader.Skip();
                                    }
                                }
                            }

                            frameRefs.Add(new SpriteClipRefDefinition(new FrameId(refId), dur));
                        }
                    }
                }
                else
                {
                    reader.Skip();
                }
            }
        }

        return new SpriteClipDefinition
        {
            Id = new ClipId(clipId),
            Frames = frameRefs.ToImmutable(),
            Fps = fps,
            Loop = loop,
        };
    }

    private static Rect2D ParseRect(ref Utf8JsonReader reader)
    {
        float x = 0f;
        float y = 0f;
        float w = 0f;
        float h = 0f;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals("x"u8)) { reader.Read(); x = reader.GetSingle(); }
                else if (reader.ValueTextEquals("y"u8)) { reader.Read(); y = reader.GetSingle(); }
                else if (reader.ValueTextEquals("width"u8) || reader.ValueTextEquals("w"u8)) { reader.Read(); w = reader.GetSingle(); }
                else if (reader.ValueTextEquals("height"u8) || reader.ValueTextEquals("h"u8)) { reader.Read(); h = reader.GetSingle(); }
                else
                {
                    reader.Skip();
                }
            }
        }

        return new Rect2D(x, y, w, h);
    }

    private static Size2D ParseSize(ref Utf8JsonReader reader)
    {
        float w = 0f;
        float h = 0f;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals("width"u8) || reader.ValueTextEquals("w"u8)) { reader.Read(); w = reader.GetSingle(); }
                else if (reader.ValueTextEquals("height"u8) || reader.ValueTextEquals("h"u8)) { reader.Read(); h = reader.GetSingle(); }
                else
                {
                    reader.Skip();
                }
            }
        }

        return new Size2D(w, h);
    }

    private static Vec2 ParseVec2(ref Utf8JsonReader reader)
    {
        float x = 0f;
        float y = 0f;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals("x"u8)) { reader.Read(); x = reader.GetSingle(); }
                else if (reader.ValueTextEquals("y"u8)) { reader.Read(); y = reader.GetSingle(); }
                else
                {
                    reader.Skip();
                }
            }
        }

        return new Vec2(x, y);
    }

    private static Border2D ParseBorder(ref Utf8JsonReader reader)
    {
        float l = 0f;
        float r = 0f;
        float t = 0f;
        float b = 0f;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals("left"u8) || reader.ValueTextEquals("l"u8)) { reader.Read(); l = reader.GetSingle(); }
                else if (reader.ValueTextEquals("right"u8) || reader.ValueTextEquals("r"u8)) { reader.Read(); r = reader.GetSingle(); }
                else if (reader.ValueTextEquals("top"u8) || reader.ValueTextEquals("t"u8)) { reader.Read(); t = reader.GetSingle(); }
                else if (reader.ValueTextEquals("bottom"u8) || reader.ValueTextEquals("b"u8)) { reader.Read(); b = reader.GetSingle(); }
                else
                {
                    reader.Skip();
                }
            }
        }

        return new Border2D(l, r, t, b);
    }

    private static bool IsCanonicalJson(ReadOnlySpan<byte> utf8)
    {
        try
        {
            var reader = new Utf8JsonReader(utf8);
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("frames"u8))
                {
                    reader.Read();
                    return reader.TokenType == JsonTokenType.StartArray;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}

/// <summary>
/// High-performance TexturePacker format JSON importer (zero DTO allocations, Native AOT safe).
/// </summary>
public readonly struct TexturePackerSpriteAtlasImporter<TCoordPolicy> : IAssetImporterPolicy<TCoordPolicy>
    where TCoordPolicy : struct, ICoordinatePolicy
{
    /// <inheritdoc/>
    public static string ImporterId => "asset-2d.sprite-atlas.texturepacker";

    /// <inheritdoc/>
    public static int Priority => 10;

    /// <inheritdoc/>
    public static bool CanImport(in AssetImportContext context)
    {
        if (!context.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return IsTexturePackerJson(context.Content);
    }

    /// <inheritdoc/>
    public static Asset2DImportResult<TCoordPolicy> Import(in AssetImportContext context)
    {
        try
        {
            var frameList = new List<SpriteFrameDefinition>(64);
            string? metaImage = null;
            Size2D? metaSize = null;

            var frameTags = new List<(string Name, int From, int To, string Direction)>(4);
            var reader = new Utf8JsonReader(context.Content);

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    if (reader.ValueTextEquals("frames"u8))
                    {
                        reader.Read();
                        if (reader.TokenType == JsonTokenType.StartObject)
                        {
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                            {
                                if (reader.TokenType == JsonTokenType.PropertyName)
                                {
                                    string frameKey = reader.GetString()!;
                                    reader.Read();
                                    frameList.Add(ParseTexturePackerFrame(ref reader, frameKey));
                                }
                            }
                        }
                    }
                    else if (reader.ValueTextEquals("meta"u8))
                    {
                        reader.Read();
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                        {
                            if (reader.TokenType == JsonTokenType.PropertyName)
                            {
                                if (reader.ValueTextEquals("image"u8))
                                {
                                    reader.Read();
                                    metaImage = reader.GetString();
                                }
                                else if (reader.ValueTextEquals("size"u8))
                                {
                                    reader.Read();
                                    float w = 0f;
                                    float h = 0f;
                                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                                    {
                                        if (reader.TokenType == JsonTokenType.PropertyName)
                                        {
                                            if (reader.ValueTextEquals("w"u8)) { reader.Read(); w = reader.GetSingle(); }
                                            else if (reader.ValueTextEquals("h"u8)) { reader.Read(); h = reader.GetSingle(); }
                                            else
                                            {
                                                reader.Skip();
                                            }
                                        }
                                    }

                                    metaSize = new Size2D(w, h);
                                }
                                else if (reader.ValueTextEquals("frameTags"u8))
                                {
                                    reader.Read();
                                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                                    {
                                        if (reader.TokenType == JsonTokenType.StartObject)
                                        {
                                            string name = string.Empty;
                                            int from = 0;
                                            int to = 0;
                                            string dir = "forward";
                                            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                                            {
                                                if (reader.TokenType == JsonTokenType.PropertyName)
                                                {
                                                    if (reader.ValueTextEquals("name"u8)) { reader.Read(); name = reader.GetString() ?? string.Empty; }
                                                    else if (reader.ValueTextEquals("from"u8)) { reader.Read(); from = reader.GetInt32(); }
                                                    else if (reader.ValueTextEquals("to"u8)) { reader.Read(); to = reader.GetInt32(); }
                                                    else if (reader.ValueTextEquals("direction"u8)) { reader.Read(); dir = reader.GetString() ?? "forward"; }
                                                    else
                                                    {
                                                        reader.Skip();
                                                    }
                                                }
                                            }

                                            frameTags.Add((name, from, to, dir));
                                        }
                                    }
                                }
                                else
                                {
                                    reader.Skip();
                                }
                            }
                        }
                    }
                }
            }

            if (frameList.Count == 0 || !metaSize.HasValue)
            {
                return Asset2DImportResult<TCoordPolicy>.Failure("TexturePacker JSON missing required 'frames' or 'meta' root sections.");
            }

            string atlasId = new string(SpriteAtlasUtil.DeriveAtlasId(context.Path));
            string textureId = metaImage ?? $"{atlasId}.png";

            AtlasBuilder<AtlasBuildStage.Configured, TCoordPolicy> configured = AtlasBuilder.Create<TCoordPolicy>()
                .WithIdentity(new AtlasId(atlasId), new TextureId(textureId), metaSize.Value);

            Span<SpriteFrameDefinition> framesSpan = CollectionsMarshal.AsSpan(frameList);
            AtlasBuilder<AtlasBuildStage.FramesDefined, TCoordPolicy> builder = configured.AddFrame(in framesSpan[0]);
            for (int i = 1; i < framesSpan.Length; i++)
            {
                builder = builder.AddFrame(in framesSpan[i]);
            }

            for (int t = 0; t < frameTags.Count; t++)
            {
                (string Name, int From, int To, string Direction) tag = frameTags[t];
                string tagId = string.IsNullOrWhiteSpace(tag.Name) ? $"anim_{t}" : tag.Name;
                int minIdx = Math.Min(tag.From, tag.To);
                int maxIdx = Math.Max(tag.From, tag.To);

                ImmutableArray<SpriteClipRefDefinition>.Builder refBuilder = ImmutableArray.CreateBuilder<SpriteClipRefDefinition>();
                string dir = tag.Direction.ToLowerInvariant();

                switch (dir)
                {
                    case "reverse":
                        for (int idx = maxIdx; idx >= minIdx; idx--)
                        {
                            if ((uint)idx < (uint)frameList.Count)
                            {
                                refBuilder.Add(new SpriteClipRefDefinition(frameList[idx].Id));
                            }
                        }

                        break;

                    case "pingpong":
                        for (int idx = minIdx; idx <= maxIdx; idx++)
                        {
                            if ((uint)idx < (uint)frameList.Count)
                            {
                                refBuilder.Add(new SpriteClipRefDefinition(frameList[idx].Id));
                            }
                        }

                        for (int idx = maxIdx - 1; idx >= minIdx + 1; idx--)
                        {
                            if ((uint)idx < (uint)frameList.Count)
                            {
                                refBuilder.Add(new SpriteClipRefDefinition(frameList[idx].Id));
                            }
                        }

                        break;

                    case "forward":
                    default:
                        for (int idx = minIdx; idx <= maxIdx; idx++)
                        {
                            if ((uint)idx < (uint)frameList.Count)
                            {
                                refBuilder.Add(new SpriteClipRefDefinition(frameList[idx].Id));
                            }
                        }

                        break;
                }

                if (refBuilder.Count > 0)
                {
                    builder = builder.AddAnimation(new SpriteClipDefinition
                    {
                        Id = new ClipId(tagId),
                        Frames = refBuilder.ToImmutable(),
                        Fps = 12f,
                        Loop = true,
                    });
                }
            }

            return Asset2DImportResult<TCoordPolicy>.Success(builder.Build());
        }
        catch (Exception ex)
        {
            return Asset2DImportResult<TCoordPolicy>.Failure(ex.Message);
        }
    }

    private static SpriteFrameDefinition ParseTexturePackerFrame(ref Utf8JsonReader reader, string frameKey)
    {
        float fx = 0f;
        float fy = 0f;
        float fw = 0f;
        float fh = 0f;
        bool rotated = false;
        float srcW = 0f;
        float srcH = 0f;
        float? ssX = null;
        float? ssY = null;
        float? pivX = null;
        float? pivY = null;
        float? durationMs = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals("frame"u8))
                {
                    reader.Read();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                    {
                        if (reader.TokenType == JsonTokenType.PropertyName)
                        {
                            if (reader.ValueTextEquals("x"u8)) { reader.Read(); fx = reader.GetSingle(); }
                            else if (reader.ValueTextEquals("y"u8)) { reader.Read(); fy = reader.GetSingle(); }
                            else if (reader.ValueTextEquals("w"u8)) { reader.Read(); fw = reader.GetSingle(); }
                            else if (reader.ValueTextEquals("h"u8)) { reader.Read(); fh = reader.GetSingle(); }
                            else
                            {
                                reader.Skip();
                            }
                        }
                    }
                }
                else if (reader.ValueTextEquals("rotated"u8))
                {
                    reader.Read();
                    rotated = reader.GetBoolean();
                }
                else if (reader.ValueTextEquals("sourceSize"u8))
                {
                    reader.Read();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                    {
                        if (reader.TokenType == JsonTokenType.PropertyName)
                        {
                            if (reader.ValueTextEquals("w"u8)) { reader.Read(); srcW = reader.GetSingle(); }
                            else if (reader.ValueTextEquals("h"u8)) { reader.Read(); srcH = reader.GetSingle(); }
                            else
                            {
                                reader.Skip();
                            }
                        }
                    }
                }
                else if (reader.ValueTextEquals("spriteSourceSize"u8))
                {
                    reader.Read();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                    {
                        if (reader.TokenType == JsonTokenType.PropertyName)
                        {
                            if (reader.ValueTextEquals("x"u8)) { reader.Read(); ssX = reader.GetSingle(); }
                            else if (reader.ValueTextEquals("y"u8)) { reader.Read(); ssY = reader.GetSingle(); }
                            else
                            {
                                reader.Skip();
                            }
                        }
                    }
                }
                else if (reader.ValueTextEquals("pivot"u8))
                {
                    reader.Read();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                    {
                        if (reader.TokenType == JsonTokenType.PropertyName)
                        {
                            if (reader.ValueTextEquals("x"u8)) { reader.Read(); pivX = reader.GetSingle(); }
                            else if (reader.ValueTextEquals("y"u8)) { reader.Read(); pivY = reader.GetSingle(); }
                            else
                            {
                                reader.Skip();
                            }
                        }
                    }
                }
                else if (reader.ValueTextEquals("duration"u8))
                {
                    reader.Read();
                    durationMs = reader.GetSingle();
                }
                else
                {
                    reader.Skip();
                }
            }
        }

        float regionW = rotated ? fh : fw;
        float regionH = rotated ? fw : fh;
        var region = new Rect2D(fx, fy, regionW, regionH);
        var sourceSize = new Size2D(srcW > 0f ? srcW : regionW, srcH > 0f ? srcH : regionH);
        Vec2 pivot = pivX.HasValue && pivY.HasValue ? new Vec2(pivX.Value, pivY.Value) : new Vec2(0.5f, 0.5f);
        Vec2? trimOffset = ssX.HasValue && ssY.HasValue ? new Vec2(ssX.Value, ssY.Value) : null;

        return new SpriteFrameDefinition
        {
            Id = new FrameId(frameKey),
            Region = region,
            SourceSize = sourceSize,
            Pivot = pivot,
            SliceBorder = null,
            Duration = durationMs.HasValue && durationMs.Value > 0f ? new Milliseconds(durationMs.Value) : null,
            Rotated = rotated,
            TrimOffset = trimOffset,
        };
    }

    private static bool IsTexturePackerJson(ReadOnlySpan<byte> utf8)
    {
        try
        {
            var reader = new Utf8JsonReader(utf8);
            bool hasFramesObject = false;
            bool hasMeta = false;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    if (reader.ValueTextEquals("frames"u8))
                    {
                        reader.Read();
                        if (reader.TokenType == JsonTokenType.StartObject)
                        {
                            hasFramesObject = true;
                        }
                    }
                    else if (reader.ValueTextEquals("meta"u8))
                    {
                        hasMeta = true;
                    }
                }

                if (hasFramesObject && hasMeta)
                {
                    return true;
                }
            }

            return hasFramesObject;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>Atlas path and naming helpers.</summary>
public static class SpriteAtlasUtil
{
    private static readonly string[] KnownExtensions =
    [
        ".spriteatlas.json",
        ".atlas.json",
        ".texturepacker.json",
        ".json",
    ];

    /// <summary>Derives an atlas id from a path or URI without allocating.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<char> DeriveAtlasId(ReadOnlySpan<char> pathOrUri)
    {
        if (pathOrUri.IsEmpty)
        {
            return "atlas";
        }

        int queryIdx = pathOrUri.IndexOfAny('?', '#');
        if (queryIdx >= 0)
        {
            pathOrUri = pathOrUri[..queryIdx];
        }

        int lastSlash = pathOrUri.LastIndexOfAny('/', '\\');
        ReadOnlySpan<char> fileName = lastSlash >= 0 ? pathOrUri[(lastSlash + 1)..] : pathOrUri;

        foreach (string ext in KnownExtensions)
        {
            if (fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                fileName = fileName[..^ext.Length];
                break;
            }
        }

        return fileName.IsEmpty ? "atlas" : fileName;
    }
}
