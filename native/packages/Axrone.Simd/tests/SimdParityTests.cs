namespace Axrone.Simd.Tests;

using System.Runtime.Intrinsics;

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

    private static ConfigurableSimdRuntime VectorRuntime(bool withFma)
    {
        FeatureBitmask256 mask = FeatureBitmask256.Create(SimdFeature.VectorHardwareAccelerated)
            .Set(SimdFeature.Vector128HardwareAccelerated)
            .Set(SimdFeature.Vector256HardwareAccelerated);
        if (withFma)
        {
            mask = mask.Set(SimdFeature.Fma);
        }
        return new(new SimdCapabilities(
            mask, SimdArchitecture.X64, SimdIsaLevel.Generic, SimdRegisterWidth.None, SimdAlignment.None));
    }

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
    public void VectorFma_FmaTierMatchesMulAddTier()
    {
        const int size = 100;
        float[] a = new float[size];
        float[] b = new float[size];
        float[] c = new float[size];
        for (int i = 0; i < size; i++)
        {
            a[i] = i * 0.37f - 11f;
            b[i] = 7f - i * 0.11f;
            c[i] = i * 1.7f;
        }

        float[] fused = new float[size];
        using (SimdRuntime.UseCustomRuntime(VectorRuntime(withFma: true)))
        {
            SimdFloat32.VectorFma(a, b, c, fused);
        }

        float[] plain = new float[size];
        using (SimdRuntime.UseCustomRuntime(VectorRuntime(withFma: false)))
        {
            SimdFloat32.VectorFma(a, b, c, plain);
        }

        for (int i = 0; i < size; i++)
            fused[i].Should().BeApproximately(plain[i], 1e-4f);
    }

    [Fact]
    public void VectorFma_FusedTierActuallyExecutes()
    {
        const int size = 64;
        float[] a = new float[size];
        float[] b = new float[size];
        float[] c = new float[size];
        for (int i = 0; i < size; i++)
        {
            float e = (i + 1) * 1e-6f;
            a[i] = 1f + e;
            b[i] = 1f + e;
            c[i] = -(1f + (2f * e));
        }

        float[] fused = new float[size];
        using (SimdRuntime.UseCustomRuntime(VectorRuntime(withFma: true)))
        {
            SimdFloat32.VectorFma(a, b, c, fused);
        }

        float[] plain = new float[size];
        using (SimdRuntime.UseCustomRuntime(VectorRuntime(withFma: false)))
        {
            SimdFloat32.VectorFma(a, b, c, plain);
        }

        fused.Should().NotEqual(plain);
    }

    [Fact]
    public void SimdRow32_FusedTierMatchesScalarTier()
    {
        var b0 = Vector128.Create(1f, 2f, 3f, 4f);
        var b1 = Vector128.Create(5f, 6f, 7f, 8f);
        var b2 = Vector128.Create(9f, 10f, 11f, 12f);
        var b3 = Vector128.Create(13f, 14f, 15f, 16f);

        Vector128<float> fused;
        using (SimdRuntime.UseCustomRuntime(VectorRuntime(withFma: true)))
        {
            fused = SimdRow32.MultiplyAddRowFused(0.5f, -1.25f, 2f, 0.1f, b0, b1, b2, b3);
        }

        Vector128<float> plain;
        using (SimdRuntime.UseCustomRuntime(VectorRuntime(withFma: false)))
        {
            plain = SimdRow32.MultiplyAddRowPlain(0.5f, -1.25f, 2f, 0.1f, b0, b1, b2, b3);
        }

        for (int i = 0; i < 4; i++)
            fused.GetElement(i).Should().BeApproximately(plain.GetElement(i), 1e-5f);
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
