using System.Numerics;
using Axrone.Input;
using FluentAssertions;
using Xunit;

namespace Axrone.Input.Tests;

public class InputProcessorTests
{
    [Fact]
    public void Scalar_Scale_Invert_Clamp()
    {
        InputProcessor.ApplyScalar(2.0f, ProcessorConfiguration.Scale(3.0f)).Should().Be(6.0f);
        InputProcessor.ApplyScalar(2.0f, ProcessorConfiguration.Invert()).Should().Be(-2.0f);
        InputProcessor.ApplyScalar(5.0f, ProcessorConfiguration.Clamp(-1.0f, 1.0f)).Should().Be(1.0f);
        InputProcessor.ApplyScalar(5.0f, ProcessorConfiguration.Clamp(1.0f, -1.0f)).Should().Be(1.0f);
    }

    [Fact]
    public void Scalar_Deadzone_Rescales_Remainder()
    {
        InputProcessor.ApplyScalar(0.1f, ProcessorConfiguration.Deadzone(0.2f)).Should().Be(0.0f);
        float processed = InputProcessor.ApplyScalar(0.6f, ProcessorConfiguration.Deadzone(0.2f));
        processed.Should().BeApproximately(0.5f, 1e-5f);
        InputProcessor.ApplyScalar(-0.6f, ProcessorConfiguration.Deadzone(0.2f)).Should().BeApproximately(-0.5f, 1e-5f);
    }

    [Fact]
    public void Scalar_Curve_Preserves_Sign()
    {
        InputProcessor.ApplyScalar(0.5f, ProcessorConfiguration.Curve(2.0f)).Should().BeApproximately(0.25f, 1e-5f);
        InputProcessor.ApplyScalar(-0.5f, ProcessorConfiguration.Curve(2.0f)).Should().BeApproximately(-0.25f, 1e-5f);
        InputProcessor.ApplyScalar(-0.5f, ProcessorConfiguration.Curve(2.0f, keepSign: false)).Should().Be(0.0f);
    }

    [Fact]
    public void Vector2_Scale_Invert_Normalize()
    {
        InputProcessor.ApplyVector2(new Vector2(1.0f, 2.0f), ProcessorConfiguration.ScaleVector2(2.0f, 0.5f))
            .Should().Be(new Vector2(2.0f, 1.0f));
        InputProcessor.ApplyVector2(new Vector2(1.0f, -2.0f), ProcessorConfiguration.InvertVector2(true, false))
            .Should().Be(new Vector2(-1.0f, -2.0f));
        InputProcessor.ApplyVector2(new Vector2(3.0f, 4.0f), ProcessorConfiguration.NormalizeVector2())
            .Should().Be(new Vector2(0.6f, 0.8f));
        InputProcessor.ApplyVector2(Vector2.Zero, ProcessorConfiguration.NormalizeVector2()).Should().Be(Vector2.Zero);
    }

    [Fact]
    public void Vector2_ClampMagnitude_Deadzone()
    {
        InputProcessor.ApplyVector2(new Vector2(3.0f, 4.0f), ProcessorConfiguration.ClampMagnitude(0.0f, 1.0f))
            .Length().Should().BeApproximately(1.0f, 1e-5f);
        InputProcessor.ApplyVector2(new Vector2(0.1f, 0.0f), ProcessorConfiguration.Deadzone(0.2f))
            .Should().Be(Vector2.Zero);
    }
}
