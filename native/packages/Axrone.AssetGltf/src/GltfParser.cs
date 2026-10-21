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
            else if (prop.SequenceEqual("cameras"u8))
            {
                ParseCameras(ref reader, model);
            }
            else if (prop.SequenceEqual("nodes"u8))
            {
                ParseNodes(ref reader, model);
            }
            else if (prop.SequenceEqual("meshes"u8))
            {
                ParseMeshes(ref reader, model);
            }
            else if (prop.SequenceEqual("accessors"u8))
            {
                ParseAccessors(ref reader, model);
            }
            else if (prop.SequenceEqual("bufferViews"u8))
            {
                ParseBufferViews(ref reader, model);
            }
            else if (prop.SequenceEqual("buffers"u8))
            {
                ParseBuffers(ref reader, model);
            }
            else if (prop.SequenceEqual("materials"u8))
            {
                ParseMaterials(ref reader, model);
            }
            else if (prop.SequenceEqual("textures"u8))
            {
                ParseTextures(ref reader, model);
            }
            else if (prop.SequenceEqual("images"u8))
            {
                ParseImages(ref reader, model);
            }
            else if (prop.SequenceEqual("samplers"u8))
            {
                ParseSamplers(ref reader, model);
            }
            else if (prop.SequenceEqual("skins"u8))
            {
                ParseSkins(ref reader, model);
            }
            else if (prop.SequenceEqual("animations"u8))
            {
                ParseAnimations(ref reader, model);
            }
            else if (prop.SequenceEqual("extensionsUsed"u8))
            {
                ParseStringArray(ref reader, model.ExtensionsUsed);
            }
            else if (prop.SequenceEqual("extensionsRequired"u8))
            {
                ParseStringArray(ref reader, model.ExtensionsRequired);
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

    private static void ParseCameras(ref Utf8JsonReader reader, GltfRawModel model)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var camera = new GltfRawModel.Camera();
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
                    camera.Name = reader.GetString();
                }
                else if (prop.SequenceEqual("type"u8))
                {
                    camera.Type = reader.GetString() ?? "perspective";
                }
                else if (prop.SequenceEqual("perspective"u8))
                {
                    ParseCameraPerspective(ref reader, camera);
                }
                else if (prop.SequenceEqual("orthographic"u8))
                {
                    ParseCameraOrthographic(ref reader, camera);
                }
                else
                {
                    reader.Skip();
                }
            }

            model.Cameras.Add(camera);
        }
    }

    private static void ParseCameraPerspective(ref Utf8JsonReader reader, GltfRawModel.Camera camera)
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
            if (prop.SequenceEqual("yfov"u8))
            {
                camera.Yfov = reader.GetSingle();
            }
            else if (prop.SequenceEqual("zfar"u8))
            {
                camera.Zfar = reader.GetSingle();
            }
            else if (prop.SequenceEqual("znear"u8))
            {
                camera.Znear = reader.GetSingle();
            }
            else
            {
                reader.Skip();
            }
        }
    }

    private static void ParseCameraOrthographic(ref Utf8JsonReader reader, GltfRawModel.Camera camera)
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
            if (prop.SequenceEqual("xmag"u8))
            {
                camera.Xmag = reader.GetSingle();
            }
            else if (prop.SequenceEqual("ymag"u8))
            {
                camera.Ymag = reader.GetSingle();
            }
            else if (prop.SequenceEqual("zfar"u8))
            {
                camera.Zfar = reader.GetSingle();
            }
            else if (prop.SequenceEqual("znear"u8))
            {
                camera.Znear = reader.GetSingle();
            }
            else
            {
                reader.Skip();
            }
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

            ReadOnlySpan<byte> extension = reader.ValueSpan;
            reader.Read();
            if (extension.SequenceEqual("KHR_lights_punctual"u8))
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    reader.Skip();
                    continue;
                }

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        continue;
                    }

                    ReadOnlySpan<byte> prop = reader.ValueSpan;
                    reader.Read();
                    if (prop.SequenceEqual("light"u8))
                    {
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

    private static void ParseMeshes(ref Utf8JsonReader reader, GltfRawModel model)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var mesh = new GltfRawModel.Mesh();
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
                    mesh.Name = reader.GetString();
                }
                else if (prop.SequenceEqual("primitives"u8))
                {
                    ParsePrimitives(ref reader, mesh);
                }
                else
                {
                    reader.Skip();
                }
            }

            model.Meshes.Add(mesh);
        }
    }

    private static void ParsePrimitives(ref Utf8JsonReader reader, GltfRawModel.Mesh mesh)
    {
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var prim = new GltfRawModel.Primitive();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                ReadOnlySpan<byte> prop = reader.ValueSpan;
                reader.Read();
                if (prop.SequenceEqual("indices"u8))
                {
                    prim.Indices = reader.GetInt32();
                }
                else if (prop.SequenceEqual("material"u8))
                {
                    prim.Material = reader.GetInt32();
                }
                else if (prop.SequenceEqual("mode"u8))
                {
                    prim.Mode = reader.GetInt32();
                }
                else if (prop.SequenceEqual("attributes"u8))
                {
                    ParseAttributeMap(ref reader, prim.Attributes);
                }
                else if (prop.SequenceEqual("targets"u8))
                {
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        var dict = new Dictionary<string, int>(StringComparer.Ordinal);
                        ParseAttributeMap(ref reader, dict);
                        prim.Targets.Add(dict);
                    }
                }
                else if (prop.SequenceEqual("extensions"u8))
                {
                    ParsePrimitiveExtensions(ref reader, prim);
                }
                else
                {
                    reader.Skip();
                }
            }

            mesh.Primitives.Add(prim);
        }
    }

    private static void ParseAttributeMap(ref Utf8JsonReader reader, Dictionary<string, int> map)
    {
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                string key = reader.GetString()!;
                reader.Read();
                map[key] = reader.GetInt32();
            }
        }
    }

    private static void ParsePrimitiveExtensions(ref Utf8JsonReader reader, GltfRawModel.Primitive prim)
    {
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual("KHR_draco_mesh_compression"u8))
            {
                prim.Draco = new GltfRawModel.DracoPrimitiveExtension();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        continue;
                    }

                    ReadOnlySpan<byte> prop = reader.ValueSpan;
                    reader.Read();
                    if (prop.SequenceEqual("bufferView"u8))
                    {
                        prim.Draco.BufferView = reader.GetInt32();
                    }
                    else if (prop.SequenceEqual("attributes"u8))
                    {
                        ParseAttributeMap(ref reader, prim.Draco.Attributes);
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

    private static void ParseAccessors(ref Utf8JsonReader reader, GltfRawModel model)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var acc = new GltfRawModel.Accessor();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                ReadOnlySpan<byte> prop = reader.ValueSpan;
                reader.Read();
                if (prop.SequenceEqual("bufferView"u8))
                {
                    acc.BufferView = reader.GetInt32();
                }
                else if (prop.SequenceEqual("byteOffset"u8))
                {
                    acc.ByteOffset = reader.GetInt32();
                }
                else if (prop.SequenceEqual("componentType"u8))
                {
                    acc.ComponentType = reader.GetInt32();
                }
                else if (prop.SequenceEqual("normalized"u8))
                {
                    acc.Normalized = reader.GetBoolean();
                }
                else if (prop.SequenceEqual("count"u8))
                {
                    acc.Count = reader.GetInt32();
                }
                else if (prop.SequenceEqual("type"u8))
                {
                    acc.Type = reader.GetString() ?? "SCALAR";
                }
                else if (prop.SequenceEqual("min"u8))
                {
                    acc.Min = ReadFloatArray(ref reader);
                }
                else if (prop.SequenceEqual("max"u8))
                {
                    acc.Max = ReadFloatArray(ref reader);
                }
                else if (prop.SequenceEqual("sparse"u8))
                {
                    acc.Sparse = ParseAccessorSparse(ref reader);
                }
                else
                {
                    reader.Skip();
                }
            }

            model.Accessors.Add(acc);
        }
    }

    private static GltfRawModel.AccessorSparse ParseAccessorSparse(ref Utf8JsonReader reader)
    {
        var sparse = new GltfRawModel.AccessorSparse();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            ReadOnlySpan<byte> prop = reader.ValueSpan;
            reader.Read();
            if (prop.SequenceEqual("count"u8))
            {
                sparse.Count = reader.GetInt32();
            }
            else if (prop.SequenceEqual("indices"u8))
            {
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        continue;
                    }

                    ReadOnlySpan<byte> iprop = reader.ValueSpan;
                    reader.Read();
                    if (iprop.SequenceEqual("bufferView"u8))
                    {
                        sparse.Indices.BufferView = reader.GetInt32();
                    }
                    else if (iprop.SequenceEqual("byteOffset"u8))
                    {
                        sparse.Indices.ByteOffset = reader.GetInt32();
                    }
                    else if (iprop.SequenceEqual("componentType"u8))
                    {
                        sparse.Indices.ComponentType = reader.GetInt32();
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
            }
            else if (prop.SequenceEqual("values"u8))
            {
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        continue;
                    }

                    ReadOnlySpan<byte> vprop = reader.ValueSpan;
                    reader.Read();
                    if (vprop.SequenceEqual("bufferView"u8))
                    {
                        sparse.Values.BufferView = reader.GetInt32();
                    }
                    else if (vprop.SequenceEqual("byteOffset"u8))
                    {
                        sparse.Values.ByteOffset = reader.GetInt32();
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

        return sparse;
    }

    private static void ParseBufferViews(ref Utf8JsonReader reader, GltfRawModel model)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var bv = new GltfRawModel.BufferView();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                ReadOnlySpan<byte> prop = reader.ValueSpan;
                reader.Read();
                if (prop.SequenceEqual("buffer"u8))
                {
                    bv.Buffer = reader.GetInt32();
                }
                else if (prop.SequenceEqual("byteOffset"u8))
                {
                    bv.ByteOffset = reader.GetInt32();
                }
                else if (prop.SequenceEqual("byteLength"u8))
                {
                    bv.ByteLength = reader.GetInt32();
                }
                else if (prop.SequenceEqual("byteStride"u8))
                {
                    bv.ByteStride = reader.GetInt32();
                }
                else if (prop.SequenceEqual("target"u8))
                {
                    bv.Target = reader.GetInt32();
                }
                else if (prop.SequenceEqual("extensions"u8))
                {
                    ParseBufferViewExtensions(ref reader, bv);
                }
                else
                {
                    reader.Skip();
                }
            }

            model.BufferViews.Add(bv);
        }
    }

    private static void ParseBufferViewExtensions(ref Utf8JsonReader reader, GltfRawModel.BufferView bv)
    {
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual("EXT_meshopt_compression"u8))
            {
                bv.Meshopt = new GltfRawModel.BufferViewMeshoptExtension();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        continue;
                    }

                    ReadOnlySpan<byte> prop = reader.ValueSpan;
                    reader.Read();
                    if (prop.SequenceEqual("buffer"u8))
                    {
                        bv.Meshopt.Buffer = reader.GetInt32();
                    }
                    else if (prop.SequenceEqual("byteOffset"u8))
                    {
                        bv.Meshopt.ByteOffset = reader.GetInt32();
                    }
                    else if (prop.SequenceEqual("byteLength"u8))
                    {
                        bv.Meshopt.ByteLength = reader.GetInt32();
                    }
                    else if (prop.SequenceEqual("byteStride"u8))
                    {
                        bv.Meshopt.ByteStride = reader.GetInt32();
                    }
                    else if (prop.SequenceEqual("count"u8))
                    {
                        bv.Meshopt.Count = reader.GetInt32();
                    }
                    else if (prop.SequenceEqual("mode"u8))
                    {
                        bv.Meshopt.Mode = reader.GetString() ?? "ATTRIBUTES";
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

    private static void ParseBuffers(ref Utf8JsonReader reader, GltfRawModel model)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var buffer = new GltfRawModel.Buffer();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                ReadOnlySpan<byte> prop = reader.ValueSpan;
                reader.Read();
                if (prop.SequenceEqual("byteLength"u8))
                {
                    buffer.ByteLength = reader.GetInt32();
                }
                else if (prop.SequenceEqual("uri"u8))
                {
                    buffer.Uri = reader.GetString();
                }
                else
                {
                    reader.Skip();
                }
            }

            model.Buffers.Add(buffer);
        }
    }

    private static void ParseMaterials(ref Utf8JsonReader reader, GltfRawModel model)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var mat = new GltfRawModel.Material();
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
                    mat.Name = reader.GetString();
                }
                else if (prop.SequenceEqual("alphaMode"u8))
                {
                    mat.AlphaMode = reader.GetString() ?? "OPAQUE";
                }
                else if (prop.SequenceEqual("alphaCutoff"u8))
                {
                    mat.AlphaCutoff = reader.GetSingle();
                }
                else if (prop.SequenceEqual("doubleSided"u8))
                {
                    mat.DoubleSided = reader.GetBoolean();
                }
                else if (prop.SequenceEqual("emissiveFactor"u8))
                {
                    mat.EmissiveFactor = ReadFloatArray(ref reader);
                }
                else if (prop.SequenceEqual("pbrMetallicRoughness"u8))
                {
                    ParsePbr(ref reader, mat);
                }
                else if (prop.SequenceEqual("normalTexture"u8))
                {
                    mat.NormalTexture = ParseTextureInfo(ref reader);
                }
                else if (prop.SequenceEqual("occlusionTexture"u8))
                {
                    mat.OcclusionTexture = ParseTextureInfo(ref reader);
                }
                else if (prop.SequenceEqual("emissiveTexture"u8))
                {
                    mat.EmissiveTexture = ParseTextureInfo(ref reader);
                }
                else if (prop.SequenceEqual("extensions"u8))
                {
                    ParseMaterialExtensions(ref reader, mat);
                }
                else
                {
                    reader.Skip();
                }
            }

            model.Materials.Add(mat);
        }
    }

    private static void ParsePbr(ref Utf8JsonReader reader, GltfRawModel.Material mat)
    {
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            ReadOnlySpan<byte> prop = reader.ValueSpan;
            reader.Read();
            if (prop.SequenceEqual("baseColorFactor"u8))
            {
                mat.Pbr.BaseColorFactor = ReadFloatArray(ref reader);
            }
            else if (prop.SequenceEqual("metallicFactor"u8))
            {
                mat.Pbr.MetallicFactor = reader.GetSingle();
            }
            else if (prop.SequenceEqual("roughnessFactor"u8))
            {
                mat.Pbr.RoughnessFactor = reader.GetSingle();
            }
            else if (prop.SequenceEqual("baseColorTexture"u8))
            {
                mat.Pbr.BaseColorTexture = ParseTextureInfo(ref reader);
            }
            else if (prop.SequenceEqual("metallicRoughnessTexture"u8))
            {
                mat.Pbr.MetallicRoughnessTexture = ParseTextureInfo(ref reader);
            }
            else
            {
                reader.Skip();
            }
        }
    }

    private static GltfRawModel.TextureInfo ParseTextureInfo(ref Utf8JsonReader reader)
    {
        var info = new GltfRawModel.TextureInfo();
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return info;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            ReadOnlySpan<byte> prop = reader.ValueSpan;
            reader.Read();
            if (prop.SequenceEqual("index"u8))
            {
                info.Index = reader.GetInt32();
            }
            else if (prop.SequenceEqual("texCoord"u8))
            {
                info.TexCoord = reader.GetInt32();
            }
            else if (prop.SequenceEqual("scale"u8))
            {
                info.Scale = reader.GetSingle();
            }
            else if (prop.SequenceEqual("strength"u8))
            {
                info.Strength = reader.GetSingle();
            }
            else if (prop.SequenceEqual("extensions"u8))
            {
                ParseTextureTransformExtension(ref reader, info);
            }
            else
            {
                reader.Skip();
            }
        }

        return info;
    }

    private static void ParseTextureTransformExtension(ref Utf8JsonReader reader, GltfRawModel.TextureInfo info)
    {
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual("KHR_texture_transform"u8))
            {
                var offset = Vec2.Zero;
                var scale = Vec2.One;
                float rotation = 0.0f;
                int texCoord = info.TexCoord;

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        continue;
                    }

                    ReadOnlySpan<byte> prop = reader.ValueSpan;
                    reader.Read();
                    if (prop.SequenceEqual("offset"u8))
                    {
                        float[]? arr = ReadFloatArray(ref reader);
                        if (arr != null && arr.Length >= 2)
                        {
                            offset = new Vec2(arr[0], arr[1]);
                        }
                    }
                    else if (prop.SequenceEqual("scale"u8))
                    {
                        float[]? arr = ReadFloatArray(ref reader);
                        if (arr != null && arr.Length >= 2)
                        {
                            scale = new Vec2(arr[0], arr[1]);
                        }
                    }
                    else if (prop.SequenceEqual("rotation"u8))
                    {
                        rotation = reader.GetSingle();
                    }
                    else if (prop.SequenceEqual("texCoord"u8))
                    {
                        texCoord = reader.GetInt32();
                    }
                    else
                    {
                        reader.Skip();
                    }
                }

                info.Transform = new GltfTextureTransform(offset, scale, rotation, texCoord);
            }
            else
            {
                reader.Skip();
            }
        }
    }

    private static void ParseMaterialExtensions(ref Utf8JsonReader reader, GltfRawModel.Material mat)
    {
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            ReadOnlySpan<byte> ext = reader.ValueSpan;
            reader.Read();
            if (ext.SequenceEqual("KHR_materials_unlit"u8))
            {
                mat.Unlit = true;
                reader.Skip();
            }
            else if (ext.SequenceEqual("KHR_materials_clearcoat"u8))
            {
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        continue;
                    }

                    ReadOnlySpan<byte> prop = reader.ValueSpan;
                    reader.Read();
                    if (prop.SequenceEqual("clearcoatFactor"u8))
                    {
                        mat.ClearcoatFactor = reader.GetSingle();
                    }
                    else if (prop.SequenceEqual("clearcoatRoughnessFactor"u8))
                    {
                        mat.ClearcoatRoughnessFactor = reader.GetSingle();
                    }
                    else if (prop.SequenceEqual("clearcoatTexture"u8))
                    {
                        mat.ClearcoatTexture = ParseTextureInfo(ref reader);
                    }
                    else if (prop.SequenceEqual("clearcoatRoughnessTexture"u8))
                    {
                        mat.ClearcoatRoughnessTexture = ParseTextureInfo(ref reader);
                    }
                    else if (prop.SequenceEqual("clearcoatNormalTexture"u8))
                    {
                        mat.ClearcoatNormalTexture = ParseTextureInfo(ref reader);
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
            }
            else if (ext.SequenceEqual("KHR_materials_emissive_strength"u8))
            {
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual("emissiveStrength"u8))
                    {
                        reader.Read();
                        mat.EmissiveStrength = reader.GetSingle();
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

    private static void ParseTextures(ref Utf8JsonReader reader, GltfRawModel model)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var tex = new GltfRawModel.Texture();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                ReadOnlySpan<byte> prop = reader.ValueSpan;
                reader.Read();
                if (prop.SequenceEqual("sampler"u8))
                {
                    tex.Sampler = reader.GetInt32();
                }
                else if (prop.SequenceEqual("source"u8))
                {
                    tex.Source = reader.GetInt32();
                }
                else if (prop.SequenceEqual("extensions"u8))
                {
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                    {
                        if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual("KHR_texture_basisu"u8))
                        {
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                            {
                                if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual("source"u8))
                                {
                                    reader.Read();
                                    tex.Source = reader.GetInt32();
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
                else
                {
                    reader.Skip();
                }
            }

            model.Textures.Add(tex);
        }
    }

    private static void ParseImages(ref Utf8JsonReader reader, GltfRawModel model)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var img = new GltfRawModel.Image();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                ReadOnlySpan<byte> prop = reader.ValueSpan;
                reader.Read();
                if (prop.SequenceEqual("uri"u8))
                {
                    img.Uri = reader.GetString();
                }
                else if (prop.SequenceEqual("mimeType"u8))
                {
                    img.MimeType = reader.GetString();
                }
                else if (prop.SequenceEqual("bufferView"u8))
                {
                    img.BufferView = reader.GetInt32();
                }
                else
                {
                    reader.Skip();
                }
            }

            model.Images.Add(img);
        }
    }

    private static void ParseSamplers(ref Utf8JsonReader reader, GltfRawModel model)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var samp = new GltfRawModel.Sampler();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                ReadOnlySpan<byte> prop = reader.ValueSpan;
                reader.Read();
                if (prop.SequenceEqual("magFilter"u8))
                {
                    samp.MagFilter = reader.GetInt32();
                }
                else if (prop.SequenceEqual("minFilter"u8))
                {
                    samp.MinFilter = reader.GetInt32();
                }
                else if (prop.SequenceEqual("wrapS"u8))
                {
                    samp.WrapS = reader.GetInt32();
                }
                else if (prop.SequenceEqual("wrapT"u8))
                {
                    samp.WrapT = reader.GetInt32();
                }
                else
                {
                    reader.Skip();
                }
            }

            model.Samplers.Add(samp);
        }
    }

    private static void ParseSkins(ref Utf8JsonReader reader, GltfRawModel model)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var skin = new GltfRawModel.Skin();
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
                    skin.Name = reader.GetString();
                }
                else if (prop.SequenceEqual("inverseBindMatrices"u8))
                {
                    skin.InverseBindMatrices = reader.GetInt32();
                }
                else if (prop.SequenceEqual("skeleton"u8))
                {
                    skin.Skeleton = reader.GetInt32();
                }
                else if (prop.SequenceEqual("joints"u8))
                {
                    ParseIntArray(ref reader, skin.Joints);
                }
                else
                {
                    reader.Skip();
                }
            }

            model.Skins.Add(skin);
        }
    }

    private static void ParseAnimations(ref Utf8JsonReader reader, GltfRawModel model)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var anim = new GltfRawModel.Animation();
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
                    anim.Name = reader.GetString();
                }
                else if (prop.SequenceEqual("samplers"u8))
                {
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        var samp = new GltfRawModel.AnimationSampler();
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                        {
                            if (reader.TokenType != JsonTokenType.PropertyName)
                            {
                                continue;
                            }

                            ReadOnlySpan<byte> sprop = reader.ValueSpan;
                            reader.Read();
                            if (sprop.SequenceEqual("input"u8))
                            {
                                samp.Input = reader.GetInt32();
                            }
                            else if (sprop.SequenceEqual("interpolation"u8))
                            {
                                samp.Interpolation = reader.GetString() ?? "LINEAR";
                            }
                            else if (sprop.SequenceEqual("output"u8))
                            {
                                samp.Output = reader.GetInt32();
                            }
                            else
                            {
                                reader.Skip();
                            }
                        }

                        anim.Samplers.Add(samp);
                    }
                }
                else if (prop.SequenceEqual("channels"u8))
                {
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        var chan = new GltfRawModel.AnimationChannel();
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                        {
                            if (reader.TokenType != JsonTokenType.PropertyName)
                            {
                                continue;
                            }

                            ReadOnlySpan<byte> cprop = reader.ValueSpan;
                            reader.Read();
                            if (cprop.SequenceEqual("sampler"u8))
                            {
                                chan.Sampler = reader.GetInt32();
                            }
                            else if (cprop.SequenceEqual("target"u8))
                            {
                                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                                {
                                    if (reader.TokenType != JsonTokenType.PropertyName)
                                    {
                                        continue;
                                    }

                                    ReadOnlySpan<byte> tprop = reader.ValueSpan;
                                    reader.Read();
                                    if (tprop.SequenceEqual("node"u8))
                                    {
                                        chan.TargetNode = reader.GetInt32();
                                    }
                                    else if (tprop.SequenceEqual("path"u8))
                                    {
                                        chan.TargetPath = reader.GetString() ?? "translation";
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

                        anim.Channels.Add(chan);
                    }
                }
                else
                {
                    reader.Skip();
                }
            }

            model.Animations.Add(anim);
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
