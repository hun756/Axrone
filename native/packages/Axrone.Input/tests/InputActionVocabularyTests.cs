using Axrone.Input;
using FluentAssertions;
using Xunit;

namespace Axrone.Input.Tests;

public class InputActionVocabularyTests
{
    [Fact]
    public void ControlPath_Parses_KeyboardMouseGamepadTouch()
    {
        InputControlPath.TryParse("keyboard/space", out InputControlPath key).Should().BeTrue();
        key.Device.Should().Be(DeviceKind.Keyboard);
        key.Index.Should().Be(32);

        InputControlPath.TryParse("keyboard/A", out InputControlPath letter).Should().BeTrue();
        letter.Index.Should().Be((ushort)'A');

        InputControlPath.TryParse("mouse/button/0", out InputControlPath button).Should().BeTrue();
        button.Type.Should().Be(ControlType.Button);

        InputControlPath.TryParse("mouse/move/y", out InputControlPath move).Should().BeTrue();
        (move.Type, move.SubIndex).Should().Be((ControlType.Move, (byte)1));

        InputControlPath.TryParse("mouse/wheel/x", out InputControlPath wheel).Should().BeTrue();
        wheel.Type.Should().Be(ControlType.Wheel);

        InputControlPath.TryParse("gamepad/south", out InputControlPath pad).Should().BeTrue();
        pad.Index.Should().Be((ushort)GamepadButton.South);

        InputControlPath.TryParse("gamepad/left-stick/y", out InputControlPath axis).Should().BeTrue();
        axis.Type.Should().Be(ControlType.Axis);

        InputControlPath.TryParse("touch/contact/any", out InputControlPath touch).Should().BeTrue();
        touch.Index.Should().Be(255);
    }

    [Fact]
    public void ControlPath_Rejects_Malformed()
    {
        InputControlPath.TryParse("", out _).Should().BeFalse();
        InputControlPath.TryParse("keyboard/no-such-key", out _).Should().BeFalse();
        InputControlPath.TryParse("mouse/frob", out _).Should().BeFalse();
        InputControlPath.TryParse("gamepad/nope", out _).Should().BeFalse();
        InputControlPath.TryParse("touch/contact/", out _).Should().BeFalse();
    }

    [Fact]
    public void BindingFactories_Compose_Shapes()
    {
        InputControlPath.TryParse("keyboard/space", out InputControlPath space).Should().BeTrue();
        InputBinding control = InputBinding.Control(space);
        control.Kind.Should().Be(BindingKind.Control);

        InputControlPath.TryParse("keyboard/A", out InputControlPath left).Should().BeTrue();
        InputControlPath.TryParse("keyboard/D", out InputControlPath right).Should().BeTrue();
        InputBinding axis = InputBinding.Axis(left, right);
        axis.Kind.Should().Be(BindingKind.Axis);

        InputBinding pad = InputBinding.Vector2Directional(left, right, left, right, normalize: true);
        pad.Normalize.Should().BeTrue();
    }

    [Fact]
    public void ContextId_Hashes_Names_Deterministically()
    {
        ContextId gameplay = "gameplay";
        ContextId again = ContextId.FromString("gameplay".AsSpan());

        gameplay.Should().Be(again);
        gameplay.Should().NotBe(ContextId.None);
        ContextId.FromString(ReadOnlySpan<char>.Empty).Should().Be(ContextId.None);
    }

    [Fact]
    public void ActionDefinitions_Guard_Ranges()
    {
        var badPress = () => new InputButtonActionDefinition { PressPoint = 2.0f };
        var badDeadzone = () => new InputAxisActionDefinition { Deadzone = -1.0f };

        badPress.Should().Throw<ArgumentOutOfRangeException>();
        badDeadzone.Should().Throw<ArgumentOutOfRangeException>();

        new InputButtonActionDefinition().PressPoint.Should().Be(0.5f);
        new InputVector2ActionDefinition().Normalize.Should().BeFalse();
    }

    [Fact]
    public void SourceEvents_Carry_Payloads()
    {
        InputSourceEvent key = InputSourceEvent.Keyboard(32, true);
        key.Type.Should().Be(InputSourceEventType.Keyboard);
        key.Pressed.Should().BeTrue();

        InputSourceEvent text = InputSourceEvent.Text('a');
        text.Character.Should().Be('a');

        InputSourceEvent wheel = InputSourceEvent.MouseWheel(0.0f, -1.0f);
        wheel.Float1.Should().Be(-1.0f);
    }
}
