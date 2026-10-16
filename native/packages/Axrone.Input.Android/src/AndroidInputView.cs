// This file only compiles on net10.0-android with the Android workload.
// It wires Android.Views callbacks into the portable maps and the transport queue.

using Android.Views;
using Axrone.Input;

namespace Axrone.Input.Android;

/// <summary>
/// Android host bridge: the app's <c>Activity</c> forwards its
/// <c>OnKeyDown/OnKeyUp/OnTouchEvent/OnGenericMotionEvent</c> overrides here.
/// Key/mouse/touch/gamepad translate through the portable maps into raw
/// transport events. Multi-gamepad slots assign on first sight (max four).
/// </summary>
public sealed class AndroidInputHost
{
    private readonly IInputEventQueue _queue;
    private readonly Dictionary<int, byte> _contacts = new();
    private readonly Dictionary<int, byte> _gamepads = new();

    /// <summary>Creates a host.</summary>
    /// <param name="queue">Transport queue.</param>
    public AndroidInputHost(IInputEventQueue queue)
    {
        ArgumentNullException.ThrowIfNull(queue);
        _queue = queue;
    }

    /// <summary>Raised when window focus changes; forward to <c>InputEngine.SetFocus</c>.</summary>
    public event Action<bool>? FocusChanged;

    /// <summary>Notifies window focus changes.</summary>
    /// <param name="focused">Whether the window is focused.</param>
    public void OnWindowFocusChanged(bool focused) => FocusChanged?.Invoke(focused);

    /// <summary>Handles a key transition.</summary>
    /// <param name="keyCode">Android keycode.</param>
    /// <param name="e">Key event.</param>
    /// <param name="down">Whether pressed.</param>
    /// <returns><see langword="true"/> when consumed.</returns>
    public bool OnKey(Keycode keyCode, KeyEvent e, bool down)
    {
        int code = (int)keyCode;
        InputTimestamp timestamp = InputTimestamp.Now();

        if (AndroidMotionMap.IsGamepadSource((int)e.Source))
        {
            if (AndroidGamepadMap.TryToButton(code, out ushort button))
            {
                byte slot = GamepadSlot(e.DeviceId);
                var type = down ? RawInputType.GamepadButtonDown : RawInputType.GamepadButtonUp;
                _queue.TryEnqueue(new RawInputEvent(timestamp, type, (DeviceIndex)slot, button));
                return true;
            }

            return false;
        }

        Key key = AndroidKeyMap.ToKey(code);
        if (key == Key.Unknown)
        {
            return false;
        }

        if (down)
        {
            _queue.TryEnqueue(RawInputEvent.CreateKeyDown(DeviceIndex.Primary, key, (ushort)e.ScanCode, timestamp));
            if (e.RepeatCount == 0 && e.UnicodeChar != 0)
            {
                _queue.TryEnqueue(RawInputEvent.CreateKeyChar(DeviceIndex.Primary, (char)e.UnicodeChar, timestamp));
            }
        }
        else
        {
            _queue.TryEnqueue(RawInputEvent.CreateKeyUp(DeviceIndex.Primary, key, (ushort)e.ScanCode, timestamp));
        }

        return true;
    }

    /// <summary>Handles a touch or mouse motion event.</summary>
    /// <param name="e">Motion event.</param>
    public void OnTouch(MotionEvent e)
    {
        MotionEventActions action = e.ActionMasked;
        InputTimestamp timestamp = InputTimestamp.Now();

        for (int i = 0; i < e.PointerCount; i++)
        {
            int pointerId = e.GetPointerId(i);
            float x = e.GetX(i);
            float y = e.GetY(i);
            bool isActionPointer = i == e.ActionIndex;

            if (e.GetToolType(i) == MotionEventToolType.Mouse)
            {
                FeedMouse(action, isActionPointer, x, y, e, timestamp);
                continue;
            }

            switch (action)
            {
                case MotionEventActions.Down:
                case MotionEventActions.PointerDown:
                    if (isActionPointer)
                    {
                        _queue.TryEnqueue(RawInputEvent.CreateTouch(ContactSlot(pointerId), TouchPhase.Start, x, y, timestamp));
                    }

                    break;
                case MotionEventActions.Move:
                    if (TryContactSlot(pointerId, out byte moveSlot))
                    {
                        _queue.TryEnqueue(RawInputEvent.CreateTouch(moveSlot, TouchPhase.Move, x, y, timestamp));
                    }

                    break;
                case MotionEventActions.Up:
                case MotionEventActions.PointerUp:
                    if (isActionPointer && ReleaseContactSlot(pointerId, out byte upSlot))
                    {
                        _queue.TryEnqueue(RawInputEvent.CreateTouch(upSlot, TouchPhase.End, x, y, timestamp));
                    }

                    break;
                case MotionEventActions.Cancel:
                    if (ReleaseContactSlot(pointerId, out byte cancelSlot))
                    {
                        _queue.TryEnqueue(RawInputEvent.CreateTouch(cancelSlot, TouchPhase.Cancel, x, y, timestamp));
                    }

                    break;
                default:
                    break;
            }
        }

        if (action == MotionEventActions.Scroll)
        {
            _queue.TryEnqueue(RawInputEvent.CreateMouseScroll(
                DeviceIndex.Primary,
                e.GetAxisValue(Axis.Hscroll),
                e.GetAxisValue(Axis.Vscroll),
                timestamp));
        }
    }

