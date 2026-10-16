namespace Axrone.Audio.Tests;

public class AudioErrorTests
{
    [Fact]
    public void Errors_Carry_Codes_And_Entities()
    {
        var bus = new AudioBusException(new AudioBusId(99U), "missing");
        bus.Code.Should().Be("audio.invalid-bus");
        bus.BusId.Should().Be(new AudioBusId(99U));

        var source = new AudioSourceException(new AudioSourceId(7U), "missing");
        source.Code.Should().Be("audio.invalid-source");

        var listener = new AudioListenerException(AudioListenerId.Primary, "missing");
        listener.ListenerId.Should().Be(AudioListenerId.Primary);

        new AudioDisposedException("mixer").Code.Should().Be("audio.disposed");
        new AudioLifecycleException("bad").Code.Should().Be("audio.state-fault");
        new AudioAssetException("bad").Code.Should().Be("audio.invalid-clip");
        new AudioConfigurationException("audio.invalid-volume", "bad").Code.Should().Be("audio.invalid-volume");
    }
}
