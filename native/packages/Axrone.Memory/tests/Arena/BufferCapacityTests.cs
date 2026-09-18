using Axrone.Memory.Arena;
using Axrone.Utility.Alignment;

namespace Axrone.Memory.Tests.Arena;

public class BufferCapacityTests
{
    [Fact]
    public void Constructor_Accepts_Power_Of_Two()
    {
        var cap = new BufferCapacity(16);
        cap.Value.Should().Be(16u);
        cap.Mask.Should().Be(15u);
    }

    [Fact]
    public void Constructor_Rejects_Non_Power_Of_Two()
    {
        Action act = () => { _ = new BufferCapacity(7); };
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_Rejects_Zero()
    {
        Action act = () => { _ = new BufferCapacity(0); };
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Implicit_Conversion_From_nuint()
    {
        BufferCapacity cap = 32u;
        cap.Value.Should().Be(32u);
    }

    [Fact]
    public void Implicit_Conversion_To_nuint()
    {
        var cap = new BufferCapacity(64);
        nuint value = cap;
        value.Should().Be(64u);
    }
}
