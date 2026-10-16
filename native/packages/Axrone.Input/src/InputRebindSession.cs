namespace Axrone.Input;

/// <summary>
/// Single rebind session: collects the first control actuation that passes
/// the device filter, magnitude threshold and accept callback, or cancels on
/// timeout. The engine owns at most one session; a newer session replaces
/// the previous one with <see cref="RebindCancelReason.Replaced"/>.
/// </summary>
/// <typeparam name="TSchema">Action enum.</typeparam>
public sealed class InputRebindSession<TSchema> where TSchema : unmanaged, Enum
{
    private readonly RebindRequest<TSchema> _request;
    private readonly RebindHandlers<TSchema> _handlers;
    private readonly double _startMs;
    private bool _active = true;

    /// <summary>Creates a session.</summary>
    /// <param name="request">Rebind request.</param>
    /// <param name="handlers">Session handlers.</param>
    /// <param name="startMs">Start time in milliseconds.</param>
    public InputRebindSession(RebindRequest<TSchema> request, RebindHandlers<TSchema> handlers, double startMs)
    {
        _request = request;
        _handlers = handlers;
        _startMs = startMs;
    }

    /// <summary>Whether the session still listens.</summary>
    public bool IsActive => _active;

    /// <summary>Original request.</summary>
    public RebindRequest<TSchema> Request => _request;

    /// <summary>Offers one actuated control.</summary>
    /// <param name="path">Candidate control address.</param>
    /// <param name="magnitude">Actuation magnitude.</param>
    /// <param name="timestampMs">Sample time in milliseconds.</param>
    /// <returns><see langword="true"/> when the session completed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryOffer(in InputControlPath path, float magnitude, double timestampMs)
    {
        if (!_active)
        {
            return false;
        }

        if (_request.DeviceFilter != DeviceKind.None && path.Device != _request.DeviceFilter)
        {
            return false;
        }

        if (MathF.Abs(magnitude) < _request.Threshold)
        {
            return false;
        }

        var candidate = new InputRebindingCandidate(path, path.Device, timestampMs);
        if (_handlers.Accept is not null && !_handlers.Accept(candidate))
        {
            return false;
        }

        _active = false;
        _handlers.Complete?.Invoke(InputBinding.Control(path));
        return true;
    }

    /// <summary>Advances the timeout clock.</summary>
    /// <param name="nowMs">Current time in milliseconds.</param>
    /// <returns><see langword="true"/> when the session timed out.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Update(double nowMs)
    {
        if (!_active)
        {
            return false;
        }

        if (nowMs - _startMs < _request.TimeoutMs)
        {
            return false;
        }

        _active = false;
        _handlers.Cancel?.Invoke(RebindCancelReason.Timeout);
        return true;
    }

    /// <summary>Cancels with a reason.</summary>
    /// <param name="reason">Cancel reason.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Cancel(RebindCancelReason reason)
    {
        if (!_active)
        {
            return;
        }

        _active = false;
        _handlers.Cancel?.Invoke(reason);
    }
}