    /// <summary>Handles a joystick/gamepad motion event.</summary>
    /// <param name="e">Motion event.</param>
    public void OnGenericMotion(MotionEvent e)
    {
        if (!AndroidMotionMap.IsGamepadSource((int)e.Source))
        {
            return;
        }

        byte slot = GamepadSlot(e.DeviceId);
        InputTimestamp timestamp = InputTimestamp.Now();
        FeedAxis(e, slot, Axis.X, timestamp);
        FeedAxis(e, slot, Axis.Y, timestamp);
        FeedAxis(e, slot, Axis.Z, timestamp);
        FeedAxis(e, slot, Axis.Rz, timestamp);
        FeedAxis(e, slot, Axis.Rx, timestamp);
        FeedAxis(e, slot, Axis.Ry, timestamp);
        FeedAxis(e, slot, Axis.Ltrigger, timestamp);
        FeedAxis(e, slot, Axis.Rtrigger, timestamp);
        FeedAxis(e, slot, Axis.HatX, timestamp);
        FeedAxis(e, slot, Axis.HatY, timestamp);
    }

    /// <summary>Marks a gamepad slot connected or disconnected.</summary>
    /// <param name="slot">Gamepad slot.</param>
    /// <param name="connected">Whether connected.</param>
    public void SetGamepadConnected(byte slot, bool connected) =>
        _queue.TryEnqueue(RawInputEvent.CreateGamepadConnection(slot, connected, InputTimestamp.Now()));

    /// <summary>Feeds one axis when mapped.</summary>
    private void FeedAxis(MotionEvent e, byte slot, Axis axis, InputTimestamp timestamp)
    {
        if (AndroidGamepadMap.TryToAxis((int)axis, out ushort engineAxis))
        {
            _queue.TryEnqueue(RawInputEvent.CreateGamepadAxis(
                (DeviceIndex)slot, (byte)engineAxis, e.GetAxisValue(axis), timestamp));
        }
    }

    /// <summary>Feeds mouse tool contacts.</summary>
    private void FeedMouse(
        MotionEventActions action,
        bool isActionPointer,
        float x,
        float y,
        MotionEvent e,
        InputTimestamp timestamp)
    {
        switch (action)
        {
            case MotionEventActions.Down when isActionPointer:
                _queue.TryEnqueue(RawInputEvent.CreateMouseMove(DeviceIndex.Primary, x, y, timestamp));
                _queue.TryEnqueue(RawInputEvent.CreateMouseDown(DeviceIndex.Primary, MouseButton.Left, timestamp));
                break;
            case MotionEventActions.Move:
                _queue.TryEnqueue(RawInputEvent.CreateMouseMove(DeviceIndex.Primary, x, y, timestamp));
                break;
            case MotionEventActions.Up when isActionPointer:
                _queue.TryEnqueue(RawInputEvent.CreateMouseUp(DeviceIndex.Primary, MouseButton.Left, timestamp));
                break;
            default:
                _ = e;
                break;
        }
    }

    /// <summary>Assigns a contact slot for a pointer id.</summary>
    private byte ContactSlot(int pointerId)
    {
        if (_contacts.TryGetValue(pointerId, out byte slot))
        {
            return slot;
        }

        for (byte candidate = 0; candidate < 8; candidate++)
        {
            if (!_contacts.ContainsValue(candidate))
            {
                _contacts[pointerId] = candidate;
                return candidate;
            }
        }

        return 7;
    }

    /// <summary>Looks up a contact slot.</summary>
    private bool TryContactSlot(int pointerId, out byte slot) =>
        _contacts.TryGetValue(pointerId, out slot);

    /// <summary>Releases a contact slot.</summary>
    private bool ReleaseContactSlot(int pointerId, out byte slot)
    {
        if (_contacts.TryGetValue(pointerId, out slot))
        {
            _contacts.Remove(pointerId);
            return true;
        }

        return false;
    }

    /// <summary>Assigns a gamepad slot for a device id.</summary>
    private byte GamepadSlot(int deviceId)
    {
        if (_gamepads.TryGetValue(deviceId, out byte slot))
        {
            return slot;
        }

        for (byte candidate = 0; candidate < 4; candidate++)
        {
            if (!_gamepads.ContainsValue(candidate))
            {
                _gamepads[deviceId] = candidate;
                _queue.TryEnqueue(RawInputEvent.CreateGamepadConnection(candidate, true, InputTimestamp.Now()));
                return candidate;
            }
        }

        return 0;
    }
}
