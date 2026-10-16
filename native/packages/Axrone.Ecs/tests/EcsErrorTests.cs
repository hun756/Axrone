namespace Axrone.Ecs.Tests;

public class EcsErrorTests
{
    [Fact]
    public void Errors_Carry_Codes_And_Entities()
    {
        var entity = new Entity(new EntityId(5), new EntityVersion(2));
        var entityError = new EcsEntityException(entity, "gone");
        entityError.Code.Should().Be("ecs.entity-not-found");
        entityError.Entity.Should().Be(entity);

        var componentError = new EcsComponentException("Health", "missing");
        componentError.Code.Should().Be("ecs.component-not-found");
        componentError.ComponentName.Should().Be("Health");

        new EcsConfigurationException("ecs.max-components", "full").Code.Should().Be("ecs.max-components");
    }
}
