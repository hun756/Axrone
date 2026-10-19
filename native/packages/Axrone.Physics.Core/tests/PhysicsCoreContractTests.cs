namespace Axrone.Physics.Core.Tests;

public sealed class PhysicsCoreContractTests
{
    [Fact]
    public void SolverFlags_Match_Web_Contract()
    {
        ((byte)SolverFlags.None).Should().Be(0);
        ((byte)SolverFlags.WarmStarting).Should().Be(1 << 0);
        ((byte)SolverFlags.ContinuousDetection).Should().Be(1 << 1);
        ((byte)SolverFlags.SubStepping).Should().Be(1 << 2);
        ((byte)SolverFlags.SleepingBodies).Should().Be(1 << 3);
        ((byte)SolverFlags.PositionCorrection).Should().Be(1 << 4);
        ((byte)SolverFlags.VelocityConstraints).Should().Be(1 << 5);
        SolverFlags.Default.Should().Be(
            SolverFlags.WarmStarting | SolverFlags.SleepingBodies | SolverFlags.PositionCorrection | SolverFlags.VelocityConstraints);
    }

    [Fact]
    public void BodyFlags_Match_Web_Contract()
    {
        ((byte)BodyFlags.None).Should().Be(0);
        ((byte)BodyFlags.FixedRotation).Should().Be(1 << 0);
        ((byte)BodyFlags.Bullet).Should().Be(1 << 1);
        ((byte)BodyFlags.Sensor).Should().Be(1 << 2);
        ((byte)BodyFlags.Sleeping).Should().Be(1 << 3);
        ((byte)BodyFlags.AutoSleep).Should().Be(1 << 4);
        ((byte)BodyFlags.Awake).Should().Be(1 << 5);
        ((byte)BodyFlags.Active).Should().Be(1 << 6);
        ((byte)BodyFlags.Island).Should().Be(1 << 7);
    }

    [Fact]
    public void CollisionCategories_Are_Valid_Bitmasks()
    {
        ((uint)CollisionCategory.None).Should().Be(0);
        ((uint)CollisionCategory.Default).Should().Be(1);
        ((uint)CollisionCategory.Static).Should().Be(2);
        ((uint)CollisionCategory.Dynamic).Should().Be(4);
        ((uint)CollisionCategory.Kinematic).Should().Be(8);
        ((uint)CollisionCategory.Trigger).Should().Be(16);
        ((uint)CollisionCategory.All).Should().Be(0xFFFF);
        ((uint)(CollisionCategory.Static | CollisionCategory.Dynamic | CollisionCategory.Kinematic | CollisionCategory.Trigger)).Should().Be(30);
    }

    [Fact]
    public void Constants_Match_Web_Reference()
    {
        PhysicsConstants.DefaultGravity2D.X.Should().Be(0.0f);
        PhysicsConstants.DefaultGravity2D.Y.Should().BeApproximately(-9.81f, 1e-6f);
        PhysicsConstants.DefaultGravity3D.Z.Should().Be(0.0f);
        PhysicsConstants.MaxVelocity.Should().Be(200.0f);
        PhysicsConstants.MaxAngularVelocity.Should().Be(250.0f);
        PhysicsConstants.LinearSlop.Should().Be(0.005f);
        PhysicsConstants.AngularSlop.Should().BeApproximately((2.0f / 180.0f) * MathF.PI, 1e-9f);
        PhysicsConstants.BaumgarteFactor.Should().Be(0.2f);
        PhysicsConstants.ToiBaumgarte.Should().Be(0.75f);
        PhysicsConstants.Epsilon.Should().Be(1e-6f);
        PhysicsConstants.MaxSubSteps.Should().Be(8);
        PhysicsConstants.MaxToiContacts.Should().Be(32);
        PhysicsConstants.VelocityThreshold.Should().Be(1.0f);
        PhysicsConstants.MaxLinearCorrection.Should().Be(0.2f);
        PhysicsConstants.MaxAngularCorrection.Should().BeApproximately((8.0f / 180.0f) * MathF.PI, 1e-9f);
        PhysicsConstants.SleepTime.Should().Be(0.5f);
        PhysicsConstants.LinearSleepTolerance.Should().Be(0.01f);
        PhysicsConstants.AngularSleepTolerance.Should().BeApproximately((2.0f / 180.0f) * MathF.PI, 1e-9f);
    }
}
