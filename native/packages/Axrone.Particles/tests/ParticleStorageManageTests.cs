namespace Axrone.Particles.Tests;

public sealed class ParticleStorageManageTests : IDisposable
{
    private readonly NativeSoaParticleStorage _storage = new((ParticleCapacity)32);

    public void Dispose() => _storage.Dispose();

    [Fact]
    public void Resize_Grow_PreservesParticles()
    {
        _storage.TryAllocate(Vec3.Zero, Vec3.Zero, 5.0f, Vec3.One, Vec4.One, out ParticleId id).Should().BeTrue();
        _storage.TryResize(128).Should().BeTrue();
        _storage.Capacity.Should().Be(128);
        _storage.ActiveCount.Should().Be(1);
        _storage.Contains(id).Should().BeTrue();
    }

    [Fact]
    public void Resize_ShrinkBelowActive_Fails()
    {
        _storage.TryAllocate(Vec3.Zero, Vec3.Zero, 5.0f, Vec3.One, Vec4.One, out _).Should().BeTrue();
        _storage.TryResize(64).Should().BeTrue();
        for (int i = 0; i < 39; i++)
        {
            _storage.TryAllocate(Vec3.One, Vec3.Zero, 5.0f, Vec3.One, Vec4.One, out _).Should().BeTrue();
        }

        _storage.TryResize(32).Should().BeFalse();
        _storage.Capacity.Should().Be(64);
        _storage.TryResize(64).Should().BeTrue();
        _storage.ActiveCount.Should().Be(40);
    }

    [Fact]
    public unsafe void SortByAge_OrdersAscending()
    {
        _storage.TryAllocate(Vec3.Zero, Vec3.Zero, 10.0f, Vec3.One, Vec4.One, out _).Should().BeTrue();
        _storage.TryAllocate(Vec3.Zero, Vec3.Zero, 1.0f, Vec3.One, Vec4.One, out _).Should().BeTrue();
        _storage.TryAllocate(Vec3.Zero, Vec3.Zero, 5.0f, Vec3.One, Vec4.One, out _).Should().BeTrue();

        _storage.Age[0] = 9.0f;
        _storage.Age[1] = 0.5f;
        _storage.Age[2] = 4.0f;
        _storage.SortByAge(ascending: true);

        _storage.Age[0].Should().BeApproximately(0.5f, 1e-6f);
        _storage.Age[1].Should().BeApproximately(4.0f, 1e-6f);
        _storage.Age[2].Should().BeApproximately(9.0f, 1e-6f);
    }

    [Fact]
    public void Compact_PreservesCount()
    {
        _storage.TryAllocate(Vec3.Zero, Vec3.Zero, 1.0f, Vec3.One, Vec4.One, out _).Should().BeTrue();
        _storage.Compact().Should().Be(1);
    }
}
