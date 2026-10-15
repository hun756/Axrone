using Axrone.Input;
using FluentAssertions;
using Xunit;

namespace Axrone.Input.Tests;

public class InputDeviceTests
{
    [Fact]
    public void KeyCodes_Match_SilkNet_Values()
    {
        ((short)Key.Unknown).Should().Be(-1);
        ((short)Key.Space).Should().Be(32);
        ((short)Key.A).Should().Be(65);
        ((short)Key.Escape).Should().Be(256);
        ((short)Key.F1).Should().Be(290);
        ((short)Key.ShiftLeft).Should().Be(340);
        ((short)Key.Menu).Should().Be(348);
    }

    [Fact]
    public void MouseAndGamepadCodes_Match_SilkNet_Values()
    {
        ((byte)MouseButton.Left).Should().Be(0);
        ((byte)MouseButton.Middle).Should().Be(2);
        ((byte)ButtonName.A).Should().Be(0);
        ((byte)ButtonName.DPadLeft).Should().Be(14);
    }

    [Fact]
    public void Modifiers_Combine_AsFlags()
    {
        var combined = InputModifiers.Shift | InputModifiers.Control;

        combined.HasFlag(InputModifiers.Shift).Should().BeTrue();
        combined.HasFlag(InputModifiers.Alt).Should().BeFalse();
    }
}
