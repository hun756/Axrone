using Axrone.Input;
using FluentAssertions;
using Xunit;

namespace Axrone.Input.Tests;

public class InputEnginePolicyTests
{
    [Fact]
    public void Capture_Consumes_Lower_Priority()
    {
        using var engine = new InputEngine<TestAction>();
        InputControlPath.TryParse("keyboard/space", out InputControlPath space).Should().BeTrue();
        engine.SetButtonDefinition(TestAction.Fire, new InputButtonActionDefinition());
        engine.RegisterUser(new InputUserDefinition { Id = UserId.System });
        engine.RegisterContext(new InputContextDefinition<TestAction>
        {
            Id = "ui",
            Priority = 10,
            Capture = ContextCaptureMode.Used,
            BindingsByAction = [[InputBinding.Control(space)], [], []],
        });
        engine.RegisterContext(new InputContextDefinition<TestAction>
        {
            Id = "gameplay",
            Priority = 0,
            BindingsByAction = [[InputBinding.Control(space)], [], []],
        });

        var snapshot = new InputSnapshot();
        snapshot.Keys.Set(32);
        engine.SetSnapshot(snapshot);
        engine.Update(16.0f);

        engine.TryGetButton("ui", TestAction.Fire, out InputButtonState top).Should().BeTrue();
        engine.TryGetButton("gameplay", TestAction.Fire, out InputButtonState low).Should().BeTrue();
        top.Value.Should().BeTrue();
        low.Value.Should().BeFalse();
    }

    [Fact]
    public void Disabled_User_Skips_Context()
    {
        using var engine = new InputEngine<TestAction>();
        InputControlPath.TryParse("keyboard/space", out InputControlPath space).Should().BeTrue();
        engine.SetButtonDefinition(TestAction.Fire, new InputButtonActionDefinition());
        engine.RegisterUser(new InputUserDefinition { Id = new UserId(1), Enabled = false });
        engine.RegisterContext(new InputContextDefinition<TestAction>
        {
            Id = "p1",
            UserId = new UserId(1),
            BindingsByAction = [[InputBinding.Control(space)], [], []],
        });

        var snapshot = new InputSnapshot();
        snapshot.Keys.Set(32);
        engine.SetSnapshot(snapshot);
        engine.Update(16.0f);

        engine.TryGetButton("p1", TestAction.Fire, out InputButtonState state).Should().BeTrue();
        state.Value.Should().BeFalse();
    }

    [Fact]
    public void Focus_Loss_Resets_Gestures()
    {
        using var engine = new InputEngine<TestAction>();
        InputControlPath.TryParse("keyboard/space", out InputControlPath space).Should().BeTrue();
        engine.SetButtonDefinition(TestAction.Fire, new InputButtonActionDefinition());
        engine.RegisterUser(new InputUserDefinition { Id = UserId.System });
        engine.RegisterContext(new InputContextDefinition<TestAction>
        {
            Id = "gameplay",
            BindingsByAction = [[InputBinding.Control(space)], [], []],
        });

        var snapshot = new InputSnapshot();
        snapshot.Keys.Set(32);
        engine.SetSnapshot(snapshot);
        engine.Update(16.0f);

        engine.SetFocus(false);
        engine.TryGetButton("gameplay", TestAction.Fire, out InputButtonState state).Should().BeTrue();
        state.Value.Should().BeFalse();
        engine.Focused.Should().BeFalse();
    }

    [Fact]
    public void Duplicate_Context_Throws_Coded_Error()
    {
        using var engine = new InputEngine<TestAction>();
        engine.RegisterContext(new InputContextDefinition<TestAction> { Id = "gameplay" });
        Action register = () => engine.RegisterContext(new InputContextDefinition<TestAction> { Id = "gameplay" });
        register.Should().Throw<InputContextError>().WithMessage("*same id*");
    }

    [Fact]
    public void Hysteresis_Default_Follows_Half_Press()
    {
        InputButtonActionDefinition.EffectiveReleasePoint(new InputButtonActionDefinition { PressPoint = 0.6f })
            .Should().BeApproximately(0.3f, 1e-6f);
        InputButtonActionDefinition.EffectiveReleasePoint(new InputButtonActionDefinition { PressPoint = 0.6f, ReleasePoint = 0.4f })
            .Should().BeApproximately(0.4f, 1e-6f);

        using var engine = new InputEngine<TestAction>();
        InputControlPath.TryParse("gamepad/left-trigger", out InputControlPath trigger).Should().BeTrue();
        engine.SetButtonDefinition(TestAction.Fire, new InputButtonActionDefinition());
        engine.RegisterUser(new InputUserDefinition { Id = UserId.System });
        engine.RegisterContext(new InputContextDefinition<TestAction>
        {
            Id = "gameplay",
            BindingsByAction = [[InputBinding.Control(trigger)], [], []],
        });

        var held = new InputSnapshot();
        held.GamepadAxes[4] = 1.0f;
        engine.SetSnapshot(held);
        engine.Update(16.0f);

        var partial = new InputSnapshot();
        partial.GamepadAxes[4] = 0.3f;
        engine.SetSnapshot(partial);
        engine.Update(16.0f);

        engine.TryGetButton("gameplay", TestAction.Fire, out InputButtonState state).Should().BeTrue();
        state.Value.Should().BeTrue();
    }

