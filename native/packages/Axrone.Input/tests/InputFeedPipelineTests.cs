using Axrone.Input;
using FluentAssertions;
using Xunit;

namespace Axrone.Input.Tests;

public class InputFeedPipelineTests
{
    [Fact]
    public void Silk_Mapping_Covers_DPad_And_System()
    {
        InputControlReader.MapSilkButton(11).Should().Be(12);
        InputControlReader.MapSilkButton(12).Should().Be(15);
        InputControlReader.MapSilkButton(8).Should().Be(16);
        InputControlReader.MapSilkButton(0).Should().Be(0);
    }

    [Fact]
    public void Feed_Pump_Drives_Engine_End_To_End()
    {
        using var coordinator = new InputFrameCoordinator();
        using var engine = new InputEngine<TestAction>();

        InputControlPath.TryParse("keyboard/space", out InputControlPath space).Should().BeTrue();
        engine.SetButtonDefinition(TestAction.Fire, new InputButtonActionDefinition());
        engine.RegisterUser(new InputUserDefinition { Id = UserId.System });
        engine.RegisterContext(new InputContextDefinition<TestAction>
        {
            Id = "gameplay",
            BindingsByAction = [[InputBinding.Control(space)], [], []],
        });

        coordinator.RingBuffer.FeedKeyDown(Key.Space).Should().BeTrue();
        coordinator.RingBuffer.FeedKeyChar('a').Should().BeTrue();

        engine.ClearFrameText();
        InputPipeline.PumpFrame(coordinator, engine, 16.0f);

        engine.TryGetButton("gameplay", TestAction.Fire, out InputButtonState state).Should().BeTrue();
        state.Phase.Should().Be(InputActionPhase.Performed);
        engine.Text.Should().ContainSingle().Which.Character.Should().Be('a');
    }

    [Fact]
    public void Feed_Gamepad_DPadUp_Reaches_Engine_Path()
    {
        using var coordinator = new InputFrameCoordinator();
        using var engine = new InputEngine<TestAction>();

        InputControlPath.TryParse("gamepad/dpad-up", out InputControlPath dpad).Should().BeTrue();
        dpad.Index.Should().Be(12);
        engine.SetButtonDefinition(TestAction.Fire, new InputButtonActionDefinition());
        engine.RegisterUser(new InputUserDefinition { Id = UserId.System });
        engine.RegisterContext(new InputContextDefinition<TestAction>
        {
            Id = "gameplay",
            BindingsByAction = [[InputBinding.Control(dpad)], [], []],
        });

        coordinator.RingBuffer.FeedGamepadButton(0, ButtonName.DPadUp, true).Should().BeTrue();
        InputPipeline.PumpFrame(coordinator, engine, 16.0f);

        engine.TryGetButton("gameplay", TestAction.Fire, out InputButtonState state).Should().BeTrue();
        state.Value.Should().BeTrue();
    }

    [Fact]
    public void Feed_Mouse_Move_Reaches_Axis()
    {
        using var coordinator = new InputFrameCoordinator();
        using var engine = new InputEngine<TestAction>();

        InputControlPath.TryParse("mouse/move/x", out InputControlPath moveX).Should().BeTrue();
        engine.SetAxisDefinition(TestAction.Move, new InputAxisActionDefinition());
        engine.RegisterUser(new InputUserDefinition { Id = UserId.System });
        engine.RegisterContext(new InputContextDefinition<TestAction>
        {
            Id = "gameplay",
            BindingsByAction = [[], [InputBinding.Control(moveX)], []],
        });

        coordinator.RingBuffer.FeedMouseMove(3.0f, 0.0f).Should().BeTrue();
        InputPipeline.PumpFrame(coordinator, engine, 16.0f);

        engine.TryGetAxis("gameplay", TestAction.Move, out InputAxisState state).Should().BeTrue();
        state.Value.Should().Be(1.0f);
    }
}
