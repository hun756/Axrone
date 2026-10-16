using Axrone.Input;
using FluentAssertions;
using Xunit;

namespace Axrone.Input.Tests;

public class InputEngineManageTests
{
    [Fact]
    public void Validator_Rejects_Mismatched_Bindings()
    {
        InputControlPath.TryParse("keyboard/A", out InputControlPath left).Should().BeTrue();
        InputControlPath.TryParse("keyboard/D", out InputControlPath right).Should().BeTrue();
        InputBinding directional = InputBinding.Vector2Directional(left, right, left, right);

        InputBindingValidator.TryValidate(ActionKind.Button, directional, out string? error).Should().BeFalse();
        error.Should().NotBeNullOrEmpty();

        var badProcessor = new InputBinding(
            BindingKind.Control, left,
            proc0: ProcessorConfiguration.NormalizeVector2());
        InputBindingValidator.TryValidate(ActionKind.Button, badProcessor, out _).Should().BeFalse();
        InputBindingValidator.TryValidate(ActionKind.Vector2, badProcessor, out _).Should().BeTrue();

        using var engine = new InputEngine<TestAction>();
        engine.RegisterUser(new InputUserDefinition { Id = UserId.System });
        Action register = () => engine.RegisterContext(new InputContextDefinition<TestAction>
        {
            Id = "gameplay",
            BindingsByAction = [[directional], [], []],
        });
        register.Should().Throw<InputConfigurationError>();
    }

    [Fact]
    public void Context_Management_Works()
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

        engine.HasContext("gameplay").Should().BeTrue();
        engine.HasContext("missing").Should().BeFalse();

        engine.ActivateContext("gameplay", false).Should().BeTrue();
        var snapshot = new InputSnapshot();
        snapshot.Keys.Set(32);
        engine.SetSnapshot(snapshot);
        engine.Update(16.0f);
        engine.IsActive(TestAction.Fire, UserId.Any).Should().BeFalse();

        engine.ActivateContext("gameplay", true).Should().BeTrue();
        engine.SetContextPriority("gameplay", new PriorityLevel(5)).Should().BeTrue();
        engine.Update(16.0f);
        engine.IsActive(TestAction.Fire, UserId.Any).Should().BeTrue();

        engine.UpsertContext(new InputContextDefinition<TestAction> { Id = "gameplay" });
        engine.GetBindings("gameplay", TestAction.Fire).Should().BeEmpty();

        engine.RemoveContext("gameplay").Should().BeTrue();
        engine.HasContext("gameplay").Should().BeFalse();
        engine.RemoveContext("gameplay").Should().BeFalse();
    }

    [Fact]
    public void User_Gamepad_Management_Works()
    {
        using var engine = new InputEngine<TestAction>();
        var first = new UserId(1);
        var second = new UserId(2);
        engine.RegisterUser(new InputUserDefinition { Id = first, AssignedGamepadMask = 0u });
        engine.UpsertUser(new InputUserDefinition { Id = second, AssignedGamepadMask = 0u });

        engine.AssignGamepad(first, 0).Should().BeTrue();
        engine.AssignGamepad(second, 0).Should().BeTrue();
        engine.TryGetUser(first, out InputUserDefinition? stolen).Should().BeTrue();
        stolen!.AssignedGamepadMask.Should().Be(0u);
        engine.TryGetUser(second, out InputUserDefinition? owner).Should().BeTrue();
        owner!.AssignedGamepadMask.Should().Be(1u);

        engine.ReleaseGamepad(0).Should().BeTrue();
        engine.ReleaseGamepad(0).Should().BeFalse();

        engine.SetUserEnabled(second, false).Should().BeTrue();
        engine.TryGetUser(second, out InputUserDefinition? disabled).Should().BeTrue();
        disabled!.Enabled.Should().BeFalse();

        engine.RemoveUser(second).Should().BeTrue();
        engine.TryGetUser(second, out _).Should().BeFalse();
    }

    [Fact]
    public void Dispose_Cancels_Rebind()
    {
        var engine = new InputEngine<TestAction>();
        RebindCancelReason? reason = null;
        engine.BeginRebind(
            new RebindRequest<TestAction>("gameplay", TestAction.Fire),
            new RebindHandlers<TestAction> { Cancel = r => reason = r });
        engine.Dispose();
        reason.Should().Be(RebindCancelReason.Disposed);
    }
}
