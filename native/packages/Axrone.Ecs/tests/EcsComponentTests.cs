namespace Axrone.Ecs.Tests;

internal struct EcsTestPosition
{
    public float X;
    public float Y;

    public EcsTestPosition(float x, float y)
    {
        X = x;
        Y = y;
    }
}

internal struct EcsTestVelocity
{
    public float VX;

    public EcsTestVelocity(float vx) => VX = vx;
}

public class EcsComponentTests
{
    [Fact]
    public void Registry_Assigns_StableIds()
    {
        ComponentRegistry.Register<EcsTestPosition>();
        ComponentRegistry.Register<EcsTestVelocity>();

        ComponentType<EcsTestPosition>.Id.Should().NotBe(ComponentType<EcsTestVelocity>.Id);
        ComponentType<EcsTestPosition>.Mask.IsSet(ComponentType<EcsTestPosition>.Id).Should().BeTrue();
        ComponentType<EcsTestPosition>.Mask.IsSet(ComponentType<EcsTestVelocity>.Id).Should().BeFalse();
    }

    [Fact]
    public void Column_Get_Set_Move_Copy()
    {
        using var column = new ComponentColumn<EcsTestPosition>(4);
        column.Capacity.Should().Be(4);
        column.Id.Should().Be(ComponentType<EcsTestPosition>.Id);

        column.Set(0, new EcsTestPosition { X = 1.0f, Y = 2.0f });
        column.Get(0).X.Should().Be(1.0f);

        column.MoveRow(0, 1);
        column.Get(1).Y.Should().Be(2.0f);

        using var other = new ComponentColumn<EcsTestPosition>(4);
        column.CopyTo(1, other, 0);
        other.Get(0).X.Should().Be(1.0f);

        column.Resize(8);
        column.Capacity.Should().Be(8);
        column.Get(1).X.Should().Be(1.0f);
    }

    [Fact]
    public void Factory_Creates_Registered_Unregistered_Throws()
    {
        ComponentRegistry.Register<EcsTestPosition>();
        using IComponentColumn column = ComponentRegistry.Create(ComponentType<EcsTestPosition>.Id, 4);
        column.Capacity.Should().Be(4);

        Action create = () => ComponentRegistry.Create(new ComponentId(250), 4);
        create.Should().Throw<EcsConfigurationException>().WithMessage("*no registered*");
    }
}
