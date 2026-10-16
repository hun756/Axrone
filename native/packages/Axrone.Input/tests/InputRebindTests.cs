using Axrone.Input;
using FluentAssertions;
using Xunit;

namespace Axrone.Input.Tests;

public class InputRebindTests
{
    [Fact]
    public void Session_Completes_On_First_Actuation()
    {
        InputBinding? completed = null;
        var session = new InputRebindSession<TestAction>(
            new RebindRequest<TestAction>("gameplay", TestAction.Fire),
            new RebindHandlers<TestAction> { Complete = binding => completed = binding },
            startMs: 0.0);

        InputControlPath.TryParse("keyboard/space", out InputControlPath space).Should().BeTrue();
        session.TryOffer(space, 0.1f, 10.0).Should().BeFalse();
        session.TryOffer(space, 1.0f, 10.0).Should().BeTrue();
        session.IsActive.Should().BeFalse();
        completed.HasValue.Should().BeTrue();
    }

    [Fact]
    public void Session_Respects_Filter_And_Timeout()
    {
        RebindCancelReason? reason = null;
        var session = new InputRebindSession<TestAction>(
            new RebindRequest<TestAction>("gameplay", TestAction.Fire, TimeoutMs: 100.0f, DeviceFilter: DeviceKind.Gamepad),
            new RebindHandlers<TestAction> { Cancel = r => reason = r },
            startMs: 0.0);

        InputControlPath.TryParse("keyboard/space", out InputControlPath space).Should().BeTrue();
        session.TryOffer(space, 1.0f, 10.0).Should().BeFalse();
        session.Update(50.0).Should().BeFalse();
        session.Update(150.0).Should().BeTrue();
        reason.Should().Be(RebindCancelReason.Timeout);
    }

    [Fact]
    public void Engine_AutoFeeds_And_Replaces()
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

        InputBinding? first = null;
        RebindCancelReason? firstCancel = null;
        engine.BeginRebind(
            new RebindRequest<TestAction>("gameplay", TestAction.Fire),
            new RebindHandlers<TestAction>
            {
                Complete = binding => first = binding,
                Cancel = r => firstCancel = r,
            });

        InputBinding? second = null;
        engine.BeginRebind(
            new RebindRequest<TestAction>("gameplay", TestAction.Fire),
            new RebindHandlers<TestAction> { Complete = binding => second = binding });
        firstCancel.Should().Be(RebindCancelReason.Replaced);
        first.Should().BeNull();

        var snapshot = new InputSnapshot();
        snapshot.Keys.Set((uint)'A');
        engine.SetSnapshot(snapshot);
        engine.Update(16.0f);

        engine.HasActiveRebind.Should().BeFalse();
        second.HasValue.Should().BeTrue();
        second!.Value.Path0.Should().Be(keyA);

        engine.TrySetBinding("gameplay", TestAction.Fire, new SlotIndex(0), second!.Value).Should().BeTrue();
        engine.SetSnapshot(snapshot);
        engine.Update(16.0f);
        engine.TryGetButton("gameplay", TestAction.Fire, out InputButtonState state).Should().BeTrue();
        state.Value.Should().BeTrue();
    }
}
