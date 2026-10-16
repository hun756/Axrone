namespace Axrone.Audio.OpenAL.Tests;

/// <summary>
/// Real-device smoke tests: probe the default device and, when present,
/// run the full upload/play/stop/teardown sequence. Without audio hardware
/// the probe passes vacuously; filter by Category=Hardware in CI.
/// </summary>
[Trait("Category", "Hardware")]
public sealed class OpenALHardwareSmokeTests
{
    private readonly ITestOutputHelper _output;

    public OpenALHardwareSmokeTests(ITestOutputHelper output) => _output = output;
    private static bool TryOpen(SilkAudioDriver driver)
    {
        try
        {
            driver.Open(null, 48000);
            return driver.IsOpen;
        }
        catch (Exception ex) when (ex is AudioDriverException or DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    [Fact]
    public void Device_Open_Probe()
    {
        using var driver = new SilkAudioDriver();
        if (!TryOpen(driver))
        {
            _output.WriteLine("No audio device; probe vacuous.");
            return;
        }

        _output.WriteLine("Audio device opened.");
        try
        {
            driver.IsOpen.Should().BeTrue();
        }
        finally
        {
            driver.Close();
        }

        driver.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Device_Play_Silence_Teardown()
    {
        using var driver = new SilkAudioDriver();
        if (!TryOpen(driver))
        {
            _output.WriteLine("No audio device; smoke test vacuous.");
            return;
        }

        _output.WriteLine("Audio device opened; running full sequence.");

        try
        {
            uint[] buffers = driver.GenBuffers(2);
            uint source = driver.GenSource();

            short[] silence = new short[512];
            driver.BufferDataShort(buffers[0], silence, 48000);
            driver.BufferDataShort(buffers[1], silence, 48000);
            driver.QueueBuffers(source, buffers);
            driver.Play(source);
            driver.GetSourceState(source).Should().Be(AudioDriverSourceState.Playing);

            driver.StopPlayback(source);
            driver.GetSourceState(source).Should().Be(AudioDriverSourceState.Stopped);

            uint[] scratch = new uint[4];
            driver.UnqueueProcessed(source, scratch);
            driver.DeleteSource(source);
            driver.DeleteBuffers(buffers);
        }
        finally
        {
            driver.Close();
        }
    }
}
