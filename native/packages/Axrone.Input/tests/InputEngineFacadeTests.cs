using Axrone.Input;
using FluentAssertions;
using Xunit;

namespace Axrone.Input.Tests;

public class InputEngineFacadeTests
{
    [Fact]
    public void Reads_Work_Without_Context()
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

        engine.Frame.Should().Be(0);
        var snapshot = new InputSnapshot();
        snapshot.Keys.Set(32);
        engine.SetSnapshot(snapshot);
        engine.Update(16.0f);

        engine.Frame.Should().Be(1);
        engine.IsActive(TestAction.Fire, UserId.Any).Should().BeTrue();
        engine.IsPressed(TestAction.Fire, UserId.Any).Should().BeTrue();
        engine.ReadValue(TestAction.Fire, UserId.Any).BoolValue.Should().BeTrue();
        engine.TryReadAnyButton(TestAction.Fire, UserId.Any, out InputButtonState state).Should().BeTrue();
        state.Value.Should().BeTrue();

        engine.SetSnapshot(new InputSnapshot());
        engine.Update(16.0f);
        engine.IsReleased(TestAction.Fire, UserId.Any).Should().BeTrue();
        engine.IsActive(TestAction.Fire, UserId.Any).Should().BeFalse();
    }

    [Fact]
    public void Filtered_Subscriptions_Select_Action_And_Phase()
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

        int all = 0;
        int fireOnly = 0;
        int performedOnly = 0;
        engine.Subscribe(_ => all++);
        engine.Subscribe(_ => fireOnly++, TestAction.Fire);
        engine.Subscribe(_ => performedOnly++, InputActionPhase.Performed);

        var snapshot = new InputSnapshot();
        snapshot.Keys.Set(32);
        engine.SetSnapshot(snapshot);
        engine.Update(16.0f);

        all.Should().Be(1);
        fireOnly.Should().Be(1);
        performedOnly.Should().Be(1);

        engine.SetSnapshot(new InputSnapshot());
        engine.Update(16.0f);

        all.Should().Be(2);
        fireOnly.Should().Be(2);
        performedOnly.Should().Be(1);
    }

    [Fact]
    public void Binding_Management_Reads_Patches_Resets()
    {
        using var engine = new InputEngine<TestAction>();
        InputControlPath.TryParse("keyboard/space", out InputControlPath space).Should().BeTrue();
        InputControlPath.TryParse("keyboard/A", out InputControlPath keyA).Should().BeTrue();
        engine.SetButtonDefinition(TestAction.Fire, new InputButtonActionDefinition());
        engine.RegisterUser(new InputUserDefinition { Id = UserId.System });
        engine.RegisterContext(new InputContextDefinition<TestAction>
        {
            Id = "gameplay",
            BindingsByAction = [[InputBinding.Control(space)], [], []],
        });

        engine.GetBindings("gameplay", TestAction.Fire).Should().HaveCount(1);
        engine.TryPatchBinding("gameplay", TestAction.Fire, 0, "control", keyA).Should().BeTrue();
        engine.GetBindings("gameplay", TestAction.Fire)[0].Path0.Should().Be(keyA);
        engine.TryPatchBinding("gameplay", TestAction.Fire, 0, "nope", keyA).Should().BeFalse();
        engine.TryPatchBinding("gameplay", TestAction.Fire, 5, "control", keyA).Should().BeFalse();

        engine.ResetBindings("gameplay", TestAction.Fire).Should().BeTrue();
        engine.GetBindings("gameplay", TestAction.Fire)[0].Path0.Should().Be(space);
    }

    [Fact]
    public void Snapshot_Restores_Into_Fresh_Engine()
    {
        using var engine = new InputEngine<TestAction>();
        InputControlPath.TryParse("keyboard/space", out InputControlPath space).Should().BeTrue();
        engine.SetButtonDefinition(TestAction.Fire, new InputButtonActionDefinition());
        engine.RegisterUser(new InputUserDefinition { Id = UserId.System });
        engine.RegisterContext(new InputContextDefinition<TestAction>
        {
            Id = "gameplay",
            Priority = 7,
            BindingsByAction = [[InputBinding.Control(space)], [], []],
        });

        InputSystemSnapshot snapshot = engine.Snapshot();
        snapshot.Contexts.Should().ContainSingle();

        using var fresh = new InputEngine<TestAction>();
        fresh.SetButtonDefinition(TestAction.Fire, new InputButtonActionDefinition());
        fresh.Restore(snapshot);

        var state = new InputSnapshot();
        state.Keys.Set(32);
        fresh.SetSnapshot(state);
        fresh.Update(16.0f);
        fresh.IsActive(TestAction.Fire, UserId.Any).Should().BeTrue();

        Action bad = () => fresh.Restore(new InputSystemSnapshot { Version = 99 });
        bad.Should().Throw<InputSnapshotError>();
    }
}
