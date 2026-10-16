namespace Axrone.Audio.Tests;

public sealed class AudioBusRegistryTests
{
    private readonly AudioObservabilityRuntime _observability =
        new(new CacheAlignedDiagnosticsCounters());

    [Fact]
    public void Defaults_Form_Hierarchy_WithUnityGains()
    {
        var registry = new AudioBusRegistry(_observability);
        registry.GetEffectiveGain(AudioBusId.Master).Should().BeApproximately(1.0f, 1e-5f);
        registry.GetEffectiveGain(AudioBusId.Music).Should().BeApproximately(1.0f, 1e-5f);
        registry.GetEffectiveGain(AudioBusId.Ui).Should().BeApproximately(1.0f, 1e-5f);
        registry.GetEffectiveGain(new AudioBusId(100U)).Should().Be(0.0f);
    }

    [Fact]
    public void Volume_Accumulates_DownTheTree()
    {
        var registry = new AudioBusRegistry(_observability);
        registry.UpdateBus(AudioBusId.Music, new AudioBusPatch(Volume: 0.5f));
        registry.GetEffectiveGain(AudioBusId.Ambient).Should().BeApproximately(0.5f, 1e-5f);
        registry.GetEffectiveGain(AudioBusId.Sfx).Should().BeApproximately(1.0f, 1e-5f);
    }

    [Fact]
    public void Mute_Zeroes_Subtree()
    {
        var registry = new AudioBusRegistry(_observability);
        registry.UpdateBus(AudioBusId.Master, new AudioBusPatch(Mute: true));
        registry.GetEffectiveGain(AudioBusId.Ui).Should().Be(0.0f);
        registry.UpdateBus(AudioBusId.Master, new AudioBusPatch(Mute: false));
        registry.GetEffectiveGain(AudioBusId.Ui).Should().BeApproximately(1.0f, 1e-5f);
    }

    [Fact]
    public void Update_UnknownBus_Throws()
    {
        var registry = new AudioBusRegistry(_observability);
        Action update = () => registry.UpdateBus(new AudioBusId(100U), new AudioBusPatch(Volume: 0.5f));
        update.Should().Throw<AudioBusException>();
    }

    [Fact]
    public void Enumeration_Covers_ActiveBuses()
    {
        var registry = new AudioBusRegistry(_observability);
        int count = 0;
        foreach (AudioBusState state in registry)
        {
            count++;
            state.EffectiveGain.Should().BeGreaterThanOrEqualTo(0.0f);
        }

        count.Should().Be(8);
    }
}
