namespace Axrone.Ecs.Tests;

internal struct EcsCompiledComponent
{
    public float Value;

    public EcsCompiledComponent(float value) => Value = value;
}

internal struct EcsCompiledSecondary
{
    public int Count;

    public EcsCompiledSecondary(int count) => Count = count;
}

internal struct EcsCompiledCycleA
{
}

internal struct EcsCompiledCycleB
{
}

[Collection("ScriptRegistry")]
public sealed class EcsCompiledScriptTests : IDisposable
{
    public EcsCompiledScriptTests() => ScriptRegistry.Clear();

    public void Dispose()
    {
        ScriptRegistry.Clear();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void CompiledRegistrationNeedsNoAttributes()
    {
        ScriptMetadata metadata = Metadata("Compiled", ["compiled"]);
        PropertyMetadata[] properties =
        [
            new PropertyMetadata(
                "Value",
                "Value",
                "Compiled property",
                PropertyTypes.Number,
                null,
                null,
                true,
                true,
                0.0,
                10.0,
                0.5),
        ];

        ScriptMetadata registered = ScriptRegistry.RegisterCompiled<EcsCompiledComponent>(metadata, properties);

        registered.Should().Be(metadata);
        ScriptRegistry.TryGetMetadata(typeof(EcsCompiledComponent), out ScriptMetadata? found).Should().BeTrue();
        found.Should().Be(metadata);
        ScriptRegistry.TryGetByName("Compiled", out var registration).Should().BeTrue();
        registration!.ComponentType.Should().Be<EcsCompiledComponent>();
        ScriptRegistry.GetProperties(typeof(EcsCompiledComponent)).Should().ContainSingle()
            .Which.PropertyKey.Should().Be("Value");
        ScriptRegistry.GetAll(tag: "compiled").Should().ContainSingle();
        ScriptRegistry.TryGetByName("Missing", out _).Should().BeFalse();
    }

    [Fact]
    public void ScriptDescriptorRegistersPropertiesWholesale()
    {
        ScriptMetadata metadata = Metadata("Descriptor", []);
        PropertyMetadata[] properties =
        [
            new PropertyMetadata("Count", "Count", null, PropertyTypes.Number, null, 3, true, true, 0.0, 100.0, 1.0),
        ];
        var descriptor = new ScriptDescriptor(typeof(EcsCompiledSecondary), metadata, properties);

        ScriptRegistry.RegisterCompiled(descriptor);

        ScriptRegistry.TryGetMetadata(typeof(EcsCompiledSecondary), out ScriptMetadata? found).Should().BeTrue();
        found!.ScriptName.Should().Be("Descriptor");
        ScriptRegistry.TryGetProperty(typeof(EcsCompiledSecondary), "Count", out PropertyMetadata? property).Should().BeTrue();
        property!.Max.Should().Be(100.0);
    }

    [Fact]
    public void EmptyScriptNameIsRejected()
    {
        ScriptMetadata metadata = Metadata(" ", []);
        Action register = () => ScriptRegistry.RegisterCompiled<EcsCompiledComponent>(metadata, []);
        register.Should().Throw<EcsConfigurationException>().WithMessage("*Script name is required*");
    }

    [Fact]
    public void CompiledCircularDependencyIsRejected()
    {
        ScriptMetadata dependency = Metadata(
            "CompiledB",
            [],
            [typeof(EcsCompiledCycleA)]);
        ScriptRegistry.RegisterCompiled<EcsCompiledCycleB>(dependency, []);

        ScriptMetadata cyclic = Metadata(
            "CompiledA",
            [],
            [typeof(EcsCompiledCycleB)]);
        Action register = () => ScriptRegistry.RegisterCompiled<EcsCompiledCycleA>(cyclic, []);
        register.Should().Throw<EcsConfigurationException>().WithMessage("*Circular*");
    }

    [Fact]
    public void DuplicateNamesKeepTheLatestRegistration()
    {
        ScriptRegistry.RegisterCompiled<EcsCompiledComponent>(Metadata("Shared", []), []);
        ScriptRegistry.RegisterCompiled<EcsCompiledSecondary>(Metadata("Shared", []), []);

        ScriptRegistry.TryGetByName("Shared", out var registration).Should().BeTrue();
        registration!.ComponentType.Should().Be<EcsCompiledSecondary>();
        ScriptRegistry.ValidateAll().IsValid.Should().BeTrue();
    }

    [Fact]
    public void DescriptorRejectsReferenceTypes()
    {
        var descriptor = new ScriptDescriptor(typeof(string), Metadata("Reference", []), []);
        Action register = () => ScriptRegistry.RegisterCompiled(descriptor);
        register.Should().Throw<EcsConfigurationException>().WithMessage("*unmanaged component*");
    }

    private static ScriptMetadata Metadata(
        string name,
        string[] tags,
        Type[]? dependencies = null) =>
        new(
            name,
            7,
            dependencies ?? [],
            true,
            false,
            true,
            false,
            "1.2.3",
            "tester",
            "compiled descriptor",
            tags,
            false,
            false,
            null);
}
