namespace Axrone.Audio.Ecs.Tests;

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
}
