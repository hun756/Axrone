namespace Axrone.Shapes2D.Tests;

public sealed class ShapeNominalTests
{
    [Fact]
    public void Constants_Match_Reference()
    {
        ShapeConstants.Epsilon.Should().Be(1e-7f);
        ShapeConstants.Tau.Should().BeApproximately(MathF.PI * 2f, 1e-6f);
        ShapeConstants.DefaultGradientLookupSize.Should().Be(256);
        ShapeConstants.DefaultRegistryMaxShapes.Should().Be(2048);
        ShapeConstants.MiterLimit.Should().Be(4.0f);
    }

    [Fact]
    public void ShapeId_Formats_And_Compares()
    {
        ShapeId.Invalid.Value.Should().Be(0u);
        new ShapeId(7u).ToString().Should().Be("shape_7");
        (new ShapeId(1u) < new ShapeId(2u)).Should().BeTrue();
        (new ShapeId(2u) >= new ShapeId(2u)).Should().BeTrue();
    }

    [Fact]
    public void CapacityLimit_Validates_And_Converts()
    {
        CapacityLimit.DefaultShapes.Value.Should().Be(2048);
        CapacityLimit.DefaultCompiled.Value.Should().Be(4096);
        ((int)new CapacityLimit(8)).Should().Be(8);
        ((CapacityLimit)8).Value.Should().Be(8);
        Action zero = () => _ = new CapacityLimit(0);
        zero.Should().Throw<ShapeRegistryException>()
            .Where(e => e.ErrorCode == Shapes2DErrorCode.CapacityExceeded);
    }

    [Fact]
    public void NormalizedScalar_Clamps()
    {
        ((NormalizedScalar)(-0.5f)).Value.Should().Be(0f);
        ((NormalizedScalar)1.5f).Value.Should().Be(1f);
        ((NormalizedScalar)0.25f).Value.Should().Be(0.25f);
        ((NormalizedScalar)0.1f < (NormalizedScalar)0.2f).Should().BeTrue();
        Action nan = () => _ = new NormalizedScalar(float.NaN);
        nan.Should().Throw<ShapeValidationException>();
    }

    [Fact]
    public void ResolvedColor_Clamps_And_Modulates()
    {
        var clamped = new ResolvedColor(2f, -1f, 0.5f, 3f).Clamp();
        clamped.Should().Be(new ResolvedColor(1f, 0f, 0.5f, 1f));
        var modulated = new ResolvedColor(1f, 0.5f, 0.25f, 1f).ModulateAlpha(0.5f);
        modulated.A.Should().BeApproximately(0.5f, 1e-6f);
        ResolvedColor.Transparent.Should().Be(new ResolvedColor(0f, 0f, 0f, 0f));
    }

    [Fact]
    public void ShapeBounds_Normalizes_Expands_Contains()
    {
        ShapeBounds bounds = ShapeBounds.Create(10f, 10f, 0f, 0f);
        bounds.Should().Be(new ShapeBounds(0f, 0f, 10f, 10f));
        bounds.Width.Should().Be(10f);
        bounds.CenterX.Should().Be(5f);
        bounds.Expand(1f).Should().Be(new ShapeBounds(-1f, -1f, 11f, 11f));
        bounds.Contains(new Vec2(5f, 5f)).Should().BeTrue();
        bounds.Contains(new Vec2(99f, 99f)).Should().BeFalse();
        ShapeBounds.Empty.Should().Be(new ShapeBounds(0f, 0f, 0f, 0f));
    }

    [Fact]
    public void ResolvedColor_Interops_With_Shared_Color()
    {
        ResolvedColor local = new(0.2f, 0.4f, 0.6f, 1f);
        Rgba128Float shared = local;
        shared.Should().Be(new Rgba128Float(0.2f, 0.4f, 0.6f, 1f));
        ResolvedColor back = shared;
        back.Should().Be(local);
        HslColor hsl = ColorOps.ToHsl(shared);
        hsl.IsFinite.Should().BeTrue();
    }

    [Fact]
    public void Errors_Carry_Codes()
    {
        new ShapeValidationException("bad").ErrorCode.Should().Be(Shapes2DErrorCode.InvalidShape);
        new PaintValidationException("bad").ErrorCode.Should().Be(Shapes2DErrorCode.InvalidPaint);
        new SerializationException("bad").ErrorCode.Should().Be(Shapes2DErrorCode.InvalidSerializedPayload);
        new ShapeRegistryException(Shapes2DErrorCode.ShapeNotFound, "bad").ErrorCode.Should().Be(Shapes2DErrorCode.ShapeNotFound);
        new Shapes2DException(Shapes2DErrorCode.InvalidNumber, "bad").Message.Should().Be("bad");
    }

    [Fact]
    public void AssertHelpers_Throw_Coded_Errors()
    {
        Action nan = () => ThrowHelper.AssertFinite(float.NaN, "x");
        nan.Should().Throw<ShapeValidationException>();
        Action neg = () => ThrowHelper.AssertPositive(-1f, "x");
        neg.Should().Throw<ShapeValidationException>();
        Action negZero = () => ThrowHelper.AssertNonNegative(-0.5f, "x");
        negZero.Should().Throw<ShapeValidationException>();
    }
}
