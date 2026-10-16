namespace Axrone.Ecs.Tests;

[Script(ScriptName = "AudioSource", Priority = 810, ExecuteInEditMode = true)]
internal struct EcsScriptedAudio
{
    public float Volume;

    public EcsScriptedAudio(float volume) => Volume = volume;
}

[Script(Dependencies = new[] { typeof(EcsScriptedAudio) })]
internal struct EcsDependentScript
{
}

public class EcsScriptAttributeTests
{
    [Fact]
    public void Attribute_Carries_Options()
    {
        ScriptAttribute? attribute = typeof(EcsScriptedAudio).GetCustomAttributes(typeof(ScriptAttribute), false)
            .Cast<ScriptAttribute>()
            .SingleOrDefault();

        attribute.Should().NotBeNull();
        attribute!.ScriptName.Should().Be("AudioSource");
        attribute.Priority.Should().Be(810);
        attribute.ExecuteInEditMode.Should().BeTrue();
        attribute.Singleton.Should().BeFalse();
        attribute.TrackInstances.Should().BeTrue();
    }

    [Fact]
    public void Attribute_Dependencies_AreTypes()
    {
        ScriptAttribute? attribute = typeof(EcsDependentScript).GetCustomAttributes(typeof(ScriptAttribute), false)
            .Cast<ScriptAttribute>()
            .SingleOrDefault();

        attribute.Should().NotBeNull();
        attribute!.Dependencies.Should().ContainSingle().Which.Should().Be<EcsScriptedAudio>();
        attribute.ScriptName.Should().BeNull();
    }

    [Fact]
    public void Missing_Attribute_Returns_Null()
    {
        typeof(EcsTestPosition).GetCustomAttributes(typeof(ScriptAttribute), false)
            .Should().BeEmpty();
    }
}
