namespace Axrone.Shapes2D.Tests;

public sealed class ShapeRegistryTests : IDisposable
{
    private readonly ShapeRegistry _registry = new();

    public void Dispose() => _registry.Dispose();

    [Fact]
    public void Register_Interns_And_Unregisters()
    {
        string first = _registry.Register(ShapeFactory.CreateCircle(0f, 0f, 1f));
        string second = _registry.Register(ShapeFactory.CreateCircle(0f, 0f, 1f));
        first.Should().Be(second);
        first.Should().StartWith("shape_");

        _registry.Has(first).Should().BeTrue();
        _registry.Get(first).Should().BeOfType<CircleShape>();
        _registry.Get("shape_424242").Should().BeNull();
        _registry.Stats.ShapeCount.Should().Be(1);
        _registry.Stats.FingerprintCount.Should().Be(1);

        _registry.Unregister(first).Should().BeTrue();
        _registry.Unregister(first).Should().BeFalse();
        _registry.Has(first).Should().BeFalse();
    }

    [Fact]
    public void Capacity_Is_Enforced()
    {
        using var small = new ShapeRegistry(new ShapeRegistryOptions(MaxShapes: 1, MaxCompiledEntries: 1));
        small.Register(ShapeFactory.CreateCircle(0f, 0f, 1f));
        Action overflow = () => small.Register(ShapeFactory.CreateCircle(5f, 5f, 1f));
        overflow.Should().Throw<ShapeRegistryException>()
            .Where(e => e.ErrorCode == Shapes2DErrorCode.CapacityExceeded);
    }

    [Fact]
    public void Compile_Caches_And_Evicts_Lru()
    {
        using var registry = new ShapeRegistry(new ShapeRegistryOptions(MaxShapes: 8, MaxCompiledEntries: 2));
        string a = registry.Register(ShapeFactory.CreateCircle(0f, 0f, 1f));
        string b = registry.Register(ShapeFactory.CreateCircle(5f, 5f, 2f));
        string c = registry.Register(ShapeFactory.CreateCircle(9f, 9f, 3f));

        CompiledShape2D first = registry.Compile(a, new ShapeCompileOptions(IncludeStrokeMesh: false));
        registry.Compile(a, new ShapeCompileOptions(IncludeStrokeMesh: false)).Should().BeSameAs(first);
        registry.Stats.CompiledCount.Should().Be(1);

        registry.Compile(b, new ShapeCompileOptions(IncludeStrokeMesh: false));
        registry.Compile(c, new ShapeCompileOptions(IncludeStrokeMesh: false));
        registry.Stats.CompiledCount.Should().Be(2);

        CompiledShape2D rebuilt = registry.Compile(a, new ShapeCompileOptions(IncludeStrokeMesh: false));
        rebuilt.Should().NotBeSameAs(first);
        rebuilt.Area.Should().BeApproximately(first.Area, 1e-6f);
    }

    [Fact]
    public void Serialize_RoundTrips_Registered_Shape()
    {
        string id = _registry.Register(ShapeFactory.CreateRectangle(0f, 0f, 4f, 2f));
        string json = _registry.Serialize(id);
        Shape2D restored = ShapeSerializer.DeserializeShape(json);
        restored.Should().BeOfType<RectangleShape>();

        Action missing = () => _registry.Serialize("shape_424242");
        missing.Should().Throw<ShapeRegistryException>()
            .Where(e => e.ErrorCode == Shapes2DErrorCode.ShapeNotFound);
        Action missingCompile = () => _registry.Compile("shape_424242");
        missingCompile.Should().Throw<ShapeRegistryException>();
    }

    [Fact]
    public void Disposed_Registry_Throws()
    {
        var registry = new ShapeRegistry();
        registry.Dispose();
        registry.Stats.Disposed.Should().BeTrue();
        Action get = () => registry.Get("shape_1");
        get.Should().Throw<ShapeRegistryException>()
            .Where(e => e.ErrorCode == Shapes2DErrorCode.RegistryDisposed);
        Action register = () => registry.Register(ShapeFactory.CreateCircle(0f, 0f, 1f));
        register.Should().Throw<ShapeRegistryException>();
        registry.Dispose();
    }

    [Fact]
    public void Clear_Empties_Registry()
    {
        _registry.Register(ShapeFactory.CreateCircle(0f, 0f, 1f));
        _registry.Clear();
        _registry.Stats.ShapeCount.Should().Be(0);
        _registry.Stats.CompiledCount.Should().Be(0);
    }
}
