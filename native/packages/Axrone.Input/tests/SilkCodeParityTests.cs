using Axrone.Input;
using FluentAssertions;
using Xunit;

namespace Axrone.Input.Tests;

public class SilkCodeParityTests
{
    [Fact]
    public void Key_Codes_Match_Silk_By_Name()
    {
        foreach (string name in Enum.GetNames<Key>())
        {
            Silk.NET.Input.Key silk = Enum.Parse<Silk.NET.Input.Key>(name);
            Convert.ToInt32((Key)Enum.Parse<Key>(name)).Should().Be(Convert.ToInt32(silk), name);
        }
    }

    [Fact]
    public void MouseButton_Codes_Match_Silk_By_Name()
    {
        foreach (string name in Enum.GetNames<MouseButton>())
        {
            Silk.NET.Input.MouseButton silk = Enum.Parse<Silk.NET.Input.MouseButton>(name);
            Convert.ToInt32((MouseButton)Enum.Parse<MouseButton>(name)).Should().Be(Convert.ToInt32(silk), name);
        }
    }

    [Fact]
    public void ButtonName_Codes_Match_Silk_By_Name()
    {
        foreach (string name in Enum.GetNames<ButtonName>())
        {
            Silk.NET.Input.ButtonName silk = Enum.Parse<Silk.NET.Input.ButtonName>(name);
            Convert.ToInt32((ButtonName)Enum.Parse<ButtonName>(name)).Should().Be(Convert.ToInt32(silk), name);
        }
    }

    [Fact]
    public void Mapping_Guards_Unknown_Codes()
    {
        SilkInputMapping.ToKey((Silk.NET.Input.Key)9999).Should().Be(Key.Unknown);
        SilkInputMapping.TryToMouseButton((Silk.NET.Input.MouseButton)99, out _).Should().BeFalse();
        SilkInputMapping.TryToGamepadButton(Silk.NET.Input.ButtonName.Unknown, out _).Should().BeFalse();

        SilkInputMapping.TryToGamepadButton(Silk.NET.Input.ButtonName.DPadUp, out ushort engine).Should().BeTrue();
        engine.Should().Be(12);
    }

    [Fact]
    public void Attachment_Rejects_Null_Arguments()
    {
        using var coordinator = new InputFrameCoordinator();
        Action nullContext = () => SilkInputAttachment.Attach(null!, coordinator.RingBuffer);
        Action nullQueue = () => SilkInputAttachment.Attach(null!, null!);
        nullContext.Should().Throw<ArgumentNullException>();
        nullQueue.Should().Throw<ArgumentNullException>();
    }
}
