namespace Axrone.Asset2D.Tests;

public sealed class Asset2DGeometryTests
{
    [Fact]
    public void Rect2D_Size_Position_And_Format()
    {
        var rect = new Rect2D(10f, 20f, 30f, 40f);
        rect.Size.Should().Be(new Size2D(30f, 40f));
        rect.Position.Should().Be(new Vec2(10f, 20f));
        rect.ToString().Should().Be("[X:10.00px, Y:20.00px, W:30.00px, H:40.00px]");
        Rect2D.Zero.Size.Should().Be(Size2D.Zero);
    }

    [Fact]
    public void TopLeft_Policy_Maps_Pixels_To_Uv()
    {
        var region = new Rect2D(10f, 20f, 30f, 40f);
        var size = new Size2D(100f, 200f);
        UvRect uv = TopLeftOriginPolicy.ComputeUv(in region, in size);
        uv.U.Value.Should().BeApproximately(0.1f, 1e-6f);
        uv.V.Value.Should().BeApproximately(0.1f, 1e-6f);
        uv.Width.Value.Should().BeApproximately(0.3f, 1e-6f);
        uv.Height.Value.Should().BeApproximately(0.2f, 1e-6f);
    }

    [Fact]
    public void BottomLeft_Policy_Flips_V()
    {
        var region = new Rect2D(10f, 20f, 30f, 40f);
        var size = new Size2D(100f, 200f);
        UvRect uv = BottomLeftOriginPolicy.ComputeUv(in region, in size);
        uv.U.Value.Should().BeApproximately(0.1f, 1e-6f);
        uv.V.Value.Should().BeApproximately(0.7f, 1e-6f);
        uv.Width.Value.Should().BeApproximately(0.3f, 1e-6f);
        uv.Height.Value.Should().BeApproximately(0.2f, 1e-6f);
    }

    [Fact]
    public void Border2D_Formats_Insets()
    {
        var border = new Border2D(1f, 2f, 3f, 4f);
        border.ToString().Should().Be("[L:1.00px, R:2.00px, T:3.00px, B:4.00px]");
        Border2D.Zero.Should().Be(new Border2D(0f, 0f, 0f, 0f));
    }
}
