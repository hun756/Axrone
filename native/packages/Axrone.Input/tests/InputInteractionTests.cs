using Axrone.Input;
using FluentAssertions;
using Xunit;

namespace Axrone.Input.Tests;

public class InputInteractionTests
{
    [Fact]
    public void Press_Fires_Performed_Then_Canceled()
    {
        var state = new InputButtonState();
        InputInteraction.UpdatePress(ref state, 1.0f, 16.0f);
        state.Pressed.Should().BeTrue();
        state.Phase.Should().Be(InputActionPhase.Performed);

        InputInteraction.UpdatePress(ref state, 1.0f, 16.0f);
        state.Pressed.Should().BeFalse();
        state.Value.Should().BeTrue();

        InputInteraction.UpdatePress(ref state, 0.0f, 16.0f);
        state.Released.Should().BeTrue();
        state.Phase.Should().Be(InputActionPhase.Canceled);
    }

    [Fact]
    public void Press_Hysteresis_Blocks_Chatter()
    {
        var state = new InputButtonState();
        InputInteraction.UpdatePress(ref state, 1.0f, 16.0f, pressPoint: 0.5f, releasePoint: 0.5f);
        InputInteraction.UpdatePress(ref state, 0.4f, 16.0f, pressPoint: 0.5f, releasePoint: 0.3f);
        state.Value.Should().BeTrue();
        InputInteraction.UpdatePress(ref state, 0.2f, 16.0f, pressPoint: 0.5f, releasePoint: 0.3f);
        state.Value.Should().BeFalse();
    }

    [Fact]
    public void Hold_Requires_Duration_Once()
    {
        var state = new InputButtonState();
        var settings = new InteractionSettings(Type: InputInteractionType.Hold, DurationMs: 400.0f, Mode: HoldMode.Once);

        InputInteraction.UpdateHold(ref state, 1.0f, 100.0f, settings);
        state.Phase.Should().Be(InputActionPhase.Started);

        InputInteraction.UpdateHold(ref state, 1.0f, 100.0f, settings);
        InputInteraction.UpdateHold(ref state, 1.0f, 100.0f, settings);
        InputInteraction.UpdateHold(ref state, 1.0f, 100.0f, settings);
        InputInteraction.UpdateHold(ref state, 1.0f, 100.0f, settings);
        state.HoldTriggered.Should().BeTrue();
        state.Phase.Should().Be(InputActionPhase.Performed);

        InputInteraction.UpdateHold(ref state, 1.0f, 100.0f, settings);
        state.Phase.Should().Be(InputActionPhase.None);

        InputInteraction.UpdateHold(ref state, 0.0f, 100.0f, settings);
        state.Phase.Should().Be(InputActionPhase.Canceled);
    }

    [Fact]
    public void Hold_Continuous_Emits_Every_Frame()
    {
        var state = new InputButtonState();
        var settings = new InteractionSettings(Type: InputInteractionType.Hold, DurationMs: 200.0f, Mode: HoldMode.Continuous);

        InputInteraction.UpdateHold(ref state, 1.0f, 100.0f, settings);
        InputInteraction.UpdateHold(ref state, 1.0f, 100.0f, settings);
        InputInteraction.UpdateHold(ref state, 1.0f, 100.0f, settings);
        state.Phase.Should().Be(InputActionPhase.Performed);
        InputInteraction.UpdateHold(ref state, 1.0f, 100.0f, settings);
        state.Phase.Should().Be(InputActionPhase.Performed);
    }

    [Fact]
    public void Tap_Quick_Release_Performs()
    {
        var state = new InputButtonState();
        var settings = new InteractionSettings(Type: InputInteractionType.Tap, DurationMs: 400.0f);

        InputInteraction.UpdateTap(ref state, 1.0f, 100.0f, settings);
        InputInteraction.UpdateTap(ref state, 1.0f, 100.0f, settings);
        InputInteraction.UpdateTap(ref state, 0.0f, 100.0f, settings);
        state.TapTriggered.Should().BeTrue();
        state.Phase.Should().Be(InputActionPhase.Performed);
    }

    [Fact]
    public void Tap_Overtime_Cancels()
    {
        var state = new InputButtonState();
        var settings = new InteractionSettings(Type: InputInteractionType.Tap, DurationMs: 200.0f);

        InputInteraction.UpdateTap(ref state, 1.0f, 100.0f, settings);
        InputInteraction.UpdateTap(ref state, 1.0f, 150.0f, settings);
        InputInteraction.UpdateTap(ref state, 1.0f, 150.0f, settings);
        state.Phase.Should().Be(InputActionPhase.Canceled);
    }

    [Fact]
    public void MultiTap_Double_Completes()
    {
        var state = new InputButtonState();
        var settings = new InteractionSettings(Type: InputInteractionType.MultiTap, DurationMs: 400.0f, TapCount: 2, MaxDelayMs: 300.0f);

        InputInteraction.UpdateMultiTap(ref state, 1.0f, 50.0f, settings);
        InputInteraction.UpdateMultiTap(ref state, 0.0f, 50.0f, settings);
        state.Phase.Should().Be(InputActionPhase.Started);
        InputInteraction.UpdateMultiTap(ref state, 1.0f, 50.0f, settings);
        InputInteraction.UpdateMultiTap(ref state, 0.0f, 50.0f, settings);
        state.MultiTapTriggered.Should().BeTrue();
        state.Phase.Should().Be(InputActionPhase.Performed);
    }

    [Fact]
    public void MultiTap_Gap_Expiry_Cancels()
    {
        var state = new InputButtonState();
        var settings = new InteractionSettings(Type: InputInteractionType.MultiTap, DurationMs: 400.0f, TapCount: 2, MaxDelayMs: 100.0f);

        InputInteraction.UpdateMultiTap(ref state, 1.0f, 50.0f, settings);
        InputInteraction.UpdateMultiTap(ref state, 0.0f, 50.0f, settings);
        InputInteraction.UpdateMultiTap(ref state, 0.0f, 150.0f, settings);
        state.Phase.Should().Be(InputActionPhase.Canceled);
    }

    [Fact]
    public void Repeat_Ticks_On_Interval()
    {
        var state = new InputButtonState();
        var settings = new InteractionSettings(Type: InputInteractionType.Repeat, DurationMs: 200.0f, IntervalMs: 100.0f);

        InputInteraction.UpdateRepeat(ref state, 1.0f, 16.0f, settings);
        state.RepeatTriggered.Should().BeTrue();
        state.RepeatCount.Should().Be(1);

        InputInteraction.UpdateRepeat(ref state, 1.0f, 100.0f, settings);
        state.RepeatTriggered.Should().BeFalse();
        InputInteraction.UpdateRepeat(ref state, 1.0f, 100.0f, settings);
        state.RepeatTriggered.Should().BeTrue();
        state.RepeatCount.Should().Be(2);
    }

    [Fact]
    public void Trackers_Report_Delta_And_Phase()
    {
        var axis = new InputAxisState();
        InputInteraction.UpdateAxis(ref axis, 0.5f);
        axis.Delta.Should().Be(0.5f);
        axis.Phase.Should().Be(InputActionPhase.Changed);

        var vec = new InputVector2State();
        InputInteraction.UpdateVector2(ref vec, new System.Numerics.Vector2(1.0f, 0.0f));
        vec.Magnitude.Should().BeApproximately(1.0f, 1e-5f);
        vec.Phase.Should().Be(InputActionPhase.Changed);
    }
}
