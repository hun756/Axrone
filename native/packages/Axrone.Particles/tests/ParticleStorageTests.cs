namespace Axrone.Particles.Tests;

public sealed class ParticleStorageTests : IDisposable
{
    private readonly NativeSoaParticleStorage _storage = new((ParticleCapacity)64);

    public void Dispose() => _storage.Dispose();

    [Fact]
    public void AllocateRemove_MaintainsGenerationalHandles()
    {
        _storage.TryAllocate(Vec3.Zero, Vec3.UnitY, 5.0f, Vec3.One, Vec4.One, out ParticleId a).Should().BeTrue();
        _storage.TryAllocate(Vec3.One, Vec3.Zero, 5.0f, Vec3.One, Vec4.One, out ParticleId b).Should().BeTrue();
        a.Should().NotBe(b);
        _storage.ActiveCount.Should().Be(2);

        _storage.RemoveAtDense(0).Should().BeTrue();
        _storage.ActiveCount.Should().Be(1);
        _storage.Contains(a).Should().BeFalse();
        _storage.Contains(b).Should().BeTrue();
        _storage.TryGetDenseIndex(b, out int dense).Should().BeTrue();
        dense.Should().Be(0);
    }

    [Fact]
    public void Clear_ResetsActiveCount()
    {
        _storage.TryAllocate(Vec3.Zero, Vec3.Zero, 1.0f, Vec3.One, Vec4.One, out _).Should().BeTrue();
        _storage.Clear();
        _storage.ActiveCount.Should().Be(0);
    }

    [Fact]
    public void Enumerator_VisitsAllParticles()
    {
        _storage.TryAllocate(new Vec3(1, 0, 0), Vec3.Zero, 1.0f, Vec3.One, Vec4.One, out _).Should().BeTrue();
        _storage.TryAllocate(new Vec3(2, 0, 0), Vec3.Zero, 1.0f, Vec3.One, Vec4.One, out _).Should().BeTrue();

        int seen = 0;
        foreach (ParticleReference reference in _storage)
        {
            reference.Position.X.Should().BeGreaterThan(0.0f);
            seen++;
        }

        seen.Should().Be(2);
    }
}
