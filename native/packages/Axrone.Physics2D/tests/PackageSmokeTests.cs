namespace Axrone.Physics2D.Tests;

using Axrone.Geometry;
using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public class PackageSmokeTests
{
    [Fact]
    public void ReferencesResolve()
    {
        var box = new Aabb2D(new Vec2(0, 0), new Vec2(1, 1));

        box.Contains(new Vec2(0.5f, 0.5f)).Should().BeTrue();
    }
}
