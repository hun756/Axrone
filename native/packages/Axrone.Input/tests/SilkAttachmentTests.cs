using Axrone.Input;
using FluentAssertions;
using Moq;
using Xunit;

namespace Axrone.Input.Tests;

public class SilkAttachmentTests
{
    [Fact]
    public void Keyboard_Events_Reach_Engine_End_To_End()
    {
        var keyboard = new Mock<Silk.NET.Input.IKeyboard>();
        var context = new Mock<Silk.NET.Input.IInputContext>();
        context.SetupGet(c => c.Keyboards).Returns(new List<Silk.NET.Input.IKeyboard> { keyboard.Object });
        context.SetupGet(c => c.Mice).Returns(new List<Silk.NET.Input.IMouse>());
        context.SetupGet(c => c.Gamepads).Returns(new List<Silk.NET.Input.IGamepad>());

        using var coordinator = new InputFrameCoordinator();
        using var attachment = SilkInputAttachment.Attach(context.Object, coordinator.RingBuffer);
        using var engine = new InputEngine<TestAction>();

        InputControlPath.TryParse("keyboard/space", out InputControlPath space).Should().BeTrue();
        engine.SetButtonDefinition(TestAction.Fire, new InputButtonActionDefinition());
        engine.RegisterUser(new InputUserDefinition { Id = UserId.System });
        engine.RegisterContext(new InputContextDefinition<TestAction>
        {
            Id = "gameplay",
            BindingsByAction = [[InputBinding.Control(space)], [], []],
        });

        keyboard.Raise(k => k.KeyDown += null, keyboard.Object, Silk.NET.Input.Key.Space, 0);
        InputPipeline.PumpFrame(coordinator, engine, 16.0f);
        engine.IsActive(TestAction.Fire, UserId.Any).Should().BeTrue();

        keyboard.Raise(k => k.KeyUp += null, keyboard.Object, Silk.NET.Input.Key.Space, 0);
        InputPipeline.PumpFrame(coordinator, engine, 16.0f);
        engine.IsReleased(TestAction.Fire, UserId.Any).Should().BeTrue();
    }

    [Fact]
    public void Gamepad_Events_Reach_Engine_With_Connection()
    {
        var gamepad = new Mock<Silk.NET.Input.IGamepad>();
        gamepad.SetupGet(g => g.Index).Returns(0);
        var context = new Mock<Silk.NET.Input.IInputContext>();
        context.SetupGet(c => c.Keyboards).Returns(new List<Silk.NET.Input.IKeyboard>());
        context.SetupGet(c => c.Mice).Returns(new List<Silk.NET.Input.IMouse>());
        context.SetupGet(c => c.Gamepads).Returns(new List<Silk.NET.Input.IGamepad> { gamepad.Object });

        using var coordinator = new InputFrameCoordinator();
        using var attachment = SilkInputAttachment.Attach(context.Object, coordinator.RingBuffer);
        using var engine = new InputEngine<TestAction>();

        InputControlPath.TryParse("gamepad/connected", out InputControlPath connected).Should().BeTrue();
        InputControlPath.TryParse("gamepad/south", out InputControlPath south).Should().BeTrue();
        engine.SetButtonDefinition(TestAction.Fire, new InputButtonActionDefinition());
        engine.SetButtonDefinition(TestAction.Move, new InputButtonActionDefinition());
        engine.RegisterUser(new InputUserDefinition { Id = UserId.System });
        engine.RegisterContext(new InputContextDefinition<TestAction>
        {
            Id = "gameplay",
            BindingsByAction = [[InputBinding.Control(south)], [InputBinding.Control(connected)], []],
        });

        InputPipeline.PumpFrame(coordinator, engine, 16.0f);
        engine.IsActive(TestAction.Move, UserId.Any).Should().BeTrue();

        gamepad.Raise(g => g.ButtonDown += null, gamepad.Object, new Silk.NET.Input.Button(Silk.NET.Input.ButtonName.A, 0, true));
        InputPipeline.PumpFrame(coordinator, engine, 16.0f);
        engine.IsActive(TestAction.Fire, UserId.Any).Should().BeTrue();
    }
}
