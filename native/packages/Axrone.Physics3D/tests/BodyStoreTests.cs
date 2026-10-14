using Axrone.Numeric;
using Axrone.Physics3D;
using FluentAssertions;
using Xunit;

namespace Axrone.Physics3D.Tests;

public class BodyStoreTests
{
    [Fact]
    public void Create_Destroy_RecyclesSlots()
    {
        var store = new BodyStore();

        BodyId first = store.Create(new BodyDef());
        store.Create(new BodyDef()).IsValid.Should().BeTrue();
        store.Count.Should().Be(2);

        store.Destroy(first);
        store.IsValid(first).Should().BeFalse();
        store.Count.Should().Be(1);

        BodyId recycled = store.Create(new BodyDef());
        recycled.ToIndex().Should().Be(first.ToIndex());
    }

    [Fact]
    public void StaticBody_Ignores_VelocitiesAndForces()
    {
        var store = new BodyStore();
        BodyId id = store.Create(new BodyDef { Type = BodyType.Static });

        store.SetLinearVelocity(id, new Vec3(5.0f, 0.0f, 0.0f));
        store.ApplyForce(id, new Vec3(0.0f, -10.0f, 0.0f));
        store.Integrate(1.0f / 60.0f, new Vec3(0.0f, -9.81f, 0.0f));

        store.GetLinearVelocity(id).Should().Be(Vec3.Zero);
        store.GetTransform(id).Position.Should().Be(Vec3.Zero);
    }

    [Fact]
    public void Gravity_Integrates_VelocityLinearly()
    {
        var store = new BodyStore();
        BodyId id = store.Create(new BodyDef());
        var gravity = new Vec3(0.0f, -9.81f, 0.0f);

        store.Integrate(1.0f, gravity);

        Vec3 velocity = store.GetLinearVelocity(id);
        velocity.X.Should().BeApproximately(0.0f, 1e-5f);
        velocity.Y.Should().BeApproximately(-9.81f, 1e-4f);
        velocity.Z.Should().BeApproximately(0.0f, 1e-5f);
    }

    [Fact]
    public void AngularVelocity_Spins_AboutAxis()
    {
        var store = new BodyStore();
        BodyId id = store.Create(new BodyDef
        {
            AngularVelocity = new Vec3(0.0f, 0.0f, MathF.PI / 2.0f),
            GravityScale = 0.0f,
        });

        store.Integrate(1.0f, Vec3.Zero);

        Quat expected = Quat.CreateFromAxisAngle(new Vec3(0.0f, 0.0f, 1.0f), MathF.PI / 2.0f);
        Quat actual = store.GetTransform(id).Rotation;
        (actual * expected.Conjugate()).W.Should().BeApproximately(1.0f, 1e-4f);
    }

    [Fact]
    public void LinearImpulse_Changes_VelocityByInverseMass()
    {
        var store = new BodyStore();
        BodyId id = store.Create(new BodyDef());
        store.SetMassData(id, 2.0f, new Vec3(1.0f, 1.0f, 1.0f));

        store.ApplyLinearImpulse(id, new Vec3(4.0f, 0.0f, 0.0f));

        store.GetLinearVelocity(id).X.Should().BeApproximately(2.0f, 1e-6f);
    }

    [Fact]
    public void SetAwake_False_ZeroesMotion()
    {
        var store = new BodyStore();
        BodyId id = store.Create(new BodyDef());
        store.SetLinearVelocity(id, new Vec3(3.0f, 0.0f, 0.0f));

        store.SetAwake(id, false);

        store.IsAwake(id).Should().BeFalse();
        store.GetLinearVelocity(id).Should().Be(Vec3.Zero);
    }

    [Fact]
    public void DeadIdentity_Throws_OnAccess()
    {
        var store = new BodyStore();

        var act = () => store.GetLinearVelocity(BodyId.FromRaw(999u));

        act.Should().Throw<InvalidOperationException>();
    }
}
