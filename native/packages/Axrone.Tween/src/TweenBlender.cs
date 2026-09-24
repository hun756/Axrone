namespace Axrone.Tween;

using System.Numerics;

/// <summary>
/// Blendable (additive) tweens: each playthrough delivers per-tick deltas instead of
/// absolute values, so any number of blendables compose on one target without fighting.
/// </summary>
/// <remarks>
/// The baseline lives in the registration closure, not the core: per-tick execution
/// costs one subtraction per lane and no lookup. Applied deltas persist — killing a
/// blendable stops future deltas but does not roll back what was applied, matching the
/// relative-tween contract. <c>Goto</c>/<c>Rewind</c> on the raw handle jump the play
/// head without moving the baseline and produce one spike delta; restart through
/// <see cref="Restart"/> instead, which resets the baseline first.
/// </remarks>
public sealed class TweenBlender
{
    private readonly TweenEngine _engine;
    private readonly Lock _gate = new();
    private readonly Dictionary<TweenId, Action> _resets = new();

    /// <summary>Creates a blender over an engine.</summary>
    public TweenBlender(TweenEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    /// <summary>Plays a single-lane blendable, delivering deltas to <paramref name="applyDelta"/>.</summary>
    public TweenHandle PlayBlendable(TweenSpec spec, Action<float> applyDelta)
    {
        RequireArity(spec, 1);
        ArgumentNullException.ThrowIfNull(applyDelta);

        float last = spec.Start.GetElement(0);
        Action<float>? original = spec.OnUpdateFloat;
        TweenId id = default;
        TweenSpec wrapped = spec with
        {
            OnUpdateFloat = current =>
            {
                original?.Invoke(current);
                float delta = current - last;
                last = current;
                applyDelta(delta);
            },
            OnComplete = Chain(spec.OnComplete, () => Forget(id)),
            OnKill = Chain(spec.OnKill, () => Forget(id)),
        };
        if (!_engine.TryPlay(wrapped, out TweenHandle handle))
        {
            return handle;
        }

        id = handle.Id;
        Remember(id, () => last = spec.Start.GetElement(0));
        return handle;
    }

    /// <summary>Plays a two-lane blendable, delivering deltas to <paramref name="applyDelta"/>.</summary>
    public TweenHandle PlayBlendable(TweenSpec spec, Action<Vector2> applyDelta)
    {
        RequireArity(spec, 2);
        ArgumentNullException.ThrowIfNull(applyDelta);

        Vector2 last = new(spec.Start.GetElement(0), spec.Start.GetElement(1));
        Action<Vector2>? original = spec.OnUpdateVector2;
        TweenId id = default;
        TweenSpec wrapped = spec with
        {
            OnUpdateVector2 = current =>
            {
                original?.Invoke(current);
                Vector2 delta = current - last;
                last = current;
                applyDelta(delta);
            },
            OnComplete = Chain(spec.OnComplete, () => Forget(id)),
            OnKill = Chain(spec.OnKill, () => Forget(id)),
        };
        if (!_engine.TryPlay(wrapped, out TweenHandle handle))
        {
            return handle;
        }

        id = handle.Id;
        Vector2 start = last;
        Remember(id, () => last = start);
        return handle;
    }

    /// <summary>Plays a three-lane blendable, delivering deltas to <paramref name="applyDelta"/>.</summary>
    public TweenHandle PlayBlendable(TweenSpec spec, Action<Vector3> applyDelta)
    {
        RequireArity(spec, 3);
        ArgumentNullException.ThrowIfNull(applyDelta);

        Vector3 last = new(spec.Start.GetElement(0), spec.Start.GetElement(1), spec.Start.GetElement(2));
        Action<Vector3>? original = spec.OnUpdateVector3;
        TweenId id = default;
        TweenSpec wrapped = spec with
        {
            OnUpdateVector3 = current =>
            {
                original?.Invoke(current);
                Vector3 delta = current - last;
                last = current;
                applyDelta(delta);
            },
            OnComplete = Chain(spec.OnComplete, () => Forget(id)),
            OnKill = Chain(spec.OnKill, () => Forget(id)),
        };
        if (!_engine.TryPlay(wrapped, out TweenHandle handle))
        {
            return handle;
        }

        id = handle.Id;
        Vector3 start = last;
        Remember(id, () => last = start);
        return handle;
    }

    /// <summary>Plays a four-lane blendable, delivering deltas to <paramref name="applyDelta"/>.</summary>
    public TweenHandle PlayBlendable(TweenSpec spec, Action<Vector4> applyDelta)
    {
        RequireArity(spec, 4);
        ArgumentNullException.ThrowIfNull(applyDelta);

        Vector4 last = spec.Start.AsVector4();
        Action<Vector4>? original = spec.OnUpdateVector4;
        TweenId id = default;
        TweenSpec wrapped = spec with
        {
            OnUpdateVector4 = current =>
            {
                original?.Invoke(current);
                Vector4 delta = current - last;
                last = current;
                applyDelta(delta);
            },
            OnComplete = Chain(spec.OnComplete, () => Forget(id)),
            OnKill = Chain(spec.OnKill, () => Forget(id)),
        };
        if (!_engine.TryPlay(wrapped, out TweenHandle handle))
        {
            return handle;
        }

        id = handle.Id;
        Vector4 start = last;
        Remember(id, () => last = start);
        return handle;
    }

    /// <summary>Restarts a blendable from zero with the baseline reset. Unknown handles fail.</summary>
    public bool Restart(TweenHandle handle)
    {
        Action? reset;
        lock (_gate)
        {
            if (!_resets.TryGetValue(handle.Id, out reset))
            {
                return false;
            }
        }

        reset();
        return handle.Restart();
    }

    private static void RequireArity(TweenSpec spec, byte lanes)
    {
        if (spec.ChannelCount != lanes)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(spec), $"Blendable arity ({lanes}) must match the spec channels ({spec.ChannelCount}).");
        }
    }

    private static Action Chain(Action? original, Action second) => () =>
    {
        original?.Invoke();
        second();
    };

    private void Remember(TweenId id, Action reset)
    {
        lock (_gate)
        {
            _resets[id] = reset;
        }
    }

    private void Forget(TweenId id)
    {
        lock (_gate)
        {
            _resets.Remove(id);
        }
    }
}
