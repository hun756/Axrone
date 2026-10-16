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
}
