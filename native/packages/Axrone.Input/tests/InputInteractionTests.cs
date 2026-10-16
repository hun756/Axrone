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
}
