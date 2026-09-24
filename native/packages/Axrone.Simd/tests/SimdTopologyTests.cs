namespace Axrone.Simd.Tests;

public class SimdTopologyTests
{
    [Fact]
    public void Bitmask_AlgebraHolds()
    {
        FeatureBitmask256 mask = FeatureBitmask256.Create(SimdFeature.Avx2).Set(SimdFeature.Fma);
        mask.Contains(SimdFeature.Avx2).Should().BeTrue();
        mask.Contains(SimdFeature.Fma).Should().BeTrue();
        mask.Contains(SimdFeature.Avx512F).Should().BeFalse();
        mask.PopCount().Should().Be(2);

        FeatureBitmask256 need = FeatureBitmask256.Create(SimdFeature.Avx2);
        mask.ContainsAll(need).Should().BeTrue();
        mask.ContainsAny(FeatureBitmask256.Create(SimdFeature.Avx512F)).Should().BeFalse();
        mask.ContainsAny(need | FeatureBitmask256.Create(SimdFeature.Avx512F)).Should().BeTrue();

        FeatureBitmask256.BitwiseOr(mask, need).Should().Be(mask);
        FeatureBitmask256.BitwiseAnd(mask, need).Should().Be(need);
        FeatureBitmask256.Xor(mask, need).PopCount().Should().Be(1);
        FeatureBitmask256.OnesComplement(FeatureBitmask256.Empty).Should().NotBe(FeatureBitmask256.Empty);
        FeatureBitmask256.Empty.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Classifier_GradesKnownMasks()
    {
        FeatureBitmask256 v1 = FeatureBitmask256.Create(SimdFeature.Sse).Set(SimdFeature.Sse2);
        TopologyClassifier.ClassifyLevel(SimdArchitecture.X64, v1).Should().Be(SimdIsaLevel.X86_64_V1);

        FeatureBitmask256 v2 = FeatureBitmask256.Create(SimdFeature.Sse3)
            .Set(SimdFeature.Ssse3).Set(SimdFeature.Sse41).Set(SimdFeature.Sse42).Set(SimdFeature.Popcnt);
        TopologyClassifier.ClassifyLevel(SimdArchitecture.X64, v2).Should().Be(SimdIsaLevel.X86_64_V2);

        FeatureBitmask256 v3 = FeatureBitmask256.Create(SimdFeature.Avx)
            .Set(SimdFeature.Avx2).Set(SimdFeature.Bmi1).Set(SimdFeature.Bmi2).Set(SimdFeature.Fma);
        TopologyClassifier.ClassifyLevel(SimdArchitecture.X64, v3).Should().Be(SimdIsaLevel.X86_64_V3);

        FeatureBitmask256 v4 = FeatureBitmask256.Create(SimdFeature.Avx512F)
            .Set(SimdFeature.Avx512BW).Set(SimdFeature.Avx512CD).Set(SimdFeature.Avx512DQ).Set(SimdFeature.Avx512VL);
        TopologyClassifier.ClassifyLevel(SimdArchitecture.X64, v4).Should().Be(SimdIsaLevel.X86_64_V4);

        TopologyClassifier.ClassifyLevel(SimdArchitecture.X64, FeatureBitmask256.Empty).Should().Be(SimdIsaLevel.Generic);
        TopologyClassifier.ClassifyLevel(SimdArchitecture.Arm64, FeatureBitmask256.Create(SimdFeature.AdvSimd)).Should().Be(SimdIsaLevel.Arm64_V8_0);

        TopologyClassifier.ResolveMaxRegisterWidth(v4.Set(SimdFeature.Vector512HardwareAccelerated)).Should().Be(SimdRegisterWidth.Bits512);
        TopologyClassifier.ResolveMaxRegisterWidth(v1).Should().Be(SimdRegisterWidth.Bits128);
        TopologyClassifier.ResolveAlignment(SimdRegisterWidth.Bits256).Should().Be(SimdAlignment.Byte32);
    }

    [Fact]
    public void Topology_StructsValidate()
    {
        Action width = () => { _ = new SimdRegisterWidth(96); };
        width.Should().Throw<ArgumentOutOfRangeException>();
        Action alignment = () => { _ = new SimdAlignment(24); };
        alignment.Should().Throw<ArgumentException>();

        (SimdRegisterWidth.Bits256 > SimdRegisterWidth.Bits128).Should().BeTrue();
        SimdRegisterWidth.Bits128.ByteWidth.Should().Be(16);
    }
}
