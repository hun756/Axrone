namespace Axrone.Input;

/// <summary>
/// Silk.NET host feed: translates Silk-compatible device callbacks into raw
/// transport events. The <see cref="Key"/>, <see cref="MouseButton"/> and
/// <see cref="ButtonName"/> codes already match Silk.NET, so hosts forward
/// values without remapping; gamepad buttons translate to engine numbering.
/// Timestamps sample at enqueue time.
/// </summary>
public static class SilkInputFeed
{
    /// <summary>Feeds a key press.</summary>
    /// <param name="buffer">Transport queue.</param>
    /// <param name="key">Silk key code.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FeedKeyDown<TBackoff, TMetrics>(
        this LockFreeInputRingBuffer<TBackoff, TMetrics> buffer, Key key)
        where TBackoff : struct, IBackoffPolicy
        where TMetrics : struct, IInputMetricsSink
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return buffer.TryEnqueue(RawInputEvent.CreateKeyDown(DeviceIndex.Primary, key, 0, InputTimestamp.Now()));
    }

    /// <summary>Feeds a key release.</summary>
    /// <param name="buffer">Transport queue.</param>
    /// <param name="key">Silk key code.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FeedKeyUp<TBackoff, TMetrics>(
        this LockFreeInputRingBuffer<TBackoff, TMetrics> buffer, Key key)
        where TBackoff : struct, IBackoffPolicy
        where TMetrics : struct, IInputMetricsSink
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return buffer.TryEnqueue(RawInputEvent.CreateKeyUp(DeviceIndex.Primary, key, 0, InputTimestamp.Now()));
    }

    /// <summary>Feeds a typed character.</summary>
    /// <param name="buffer">Transport queue.</param>
    /// <param name="character">Typed character.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FeedKeyChar<TBackoff, TMetrics>(
        this LockFreeInputRingBuffer<TBackoff, TMetrics> buffer, char character)
        where TBackoff : struct, IBackoffPolicy
        where TMetrics : struct, IInputMetricsSink
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return buffer.TryEnqueue(RawInputEvent.CreateKeyChar(DeviceIndex.Primary, character, InputTimestamp.Now()));
    }

    /// <summary>Feeds a mouse-button press.</summary>
    /// <param name="buffer">Transport queue.</param>
    /// <param name="button">Silk mouse button.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FeedMouseDown<TBackoff, TMetrics>(
        this LockFreeInputRingBuffer<TBackoff, TMetrics> buffer, MouseButton button)
        where TBackoff : struct, IBackoffPolicy
        where TMetrics : struct, IInputMetricsSink
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return buffer.TryEnqueue(RawInputEvent.CreateMouseDown(DeviceIndex.Primary, button, InputTimestamp.Now()));
    }

    /// <summary>Feeds a mouse-button release.</summary>
    /// <param name="buffer">Transport queue.</param>
    /// <param name="button">Silk mouse button.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FeedMouseUp<TBackoff, TMetrics>(
        this LockFreeInputRingBuffer<TBackoff, TMetrics> buffer, MouseButton button)
        where TBackoff : struct, IBackoffPolicy
        where TMetrics : struct, IInputMetricsSink
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return buffer.TryEnqueue(RawInputEvent.CreateMouseUp(DeviceIndex.Primary, button, InputTimestamp.Now()));
    }

    /// <summary>Feeds an absolute pointer position.</summary>
    /// <param name="buffer">Transport queue.</param>
    /// <param name="x">Pointer X.</param>
    /// <param name="y">Pointer Y.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FeedMouseMove<TBackoff, TMetrics>(
        this LockFreeInputRingBuffer<TBackoff, TMetrics> buffer, float x, float y)
        where TBackoff : struct, IBackoffPolicy
        where TMetrics : struct, IInputMetricsSink
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return buffer.TryEnqueue(RawInputEvent.CreateMouseMove(DeviceIndex.Primary, x, y, InputTimestamp.Now()));
    }

    /// <summary>Feeds a wheel scroll.</summary>
    /// <param name="buffer">Transport queue.</param>
    /// <param name="x">Horizontal scroll.</param>
    /// <param name="y">Vertical scroll.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FeedMouseScroll<TBackoff, TMetrics>(
        this LockFreeInputRingBuffer<TBackoff, TMetrics> buffer, float x, float y)
        where TBackoff : struct, IBackoffPolicy
        where TMetrics : struct, IInputMetricsSink
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return buffer.TryEnqueue(RawInputEvent.CreateMouseScroll(DeviceIndex.Primary, x, y, InputTimestamp.Now()));
    }

    /// <summary>Feeds a gamepad button transition in Silk codes.</summary>
    /// <param name="buffer">Transport queue.</param>
    /// <param name="slot">Gamepad slot.</param>
    /// <param name="name">Silk button name.</param>
    /// <param name="down">Whether pressed.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FeedGamepadButton<TBackoff, TMetrics>(
        this LockFreeInputRingBuffer<TBackoff, TMetrics> buffer, byte slot, ButtonName name, bool down)
        where TBackoff : struct, IBackoffPolicy
        where TMetrics : struct, IInputMetricsSink
    {
        ArgumentNullException.ThrowIfNull(buffer);
        DeviceIndex device = (DeviceIndex)slot;
        ushort code = InputControlReader.MapSilkButton((ushort)name);
        InputTimestamp timestamp = InputTimestamp.Now();
        return down
            ? buffer.TryEnqueue(new RawInputEvent(timestamp, RawInputType.GamepadButtonDown, device, code))
            : buffer.TryEnqueue(new RawInputEvent(timestamp, RawInputType.GamepadButtonUp, device, code));
    }

    /// <summary>Feeds a gamepad axis position.</summary>
    /// <param name="buffer">Transport queue.</param>
    /// <param name="slot">Gamepad slot.</param>
    /// <param name="axis">Axis index.</param>
    /// <param name="value">Axis position.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FeedGamepadAxis<TBackoff, TMetrics>(
        this LockFreeInputRingBuffer<TBackoff, TMetrics> buffer, byte slot, byte axis, float value)
        where TBackoff : struct, IBackoffPolicy
        where TMetrics : struct, IInputMetricsSink
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return buffer.TryEnqueue(RawInputEvent.CreateGamepadAxis((DeviceIndex)slot, axis, value, InputTimestamp.Now()));
    }

    /// <summary>Feeds a touch-contact transition.</summary>
    /// <param name="buffer">Transport queue.</param>
    /// <param name="contactId">Contact slot.</param>
    /// <param name="phase">Contact phase.</param>
    /// <param name="x">Contact X.</param>
    /// <param name="y">Contact Y.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FeedTouch<TBackoff, TMetrics>(
        this LockFreeInputRingBuffer<TBackoff, TMetrics> buffer, byte contactId, TouchPhase phase, float x, float y)
        where TBackoff : struct, IBackoffPolicy
        where TMetrics : struct, IInputMetricsSink
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return buffer.TryEnqueue(RawInputEvent.CreateTouch(contactId, phase, x, y, InputTimestamp.Now()));
    }

    /// <summary>Feeds a gamepad connection change.</summary>
    /// <param name="buffer">Transport queue.</param>
    /// <param name="slot">Gamepad slot.</param>
    /// <param name="connected">Whether connected.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FeedGamepadConnection<TBackoff, TMetrics>(
        this LockFreeInputRingBuffer<TBackoff, TMetrics> buffer, byte slot, bool connected)
        where TBackoff : struct, IBackoffPolicy
        where TMetrics : struct, IInputMetricsSink
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return buffer.TryEnqueue(RawInputEvent.CreateGamepadConnection(slot, connected, InputTimestamp.Now()));
    }
}
