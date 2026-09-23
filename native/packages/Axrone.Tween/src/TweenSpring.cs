namespace Axrone.Tween;

using System.Numerics;

/// <summary>
/// Spring configuration: semi-implicit Euler integration over mass, stiffness, and damping.
/// </summary>
/// <param name="Stiffness">Restoring force per unit displacement; must be positive.</param>
/// <param name="Damping">Velocity damping; zero oscillates forever, higher settles faster.</param>
/// <param name="Mass">Inertia; must be positive.</param>
/// <param name="Precision">Settle band: position and velocity under this snap to rest.</param>
/// <param name="MaxStepSeconds">Substep ceiling; large deltas split instead of exploding.</param>
public readonly record struct SpringConfig(
    float Stiffness,
    float Damping,
    float Mass,
    float Precision = 0.001f,
    float MaxStepSeconds = 0.064f);

/// <summary>
/// Target-tracking spring driver: retargetable, pausable, deterministic. A complement to
/// keyframed tweens for UI snap, camera follow, and anything that chases a moving goal.
/// </summary>
/// <remarks>
/// Integration is semi-implicit Euler with substepping: a one-second stall takes sixteen
/// stable 64ms bites instead of one exploding leap. The bite size also respects the
/// stability limit (one over the natural frequency), so stiff springs subdivide further
/// instead of blowing up. The trajectory is a pure function of the state and the delta
/// sequence, so replays are bit-identical. Settling snaps exactly to target and fires
/// completion once; retargeting re-arms automatically.
/// </remarks>
public sealed class TweenSpring
{
    private readonly SpringConfig _config;
    private readonly float _stepCeiling;
    private readonly byte _channels;
    private Vector128<float> _position;
    private Vector128<float> _velocity;
    private Vector128<float> _target;
    private bool _running;
    private bool _paused;

    /// <summary>Per-tick callback for single-lane springs.</summary>
    public Action<float>? OnUpdateFloat { get; set; }

    /// <summary>Per-tick callback for two-lane springs.</summary>
    public Action<Vector2>? OnUpdateVector2 { get; set; }

    /// <summary>Per-tick callback for three-lane springs.</summary>
    public Action<Vector3>? OnUpdateVector3 { get; set; }

    /// <summary>Per-tick callback for four-lane springs.</summary>
    public Action<Vector4>? OnUpdateVector4 { get; set; }

    /// <summary>Fires once when the spring settles on target.</summary>
    public Action? OnComplete { get; set; }

