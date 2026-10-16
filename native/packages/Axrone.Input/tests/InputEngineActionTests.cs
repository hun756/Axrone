using Axrone.Input;
using FluentAssertions;
using Xunit;

namespace Axrone.Input.Tests;

internal enum TestAction
{
    Fire = 0,
    Move = 1,
    Look = 2,
}

public class InputEngineActionTests
{
    [Fact]
    public void Button_Press_Commits_Performed()
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

        engine.TryGetButton("gameplay", TestAction.Fire, out InputButtonState state).Should().BeTrue();
        state.Phase.Should().Be(InputActionPhase.Performed);
        state.Value.Should().BeTrue();
    }

    [Fact]
    public void Axis_Pair_With_Deadzone_And_Clamp()
    {
        using var engine = new InputEngine<TestAction>();
        InputControlPath.TryParse("keyboard/A", out InputControlPath left).Should().BeTrue();
        InputControlPath.TryParse("keyboard/D", out InputControlPath right).Should().BeTrue();
        engine.SetAxisDefinition(TestAction.Move, new InputAxisActionDefinition { Deadzone = 0.2f });
        engine.RegisterUser(new InputUserDefinition { Id = UserId.System });
        engine.RegisterContext(new InputContextDefinition<TestAction>
        {
            Id = "gameplay",
            BindingsByAction = [[], [InputBinding.Axis(left, right)], []],
        });

        var snapshot = new InputSnapshot();
        snapshot.Keys.Set((uint)'D');
        engine.SetSnapshot(snapshot);
        engine.Update(16.0f);

        engine.TryGetAxis("gameplay", TestAction.Move, out InputAxisState state).Should().BeTrue();
        state.Value.Should().Be(1.0f);
    }

    [Fact]
    public void Vector2_Pad_With_Normalize()
    {
        using var engine = new InputEngine<TestAction>();
        InputControlPath.TryParse("keyboard/W", out InputControlPath up).Should().BeTrue();
        InputControlPath.TryParse("keyboard/S", out InputControlPath down).Should().BeTrue();
        InputControlPath.TryParse("keyboard/A", out InputControlPath left).Should().BeTrue();
        InputControlPath.TryParse("keyboard/D", out InputControlPath right).Should().BeTrue();
        engine.SetVector2Definition(TestAction.Look, new InputVector2ActionDefinition { Normalize = true });
        engine.RegisterUser(new InputUserDefinition { Id = UserId.System });
        engine.RegisterContext(new InputContextDefinition<TestAction>
        {
            Id = "gameplay",
            BindingsByAction = [[], [], [InputBinding.Vector2Directional(up, down, left, right, normalize: true)]],
        });

        var snapshot = new InputSnapshot();
        snapshot.Keys.Set((uint)'W');
        snapshot.Keys.Set((uint)'D');
        engine.SetSnapshot(snapshot);
        engine.Update(16.0f);

        engine.TryGetVector2("gameplay", TestAction.Look, out InputVector2State state).Should().BeTrue();
        state.Magnitude.Should().BeApproximately(1.0f, 1e-4f);
    }

    [Fact]
    public void Callback_Fires_On_Transition()
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

        int calls = 0;
        engine.Subscribe(_ => calls++);

        var snapshot = new InputSnapshot();
        snapshot.Keys.Set(32);
        engine.SetSnapshot(snapshot);
        engine.Update(16.0f);
        calls.Should().Be(1);

        engine.SetSnapshot(new InputSnapshot());
        engine.Update(16.0f);
        calls.Should().Be(2);
    }
}
