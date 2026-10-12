namespace Axrone.Raycast.Tests;

using Axrone.Geometry;
using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public class PackageSmokeTests
{
    [Fact]
    public void ReferencesResolve()
    {
        var box = new Aabb3D(new Vec3(0, 0, 0), new Vec3(1, 1, 1));

        box.Contains(new Vec3(0.5f, 0.5f, 0.5f)).Should().BeTrue();
    }
}
