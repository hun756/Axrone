namespace Axrone.Batching.Tests;

/// <summary>
/// Coverage for the geometry carrier types: validation, center/extent arithmetic, equality.
/// </summary>
public class GeometryTypesTests
{
    [Fact]
    public void Aabb_StoresCorners()
    {
        var box = new Aabb(1f, 2f, 3f, 4f, 5f, 6f);

        box.MinX.Should().Be(1f);
        box.MinY.Should().Be(2f);
        box.MinZ.Should().Be(3f);
        box.MaxX.Should().Be(4f);
        box.MaxY.Should().Be(5f);
        box.MaxZ.Should().Be(6f);
    }

    [Theory]
    [InlineData(5f, 0f, 0f, 4f, 1f, 1f)]
    [InlineData(0f, 5f, 0f, 1f, 4f, 1f)]
    [InlineData(0f, 0f, 5f, 1f, 1f, 4f)]
    public void Aabb_InvertedBox_Throws(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
    {
        var act = () => new Aabb(minX, minY, minZ, maxX, maxY, maxZ);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Aabb_DegeneratePoint_IsValid()
    {
        var box = new Aabb(2f, 2f, 2f, 2f, 2f, 2f);

        box.Center.Should().Be(new Vector3(2f, 2f, 2f));
        box.Extent.Should().Be(Vector3.Zero);
    }

    [Fact]
    public void Aabb_CenterExtent_RoundTrips()
    {
        var box = new Aabb(-1f, -2f, -3f, 5f, 6f, 7f);

        var rebuilt = Aabb.FromCenterExtent(box.Center, box.Extent);

        rebuilt.Should().Be(box);
    }

    [Fact]
    public void Aabb_Equality_IsByValue()
    {
        var left = new Aabb(0f, 0f, 0f, 1f, 1f, 1f);
        var same = new Aabb(0f, 0f, 0f, 1f, 1f, 1f);
        var other = new Aabb(0f, 0f, 0f, 2f, 1f, 1f);

        left.Should().Be(same);
        (left == same).Should().BeTrue();
        (left != other).Should().BeTrue();
        left.GetHashCode().Should().Be(same.GetHashCode());
    }

    [Fact]
    public void BoneInfluence_StoresWeightsAndJoints()
    {
        var influence = new BoneInfluence4(0.5f, 0.25f, 0.125f, 0.125f, 0, 1, 2, 3);

        influence.W0.Should().Be(0.5f);
        influence.J3.Should().Be(3);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(0, -1, 0, 0)]
    [InlineData(0, 0, -1, 0)]
    [InlineData(0, 0, 0, -1)]
    public void BoneInfluence_NegativeJoint_Throws(int j0, int j1, int j2, int j3)
    {
        var act = () => new BoneInfluence4(1f, 0f, 0f, 0f, j0, j1, j2, j3);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void BoneInfluence_Equality_IsByValue()
    {
        var left = new BoneInfluence4(1f, 0f, 0f, 0f, 0, 0, 0, 0);
        var same = new BoneInfluence4(1f, 0f, 0f, 0f, 0, 0, 0, 0);
        var other = new BoneInfluence4(1f, 0f, 0f, 0f, 1, 0, 0, 0);

        left.Should().Be(same);
        (left != other).Should().BeTrue();
    }
}
