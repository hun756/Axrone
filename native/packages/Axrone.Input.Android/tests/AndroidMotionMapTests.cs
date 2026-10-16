namespace Axrone.Input.Android.Tests;

public class AndroidMotionMapTests
{
    [Fact]
    public void Action_Decode_Splits_Mask_And_Index()
    {
        int packed = AndroidMotionEvents.ActionPointerDown | (2 << AndroidMotionEvents.PointerIndexShift);
        AndroidMotionMap.Action(packed).Should().Be(AndroidMotionEvents.ActionPointerDown);
        AndroidMotionMap.PointerIndex(packed).Should().Be(2);
        AndroidMotionMap.Action(AndroidMotionEvents.ActionMove).Should().Be(AndroidMotionEvents.ActionMove);
        AndroidMotionMap.PointerIndex(AndroidMotionEvents.ActionDown).Should().Be(0);
    }

    [Fact]
    public void Sources_Classify()
    {
        AndroidMotionMap.IsGamepadSource(AndroidMotionEvents.SourceGamepad).Should().BeTrue();
        AndroidMotionMap.IsGamepadSource(AndroidMotionEvents.SourceJoystick).Should().BeTrue();
        AndroidMotionMap.IsGamepadSource(AndroidMotionEvents.SourceTouchscreen).Should().BeFalse();
        AndroidMotionMap.IsTouchSource(AndroidMotionEvents.SourceTouchscreen).Should().BeTrue();
        AndroidMotionMap.IsTouchSource(AndroidMotionEvents.SourceMouse).Should().BeFalse();
    }

    [Fact]
    public void Tools_Classify()
    {
        AndroidMotionMap.IsTouchTool(AndroidMotionEvents.ToolFinger).Should().BeTrue();
        AndroidMotionMap.IsTouchTool(AndroidMotionEvents.ToolStylus).Should().BeTrue();
        AndroidMotionMap.IsTouchTool(AndroidMotionEvents.ToolMouse).Should().BeFalse();
        AndroidMotionMap.IsTouchTool(AndroidMotionEvents.ToolEraser).Should().BeFalse();
    }
}
