namespace Axrone.Animation;

/// <summary>
/// Closed-world graph dispatch: the motion kinds form a fixed set of sealed
/// node types, so every edge routes through a <see cref="MotionKind"/> switch
/// instead of a virtual call. Each arm targets a sealed override the JIT can
/// inline — a heterogeneous recursive graph cannot be monomorphized with
/// static-abstract members (storage would erase the type), and this switch
/// is the exact C#-idiomatic equivalent: one predictable branch per edge,
/// zero vtable, inlinable leaves.
/// Virtual members stay as the extensibility contract; every internal edge
/// uses this dispatcher.
/// </summary>
public static class MotionDispatcher
{
    /// <summary>Evaluates a node into a frame.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Evaluate(
        MotionNode node,
        float normalizedTime,
        AnimationFrame outFrame,
        FrameArena arena,
        Rig rig,
        ParameterStore parameters,
        int depth)
    {
        ArgumentNullException.ThrowIfNull(node);
        switch (node.Kind)
        {
            case MotionKind.Clip:
                ((ClipMotionNode)node).Evaluate(normalizedTime, outFrame, arena, rig, parameters, depth);
                break;
            case MotionKind.Blend1D:
                ((Blend1DMotionNode)node).Evaluate(normalizedTime, outFrame, arena, rig, parameters, depth);
                break;
            case MotionKind.Blend2D:
                ((Blend2DMotionNode)node).Evaluate(normalizedTime, outFrame, arena, rig, parameters, depth);
                break;
            case MotionKind.Direct:
                ((DirectMotionNode)node).Evaluate(normalizedTime, outFrame, arena, rig, parameters, depth);
                break;
            case MotionKind.Additive:
                ((AdditiveMotionNode)node).Evaluate(normalizedTime, outFrame, arena, rig, parameters, depth);
                break;
            default:
                AnimationThrowHelper.ThrowEvaluation(AnimationErrorCode.EvaluationMotionKindInvalid, $"Unknown motion kind '{node.Kind}'.");
                break;
        }
    }

    /// <summary>Computes a node's root-joint delta.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeRootDelta(
        MotionNode node,
        float prevNormTime,
        float curNormTime,
        Rig rig,
        out Vector3 deltaPos,
        out Quaternion deltaRot)
    {
        ArgumentNullException.ThrowIfNull(node);
        switch (node.Kind)
        {
            case MotionKind.Clip:
                ((ClipMotionNode)node).ComputeRootDelta(prevNormTime, curNormTime, rig, out deltaPos, out deltaRot);
                break;
            case MotionKind.Blend1D:
                ((Blend1DMotionNode)node).ComputeRootDelta(prevNormTime, curNormTime, rig, out deltaPos, out deltaRot);
                break;
            case MotionKind.Blend2D:
                ((Blend2DMotionNode)node).ComputeRootDelta(prevNormTime, curNormTime, rig, out deltaPos, out deltaRot);
                break;
            case MotionKind.Direct:
                ((DirectMotionNode)node).ComputeRootDelta(prevNormTime, curNormTime, rig, out deltaPos, out deltaRot);
                break;
            case MotionKind.Additive:
                ((AdditiveMotionNode)node).ComputeRootDelta(prevNormTime, curNormTime, rig, out deltaPos, out deltaRot);
                break;
            default:
                AnimationThrowHelper.ThrowEvaluation(AnimationErrorCode.EvaluationMotionKindInvalid, $"Unknown motion kind '{node.Kind}'.");
                deltaPos = Vector3.Zero;
                deltaRot = Quaternion.Identity;
                break;
        }
    }

    /// <summary>Collects a node's clip events into a zero-allocation sink.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CollectEvents<TSink>(
        MotionNode node,
        float prevNormTime,
        float curNormTime,
        float layerWeight,
        ref TSink sink)
        where TSink : struct, IClipEventSink
    {
        ArgumentNullException.ThrowIfNull(node);
        switch (node.Kind)
        {
            case MotionKind.Clip:
                ((ClipMotionNode)node).CollectEvents(prevNormTime, curNormTime, layerWeight, ref sink);
                break;
            case MotionKind.Blend1D:
                ((Blend1DMotionNode)node).CollectEvents(prevNormTime, curNormTime, layerWeight, ref sink);
                break;
            case MotionKind.Blend2D:
                ((Blend2DMotionNode)node).CollectEvents(prevNormTime, curNormTime, layerWeight, ref sink);
                break;
            case MotionKind.Direct:
                ((DirectMotionNode)node).CollectEvents(prevNormTime, curNormTime, layerWeight, ref sink);
                break;
            case MotionKind.Additive:
                ((AdditiveMotionNode)node).CollectEvents(prevNormTime, curNormTime, layerWeight, ref sink);
                break;
            default:
                AnimationThrowHelper.ThrowEvaluation(AnimationErrorCode.EvaluationMotionKindInvalid, $"Unknown motion kind '{node.Kind}'.");
                break;
        }
    }

    /// <summary>Collects a node's clip events into a collection.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CollectEvents(
        MotionNode node,
        float prevNormTime,
        float curNormTime,
        float layerWeight,
        ICollection<ClipEvent> outEvents)
    {
        ArgumentNullException.ThrowIfNull(outEvents);
        var adapter = new CollectionEventSinkAdapter(outEvents);
        CollectEvents(node, prevNormTime, curNormTime, layerWeight, ref adapter);
    }

    /// <summary>Reads a node's wall-clock duration.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetDuration(MotionNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.Kind switch
        {
            MotionKind.Clip => ((ClipMotionNode)node).GetDuration(),
            MotionKind.Blend1D => ((Blend1DMotionNode)node).GetDuration(),
            MotionKind.Blend2D => ((Blend2DMotionNode)node).GetDuration(),
            MotionKind.Direct => ((DirectMotionNode)node).GetDuration(),
            MotionKind.Additive => ((AdditiveMotionNode)node).GetDuration(),
            _ => throw new EvaluationException(AnimationErrorCode.EvaluationMotionKindInvalid, $"Unknown motion kind '{node.Kind}'."),
        };
    }
}