    /// <summary>Creates a spring over the given lane count.</summary>
    public TweenSpring(SpringConfig config, byte channels = 1)
    {
        if (config.Stiffness <= 0.0f || !float.IsFinite(config.Stiffness))
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(config));
        }

        if (config.Damping < 0.0f || !float.IsFinite(config.Damping))
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(config));
        }

        if (config.Mass <= 0.0f || !float.IsFinite(config.Mass))
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(config));
        }

        if (config.Precision <= 0.0f || !float.IsFinite(config.Precision))
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(config));
        }

        if (config.MaxStepSeconds <= 0.0f || !float.IsFinite(config.MaxStepSeconds))
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(config));
        }

        if (channels is < 1 or > 4)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(channels));
        }

        _config = config;
        float naturalFrequency = MathF.Sqrt(config.Stiffness / config.Mass);
        _stepCeiling = Math.Min(config.MaxStepSeconds, 1.0f / naturalFrequency);
        _channels = channels;
    }

    /// <summary>Whether the spring is still moving toward its target.</summary>
    public bool IsRunning => _running && !_paused;

    /// <summary>Launches from a start toward a target with zero velocity.</summary>
    public void Reset(float current, float target)
    {
        _position = Vector128.Create(current, 0.0f, 0.0f, 0.0f);
        _velocity = Vector128<float>.Zero;
        _target = Vector128.Create(target, 0.0f, 0.0f, 0.0f);
        _running = true;
        _paused = false;
    }

    /// <summary>Launches from a start toward a target with zero velocity.</summary>
    public void Reset(Vector2 current, Vector2 target)
    {
        _position = Vector128.Create(current.X, current.Y, 0.0f, 0.0f);
        _velocity = Vector128<float>.Zero;
        _target = Vector128.Create(target.X, target.Y, 0.0f, 0.0f);
        _running = true;
        _paused = false;
    }

    /// <summary>Launches from a start toward a target with zero velocity.</summary>
    public void Reset(Vector3 current, Vector3 target)
    {
        _position = Vector128.Create(current.X, current.Y, current.Z, 0.0f);
        _velocity = Vector128<float>.Zero;
        _target = Vector128.Create(target.X, target.Y, target.Z, 0.0f);
        _running = true;
        _paused = false;
    }

    /// <summary>Launches from a start toward a target with zero velocity.</summary>
    public void Reset(Vector4 current, Vector4 target)
    {
        _position = current.AsVector128();
        _velocity = Vector128<float>.Zero;
        _target = target.AsVector128();
        _running = true;
        _paused = false;
    }

    /// <summary>Moves the goal; a settled spring re-arms automatically.</summary>
    public void Retarget(float target)
    {
        _target = Vector128.Create(target, _target.GetElement(1), _target.GetElement(2), _target.GetElement(3));
        _running = true;
    }

    /// <summary>Moves the goal; a settled spring re-arms automatically.</summary>
    public void Retarget(Vector2 target)
    {
        _target = Vector128.Create(target.X, target.Y, _target.GetElement(2), _target.GetElement(3));
        _running = true;
    }

    /// <summary>Moves the goal; a settled spring re-arms automatically.</summary>
    public void Retarget(Vector3 target)
    {
        _target = Vector128.Create(target.X, target.Y, target.Z, _target.GetElement(3));
        _running = true;
    }

    /// <summary>Moves the goal; a settled spring re-arms automatically.</summary>
    public void Retarget(Vector4 target)
    {
        _target = target.AsVector128();
        _running = true;
    }

    /// <summary>Pauses integration; state is held, not lost.</summary>
    public void Pause() => _paused = true;

    /// <summary>Resumes a paused spring.</summary>
    public void Resume() => _paused = false;

    /// <summary>Stops dead: velocity cleared, completion suppressed.</summary>
    public void Stop()
    {
        _running = false;
        _paused = false;
        _velocity = Vector128<float>.Zero;
    }

    /// <summary>Current position (single lane).</summary>
    public float Position => _position.GetElement(0);

    /// <summary>Current velocity (single lane).</summary>
    public float Velocity => _velocity.GetElement(0);

    /// <summary>
    /// Advances the simulation, firing the update callback once with the end state.
    /// Returns true while still moving. Zero and negative deltas are no-ops.
    /// </summary>
    public bool Update(DurationNs delta)
    {
        if (!_running || _paused || delta.Value <= 0)
        {
            return _running && !_paused;
        }

        float remaining = (float)(delta.Value * 1e-9);
        float ceiling = _stepCeiling;
        while (remaining > 0.0f)
        {
            float step = Math.Min(remaining, ceiling);
            Integrate(step);
            remaining -= step;
        }

        Emit();

        if (Settled())
        {
            _position = _target;
            _velocity = Vector128<float>.Zero;
            _running = false;
            Emit();
            OnComplete?.Invoke();
            return false;
        }

        return true;
    }

    private void Integrate(float dt)
    {
        float stiffness = _config.Stiffness;
        float damping = _config.Damping;
        float mass = _config.Mass;
        for (int lane = 0; lane < _channels; lane++)
        {
            float displacement = _target.GetElement(lane) - _position.GetElement(lane);
            float acceleration = ((stiffness * displacement) - (damping * _velocity.GetElement(lane))) / mass;
            _velocity = _velocity.WithElement(lane, _velocity.GetElement(lane) + (acceleration * dt));
            _position = _position.WithElement(lane, _position.GetElement(lane) + (_velocity.GetElement(lane) * dt));
        }
    }

    private bool Settled()
    {
        float precision = _config.Precision;
        for (int lane = 0; lane < _channels; lane++)
        {
            if (Math.Abs(_target.GetElement(lane) - _position.GetElement(lane)) > precision)
            {
                return false;
            }

            if (Math.Abs(_velocity.GetElement(lane)) > precision)
            {
                return false;
            }
        }

        return true;
    }

    private void Emit()
    {
        switch (_channels)
        {
            case 1:
                OnUpdateFloat?.Invoke(_position.GetElement(0));
                break;
            case 2:
                OnUpdateVector2?.Invoke(new Vector2(_position.GetElement(0), _position.GetElement(1)));
                break;
            case 3:
                OnUpdateVector3?.Invoke(new Vector3(_position.GetElement(0), _position.GetElement(1), _position.GetElement(2)));
                break;
            default:
                OnUpdateVector4?.Invoke(_position.AsVector4());
                break;
        }
    }
}
