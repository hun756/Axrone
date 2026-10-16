using System.Numerics;
using Axrone.Input;
using FluentAssertions;
using Xunit;

namespace Axrone.Input.Tests;

public class InputControlGrammarTests
{
    [Fact]
    public void Parses_Mouse_Position_Touch_Shapes_And_Slots()
    {
        InputControlPath.TryParse("mouse/position/x", out InputControlPath pos).Should().BeTrue();
        pos.Type.Should().Be(ControlType.Position);

        InputControlPath.TryParse("touch/contact/primary", out InputControlPath primary).Should().BeTrue();
        primary.Index.Should().Be(254);

        InputControlPath.TryParse("touch/position/y/2", out InputControlPath touchPos).Should().BeTrue();
        (touchPos.Type, touchPos.Index, touchPos.SubIndex).Should().Be((ControlType.Position, (ushort)2, (byte)1));

        InputControlPath.TryParse("touch/count", out InputControlPath count).Should().BeTrue();
        count.Type.Should().Be(ControlType.Count);

        InputControlPath.TryParse("touch/pinch-delta", out InputControlPath pinch).Should().BeTrue();
        (pinch.Type, pinch.SubIndex).Should().Be((ControlType.Pinch, (byte)1));

        InputControlPath.TryParse("gamepad/1/south", out InputControlPath slot).Should().BeTrue();
        slot.Flags.Should().Be(0x81);

        InputControlPath.TryParse("gamepad/any/left-stick/x", out InputControlPath any).Should().BeTrue();
        any.Flags.Should().Be(0xC0);

        InputControlPath.TryParse("gamepad/connected", out InputControlPath connected).Should().BeTrue();
        connected.Type.Should().Be(ControlType.Connected);

        InputControlPath.TryParse("keyboard/F12", out InputControlPath f12).Should().BeTrue();
        f12.Index.Should().Be(301);

        InputControlPath.TryParse("keyboard/shift", out InputControlPath shift).Should().BeTrue();
        shift.Index.Should().Be(340);
    }

    [Fact]
    public void Reads_Position_Count_Pinches_And_Slots()
    {
        var snapshot = new InputSnapshot
        {
            MouseX = 12.0f,
            GamepadConnectedMask = 0b101u,
        };
        snapshot.TouchContacts[1] = new TouchContact { Active = true, X = 3.0f, Y = 4.0f };
        snapshot.TouchContacts[3] = new TouchContact { Active = true, X = 7.0f, Y = 8.0f };
        snapshot.GamepadAxes[6] = -0.9f;
        snapshot.GamepadAxes[0] = 0.2f;

        InputControlPath.TryParse("mouse/position/x", out InputControlPath pos).Should().BeTrue();
        InputControlReader.ReadScalar(snapshot, pos).Should().Be(12.0f);

        InputControlPath.TryParse("touch/contact/primary", out InputControlPath primary).Should().BeTrue();
        InputControlReader.ReadScalar(snapshot, primary).Should().Be(1.0f);

        InputControlPath.TryParse("touch/position/y/primary", out InputControlPath posY).Should().BeTrue();
        InputControlReader.ReadScalar(snapshot, posY).Should().Be(4.0f);

        InputControlPath.TryParse("touch/count", out InputControlPath count).Should().BeTrue();
        InputControlReader.ReadScalar(snapshot, count).Should().Be(2.0f);

        InputControlPath.TryParse("gamepad/connected", out InputControlPath connected).Should().BeTrue();
        InputControlReader.ReadScalar(snapshot, connected, gamepadSlot: 2).Should().Be(1.0f);
        InputControlReader.ReadScalar(snapshot, connected, gamepadSlot: 1).Should().Be(0.0f);

        InputControlPath.TryParse("gamepad/1/left-stick/x", out InputControlPath explicit_).Should().BeTrue();
        InputControlReader.ReadScalar(snapshot, explicit_, gamepadSlot: 0).Should().Be(-0.9f);

        InputControlPath.TryParse("gamepad/any/left-stick/x", out InputControlPath any).Should().BeTrue();
        InputControlReader.ReadScalar(snapshot, any, gamepadSlot: 0).Should().Be(-0.9f);
    }

    [Fact]
    public void Coordinator_Computes_Pinch_And_Connection_Clears()
    {
        using var coordinator = new InputFrameCoordinator();
        coordinator.RingBuffer.FeedGamepadConnection(0, true).Should().BeTrue();
        coordinator.RingBuffer.FeedTouch(0, TouchPhase.Start, 0.0f, 0.0f).Should().BeTrue();
        coordinator.RingBuffer.FeedTouch(1, TouchPhase.Start, 3.0f, 4.0f).Should().BeTrue();
        coordinator.NewFrame();

        coordinator.CurrentSnapshot.GamepadConnectedMask.Should().Be(1u);
        coordinator.CurrentSnapshot.TouchPinch.Should().BeApproximately(5.0f, 1e-5f);

        coordinator.RingBuffer.FeedGamepadConnection(0, false).Should().BeTrue();
        coordinator.NewFrame();
        coordinator.CurrentSnapshot.GamepadConnectedMask.Should().Be(0u);
        coordinator.CurrentSnapshot.TouchPinchDelta.Should().Be(0.0f);
    }

    [Fact]
    public void Polled_Frame_Reaches_Engine()
    {
        using var coordinator = new InputFrameCoordinator();
        using var engine = new InputEngine<TestAction>();

        InputControlPath.TryParse("gamepad/0/south", out InputControlPath south).Should().BeTrue();
        engine.SetButtonDefinition(TestAction.Fire, new InputButtonActionDefinition());
        engine.RegisterUser(new InputUserDefinition { Id = UserId.System });
        engine.RegisterContext(new InputContextDefinition<TestAction>
        {
            Id = "gameplay",
            BindingsByAction = [[InputBinding.Control(south)], [], []],
        });

        Span<float> axes = stackalloc float[6];
        coordinator.SetGamepadFrame(0, 1u, axes);
        InputPipeline.PumpFrame(coordinator, engine, 16.0f);

        engine.IsActive(TestAction.Fire, UserId.Any).Should().BeTrue();
    }
}
