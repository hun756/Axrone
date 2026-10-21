namespace Axrone.AssetGltf;

/// <summary>Manual Utf8JsonReader glTF parser (AOT-safe, no reflection).</summary>
internal static class GltfJsonParser
{
    /// <summary>Parses glTF JSON bytes into a raw model.</summary>
    public static GltfRawModel Parse(ReadOnlySpan<byte> jsonBytes)
    {
        var model = new GltfRawModel();
        var reader = new Utf8JsonReader(jsonBytes, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            ThrowHelper.ThrowContainer("Root glTF JSON payload must be an object.");
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            ReadOnlySpan<byte> prop = reader.ValueSpan;
            reader.Read();

            if (prop.SequenceEqual("asset"u8))
            {
                ParseAsset(ref reader, model);
            }
            else if (prop.SequenceEqual("scene"u8))
            {
                model.DefaultScene = reader.GetInt32();
            }
            else if (prop.SequenceEqual("scenes"u8))
            {
                ParseScenes(ref reader, model);
            }
            else if (prop.SequenceEqual("nodes"u8))
            {
                ParseNodes(ref reader, model);
            }
            else if (prop.SequenceEqual("extensions"u8))
            {
                ParseRootExtensions(ref reader, model);
            }
            else
            {
                reader.Skip();
            }
        }

        return model;
    }

    private static void ParseAsset(ref Utf8JsonReader reader, GltfRawModel model)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            ReadOnlySpan<byte> prop = reader.ValueSpan;
            reader.Read();
            if (prop.SequenceEqual("version"u8))
            {
                model.Version = reader.GetString() ?? "2.0";
            }
            else if (prop.SequenceEqual("generator"u8))
            {
                model.Generator = reader.GetString();
            }
            else if (prop.SequenceEqual("copyright"u8))
            {
                model.Copyright = reader.GetString();
            }
            else
            {
                reader.Skip();
            }
        }
    }

    private static void ParseScenes(ref Utf8JsonReader reader, GltfRawModel model)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var scene = new GltfRawModel.Scene();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                ReadOnlySpan<byte> prop = reader.ValueSpan;
                reader.Read();
                if (prop.SequenceEqual("name"u8))
                {
                    scene.Name = reader.GetString();
                }
                else if (prop.SequenceEqual("nodes"u8))
                {
                    ParseIntArray(ref reader, scene.Nodes);
                }
                else
                {
                    reader.Skip();
                }
            }

            model.Scenes.Add(scene);
        }
    }

    private static void ParseNodes(ref Utf8JsonReader reader, GltfRawModel model)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var node = new GltfRawModel.Node();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                ReadOnlySpan<byte> prop = reader.ValueSpan;
                reader.Read();
                if (prop.SequenceEqual("name"u8))
                {
                    node.Name = reader.GetString();
                }
                else if (prop.SequenceEqual("mesh"u8))
                {
                    node.Mesh = reader.GetInt32();
                }
                else if (prop.SequenceEqual("skin"u8))
                {
                    node.Skin = reader.GetInt32();
                }
                else if (prop.SequenceEqual("camera"u8))
                {
                    node.Camera = reader.GetInt32();
                }
                else if (prop.SequenceEqual("children"u8))
                {
                    ParseIntArray(ref reader, node.Children);
                }
                else if (prop.SequenceEqual("matrix"u8))
                {
                    node.Matrix = ReadFloatArray(ref reader);
                }
                else if (prop.SequenceEqual("translation"u8))
                {
                    node.Translation = ReadFloatArray(ref reader);
                }
                else if (prop.SequenceEqual("rotation"u8))
                {
                    node.Rotation = ReadFloatArray(ref reader);
                }
                else if (prop.SequenceEqual("scale"u8))
                {
                    node.Scale = ReadFloatArray(ref reader);
                }
                else if (prop.SequenceEqual("weights"u8))
                {
                    node.Weights = ReadFloatArray(ref reader);
                }
                else if (prop.SequenceEqual("extensions"u8))
                {
                    ParseNodeExtensions(ref reader, node);
                }
                else
                {
                    reader.Skip();
                }
            }

            model.Nodes.Add(node);
        }
    }

    private static void ParseNodeExtensions(ref Utf8JsonReader reader, GltfRawModel.Node node)
    {
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual("KHR_lights_punctual"u8))
            {
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual("light"u8))
                    {
                        reader.Read();
                        node.Light = reader.GetInt32();
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
            }
            else
            {
                reader.Skip();
            }
        }
    }

    private static void ParseRootExtensions(ref Utf8JsonReader reader, GltfRawModel model)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            ReadOnlySpan<byte> ext = reader.ValueSpan;
            reader.Read();
            if (ext.SequenceEqual("KHR_lights_punctual"u8))
            {
                ParseLightsExtension(ref reader, model);
            }
            else
            {
                reader.Skip();
            }
        }
    }

    private static void ParseLightsExtension(ref Utf8JsonReader reader, GltfRawModel model)
    {
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual("lights"u8))
            {
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    var light = new GltfRawModel.Light();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                    {
                        if (reader.TokenType != JsonTokenType.PropertyName)
                        {
                            continue;
                        }

                        ReadOnlySpan<byte> prop = reader.ValueSpan;
                        reader.Read();
                        if (prop.SequenceEqual("name"u8))
                        {
                            light.Name = reader.GetString();
                        }
                        else if (prop.SequenceEqual("color"u8))
                        {
                            light.Color = ReadFloatArray(ref reader) ?? light.Color;
                        }
                        else if (prop.SequenceEqual("intensity"u8))
                        {
                            light.Intensity = reader.GetSingle();
                        }
                        else if (prop.SequenceEqual("type"u8))
                        {
                            light.Type = reader.GetString() ?? "directional";
                        }
                        else if (prop.SequenceEqual("range"u8))
                        {
                            light.Range = reader.GetSingle();
                        }
                        else if (prop.SequenceEqual("spot"u8))
                        {
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                            {
                                if (reader.TokenType != JsonTokenType.PropertyName)
                                {
                                    continue;
                                }

                                ReadOnlySpan<byte> spot = reader.ValueSpan;
                                reader.Read();
                                if (spot.SequenceEqual("innerConeAngle"u8))
                                {
                                    light.InnerConeAngle = reader.GetSingle();
                                }
                                else if (spot.SequenceEqual("outerConeAngle"u8))
                                {
                                    light.OuterConeAngle = reader.GetSingle();
                                }
                                else
                                {
                                    reader.Skip();
                                }
                            }
                        }
                        else
                        {
                            reader.Skip();
                        }
                    }

                    model.Lights.Add(light);
                }
            }
            else
            {
                reader.Skip();
            }
        }
    }

    private static void ParseIntArray(ref Utf8JsonReader reader, List<int> list)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            list.Add(reader.GetInt32());
        }
    }

    private static void ParseStringArray(ref Utf8JsonReader reader, HashSet<string> set)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            string? s = reader.GetString();
            if (s != null)
            {
                set.Add(s);
            }
        }
    }

    private static float[]? ReadFloatArray(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return null;
        }

        var list = new List<float>(4);
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            list.Add(reader.GetSingle());
        }

        return list.ToArray();
    }
}
