namespace Axrone.Ecs.Tests;

[Script(ScriptName = "Health", Priority = 5, Tags = new[] { "gameplay" })]
internal struct EcsHealthScript
{
    public int Value;

    public EcsHealthScript(int value) => Value = value;
}

[Script(ScriptName = "Shared")]
internal struct EcsSharedNameA
{
}

[Script(ScriptName = "Shared")]
internal struct EcsSharedNameB
{
}

public sealed class EcsScriptRegistryTests : IDisposable
{
    public EcsScriptRegistryTests() => ScriptRegistry.Clear();

    public void Dispose()
    {
        ScriptRegistry.Clear();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Register_Resolves_Metadata()
    {
        ScriptMetadata metadata = ScriptRegistry.Register<EcsHealthScript>();

        metadata.ScriptName.Should().Be("Health");
        metadata.Priority.Should().Be(5);
        metadata.Tags.Should().ContainSingle().Which.Should().Be("gameplay");

        ScriptRegistry.TryGetMetadata(typeof(EcsHealthScript), out ScriptMetadata? found).Should().BeTrue();
        found.Should().Be(metadata);

        ScriptRegistry.TryGetByName("Health", out var registration).Should().BeTrue();
        registration!.ComponentType.Should().Be<EcsHealthScript>();
    }

    [Fact]
    public void Register_WithoutAttribute_Uses_TypeName()
    {
        ScriptMetadata metadata = ScriptRegistry.Register<EcsTestPosition>();
        metadata.ScriptName.Should().Be(nameof(EcsTestPosition));
        metadata.Priority.Should().Be(0);
        metadata.TrackInstances.Should().BeTrue();
    }

    [Fact]
    public void Duplicate_Name_Keeps_Latest_AndValidates()
    {
        ScriptRegistry.Register<EcsSharedNameA>();
        ScriptRegistry.Register<EcsSharedNameB>();

        ScriptRegistry.TryGetByName("Shared", out var registration).Should().BeTrue();
        registration!.ComponentType.Should().Be<EcsSharedNameB>();

        ScriptRegistry.ValidateAll().IsValid.Should().BeTrue();
    }

    [Fact]
    public void Circular_Dependency_Throws()
    {
        ScriptRegistry.Register<EcsCircularB>();
        Action register = () => ScriptRegistry.Register<EcsCircularA>();
        register.Should().Throw<EcsConfigurationException>().WithMessage("*Circular*");
    }

    [Fact]
    public void DependencyTree_Lists_Transitive_Deps()
    {
        ScriptRegistry.Register<EcsScriptedAudio>();
        ScriptRegistry.Register<EcsDependentScript>();

        ScriptRegistry.GetDependencyTree(typeof(EcsDependentScript))
            .Should().ContainSingle().Which.Should().Be<EcsScriptedAudio>();
        ScriptRegistry.GetDependencyTree(typeof(EcsScriptedAudio)).Should().BeEmpty();
    }

    [Fact]
    public void GetAll_Filters_ByTag()
    {
        ScriptRegistry.Register<EcsHealthScript>();
        ScriptRegistry.Register<EcsTestPosition>();

        ScriptRegistry.GetAll(tag: "gameplay").Should().ContainSingle();
        ScriptRegistry.GetAll().Should().HaveCount(2);
    }

    [Script(Dependencies = new[] { typeof(EcsCircularB) })]
    internal struct EcsCircularA
    {
    }

    [Script(Dependencies = new[] { typeof(EcsCircularA) })]
    internal struct EcsCircularB
    {
    }
}
