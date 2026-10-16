namespace Axrone.Input.Android.Tests;

public class AndroidGamepadMapTests
{
    [Fact]
    public void Buttons_Map_To_Engine_Numbering()
    {
        AndroidGamepadMap.TryToButton(AndroidGamepadButtons.A, out ushort south).Should().BeTrue();
        south.Should().Be(0);
        AndroidGamepadMap.TryToButton(AndroidGamepadButtons.Start, out ushort start).Should().BeTrue();
        start.Should().Be(9);
        AndroidGamepadMap.TryToButton(AndroidKeycodes.DPadUp, out ushort dpad).Should().BeTrue();
        dpad.Should().Be(12);
        AndroidGamepadMap.TryToButton(AndroidGamepadButtons.Mode, out ushort guide).Should().BeTrue();
        guide.Should().Be(16);
        AndroidGamepadMap.TryToButton(AndroidKeycodes.DPadCenter, out _).Should().BeFalse();
        AndroidGamepadMap.TryToButton(1, out _).Should().BeFalse();
    }

    [Fact]
    public void Axes_Map_To_Engine_Numbering()
    {
        AndroidGamepadMap.TryToAxis(AndroidMotionEvents.AxisX, out ushort x).Should().BeTrue();
        x.Should().Be(0);
        AndroidGamepadMap.TryToAxis(AndroidMotionEvents.AxisRZ, out ushort rz).Should().BeTrue();
        rz.Should().Be(3);
        AndroidGamepadMap.TryToAxis(AndroidMotionEvents.AxisLTrigger, out ushort lt).Should().BeTrue();
        lt.Should().Be(4);
        AndroidGamepadMap.TryToAxis(AndroidMotionEvents.AxisHatX, out _).Should().BeFalse();
        AndroidGamepadMap.TryToAxis(AndroidMotionEvents.AxisPressure, out _).Should().BeFalse();
    }

    [Fact]
    public void Mapped_Buttons_Feed_Engine_Paths()
    {
        AndroidGamepadMap.TryToButton(AndroidGamepadButtons.A, out ushort engine).Should().BeTrue();
        var path = new InputControlPath(DeviceKind.Gamepad, ControlType.Button, engine, 0);

        var snapshot = new InputSnapshot { GamepadButtons0 = 1u };
        InputControlReader.ReadScalar(snapshot, path).Should().Be(1.0f);
    }
}
