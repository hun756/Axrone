namespace Axrone.Ecs.Tests;

public class EcsNominalTests
{
    [Fact]
    public void Entity_Null_IsZero_AndOrdered()
    {
        Entity.Null.IsNull.Should().BeTrue();
        var entity = new Entity(new EntityId(3), new EntityVersion(1));
        entity.IsNull.Should().BeFalse();
        (entity > Entity.Null).Should().BeTrue();
        entity.CompareTo(Entity.Null).Should().BeGreaterThan(0);
        entity.ToString().Should().Be("Entity(3v1)");
    }

    [Fact]
    public void Ids_Compare_AndFormat()
    {
        new EntityId(1).CompareTo(new EntityId(2)).Should().BeLessThan(0);
        new EntityVersion(2).ToString().Should().Be("2");
        new ComponentId(7).ToString().Should().Be("7");
        new ArchetypeId(0).ToString().Should().Be("0");
    }

    [Fact]
    public void Mask_Set_Clear_Query()
    {
        ComponentMask mask = ComponentMask.Empty
            .With(new ComponentId(0))
            .With(new ComponentId(65))
            .With(new ComponentId(200));

        mask.IsEmpty.Should().BeFalse();
        mask.IsSet(new ComponentId(0)).Should().BeTrue();
        mask.IsSet(new ComponentId(65)).Should().BeTrue();
        mask.IsSet(new ComponentId(200)).Should().BeTrue();
        mask.IsSet(new ComponentId(1)).Should().BeFalse();

        ComponentMask cleared = mask.Without(new ComponentId(65));
        cleared.IsSet(new ComponentId(65)).Should().BeFalse();
        cleared.IsSet(new ComponentId(0)).Should().BeTrue();
    }

    [Fact]
    public void Mask_Algebra_Works()
    {
        ComponentMask a = ComponentMask.Empty.With(new ComponentId(1)).With(new ComponentId(2));
        ComponentMask b = ComponentMask.Empty.With(new ComponentId(2)).With(new ComponentId(3));

        (a | b).Should().Be(ComponentMask.Empty.With(new ComponentId(1)).With(new ComponentId(2)).With(new ComponentId(3)));
        (a & b).Should().Be(ComponentMask.Empty.With(new ComponentId(2)));
        a.ContainsAll(ComponentMask.Empty.With(new ComponentId(1))).Should().BeTrue();
        a.ContainsAll(b).Should().BeFalse();
        a.ContainsAny(b).Should().BeTrue();
        a.ContainsAny(ComponentMask.Empty.With(new ComponentId(9))).Should().BeFalse();
        a.Should().NotBe(b);
    }
}