    [Fact]
    public void Axis_Latest_Keeps_First_Nonzero()
    {
        using var engine = new InputEngine<TestAction>();
        engine.SetAxisDefinition(TestAction.Move, new InputAxisActionDefinition { Combine = AxisCombineMode.Latest });
        engine.RegisterUser(new InputUserDefinition { Id = UserId.System });

        var first = new InputBinding(BindingKind.Control,
            new InputControlPath(DeviceKind.Gamepad, ControlType.Axis, 0, 0));
        var second = new InputBinding(BindingKind.Control,
            new InputControlPath(DeviceKind.Gamepad, ControlType.Axis, 1, 0));
        engine.RegisterContext(new InputContextDefinition<TestAction>
        {
            Id = "gameplay",
            BindingsByAction = [[], [first, second], []],
        });

        var snapshot = new InputSnapshot();
        snapshot.GamepadAxes[0] = 0.8f;
        snapshot.GamepadAxes[1] = 0.3f;
        engine.SetSnapshot(snapshot);
        engine.Update(16.0f);

        engine.TryGetAxis("gameplay", TestAction.Move, out InputAxisState state).Should().BeTrue();
        state.Value.Should().BeApproximately(0.8f, 1e-6f);
    }

    [Fact]
    public void Button_MaxAbs_Beats_Later_Small()
    {
        using var engine = new InputEngine<TestAction>();
        engine.SetButtonDefinition(TestAction.Fire, new InputButtonActionDefinition());
        engine.RegisterUser(new InputUserDefinition { Id = UserId.System });

        var big = new InputBinding(BindingKind.Control,
            new InputControlPath(DeviceKind.Gamepad, ControlType.Axis, 0, 0));
        var small = new InputBinding(BindingKind.Control,
            new InputControlPath(DeviceKind.Gamepad, ControlType.Axis, 1, 0));
        engine.RegisterContext(new InputContextDefinition<TestAction>
        {
            Id = "gameplay",
            BindingsByAction = [[big, small], [], []],
        });

        var snapshot = new InputSnapshot();
        snapshot.GamepadAxes[0] = -0.9f;
        snapshot.GamepadAxes[1] = 0.4f;
        engine.SetSnapshot(snapshot);
        engine.Update(16.0f);

        engine.TryGetButton("gameplay", TestAction.Fire, out InputButtonState state).Should().BeTrue();
        state.Value.Should().BeTrue();
    }

    [Fact]
    public void Normalize_Keeps_Short_Vectors()
    {
        using var engine = new InputEngine<TestAction>();
        engine.SetVector2Definition(TestAction.Look, new InputVector2ActionDefinition { Normalize = true });
        engine.RegisterUser(new InputUserDefinition { Id = UserId.System });

        var x = new InputBinding(BindingKind.Control,
            new InputControlPath(DeviceKind.Gamepad, ControlType.Axis, 0, 0));
        var y = new InputBinding(BindingKind.Control,
            new InputControlPath(DeviceKind.Gamepad, ControlType.Axis, 1, 0));
        engine.RegisterContext(new InputContextDefinition<TestAction>
        {
            Id = "gameplay",
            BindingsByAction = [[], [], [InputBinding.DualAxis(x.Path0, y.Path0, normalize: true)]],
        });

        var snapshot = new InputSnapshot();
        snapshot.GamepadAxes[0] = 0.5f;
        engine.SetSnapshot(snapshot);
        engine.Update(16.0f);

        engine.TryGetVector2("gameplay", TestAction.Look, out InputVector2State state).Should().BeTrue();
        state.Magnitude.Should().BeApproximately(0.5f, 1e-6f);
    }

    [Fact]
    public void Capture_Skips_Zero_Bindings()
    {
        using var engine = new InputEngine<TestAction>();
        InputControlPath.TryParse("keyboard/space", out InputControlPath space).Should().BeTrue();
        InputControlPath.TryParse("keyboard/A", out InputControlPath left).Should().BeTrue();
        InputControlPath.TryParse("keyboard/D", out InputControlPath right).Should().BeTrue();
        engine.SetButtonDefinition(TestAction.Fire, new InputButtonActionDefinition());
        engine.SetAxisDefinition(TestAction.Move, new InputAxisActionDefinition());
        engine.RegisterUser(new InputUserDefinition { Id = UserId.System });

        var idle = new InputBinding(BindingKind.Control,
            new InputControlPath(DeviceKind.Gamepad, ControlType.Axis, 0, 0));
        engine.RegisterContext(new InputContextDefinition<TestAction>
        {
            Id = "ui",
            Priority = 10,
            Capture = ContextCaptureMode.Used,
            BindingsByAction = [[InputBinding.Control(space), idle], [], []],
        });
        engine.RegisterContext(new InputContextDefinition<TestAction>
        {
            Id = "gameplay",
            Priority = 0,
            BindingsByAction = [[], [InputBinding.Axis(left, right)], []],
        });

        var snapshot = new InputSnapshot();
        snapshot.Keys.Set(32);
        snapshot.Keys.Set((uint)'D');
        engine.SetSnapshot(snapshot);
        engine.Update(16.0f);

        engine.TryGetAxis("gameplay", TestAction.Move, out InputAxisState state).Should().BeTrue();
        state.Value.Should().Be(1.0f);
    }
}
