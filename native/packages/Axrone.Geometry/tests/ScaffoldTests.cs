namespace Axrone.Geometry.Tests;

using Xunit;
using FluentAssertions;
using Axrone.Numeric;

public class ScaffoldTests
{
    [Fact]
    public void Scaffold_ReferencesNumericVecTypes()
    {
        typeof(Vec3).Assembly.GetName().Name.Should().Be("Axrone.Numeric");

        new Vec3(1F, 2F, 3F).X.Should().Be(1F);
    }
}