namespace Axrone.Audio.Tests;

public sealed class AudioListenerRegistryTests
{
    private readonly AudioObservabilityRuntime _observability =
        new(new CacheAlignedDiagnosticsCounters());

    [Fact]
    public void Primary_IsActive_ByDefault()
    {
        var registry = new AudioListenerRegistry(_observability);
        registry.ActiveListenerId.Should().Be(AudioListenerId.Primary);
        registry.GetActiveDescriptor().GlobalVolume.Should().Be(1.0f);
    }

    [Fact]
    public void Upsert_AndActivate_Switches_Descriptor()
    {
        var registry = new AudioListenerRegistry(_observability);
        var id = new AudioListenerId(2U);
        registry.Upsert(id, new AudioListenerDescriptor(new Vector3(5.0f, 0.0f, 0.0f), -Vector3.UnitZ, Vector3.UnitY, 0.5f));
        registry.SetActive(id);

        registry.ActiveListenerId.Should().Be(id);
        registry.GetActiveDescriptor().Position.X.Should().Be(5.0f);
        registry.GetActiveDescriptor().GlobalVolume.Should().Be(0.5f);
    }

    [Fact]
    public void Activate_Unregistered_Throws()
    {
        var registry = new AudioListenerRegistry(_observability);
        Action activate = () => registry.SetActive(new AudioListenerId(9U));
        activate.Should().Throw<AudioListenerException>();
    }
}
