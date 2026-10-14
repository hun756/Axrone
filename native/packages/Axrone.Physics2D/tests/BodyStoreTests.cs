namespace Axrone.Physics2D.Tests;

using Axrone.Numeric;
using FluentAssertions;
using Xunit;

public class BodyStoreTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void Create_Destroy_RecyclesIdentity()
    {
        var store = new BodyStore();

        BodyId first = store.Create(new BodyDef { Position = new Vec2(1, 2) });
        first.IsValid.Should().BeTrue();
        store.IsValid(first).Should().BeTrue();
        store.Count.Should().Be(1);
        store.GetTransform(first).Position.Should().Be(new Vec2(1, 2));

        store.Destroy(first);
        store.IsValid(first).Should().BeFalse();
        store.Count.Should().Be(0);

        Action read = () => store.GetTransform(first);
        read.Should().Throw<InvalidOperationException>();

        BodyId second = store.Create(new BodyDef());
        store.IsValid(second).Should().BeTrue();
    }

    [Fact]
    public void Create_BadDamping_Throws()
    {
        var store = new BodyStore();

        Action negative = () => store.Create(new BodyDef { LinearDamping = -1f });
        Action nan = () => store.Create(new BodyDef { AngularDamping = float.NaN });

        negative.Should().Throw<InvalidOperationException>();
        nan.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Transform_RoundTrips()
    {
        var transform = new Transform2D(new Vec2(3, 4), MathF.PI / 2f);

        transform.Angle.Should().BeApproximately(MathF.PI / 2f, Tolerance);
        transform.Rotate(new Vec2(1, 0)).X.Should().BeApproximately(0f, Tolerance);
        transform.Rotate(new Vec2(1, 0)).Y.Should().BeApproximately(1f, Tolerance);
        transform.TransformPoint(new Vec2(1, 0)).Should().Be(new Vec2(3, 5));
        transform.InverseTransformPoint(new Vec2(3, 5)).X.Should().BeApproximately(1f, Tolerance);
        transform.InverseTransformPoint(new Vec2(3, 5)).Y.Should().BeApproximately(0f, Tolerance);
        transform.Should().Be(new Transform2D(new Vec2(3, 4), MathF.PI / 2f));
    }

    [Fact]
    public void Integrate_FallsUnderGravity()
    {
        var store = new BodyStore();
        BodyId id = store.Create(new BodyDef { Position = new Vec2(0, 10) });
        store.SetMassData(id, 2f, 1f);

        store.Integrate(1f, new Vec2(0, -10));

        store.GetLinearVelocity(id).Should().Be(new Vec2(0, -10));
        store.GetTransform(id).Position.Y.Should().BeApproximately(0f, Tolerance);
    }

    [Fact]
    public void Integrate_StaticNeverMoves()
    {
        var store = new BodyStore();
        BodyId id = store.Create(new BodyDef { Type = BodyType.Static, Position = new Vec2(5, 5) });

        store.SetLinearVelocity(id, new Vec2(99, 99));
        store.ApplyForce(id, new Vec2(1000, 1000));
        store.Integrate(1f, new Vec2(0, -10));

        store.GetLinearVelocity(id).Should().Be(Vec2.Zero);
        store.GetTransform(id).Position.Should().Be(new Vec2(5, 5));
    }

    [Fact]
    public void Integrate_KinematicIgnoresForces()
    {
        var store = new BodyStore();
        BodyId id = store.Create(new BodyDef
        {
            Type = BodyType.Kinematic,
            Position = new Vec2(0, 0),
            LinearVelocity = new Vec2(2, 0),
        });

        store.ApplyForce(id, new Vec2(1000, 0));
        store.Integrate(1f, new Vec2(0, -10));

        store.GetTransform(id).Position.Should().Be(new Vec2(2, 0));
    }

    [Fact]
    public void Integrate_DampingBleedsVelocity()
    {
        var store = new BodyStore();
        BodyId id = store.Create(new BodyDef
        {
            LinearVelocity = new Vec2(10, 0),
            LinearDamping = 1f,
        });
        store.SetMassData(id, 1f, 1f);

        store.Integrate(1f, Vec2.Zero);

        store.GetLinearVelocity(id).X.Should().BeApproximately(5f, Tolerance);
    }

    [Fact]
    public void Integrate_FixedRotationPinsAngle()
    {
        var store = new BodyStore();
        BodyId id = store.Create(new BodyDef { FixedRotation = true, AngularVelocity = 3f });
        store.SetMassData(id, 1f, 1f);

        store.SetAngularVelocity(id, 7f);
        store.ApplyAngularImpulse(id, 5f);
        store.ApplyTorque(id, 5f);
        store.Integrate(1f, Vec2.Zero);

        store.GetAngularVelocity(id).Should().Be(0f);
        store.GetTransform(id).Angle.Should().BeApproximately(0f, Tolerance);
    }

    [Fact]
    public void Impulses_ChangeVelocity_ByInverseMass()
    {
        var store = new BodyStore();
        BodyId id = store.Create(new BodyDef());
        store.SetMassData(id, 4f, 2f);

        store.GetInverseMass(id).Should().BeApproximately(0.25f, Tolerance);
        store.GetInverseInertia(id).Should().BeApproximately(0.5f, Tolerance);

        store.ApplyLinearImpulse(id, new Vec2(2, 0));
        store.ApplyAngularImpulse(id, 1f);

        store.GetLinearVelocity(id).X.Should().BeApproximately(0.5f, Tolerance);
        store.GetAngularVelocity(id).Should().BeApproximately(0.5f, Tolerance);
    }

    [Fact]
    public void Forces_ClearAfterStep_And_Wake()
    {
        var store = new BodyStore();
        BodyId id = store.Create(new BodyDef());
        store.SetMassData(id, 1f, 1f);
        store.SetAwake(id, false);
        store.IsAwake(id).Should().BeFalse();

        store.ApplyForce(id, new Vec2(10, 0));
        store.IsAwake(id).Should().BeTrue();
        store.Integrate(1f, Vec2.Zero);

        store.GetLinearVelocity(id).X.Should().BeApproximately(10f, Tolerance);
        store.Integrate(1f, Vec2.Zero);
        store.GetLinearVelocity(id).X.Should().BeApproximately(10f, Tolerance);
    }

    [Fact]
    public void SetMassData_Negative_Throws()
    {
        var store = new BodyStore();
        BodyId id = store.Create(new BodyDef());

        Action mass = () => store.SetMassData(id, -1f, 1f);
        Action inertia = () => store.SetMassData(id, 1f, -1f);

        mass.Should().Throw<InvalidOperationException>();
        inertia.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Integrate_NonPositiveStep_Throws()
    {
        var store = new BodyStore();

        Action zero = () => store.Integrate(0f, Vec2.Zero);
        Action negative = () => store.Integrate(-1f, Vec2.Zero);

        zero.Should().Throw<InvalidOperationException>();
        negative.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Growth_BeyondInitialCapacity_KeepsBodies()
    {
        var store = new BodyStore();
        var ids = new BodyId[40];
        for (int i = 0; i < ids.Length; i++)
        {
            ids[i] = store.Create(new BodyDef { Position = new Vec2(i, 0) });
        }

        store.Count.Should().Be(40);
        for (int i = 0; i < ids.Length; i++)
        {
            store.GetTransform(ids[i]).Position.X.Should().BeApproximately(i, Tolerance);
        }
    }
}
