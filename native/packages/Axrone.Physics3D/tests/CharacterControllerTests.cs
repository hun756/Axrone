using Axrone.Numeric;
using Axrone.Physics3D;
using FluentAssertions;
using Xunit;

namespace Axrone.Physics3D.Tests;

public sealed class CharacterControllerTests : IDisposable
{
    private readonly World _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void Falls_And_Grounds_OnFloor()
    {
        BodyId ground = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        _world.Shapes.AttachBox(_world.Bodies, _world.Broadphase, ground,
            new Vec3(0.0f, -1.0f, 0.0f), new Vec3(10.0f, 1.0f, 10.0f));

        var character = new CharacterController3D(_world, new Vec3(0.0f, 5.0f, 0.0f), 0.4f, 1.8f);

        for (int i = 0; i < 240; i++)
        {
            character.SimpleMove(Vec3.Zero, 1.0f / 60.0f, new Vec3(0.0f, -10.0f, 0.0f));
        }

        character.IsGrounded.Should().BeTrue();
        (character.CollisionFlags & CollisionFlags3D.Below).Should().Be(CollisionFlags3D.Below);
        character.Center.Y.Should().BeApproximately(0.9f, 0.2f);
    }

    [Fact]
    public void Walks_Into_Wall_Reports_Sides()
    {
        BodyId ground = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        _world.Shapes.AttachBox(_world.Bodies, _world.Broadphase, ground,
            new Vec3(0.0f, -1.0f, 0.0f), new Vec3(10.0f, 1.0f, 10.0f));
        BodyId wall = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        _world.Shapes.AttachBox(_world.Bodies, _world.Broadphase, wall,
            new Vec3(3.0f, 1.0f, 0.0f), new Vec3(0.5f, 2.0f, 2.0f));

        var character = new CharacterController3D(_world, new Vec3(0.0f, 0.0f, 0.0f), 0.4f, 1.8f);

        CollisionFlags3D flags = CollisionFlags3D.None;
        for (int i = 0; i < 240; i++)
        {
            flags = character.Move(new Vec3(3.0f, -1.0f, 0.0f), 1.0f / 60.0f);
        }

        (flags & CollisionFlags3D.Sides).Should().Be(CollisionFlags3D.Sides);
        character.Center.X.Should().BeLessThan(2.6f);
    }

    [Fact]
    public void Steps_Onto_LowBlock()
    {
        BodyId ground = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        _world.Shapes.AttachBox(_world.Bodies, _world.Broadphase, ground,
            new Vec3(0.0f, -1.0f, 0.0f), new Vec3(10.0f, 1.0f, 10.0f));
        BodyId block = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        _world.Shapes.AttachBox(_world.Bodies, _world.Broadphase, block,
            new Vec3(3.0f, -0.9f, 0.0f), new Vec3(1.0f, 0.1f, 1.0f));

        var character = new CharacterController3D(_world, new Vec3(0.0f, 0.0f, 0.0f), 0.4f, 1.8f);

        for (int i = 0; i < 240; i++)
        {
            character.Move(new Vec3(2.0f, -1.0f, 0.0f), 1.0f / 60.0f);
        }

        character.Center.X.Should().BeGreaterThan(2.0f);
    }
}
