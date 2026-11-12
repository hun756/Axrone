namespace Axrone.Scene.Runtime;

using System.Diagnostics.CodeAnalysis;

using Axrone.Animation;
using Axrone.Numeric;
using Axrone.Scene.Assets;

/// <summary>
/// Builds native animation runtime objects from scene asset definitions: a skin's joints become a
/// <see cref="Rig"/>, glTF track definitions become <see cref="AnimationClip"/> instances, and the
/// first clip becomes a ready-to-bind <see cref="AnimationController"/>.
/// </summary>
/// <remarks>
/// This is the engine-side content bridge. It intentionally does not read glTF bytes, JSON or the
/// editor document; callers hand in the already-converted <see cref="SceneSkinDefinition"/>,
/// clip definitions and the node map produced by the scene importer. The controller starts with
/// one state so a scene can play immediately; callers that need a graph can build states from
/// <see cref="SceneAnimationImportResult.Clips"/>.
/// </remarks>
public static class SceneAnimationImporter
{
    /// <summary>
    /// Creates the runtime animation objects for one skinned actor. Returns false with a
    /// human-readable reason when the skin, clips or node map are insufficient.
    /// </summary>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Ownership transfers to the returned SceneAnimationImportResult.")]
    public static bool TryImport(
        SceneWorld world,
        SceneSkinDefinition skin,
        IReadOnlyList<SceneAnimationClipDefinition> clips,
        IReadOnlyDictionary<string, SceneActor> nodeMap,
        IReadOnlyDictionary<string, string?> parentByNodeId,
        [NotNullWhen(true)] out SceneAnimationImportResult? result,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(skin);
        ArgumentNullException.ThrowIfNull(clips);
        ArgumentNullException.ThrowIfNull(nodeMap);
        ArgumentNullException.ThrowIfNull(parentByNodeId);

        result = null;
        reason = string.Empty;

        if (skin.JointNodeIds.Count == 0)
        {
            reason = "skin has no joints";
            return false;
        }

        if (clips.Count == 0)
        {
            reason = "conversion has no animation clips";
            return false;
        }

        if (FindSkinnedActor(world, nodeMap) is not { } meshActor)
        {
            reason = "node map contains no live actor";
            return false;
        }

        world.UpdateTransforms();

        int jointCount = skin.JointNodeIds.Count;
        var jointIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var jointActors = new SceneActor[jointCount];
        var bones = new BoneInfo[jointCount];

        for (int i = 0; i < jointCount; i++)
        {
            string nodeId = skin.JointNodeIds[i];
            jointIndex[nodeId] = i;

            if (!nodeMap.TryGetValue(nodeId, out SceneActor actor) || !world.TryGetActor(actor, out _))
            {
                reason = $"joint '{nodeId}' is missing from the instantiated hierarchy";
                return false;
            }

            jointActors[i] = actor;
            (Vec3 translation, Quat rotation, Vec3 scale) = ReadLocalTransform(world, actor);

            bones[i] = new BoneInfo
            {
                Name = nodeId,
                ParentIndex = ResolveParentJoint(jointIndex, parentByNodeId, nodeId),
                RestTranslation = translation,
                RestRotation = rotation,
                RestScale = scale,
            };
        }

        var rig = new Rig(new RigId($"skin:{skin.Id}"), bones);
        var runtimeClips = new List<AnimationClip>(clips.Count);

        foreach (SceneAnimationClipDefinition definition in clips)
        {
            AnimationClip? clip = BuildClip(definition, jointIndex);
            if (clip is not null)
            {
                runtimeClips.Add(clip);
            }
        }

        if (runtimeClips.Count == 0)
        {
            reason = "no clip contained a usable joint track";
            return false;
        }

        var inverseBind = new Mat4[jointCount];
        ReadOnlySpan<float> inverseBindSource = skin.InverseBindMatrices.Span;
        for (int i = 0; i < jointCount; i++)
        {
            inverseBind[i] = ReadMatrix(inverseBindSource, i);
        }

        var parameters = new ParameterStore(Array.Empty<KeyValuePair<string, ParameterType>>());
        var controller = new AnimationController(rig, parameters, new CurveLayout(new Dictionary<CurveId, int>()));
        controller.AddLayer(
            new AnimationLayer(
                new LayerId("base"),
                new StateMachineInstance(
                    [new AnimationState(new StateId("clip0"), new ClipMotionNode(runtimeClips[0]))],
                    Array.Empty<StateTransition>(),
                    0)));

        result = new SceneAnimationImportResult(
            meshActor,
            rig,
            runtimeClips,
            controller,
            inverseBind,
            jointActors);
        return true;
    }

