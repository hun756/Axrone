using Axrone.Input;
using FluentAssertions;
using Xunit;

namespace Axrone.Input.Tests;

public class InputFrameTests
{
    [Fact]
    public void PressedEdge_Lasts_OneFrame()
    {
        using var input = new InputFrameCoordinator();
        var stamp = InputTimestamp.Now();

        input.RingBuffer.TryEnqueue(RawInputEvent.CreateKeyDown(DeviceIndex.Primary, Key.Space, 0, stamp));
        input.NewFrame();

        input.IsKeyDown(Key.Space).Should().BeTrue();
        input.IsKeyPressed(Key.Space).Should().BeTrue();

        input.NewFrame();

        input.IsKeyDown(Key.Space).Should().BeTrue();
        input.IsKeyPressed(Key.Space).Should().BeFalse();
    }

    [Fact]
    public void ReleaseEdge_Fires_OnKeyUp()
    {
        using var input = new InputFrameCoordinator();
        var stamp = InputTimestamp.Now();

        input.RingBuffer.TryEnqueue(RawInputEvent.CreateKeyDown(DeviceIndex.Primary, Key.A, 0, stamp));
        input.NewFrame();
        input.RingBuffer.TryEnqueue(RawInputEvent.CreateKeyUp(DeviceIndex.Primary, Key.A, 0, stamp));
        input.NewFrame();

        input.IsKeyDown(Key.A).Should().BeFalse();
        input.IsKeyReleased(Key.A).Should().BeTrue();

        input.NewFrame();

        input.IsKeyReleased(Key.A).Should().BeFalse();
    }

    [Fact]
    public void KeyChords_Query_Correctly()
    {
        using var input = new InputFrameCoordinator();
        var stamp = InputTimestamp.Now();

        input.RingBuffer.TryEnqueue(RawInputEvent.CreateKeyDown(DeviceIndex.Primary, Key.ControlLeft, 0, stamp));
        input.RingBuffer.TryEnqueue(RawInputEvent.CreateKeyDown(DeviceIndex.Primary, Key.S, 0, stamp));
        input.NewFrame();

        input.AreAllKeysDown(Key.ControlLeft, Key.S).Should().BeTrue();
        input.AreAllKeysDown(Key.ControlLeft, Key.A).Should().BeFalse();
        input.IsAnyKeyDown(Key.A, Key.S).Should().BeTrue();
        input.IsAnyKeyDown(Key.A, Key.B).Should().BeFalse();
    }

    [Fact]
    public void MouseDelta_Resets_EachFrame()
    {
        using var input = new InputFrameCoordinator();
        var stamp = InputTimestamp.Now();

        input.RingBuffer.TryEnqueue(RawInputEvent.CreateMouseMove(DeviceIndex.Primary, 100.0f, 50.0f, stamp));
        input.NewFrame();

        input.MousePosition.X.Should().Be(100.0f);
        input.MouseDelta.X.Should().Be(100.0f);

        input.RingBuffer.TryEnqueue(RawInputEvent.CreateMouseMove(DeviceIndex.Primary, 120.0f, 50.0f, stamp));
        input.NewFrame();

        input.MouseDelta.X.Should().Be(20.0f);

        input.NewFrame();

        input.MouseDelta.X.Should().Be(0.0f);
    }

    [Fact]
    public void MouseButtons_Track_PressedReleased()
    {
        using var input = new InputFrameCoordinator();
        var stamp = InputTimestamp.Now();

        input.RingBuffer.TryEnqueue(RawInputEvent.CreateMouseDown(DeviceIndex.Primary, MouseButton.Left, stamp));
        input.NewFrame();

        input.IsMouseButtonDown(MouseButton.Left).Should().BeTrue();
        input.IsMouseButtonPressed(MouseButton.Left).Should().BeTrue();

        input.RingBuffer.TryEnqueue(RawInputEvent.CreateMouseUp(DeviceIndex.Primary, MouseButton.Left, stamp));
        input.NewFrame();

        input.IsMouseButtonDown(MouseButton.Left).Should().BeFalse();
        input.IsMouseButtonReleased(MouseButton.Left).Should().BeTrue();
    }

    [Fact]
    public void Gamepad_ButtonsAndAxis_Track()
    {
        using var input = new InputFrameCoordinator();
        var stamp = InputTimestamp.Now();
        var pad = DeviceIndex.Primary;

        input.RingBuffer.TryEnqueue(RawInputEvent.CreateGamepadButtonDown(pad, ButtonName.A, 0, stamp));
        input.RingBuffer.TryEnqueue(RawInputEvent.CreateGamepadAxis(pad, 0, 0.75f, stamp));
        input.NewFrame();

        input.IsGamepadButtonDown(pad, ButtonName.A).Should().BeTrue();
        input.IsGamepadButtonPressed(pad, ButtonName.A).Should().BeTrue();
        input.GetGamepadAxis(pad, 0).Should().Be(0.75f);
        input.GetGamepadAxis(pad, 5).Should().Be(0.0f);
    }

    [Fact]
    public void PressedKeys_Enumerate()
    {
        var bitset = new KeyStateBitset();
        bitset.Set((uint)Key.A);
        bitset.Set((uint)Key.Space);

        var pressed = new List<Key>();
        foreach (Key key in bitset)
        {
            pressed.Add(key);
        }

        pressed.Should().Contain(Key.A);
        pressed.Should().Contain(Key.Space);
        pressed.Should().HaveCount(2);
    }
}
