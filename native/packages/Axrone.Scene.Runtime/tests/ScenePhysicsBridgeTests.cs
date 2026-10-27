namespace Axrone.Scene.Runtime.Tests;

using Axrone.Numeric;
using Axrone.Physics3D;
using Axrone.Scene;
using Axrone.Scene.Runtime;
using FluentAssertions;
using Xunit;

public sealed class ScenePhysicsBridgeTests
{
    [Fact]
    public void Bridge_CreatesAndDestroysBodies()
    {
        using SceneWorld world = CreatePhysicsWorld();
        using var bridge = new ScenePhysicsBridge3D(world);

        SceneActor actor = world.CreateActor("Body");
        world.GetTransform3D(actor).SetPosition(new Vec3(0.0f, 5.0f, 0.0f));
        world.AddData(actor, SceneRigidbody3D.Default);
        world.AddData(actor, SceneCollider3D.Box(0.5f, 0.5f, 0.5f));
        world.UpdateTransforms();

        bridge.SyncBodies();
        bridge.BodyCount.Should().Be(1);

        world.RemoveData<SceneRigidbody3D>(actor);
        bridge.SyncBodies();
        bridge.BodyCount.Should().Be(0);
    }

    [Fact]
    public void Bridge_DynamicBodyFallsUnderGravity()
    {
        using SceneWorld world = CreatePhysicsWorld();
        using var bridge = new ScenePhysicsBridge3D(world);

        SceneActor actor = world.CreateActor("Falling");
        world.GetTransform3D(actor).SetPosition(new Vec3(0.0f, 20.0f, 0.0f));
        world.AddData(actor, SceneRigidbody3D.Default);
        world.AddData(actor, SceneCollider3D.Default);
        world.UpdateTransforms();

        bridge.SyncBodies();

        Vec3 before = world.GetWorldPosition3D(actor);
        bridge.Step(0.1f);
        bridge.SyncTransformsFromPhysics();

        Vec3 after = world.GetWorldPosition3D(actor);
        after.Y.Should().BeLessThan(before.Y);
    }

    [Fact]
    public void Bridge_StaticBodyStaysPut()
    {
        using SceneWorld world = CreatePhysicsWorld();
        using var bridge = new ScenePhysicsBridge3D(world);

        SceneActor staticActor = world.CreateActor("Floor");
        world.GetTransform3D(staticActor).SetPosition(new Vec3(0.0f, 0.0f, 0.0f));
        SceneRigidbody3D floorBody = SceneRigidbody3D.Static;
        world.AddData(staticActor, floorBody);
        world.AddData(staticActor, SceneCollider3D.Box(5.0f, 0.5f, 5.0f));

        bridge.SyncBodies();

        Vec3 before = world.GetWorldPosition3D(staticActor);
        bridge.Step(0.1f);
        bridge.Step(0.1f);
        bridge.SyncTransformsFromPhysics();

        world.GetWorldPosition3D(staticActor).Should().Be(before);
    }

    [Fact]
    public void Bridge_ReportsContactEvents()
    {
        using SceneWorld world = CreatePhysicsWorld();
        using var bridge = new ScenePhysicsBridge3D(world);

        SceneActor falling = world.CreateActor("Falling");
        world.GetTransform3D(falling).SetPosition(new Vec3(0.0f, 10.0f, 0.0f));
        world.AddData(falling, SceneRigidbody3D.Default);
        world.AddData(falling, SceneCollider3D.Default);

        SceneActor floor = world.CreateActor("Floor");
        world.GetTransform3D(floor).SetPosition(Vec3.Zero);
        world.AddData(floor, SceneRigidbody3D.Static);
        world.AddData(floor, SceneCollider3D.Box(5.0f, 0.5f, 5.0f));
        world.UpdateTransforms();

        bridge.SyncBodies();

        // Step enough frames for the falling body to reach the floor.
        for (int i = 0; i < 300; i++)
        {
            bridge.Step(0.033f);
        }

        bridge.ContactEventCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Bridge_AppliesMassOverride()
    {
        using SceneWorld world = CreatePhysicsWorld();
        using var bridge = new ScenePhysicsBridge3D(world);

        SceneActor actor = world.CreateActor("Heavy");
        world.GetTransform3D(actor).SetPosition(new Vec3(0.0f, 5.0f, 0.0f));
        SceneRigidbody3D body = SceneRigidbody3D.Default;
        body.Mass = 3.0f;
        world.AddData(actor, body);
        world.AddData(actor, SceneCollider3D.Default);
        world.UpdateTransforms();

        bridge.SyncBodies();

        bridge.TryGetBody(actor, out BodyId bodyId).Should().BeTrue();
        bridge.PhysicsWorld.Bodies.GetMass(bodyId).Should().Be(3.0f);
    }

    private static SceneWorld CreatePhysicsWorld()
    {
        var profile = new SceneProfileBuilder(
                new SceneProfileId(SceneStringId.FromName("test/physics")))
            .AddTransform3D()
            .AddPhysics3D()
            .Build();

        return profile.CreateWorld();
    }

}
