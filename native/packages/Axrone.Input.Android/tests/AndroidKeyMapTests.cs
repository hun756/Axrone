namespace Axrone.Input.Android.Tests;

public class AndroidKeyMapTests
{
    [Fact]
    public void Letters_Digits_FunctionKeys_Map_ByRange()
    {
        AndroidKeyMap.ToKey(AndroidKeycodes.A).Should().Be(Key.A);
        AndroidKeyMap.ToKey(AndroidKeycodes.A + 25).Should().Be(Key.Z);
        AndroidKeyMap.ToKey(AndroidKeycodes.Number0).Should().Be(Key.Number0);
        AndroidKeyMap.ToKey(AndroidKeycodes.Number9).Should().Be(Key.Number9);
        AndroidKeyMap.ToKey(AndroidKeycodes.F1).Should().Be(Key.F1);
        AndroidKeyMap.ToKey(AndroidKeycodes.F1 + 11).Should().Be(Key.F12);
    }

    [Fact]
    public void ControlKeys_Map()
    {
        AndroidKeyMap.ToKey(AndroidKeycodes.Space).Should().Be(Key.Space);
        AndroidKeyMap.ToKey(AndroidKeycodes.Enter).Should().Be(Key.Enter);
        AndroidKeyMap.ToKey(AndroidKeycodes.Tab).Should().Be(Key.Tab);
        AndroidKeyMap.ToKey(AndroidKeycodes.Del).Should().Be(Key.Backspace);
        AndroidKeyMap.ToKey(AndroidKeycodes.Escape).Should().Be(Key.Escape);
        AndroidKeyMap.ToKey(AndroidKeycodes.Back).Should().Be(Key.Escape);
        AndroidKeyMap.ToKey(AndroidKeycodes.DPadUp).Should().Be(Key.Up);
        AndroidKeyMap.ToKey(AndroidKeycodes.DPadDown).Should().Be(Key.Down);
        AndroidKeyMap.ToKey(AndroidKeycodes.DPadLeft).Should().Be(Key.Left);
        AndroidKeyMap.ToKey(AndroidKeycodes.DPadRight).Should().Be(Key.Right);
        AndroidKeyMap.ToKey(AndroidKeycodes.ShiftLeft).Should().Be(Key.ShiftLeft);
        AndroidKeyMap.ToKey(AndroidKeycodes.CtrlLeft).Should().Be(Key.ControlLeft);
        AndroidKeyMap.ToKey(AndroidKeycodes.AltRight).Should().Be(Key.AltRight);
    }

    [Fact]
    public void Unknown_Codes_Map_ToUnknown()
    {
        AndroidKeyMap.ToKey(AndroidKeycodes.Unknown).Should().Be(Key.Unknown);
        AndroidKeyMap.ToKey(AndroidKeycodes.VolumeUp).Should().Be(Key.Unknown);
        AndroidKeyMap.ToKey(9999).Should().Be(Key.Unknown);
    }

    [Fact]
    public void MetaState_Maps_Modifiers()
    {
        AndroidKeyMap.ToModifiers(0).Should().Be(InputModifiers.None);
        AndroidKeyMap.ToModifiers(AndroidKeycodes.MetaShiftOn).Should().Be(InputModifiers.Shift);
        AndroidKeyMap.ToModifiers(AndroidKeycodes.MetaShiftOn | AndroidKeycodes.MetaCtrlOn | AndroidKeycodes.MetaAltOn | AndroidKeycodes.MetaMetaOn)
            .Should().Be(InputModifiers.Shift | InputModifiers.Control | InputModifiers.Alt | InputModifiers.Meta);
    }
}
