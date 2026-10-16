using SilkKey = Silk.NET.Input.Key;
using SilkMouseButton = Silk.NET.Input.MouseButton;

namespace Axrone.Input;

/// <summary>
/// Live Silk.NET attachment: subscribes an <see cref="Silk.NET.Input.IInputContext"/>
/// and forwards keyboard and mouse callbacks into the transport queue.
/// Gamepads attach in the follow-up; touch has no Silk.NET.Input API.
/// </summary>
public sealed class SilkInputAttachment : IDisposable
{
    private readonly Silk.NET.Input.IInputContext _context;
    private readonly IInputEventQueue _queue;
    private bool _disposed;

    /// <summary>Creates an attachment.</summary>
    /// <param name="context">Silk input context.</param>
    /// <param name="queue">Transport queue.</param>
    private SilkInputAttachment(Silk.NET.Input.IInputContext context, IInputEventQueue queue)
    {
        _context = context;
        _queue = queue;
    }

    /// <summary>Attaches to a Silk input context.</summary>
    /// <param name="context">Silk input context.</param>
    /// <param name="queue">Transport queue.</param>
    public static SilkInputAttachment Attach(Silk.NET.Input.IInputContext context, IInputEventQueue queue)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(queue);
        var attachment = new SilkInputAttachment(context, queue);
        attachment.SubscribeAll();
        return attachment;
    }

    /// <summary>Subscribes every known device.</summary>
    private void SubscribeAll()
    {
        for (int i = 0; i < _context.Keyboards.Count; i++)
        {
            SubscribeKeyboard(_context.Keyboards[i]);
        }

        for (int i = 0; i < _context.Mice.Count; i++)
        {
            SubscribeMouse(_context.Mice[i]);
        }
    }

    /// <summary>Subscribes one keyboard.</summary>
    /// <param name="keyboard">Silk keyboard.</param>
    private void SubscribeKeyboard(Silk.NET.Input.IKeyboard keyboard)
    {
        keyboard.KeyDown += OnKeyDown;
        keyboard.KeyUp += OnKeyUp;
        keyboard.KeyChar += OnKeyChar;
    }

    /// <summary>Subscribes one mouse.</summary>
    /// <param name="mouse">Silk mouse.</param>
    private void SubscribeMouse(Silk.NET.Input.IMouse mouse)
    {
        mouse.MouseDown += OnMouseDown;
        mouse.MouseUp += OnMouseUp;
        mouse.MouseMove += OnMouseMove;
        mouse.Scroll += OnScroll;
    }

    /// <summary>Forwards a key press.</summary>
    private void OnKeyDown(Silk.NET.Input.IKeyboard keyboard, SilkKey key, int scancode) =>
        _queue.TryWrite(RawInputEvent.CreateKeyDown(DeviceIndex.Primary, SilkInputMapping.ToKey(key), (ushort)scancode, InputTimestamp.Now()));

    /// <summary>Forwards a key release.</summary>
    private void OnKeyUp(Silk.NET.Input.IKeyboard keyboard, SilkKey key, int scancode) =>
        _queue.TryWrite(RawInputEvent.CreateKeyUp(DeviceIndex.Primary, SilkInputMapping.ToKey(key), (ushort)scancode, InputTimestamp.Now()));

    /// <summary>Forwards a typed character.</summary>
    private void OnKeyChar(Silk.NET.Input.IKeyboard keyboard, char character) =>
        _queue.TryWrite(RawInputEvent.CreateKeyChar(DeviceIndex.Primary, character, InputTimestamp.Now()));

    /// <summary>Forwards a mouse-button press.</summary>
    private void OnMouseDown(Silk.NET.Input.IMouse mouse, SilkMouseButton button)
    {
        if (SilkInputMapping.TryToMouseButton(button, out MouseButton mapped))
        {
            _queue.TryWrite(RawInputEvent.CreateMouseDown(DeviceIndex.Primary, mapped, InputTimestamp.Now()));
        }
    }

    /// <summary>Forwards a mouse-button release.</summary>
    private void OnMouseUp(Silk.NET.Input.IMouse mouse, SilkMouseButton button)
    {
        if (SilkInputMapping.TryToMouseButton(button, out MouseButton mapped))
        {
            _queue.TryWrite(RawInputEvent.CreateMouseUp(DeviceIndex.Primary, mapped, InputTimestamp.Now()));
        }
    }

    /// <summary>Forwards an absolute pointer position.</summary>
    private void OnMouseMove(Silk.NET.Input.IMouse mouse, Vector2 position) =>
        _queue.TryWrite(RawInputEvent.CreateMouseMove(DeviceIndex.Primary, position.X, position.Y, InputTimestamp.Now()));

    /// <summary>Forwards a wheel scroll.</summary>
    private void OnScroll(Silk.NET.Input.IMouse mouse, Silk.NET.Input.ScrollWheel wheel) =>
        _queue.TryWrite(RawInputEvent.CreateMouseScroll(DeviceIndex.Primary, wheel.X, wheel.Y, InputTimestamp.Now()));

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        for (int i = 0; i < _context.Keyboards.Count; i++)
        {
            Silk.NET.Input.IKeyboard keyboard = _context.Keyboards[i];
            keyboard.KeyDown -= OnKeyDown;
            keyboard.KeyUp -= OnKeyUp;
            keyboard.KeyChar -= OnKeyChar;
        }

        for (int i = 0; i < _context.Mice.Count; i++)
        {
            Silk.NET.Input.IMouse mouse = _context.Mice[i];
            mouse.MouseDown -= OnMouseDown;
            mouse.MouseUp -= OnMouseUp;
            mouse.MouseMove -= OnMouseMove;
            mouse.Scroll -= OnScroll;
        }
    }
}
