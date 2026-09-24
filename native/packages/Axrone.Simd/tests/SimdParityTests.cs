namespace Axrone.Simd.Tests;

[Collection("SimdRuntime")]
public class SimdParityTests
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

    private static ConfigurableSimdRuntime ScalarOnlyRuntime() =>
        new(new SimdCapabilities(
            FeatureBitmask256.Empty, SimdArchitecture.Unknown, SimdIsaLevel.Generic, SimdRegisterWidth.None, SimdAlignment.None));

    [Fact]
    public void CustomRuntime_ForcesScalarTierWithParity()
    {
        byte[] data = PatternBytes(4096, 0x7E, 7);
        nuint expected = ManualCount(data, 0x7E);

        nuint dispatched = CascadingByteEngine.CountOccurrences(data, 0x7E);
        dispatched.Should().Be(expected);

        using (SimdRuntime.UseCustomRuntime(ScalarOnlyRuntime()))
        {
            SimdRuntime.IsPolicySatisfied<ScalarPolicy>().Should().BeTrue();
            SimdRuntime.IsPolicySatisfied<Avx2Policy>().Should().BeFalse();

            ISimdRuntime active = SimdRuntime.Current;
            CascadingByteEngine.CountOccurrences(data, 0x7E).Should().Be(expected);
            SimdTelemetrySnapshot snapshot = active.GetTelemetrySnapshot();
            snapshot.ScalarDispatches.Should().Be(1);
            snapshot.TotalDispatches.Should().Be(1);
        }

        SimdRuntime.SetCustomRuntime(null).Should().BeNull();
    }

    [Fact]
    public void ForcedScalar_VectorOpsMatchHardwarePath()
    {
        float[] left = new float[1024];
        float[] right = new float[1024];
        for (int i = 0; i < left.Length; i++)
        {
            left[i] = i * 0.25f;
            right[i] = 1024 - i;
        }

        float[] hw = new float[1024];
        SimdFloat32.VectorAdd(left, right, hw);
        double hwDot = SimdFloat64.ComputeDotProduct(
            Array.ConvertAll(left, static v => (double)v),
            Array.ConvertAll(right, static v => (double)v));

        int[] ileft = new int[1024];
        int[] iright = new int[1024];
        for (int i = 0; i < ileft.Length; i++)
        {
            ileft[i] = i;
            iright[i] = -i;
        }

        int[] ihw = new int[1024];
        SimdInt32.Add(ileft, iright, ihw);

        using (SimdRuntime.UseCustomRuntime(ScalarOnlyRuntime()))
        {
            float[] sw = new float[1024];
            SimdFloat32.VectorAdd(left, right, sw);
            sw.Should().Equal(hw);

            double swDot = SimdFloat64.ComputeDotProduct(
                Array.ConvertAll(left, static v => (double)v),
                Array.ConvertAll(right, static v => (double)v));
            swDot.Should().Be(hwDot);

            int[] isw = new int[1024];
            SimdInt32.Add(ileft, iright, isw);
            isw.Should().Equal(ihw);
        }
    }
}
