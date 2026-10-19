namespace Axrone.Audio.Tests;

public sealed class AudioDescriptorContractTests
{
    [Fact]
    public void Defaults_Pass_Validation()
    {
        AudioListenerDescriptor listener = AudioListenerDescriptor.Default;
        Action listenerAct = () => AudioListenerDescriptor.Validate(in listener);
        listenerAct.Should().NotThrow();

        AudioSourceDefinition source = AudioSourceDefinition.Default;
        Action sourceAct = () => AudioSourceDefinition.Validate(in source);
        sourceAct.Should().NotThrow();

        AudioSpatialization spatial = AudioSpatialization.Default3D;
        Action spatialAct = () => AudioSpatialization.Validate(in spatial);
        spatialAct.Should().NotThrow();
    }

    [Fact]
    public void Listener_Rejects_Out_Of_Range_Tuning()
    {
        AudioListenerDescriptor badVolume = AudioListenerDescriptor.Default with { GlobalVolume = 2.0f };
        Action volumeAct = () => AudioListenerDescriptor.Validate(in badVolume);
        volumeAct.Should().Throw<AudioConfigurationException>().Where(e => e.Code == "audio.invalid-listener");

        AudioListenerDescriptor badRate = AudioListenerDescriptor.Default with { SampleRate = 12345 };
        Action rateAct = () => AudioListenerDescriptor.Validate(in badRate);
        rateAct.Should().Throw<AudioConfigurationException>();

        AudioListenerDescriptor badBuffer = AudioListenerDescriptor.Default with { DspBufferSize = 1000 };
        Action bufferAct = () => AudioListenerDescriptor.Validate(in badBuffer);
        bufferAct.Should().Throw<AudioConfigurationException>();
    }

    [Fact]
    public void Source_Rejects_Out_Of_Range_Tuning()
    {
        AudioSourceDefinition badPan = new AudioSourceDefinition() with { Pan = 2.0f };
        Action panAct = () => AudioSourceDefinition.Validate(in badPan);
        panAct.Should().Throw<AudioConfigurationException>().Where(e => e.Code == "audio.invalid-source");

        AudioSourceDefinition badRate = new AudioSourceDefinition() with { PlaybackRate = 0.0f };
        Action rateAct = () => AudioSourceDefinition.Validate(in badRate);
        rateAct.Should().Throw<AudioConfigurationException>();

        AudioSourceDefinition badCone = new AudioSourceDefinition() with { Spatial = AudioSpatialization.Default3D with { ConeInnerAngle = 400.0f } };
        Action coneAct = () => AudioSourceDefinition.Validate(in badCone);
        coneAct.Should().Throw<AudioConfigurationException>();
    }

    [Fact]
    public void Describe_RoundTrips_ToString()
    {
        AudioListenerDescriptor listener = AudioListenerDescriptor.Default;
        AudioListenerDescriptor.Describe(in listener).Should().Be(listener.ToString());

        AudioSourceDefinition source = AudioSourceDefinition.Default;
        AudioSourceDefinition.Describe(in source).Should().Be(source.ToString());
    }
}
