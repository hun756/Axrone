namespace Axrone.Audio.Tests;

public class AudioCommandTests
{
    [Fact]
    public void Commands_Carry_Type_And_Request()
    {
        var request = new AudioSourcePlayRequest(Volume: 0.5f);
        var play = new AudioSourceCommand(AudioSourceCommandType.Play, request);

        play.Type.Should().Be(AudioSourceCommandType.Play);
        play.Request.Volume.Should().Be(0.5f);

        var stop = new AudioSourceCommand(AudioSourceCommandType.Stop, default);
        stop.Type.Should().Be(AudioSourceCommandType.Stop);
    }
}
