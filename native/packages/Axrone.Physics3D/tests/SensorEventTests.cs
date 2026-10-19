using Axrone.Numeric;
using Axrone.Physics3D;
using FluentAssertions;
using Xunit;

namespace Axrone.Physics3D.Tests;

public sealed class SensorEventTests : IDisposable
{
    private readonly World _world = new();

    public void Dispose() => _world.Dispose();

    [Fact]
    public void SensorOverlap_Raises_Enter_Stay_Exit()
    {
        BodyId trigger = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, trigger, Vec3.Zero, 1.0f, isSensor: true);

        BodyId ball = _world.Bodies.Create(new BodyDef { Position = new Vec3(0.0f, 1.2f, 0.0f), LinearVelocity = new Vec3(0.0f, 5.0f, 0.0f) });
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, ball, Vec3.Zero, 0.5f);

        _world.Step(1.0f / 60.0f);

        _world.SensorEventCount.Should().Be(1);
        _world.GetSensorEvent(0).Type.Should().Be(SensorEventType.Enter);

        _world.Step(1.0f / 60.0f);
        bool stayed = false;
        for (int j = 0; j < _world.SensorEventCount; j++)
        {
            if (_world.GetSensorEvent(j).Type == SensorEventType.Stay)
            {
                stayed = true;
            }
        }

        stayed.Should().BeTrue();

        bool exited = false;
        for (int i = 0; i < 120 && !exited; i++)
        {
            _world.Step(1.0f / 60.0f);
            for (int j = 0; j < _world.SensorEventCount; j++)
            {
                if (_world.GetSensorEvent(j).Type == SensorEventType.Exit)
                {
                    exited = true;
                }
            }
        }

        exited.Should().BeTrue();
    }

    [Fact]
    public void SolidOverlap_Raises_No_Sensor_Event()
    {
        BodyId ground = _world.Bodies.Create(new BodyDef { Type = BodyType.Static });
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, ground, Vec3.Zero, 1.0f);

        BodyId ball = _world.Bodies.Create(new BodyDef { Position = new Vec3(0.0f, 1.2f, 0.0f) });
        _world.Shapes.AttachSphere(_world.Bodies, _world.Broadphase, ball, Vec3.Zero, 0.5f);

        _world.Step(1.0f / 60.0f);

        _world.ContactEventCount.Should().BeGreaterThan(0);
        _world.SensorEventCount.Should().Be(0);
    }
}
