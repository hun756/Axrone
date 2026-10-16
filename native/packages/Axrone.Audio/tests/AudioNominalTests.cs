namespace Axrone.Audio.Tests;

public class AudioNominalTests
{
    [Fact]
    public void Bus_Presets_AreValid_AndOrdered()
    {
        AudioBusId.Invalid.IsValid.Should().BeFalse();
        AudioBusId.Master.IsValid.Should().BeTrue();
        (AudioBusId.Master < AudioBusId.Music).Should().BeTrue();
        (AudioBusId.World > AudioBusId.Master).Should().BeTrue();
        AudioBusId.Master.Should().Be(new AudioBusId(1U));
        AudioBusId.Master.ToString().Should().Be("1");
    }

    [Fact]
    public void Clip_Listener_Source_Snapshot_Sentinels()
    {
        AudioClipId.Invalid.IsValid.Should().BeFalse();
        new AudioClipId(9U).IsValid.Should().BeTrue();
        AudioListenerId.Primary.Should().Be(new AudioListenerId(1U));
        AudioSourceId.Invalid.Should().Be(new AudioSourceId(0U));
        AudioSnapshotId.Invalid.IsValid.Should().BeFalse();
        new AudioSnapshotId(3U).CompareTo(new AudioSnapshotId(4U)).Should().BeLessThan(0);
    }
}
