namespace Axrone.Input;

/// <summary>
/// Hub consumer for committed action transitions: invokes the subscriber
/// callback when the optional action and phase filters match. The struct is
/// boxed once at subscribe time; per-dispatch delivery allocates nothing.
/// </summary>
/// <typeparam name="TSchema">Action enum.</typeparam>
internal readonly struct InputActionDispatch<TSchema> : Axrone.Event.Hub.IEventConsumer<InputActionCallbackContext<TSchema>>
    where TSchema : unmanaged, Enum
{
    private readonly Action<InputActionCallbackContext<TSchema>> _callback;
    private readonly TSchema _action;
    private readonly bool _hasAction;
    private readonly InputActionPhase[]? _phases;

    /// <summary>Creates a dispatch entry.</summary>
    /// <param name="callback">Callback to invoke.</param>
    /// <param name="action">Action filter, or any action when null.</param>
    /// <param name="phases">Phase filter, or all phases when null.</param>
    public InputActionDispatch(
        Action<InputActionCallbackContext<TSchema>> callback,
        TSchema? action,
        InputActionPhase[]? phases)
    {
        _callback = callback;
        _action = action ?? default;
        _hasAction = action.HasValue;
        _phases = phases;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnEvent(scoped in InputActionCallbackContext<TSchema> evt)
    {
        if (_hasAction && !_action.Equals(evt.Action))
        {
            return;
        }

        if (_phases is { Length: > 0 } phases && Array.IndexOf(phases, evt.Phase) < 0)
        {
            return;
        }

        _callback(evt);
    }
}
