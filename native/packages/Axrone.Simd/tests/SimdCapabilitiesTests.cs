namespace Axrone.Simd.Tests;

using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

[Collection("SimdRuntime")]
public class SimdCapabilitiesTests
{
    private static byte[] PatternBytes(int length, byte needle, int every)
    {
        var data = new byte[length];
        uint state = 0x9E3779B9u;
        for (int i = 0; i < length; i++)
        {
            state = (state * 1664525u) + 1013904223u;
            data[i] = (byte)(state >> 24);
            if (i % every == 0)
            {
                data[i] = needle;
            }
        }

        return data;
    }

    private static nuint ManualCount(ReadOnlySpan<byte> data, byte needle)
    {
        nuint count = 0;
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == needle)
            {
                count++;
            }
        }

        return count;
    }

    [Fact]
    public void Probe_MirrorsBclAccelerationFlags()
    {
        SimdRuntime.IsSupported(SimdFeature.VectorHardwareAccelerated).Should().Be(Vector.IsHardwareAccelerated);
        SimdRuntime.IsSupported(SimdFeature.Vector128HardwareAccelerated).Should().Be(Vector128.IsHardwareAccelerated);
        SimdRuntime.IsSupported(SimdFeature.Vector256HardwareAccelerated).Should().Be(Vector256.IsHardwareAccelerated);
        SimdRuntime.IsSupported(SimdFeature.Vector512HardwareAccelerated).Should().Be(Vector512.IsHardwareAccelerated);
    }

    [Fact]
    public void Probe_YieldsCoherentTopology()
    {
        ref readonly SimdCapabilities caps = ref SimdRuntime.Capabilities;
        caps.Architecture.Should().NotBe(SimdArchitecture.Unknown);
        caps.Mask.IsEmpty.Should().BeFalse();
        caps.Mask.PopCount().Should().BeGreaterThan(0);
        caps.MaxRegisterWidth.Should().NotBe(SimdRegisterWidth.None);
        caps.IsaLevel.Should().NotBe(SimdIsaLevel.Generic);
        (caps.PreferredAlignment.BoundaryBytes >= 16).Should().BeTrue();

        if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            caps.Architecture.Should().Be(SimdArchitecture.X64);
            ((byte)caps.IsaLevel >= (byte)SimdIsaLevel.X86_64_V1).Should().BeTrue();
            caps.HasFeature(SimdFeature.Sse2).Should().BeTrue();
        }
    }

    [Fact]
    public void Policies_GateOnFeaturesAndWidth()
    {
        SimdRuntime.IsPolicySatisfied<ScalarPolicy>().Should().BeTrue();

        FeatureBitmask256 avx2 = FeatureBitmask256.Create(SimdFeature.Avx2).Set(SimdFeature.Vector256HardwareAccelerated);
        var capable = new SimdCapabilities(avx2, SimdArchitecture.X64, SimdIsaLevel.X86_64_V3, SimdRegisterWidth.Bits256, SimdAlignment.Byte32);
        capable.SatisfiesPolicy<Avx2Policy>().Should().BeTrue();
        capable.SatisfiesPolicy<Avx512Policy>().Should().BeFalse();

        var narrow = new SimdCapabilities(avx2, SimdArchitecture.X64, SimdIsaLevel.X86_64_V3, SimdRegisterWidth.Bits128, SimdAlignment.Byte16);
        narrow.SatisfiesPolicy<Avx2Policy>().Should().BeFalse();

        ref readonly SimdCapabilities host = ref SimdRuntime.Capabilities;
        host.SatisfiesPolicy<ScalarPolicy>().Should().BeTrue();
        host.HasAll(SimdFeature.Sse2, SimdFeature.VectorHardwareAccelerated).Should().Be(Vector.IsHardwareAccelerated);
    }

    [Fact]
    public void Dispatch_RecordsExactlyOneTier()
    {
        byte[] data = PatternBytes(2048, 0xA5, 3);
        SimdTelemetrySnapshot before = SimdRuntime.GetTelemetrySnapshot();
        CascadingByteEngine.CountOccurrences(data, 0xA5).Should().Be(ManualCount(data, 0xA5));
        SimdTelemetrySnapshot after = SimdRuntime.GetTelemetrySnapshot();
        (after.TotalDispatches - before.TotalDispatches).Should().Be(1);
    }

    [Fact]
    public void DiagnosticReporting_NamesAndReport()
    {
        SimdFeature.Avx2.ToFeatureName().Should().Be("AVX2");
        SimdFeature.AdvSimd.ToFeatureName().Should().Contain("NEON");

        string report = SimdRuntime.Capabilities.ToDiagnosticReport();
        report.Should().Contain("Architecture");
        SimdRuntime.Capabilities.ToString().Should().Contain("Width:");
    }
}