    private static AnimationClip? BuildClip(
        SceneAnimationClipDefinition definition,
        Dictionary<string, int> jointIndex)
    {
        List<AnimationChannel> channels = [];

        foreach (SceneAnimationTrackDefinition track in definition.Tracks)
        {
            if (!jointIndex.TryGetValue(track.TargetNodeId, out int boneIndex))
            {
                continue;
            }

            ChannelTarget? target = track.Path switch
            {
                SceneAnimationPath.Translation => ChannelTarget.Translation,
                SceneAnimationPath.Rotation => ChannelTarget.Rotation,
                SceneAnimationPath.Scale => ChannelTarget.Scale,
                _ => null,
            };

            if (target is null || track.KeyframeCount < 2)
            {
                continue;
            }

            InterpolationMode interpolation = track.Interpolation switch
            {
                SceneAnimationInterpolation.Step => InterpolationMode.Step,
                SceneAnimationInterpolation.CubicSpline => InterpolationMode.CubicSpline,
                _ => InterpolationMode.Linear,
            };

            int componentCount = target.Value switch
            {
                ChannelTarget.Rotation => 4,
                _ => 3,
            };

            if (track.ValueComponentCount < componentCount)
            {
                continue;
            }

            channels.Add(new AnimationChannel(
                boneIndex,
                target.Value,
                interpolation,
                track.Times.ToArray(),
                track.Values.ToArray()));
        }

        if (channels.Count == 0)
        {
            return null;
        }

        return new AnimationClip(
            new ClipId(definition.Id.ToString()),
            definition.Duration,
            channels.ToArray(),
            tags: definition.Tags);
    }

    private static SceneActor? FindSkinnedActor(
        SceneWorld world,
        IReadOnlyDictionary<string, SceneActor> nodeMap)
    {
        SceneActor? fallback = null;

        foreach (SceneActor actor in nodeMap.Values)
        {
            if (!world.TryGetActor(actor, out _))
            {
                continue;
            }

            fallback ??= actor;
            if (world.HasData<global::Axrone.Scene.Rendering.SceneSkinnedMeshRenderer>(actor))
            {
                return actor;
            }
        }

        return fallback;
    }

    private static (Vec3 Translation, Quat Rotation, Vec3 Scale) ReadLocalTransform(
        SceneWorld world,
        SceneActor actor)
    {
        if (!world.TryGetData(actor, out SceneTransform3D transform))
        {
            return (Vec3.Zero, Quat.Identity, Vec3.One);
        }

        return (transform.Position, transform.Rotation, transform.Scale);
    }

    private static int ResolveParentJoint(
        Dictionary<string, int> jointIndex,
        IReadOnlyDictionary<string, string?> parentByNodeId,
        string nodeId)
    {
        string? parent = parentByNodeId.TryGetValue(nodeId, out string? direct) ? direct : null;

        while (parent is not null)
        {
            if (jointIndex.TryGetValue(parent, out int index))
            {
                return index;
            }

            parent = parentByNodeId.TryGetValue(parent, out string? next) ? next : null;
        }

        return -1;
    }

    private static Mat4 ReadMatrix(ReadOnlySpan<float> matrices, int index)
    {
        int offset = index * 16;
        if (matrices.Length < offset + 16)
        {
            return Mat4.Identity;
        }

        return new Mat4(
            matrices[offset + 0], matrices[offset + 1], matrices[offset + 2], matrices[offset + 3],
            matrices[offset + 4], matrices[offset + 5], matrices[offset + 6], matrices[offset + 7],
            matrices[offset + 8], matrices[offset + 9], matrices[offset + 10], matrices[offset + 11],
            matrices[offset + 12], matrices[offset + 13], matrices[offset + 14], matrices[offset + 15]);
    }
}

/// <summary>Runtime animation objects produced from one converted scene skin.</summary>
public sealed record SceneAnimationImportResult(
    SceneActor MeshActor,
    Rig Rig,
    IReadOnlyList<AnimationClip> Clips,
    AnimationController Controller,
    IReadOnlyList<Mat4> InverseBind,
    IReadOnlyList<SceneActor> JointActors) : IDisposable
{
    /// <summary>Disposes the owned controller.</summary>
    public void Dispose() => Controller.Dispose();
}
