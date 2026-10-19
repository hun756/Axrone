namespace Axrone.Audio.Ecs.Tests;

using System.Numerics;

public sealed class AudioScriptMetadataTests
{
    [Fact]
    public void Audio_Components_Carry_Web_Script_Contract()
    {
        ScriptMetadata source = ScriptRegistry.Register<AudioSourceRef>();
        source.ScriptName.Should().Be("AudioSource");
        source.Priority.Should().Be(810);
        source.ExecuteInEditMode.Should().BeTrue();

        ScriptMetadata listener = ScriptRegistry.Register<AudioListenerRef>();
        listener.ScriptName.Should().Be("AudioListener");
        listener.Priority.Should().Be(800);
        listener.ExecuteInEditMode.Should().BeTrue();
    }

    [Fact]
    public void Audio_Components_Expose_Inspector_Properties()
    {
        ScriptRegistry.Register<AudioSourceRef>();
        ScriptRegistry.Register<AudioListenerRef>();

        IReadOnlyList<PropertyMetadata> sourceProps = ScriptRegistry.GetProperties(typeof(AudioSourceRef));
        sourceProps.Should().Contain(p => p.PropertyKey == nameof(AudioSourceRef.Volume));
        sourceProps.Should().Contain(p => p.PropertyKey == nameof(AudioSourceRef.UseTransform));

        IReadOnlyList<PropertyMetadata> listenerProps = ScriptRegistry.GetProperties(typeof(AudioListenerRef));
        listenerProps.Should().Contain(p => p.PropertyKey == nameof(AudioListenerRef.Active));
        listenerProps.Should().Contain(p => p.PropertyKey == nameof(AudioListenerRef.UseTransform));
    }

    [Fact]
    public void UseTransform_Defaults_True_Like_Web()
    {
        var source = new AudioSourceRef(new AudioSourceId(1U));
        source.UseTransform.Should().BeTrue();

        var listener = new AudioListenerRef(new AudioListenerId(3U), active: true);
        listener.UseTransform.Should().BeTrue();
    }

    [Fact]
    public void Listener_Defaults_Match_Web()
    {
        var listener = new AudioListenerRef(new AudioListenerId(3U), active: true);
        listener.GlobalVolume.Should().Be(1.0f);
        listener.DopplerFactor.Should().Be(1.0f);
        listener.SpeakerMode.Should().Be(AudioSpeakerMode.Stereo);
        listener.SampleRate.Should().Be(48000);
        listener.DspBufferSize.Should().Be(1024);
        listener.RealVoices.Should().Be(32);
        listener.VirtualVoices.Should().Be(128);
        listener.VirtualVoiceBehavior.Should().Be(AudioVirtualVoiceBehavior.PlaySilent);
        listener.HrtfPlugin.Should().Be(AudioHrtfPlugin.None);
        listener.OcclusionMode.Should().Be(AudioOcclusionMode.RaycastDiffraction);
        listener.ReverbPreset.Should().Be(AudioReverbPreset.Off);
        listener.ReverbLevel.Should().Be(0.0f);
        listener.AmbientVolume.Should().Be(0.5f);
        listener.FollowCamera.Should().BeTrue();
        listener.Forward.Should().Be(-Vector3.UnitZ);
    }

    [Fact]
    public void Listener_Tuning_Propagates_To_Registry()
    {
        using var system = new AudioSystem();
        using var world = new World();
        ComponentRegistry.Register<AudioListenerRef>();
        using var binder = new AudioEcsBinder(system, world);

        Entity entity = world.BeginEntity().Commit();
        var link = new AudioListenerRef(new AudioListenerId(4U), active: true)
        {
            GlobalVolume = 0.5f,
            DopplerFactor = 2.0f,
            SpeakerMode = AudioSpeakerMode.Mono,
        };
        world.AddComponent(entity, link);
        binder.AttachListener(entity);
        binder.Update().Should().BeEmpty();

        AudioListenerDescriptor descriptor = system.Listeners.GetActiveDescriptor();
        descriptor.GlobalVolume.Should().Be(0.5f);
        descriptor.DopplerFactor.Should().Be(2.0f);
        descriptor.SpeakerMode.Should().Be(AudioSpeakerMode.Mono);
    }
}
