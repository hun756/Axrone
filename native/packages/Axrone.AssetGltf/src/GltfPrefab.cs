namespace Axrone.AssetGltf;

/// <summary>Scene graph synthesizer: prefabs from raw nodes.</summary>
internal static class GltfPrefabEngine
{
    /// <summary>Local light budget per scene.</summary>
    public const int MaxSceneLocalLights = 4;

    /// <summary>Decomposes a node matrix or TRS into translation/rotation/scale.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void DecomposeTransform(
        GltfRawModel.Node node,
        out Vec3 translation,
        out Quat rotation,
        out Vec3 scale)
    {
        if (node.Matrix != null && node.Matrix.Length == 16)
        {
            ReadOnlySpan<float> m = node.Matrix;
            var matrix = new Mat4(
                m[0], m[1], m[2], m[3],
                m[4], m[5], m[6], m[7],
                m[8], m[9], m[10], m[11],
                m[12], m[13], m[14], m[15]);

            if (matrix.Decompose(out scale, out rotation, out translation))
            {
                return;
            }
        }

        translation = node.Translation != null && node.Translation.Length >= 3
            ? new Vec3(node.Translation[0], node.Translation[1], node.Translation[2])
            : Vec3.Zero;

        rotation = node.Rotation != null && node.Rotation.Length >= 4
            ? new Quat(node.Rotation[0], node.Rotation[1], node.Rotation[2], node.Rotation[3])
            : Quat.Identity;

        scale = node.Scale != null && node.Scale.Length >= 3
            ? new Vec3(node.Scale[0], node.Scale[1], node.Scale[2])
            : Vec3.One;
    }

    /// <summary>Builds a prefab asset for one scene.</summary>
    public static GltfPrefabAsset BuildPrefab(
        int sceneIndex,
        GltfRawModel model,
        bool isDefaultScene,
        List<GltfDiagnostic> diagnostics,
        ImmutableDictionary<int, string> meshKeyMap,
        ImmutableDictionary<int, string> skinKeyMap,
        ImmutableArray<string> animationClipIds)
    {
        string prefabId = $"gltf/scene/{sceneIndex}";
        GltfRawModel.Scene scene = model.Scenes[sceneIndex];

        var actorList = new List<GltfActorSnapshot>();
        var rootNodeIds = new List<string>();
        var allNodeIds = new HashSet<string>(StringComparer.Ordinal);

        bool primaryDirectionalAllocated = false;
        int localLightCount = 0;

        var stack = new Stack<(int NodeIndex, string? ParentNodeId)>();
        var visited = new HashSet<int>();

        for (int i = scene.Nodes.Count - 1; i >= 0; i--)
        {
            stack.Push((scene.Nodes[i], null));
        }

        bool firstRoot = true;

        while (stack.Count > 0)
        {
            (int nodeIdx, string? parentId) = stack.Pop();
            if (!visited.Add(nodeIdx))
            {
                diagnostics.Add(new GltfDiagnostic(
                    GltfDiagnosticLevel.Warning,
                    "gltf.prefab.cycle",
                    $"Scene traversal cycle detected at node index {nodeIdx}."));
                continue;
            }

            GltfRawModel.Node node = model.Nodes[nodeIdx];
            string nodeId = $"node_{nodeIdx}_{node.Name ?? "unnamed"}";
            allNodeIds.Add(nodeId);

            if (parentId == null)
            {
                rootNodeIds.Add(nodeId);
            }

            DecomposeTransform(node, out Vec3 t, out Quat r, out Vec3 s);
            var compBuilder = ImmutableArray.CreateBuilder<GltfComponentSnapshot>();
            compBuilder.Add(new GltfTransformComponent(t, r, s));

            if (node.Camera.HasValue && node.Camera.Value >= 0 && node.Camera.Value < model.Cameras.Count)
            {
                GltfRawModel.Camera cam = model.Cameras[node.Camera.Value];
                bool isPrimaryCam = isDefaultScene && parentId == null;
                bool isOrtho = string.Equals(cam.Type, "orthographic", StringComparison.OrdinalIgnoreCase);

                compBuilder.Add(new GltfCameraComponent(
                    isPrimaryCam,
                    cam.Znear,
                    cam.Zfar,
                    cam.Yfov,
                    isOrtho,
                    cam.Ymag));
            }

            if (node.Light.HasValue && node.Light.Value >= 0 && node.Light.Value < model.Lights.Count)
            {
                GltfRawModel.Light light = model.Lights[node.Light.Value];
                var color = new Vec3(light.Color[0], light.Color[1], light.Color[2]);

                if (string.Equals(light.Type, "directional", StringComparison.OrdinalIgnoreCase))
                {
                    bool isPrimaryLight = !primaryDirectionalAllocated;
                    primaryDirectionalAllocated = true;
                    compBuilder.Add(new GltfDirectionalLightComponent(color, light.Intensity, isPrimaryLight));
                }
                else if (string.Equals(light.Type, "point", StringComparison.OrdinalIgnoreCase))
                {
                    localLightCount++;
                    if (localLightCount > MaxSceneLocalLights)
                    {
                        diagnostics.Add(new GltfDiagnostic(
                            GltfDiagnosticLevel.Warning,
                            "gltf.light.local.runtime-limit",
                            $"Local lights exceed engine budget limit of {MaxSceneLocalLights}."));
                    }

                    compBuilder.Add(new GltfPointLightComponent(color, light.Intensity, light.Range ?? 0.0f));
                }
                else if (string.Equals(light.Type, "spot", StringComparison.OrdinalIgnoreCase))
                {
                    localLightCount++;
                    if (localLightCount > MaxSceneLocalLights)
                    {
                        diagnostics.Add(new GltfDiagnostic(
                            GltfDiagnosticLevel.Warning,
                            "gltf.light.local.runtime-limit",
                            $"Local lights exceed engine budget limit of {MaxSceneLocalLights}."));
                    }

                    compBuilder.Add(new GltfSpotLightComponent(
                        color,
                        light.Intensity,
                        light.Range ?? 0.0f,
                        light.InnerConeAngle,
                        light.OuterConeAngle));
                }
            }

            if (firstRoot && animationClipIds.Length > 0)
            {
                compBuilder.Add(new GltfAnimatorComponent(
                    animationClipIds,
                    animationClipIds[0],
                    PlayOnStart: true,
                    Loop: true,
                    Speed: 1.0f));
                firstRoot = false;
            }

            if (node.Mesh.HasValue && node.Mesh.Value >= 0 && node.Mesh.Value < model.Meshes.Count)
            {
                GltfRawModel.Mesh mesh = model.Meshes[node.Mesh.Value];
                string? skinId = (node.Skin.HasValue && skinKeyMap.TryGetValue(node.Skin.Value, out string? sKey)) ? sKey : null;

                if (mesh.Primitives.Count == 1)
                {
                    string meshKey = meshKeyMap.TryGetValue(node.Mesh.Value, out string? k) ? k : $"{node.Mesh.Value}:0";
                    int? material = mesh.Primitives[0].Material;
                    string? matKey = material.HasValue ? $"gltf/material/{material.Value}" : null;

                    compBuilder.Add(new GltfMeshRendererComponent(
                        meshKey,
                        matKey,
                        Visible: true,
                        RenderOrder: 0,
                        PassId: "ForwardPBR",
                        ReceiveLighting: true,
                        skinId,
                        mesh.Primitives[0].Targets.Count));
                }
                else
                {
                    for (int p = 0; p < mesh.Primitives.Count; p++)
                    {
                        string childActorId = $"{nodeId}/primitive/{p}";
                        string meshKey = $"{node.Mesh.Value}:{p}";
                        int? primMaterial = mesh.Primitives[p].Material;
                        string? matKey = primMaterial.HasValue ? $"gltf/material/{primMaterial.Value}" : null;

                        var childComps = ImmutableArray.Create<GltfComponentSnapshot>(
                            new GltfTransformComponent(Vec3.Zero, Quat.Identity, Vec3.One),
                            new GltfMeshRendererComponent(
                                meshKey,
                                matKey,
                                Visible: true,
                                RenderOrder: 0,
                                PassId: "ForwardPBR",
                                ReceiveLighting: true,
                                skinId,
                                mesh.Primitives[p].Targets.Count));

                        actorList.Add(new GltfActorSnapshot
                        {
                            NodeId = childActorId,
                            ParentNodeId = nodeId,
                            Name = $"{node.Name ?? "Node"}_Primitive_{p}",
                            Components = childComps,
                        });
                        allNodeIds.Add(childActorId);
                    }
                }
            }

            actorList.Add(new GltfActorSnapshot
            {
                NodeId = nodeId,
                ParentNodeId = parentId,
                Name = node.Name ?? $"Node_{nodeIdx}",
                Components = compBuilder.MoveToImmutable(),
            });

            for (int c = node.Children.Count - 1; c >= 0; c--)
            {
                stack.Push((node.Children[c], nodeId));
            }
        }

        return new GltfPrefabAsset
        {
            Id = prefabId,
            SceneIndex = sceneIndex,
            Definition = new GltfPrefabDefinition { Actors = actorList.ToImmutableArray() },
            RootNodeIds = rootNodeIds.ToImmutableArray(),
            NodeIds = allNodeIds.ToImmutableArray(),
            MeshKeys = meshKeyMap.Values.ToImmutableArray(),
            SkinKeys = skinKeyMap.Values.ToImmutableArray(),
            AnimationKeys = animationClipIds,
            MaterialKeys = ImmutableArray<string>.Empty,
            AnimationController = animationClipIds.Length > 0 ? new GltfAnimationControllerMetadata { Clips = animationClipIds } : null,
        };
    }
}
