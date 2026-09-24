namespace Axrone.Animation;

/// <summary>Layer composition mode.</summary>
public enum LayerMode
{
    /// <summary>Masked override blend.</summary>
    Override = 0,

    /// <summary>Rest-relative additive blend.</summary>
    Additive = 1,
}

/// <summary>One state machine lane with weight, mode, and optional bone mask.</summary>
public sealed class AnimationLayer
{
    /// <summary>Layer identity.</summary>
    public LayerId Id { get; }

    /// <summary>Driven state machine.</summary>
    public StateMachineInstance Machine { get; }

    /// <summary>Composition mode.</summary>
    public LayerMode Mode { get; init; } = LayerMode.Override;

    /// <summary>Layer weight.</summary>
    public float Weight { get; set; } = 1.0f;

    /// <summary>Optional bone mask (null includes everything).</summary>
    public AnimationMask? BoneMask { get; init; }

    /// <summary>Creates a layer.</summary>
    public AnimationLayer(LayerId id, StateMachineInstance machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        Id = id;
        Machine = machine;
    }
}

/// <summary>
/// Top-level runtime: advances layer state machines, composes override/additive
/// layers onto the current frame, extracts layer-zero root motion, and clears
/// triggers at the frame boundary.
/// </summary>
public sealed class AnimationController : IDisposable
{
    private readonly List<AnimationLayer> _layers = new();

    /// <summary>Driven rig.</summary>
    public Rig Rig { get; }

    /// <summary>Shared parameters.</summary>
    public ParameterStore Parameters { get; }

    /// <summary>Composed output frame.</summary>
    public AnimationFrame CurrentFrame { get; }

    /// <summary>Scratch arena.</summary>
    public FrameArena Arena { get; }

    /// <summary>Creates a controller over a rig.</summary>
    public AnimationController(Rig rig, ParameterStore parameters, Dictionary<CurveId, int> curveLayout, int arenaCapacity = 64)
    {
        ArgumentNullException.ThrowIfNull(rig);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(curveLayout);
        Rig = rig;
        Parameters = parameters;
        CurrentFrame = new AnimationFrame(rig.BoneCount, curveLayout);
        Arena = new FrameArena(rig.BoneCount, curveLayout, arenaCapacity);
        CurrentFrame.ResetToRest(rig);
    }

    /// <summary>Adds a layer (index 0 is the base).</summary>
    public void AddLayer(AnimationLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        _layers.Add(layer);
    }

    /// <summary>Layer count.</summary>
    public int LayerCount => _layers.Count;

    /// <summary>
    /// Advances all weighted layers, composes the frame, and reports root motion.
    /// The event list is caller-owned and cleared first.
    /// </summary>
    public void Update(float deltaTime, ICollection<ClipEvent> outEvents, out Vector3 rootMotionDeltaPos, out Quaternion rootMotionDeltaRot)
    {
        ArgumentNullException.ThrowIfNull(outEvents);
        long startTimestamp = Stopwatch.GetTimestamp();
        outEvents.Clear();
        Arena.Reset();

        for (int i = 0; i < _layers.Count; i++)
        {
            AnimationLayer layer = _layers[i];
            if (layer.Weight <= 0.0f)
            {
                continue;
            }

            layer.Machine.Update(deltaTime, Parameters, outEvents, layer.Weight);
        }

        if (_layers.Count > 0 && _layers[0].Weight > 0.0f)
        {
            _layers[0].Machine.Evaluate(CurrentFrame, Arena, Rig, Parameters);
            _layers[0].Machine.ExtractRootDelta(Rig, out rootMotionDeltaPos, out rootMotionDeltaRot);
        }
        else
        {
            CurrentFrame.ResetToRest(Rig);
            rootMotionDeltaPos = Vector3.Zero;
            rootMotionDeltaRot = Quaternion.Identity;
        }

        for (int i = 1; i < _layers.Count; i++)
        {
            AnimationLayer layer = _layers[i];
            float weight = FastMath.Clamp01(layer.Weight);
            if (weight <= 0.0f)
            {
                continue;
            }

            AnimationFrame layerFrame = Arena.Alloc();
            layer.Machine.Evaluate(layerFrame, Arena, Rig, Parameters);

            if (layer.Mode == LayerMode.Override)
            {
                BlendingKernels.BlendFrame(CurrentFrame, CurrentFrame, layerFrame, weight, layer.BoneMask);
            }
            else
            {
                BlendingKernels.ApplyAdditiveFrame(CurrentFrame, CurrentFrame, layerFrame, Rig, weight);
            }

            Arena.Free();
        }

        Parameters.ClearTriggers();

        AnimationTelemetry.RecordFrameEvaluated();
        AnimationTelemetry.RecordEvaluationLatency(Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Arena.Reset();
        GC.SuppressFinalize(this);
    }
}
