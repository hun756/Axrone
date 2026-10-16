using System.Numerics;
using Axrone.Input;
using FluentAssertions;
using Xunit;

namespace Axrone.Input.Tests;

public class InputControlReaderTests
{
    [Fact]
    public void Reader_Keyboard_Mouse_Gamepad()
    {
        var snapshot = new InputSnapshot();
        snapshot.Keys.Set(32);
        snapshot.MouseButtons = 1;
        snapshot.MouseDeltaX = 5.0f;
        snapshot.ScrollDeltaY = -1.0f;
        snapshot.GamepadAxes[0] = 0.7f;

        InputControlPath.TryParse("keyboard/space", out InputControlPath key).Should().BeTrue();
        InputControlReader.ReadScalar(snapshot, key).Should().Be(1.0f);

        InputControlPath.TryParse("mouse/button/0", out InputControlPath mouse).Should().BeTrue();
        InputControlReader.ReadScalar(snapshot, mouse).Should().Be(1.0f);

        InputControlPath.TryParse("mouse/move/x", out InputControlPath move).Should().BeTrue();
        InputControlReader.ReadScalar(snapshot, move).Should().Be(5.0f);

        InputControlPath.TryParse("mouse/wheel/y", out InputControlPath wheel).Should().BeTrue();
        InputControlReader.ReadScalar(snapshot, wheel).Should().Be(-1.0f);

        InputControlPath.TryParse("gamepad/left-stick/x", out InputControlPath axis).Should().BeTrue();
        InputControlReader.ReadScalar(snapshot, axis).Should().Be(0.7f);
    }

    [Fact]
    public void Reader_Modifiers_From_Shift_Keys()
    {
        var snapshot = new InputSnapshot();
        InputControlReader.ReadModifiers(snapshot).Should().Be(InputModifiers.None);

        snapshot.Keys.Set(340);
        InputControlReader.ReadModifiers(snapshot).Should().Be(InputModifiers.Shift);

        InputControlReader.ModifiersSatisfied(InputModifiers.Shift, InputModifiers.Shift, false).Should().BeTrue();
        InputControlReader.ModifiersSatisfied(InputModifiers.None, InputModifiers.Shift, false).Should().BeFalse();
        InputControlReader.ModifiersSatisfied(InputModifiers.Shift, InputModifiers.Shift, true).Should().BeTrue();
    }

    [Fact]
    public void Resolver_Axis_Pair_Subtracts()
    {
        InputControlPath.TryParse("keyboard/A", out InputControlPath left).Should().BeTrue();
        InputControlPath.TryParse("keyboard/D", out InputControlPath right).Should().BeTrue();
        InputBinding binding = InputBinding.Axis(left, right);

        var snapshot = new InputSnapshot();
        snapshot.Keys.Set((uint)'D');

        var consumed = new ConsumedControls();
        InputBindingResolver.TryResolveScalar(snapshot, binding, consumed, 0, out float value).Should().BeTrue();
        value.Should().Be(1.0f);
    }

    [Fact]
    public void Resolver_Modifier_Gate_And_Consumption()
    {
        InputControlPath.TryParse("keyboard/space", out InputControlPath space).Should().BeTrue();
        InputBinding gated = InputBinding.Control(space, InputModifiers.Shift);

        var snapshot = new InputSnapshot();
        snapshot.Keys.Set(32);

        var consumed = new ConsumedControls();
        InputBindingResolver.TryResolveScalar(snapshot, gated, consumed, 0, out _).Should().BeFalse();

        snapshot.Keys.Set(340);
        InputBindingResolver.TryResolveScalar(snapshot, gated, consumed, 0, out float value).Should().BeTrue();
        value.Should().Be(1.0f);

        consumed.Mark(space);
        InputBinding plain = InputBinding.Control(space);
        InputBindingResolver.TryResolveScalar(snapshot, plain, consumed, 0, out _).Should().BeFalse();
    }

    [Fact]
    public void Resolver_Directional_Composes_Vector()
    {
        InputControlPath.TryParse("keyboard/W", out InputControlPath up).Should().BeTrue();
        InputControlPath.TryParse("keyboard/S", out InputControlPath down).Should().BeTrue();
        InputControlPath.TryParse("keyboard/A", out InputControlPath left).Should().BeTrue();
        InputControlPath.TryParse("keyboard/D", out InputControlPath right).Should().BeTrue();
        InputBinding pad = InputBinding.Vector2Directional(up, down, left, right);

        var snapshot = new InputSnapshot();
        snapshot.Keys.Set((uint)'W');
        snapshot.Keys.Set((uint)'D');

        var consumed = new ConsumedControls();
        InputBindingResolver.TryResolveVector2(snapshot, pad, consumed, 0, out Vector2 value).Should().BeTrue();
        value.X.Should().Be(1.0f);
        value.Y.Should().Be(1.0f);
    }
}
