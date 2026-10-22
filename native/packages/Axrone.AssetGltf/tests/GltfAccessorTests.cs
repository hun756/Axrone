namespace Axrone.AssetGltf.Tests;

public sealed class GltfAccessorTests
{
    [Fact]
    public void ComponentSizes_MatchSpec()
    {
        GltfAccessorRuntime.GetComponentByteSize(5120).Should().Be(1);
        GltfAccessorRuntime.GetComponentByteSize(5121).Should().Be(1);
        GltfAccessorRuntime.GetComponentByteSize(5122).Should().Be(2);
        GltfAccessorRuntime.GetComponentByteSize(5123).Should().Be(2);
        GltfAccessorRuntime.GetComponentByteSize(5125).Should().Be(4);
        GltfAccessorRuntime.GetComponentByteSize(5126).Should().Be(4);
    }

    [Fact]
    public void TypeCounts_MatchSpec()
    {
        GltfAccessorRuntime.GetTypeElementCount("SCALAR").Should().Be(1);
        GltfAccessorRuntime.GetTypeElementCount("VEC2").Should().Be(2);
        GltfAccessorRuntime.GetTypeElementCount("VEC3").Should().Be(3);
        GltfAccessorRuntime.GetTypeElementCount("VEC4").Should().Be(4);
        GltfAccessorRuntime.GetTypeElementCount("MAT4").Should().Be(16);
    }

    [Fact]
    public void DecodeFloats_FloatVec3()
    {
        float[] src = [1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f];
        byte[] bytes = new byte[src.Length * 4];
        Buffer.BlockCopy(src, 0, bytes, 0, bytes.Length);
        Span<float> dst = stackalloc float[6];
        GltfAccessorRuntime.DecodeFloats(bytes, 2, 5126, 3, 0, false, dst);
        dst.ToArray().Should().Equal(1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f);
    }

    [Fact]
    public void DecodeFloats_NormalizedUByte()
    {
        byte[] bytes = [255, 0, 128];
        Span<float> dst = stackalloc float[3];
        GltfAccessorRuntime.DecodeFloats(bytes, 1, 5121, 3, 0, true, dst);
        dst[0].Should().BeApproximately(1.0f, 1e-6f);
        dst[1].Should().BeApproximately(0.0f, 1e-6f);
        dst[2].Should().BeApproximately(128.0f / 255.0f, 1e-6f);
    }

    [Theory]
    [InlineData(5120, new byte[] { 0x80, 0x7F }, false, new[] { -128f, 127f })]
    [InlineData(5120, new byte[] { 0x80, 0x7F, 0x40 }, true, new[] { -1f, 1f, 0.503937f })]
    [InlineData(5121, new byte[] { 0, 255 }, false, new[] { 0f, 255f })]
    [InlineData(5121, new byte[] { 0, 255 }, true, new[] { 0f, 1f })]
    [InlineData(5122, new byte[] { 0x00, 0x80, 0xFF, 0x7F }, false, new[] { -32768f, 32767f })]
    [InlineData(5122, new byte[] { 0x00, 0x80, 0xFF, 0x7F }, true, new[] { -1f, 1f })]
    [InlineData(5123, new byte[] { 0, 0, 0xFF, 0xFF }, false, new[] { 0f, 65535f })]
    [InlineData(5123, new byte[] { 0, 0, 0xFF, 0xFF }, true, new[] { 0f, 1f })]
    [InlineData(5125, new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }, false, new[] { 0f, 16777216f })]
    [InlineData(5125, new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }, true, new[] { 0f, 0.00390625f })]
    [InlineData(5126, new byte[] { 0, 0, 0x80, 0x3F }, false, new[] { 1f })]
    public void DecodeFloats_AllComponentTypes(int componentType, byte[] source, bool normalized, float[] expected)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(expected);

        var destination = new float[expected.Length];

        GltfAccessorRuntime.DecodeFloats(
            source,
            expected.Length,
            componentType,
            componentCount: 1,
            byteStride: 0,
            normalized,
            destination);

        destination.Should().Equal(expected);
    }

    [Fact]
    public void DecodeIndices_UInt16()
    {
        byte[] bytes = [1, 0, 300 & 0xFF, (300 >> 8) & 0xFF];
        Span<uint> dst = stackalloc uint[2];
        GltfAccessorRuntime.DecodeIndices(bytes, 2, 5123, 0, dst);
        dst[0].Should().Be(1u);
        dst[1].Should().Be(300u);
    }

    [Fact]
    public void MergeSparse_OverwritesTarget()
    {
        Span<float> dense = stackalloc float[4];
        dense[0] = 1.0f;
        dense[1] = 2.0f;
        dense[2] = 3.0f;
        dense[3] = 4.0f;
        byte[] idx = [1, 0];
        float[] vals = [9.0f, 9.0f];
        byte[] valBytes = new byte[8];
        Buffer.BlockCopy(vals, 0, valBytes, 0, 8);
        GltfAccessorRuntime.MergeSparseFloats(0, idx, 5123, valBytes, 1, 2, 5126, 2, false, dense);
        dense[0].Should().Be(1.0f);
        dense[1].Should().Be(2.0f);
        dense[2].Should().Be(9.0f);
        dense[3].Should().Be(9.0f);
    }

    [Fact]
    public void ExpandTopology_Triangles_Passthrough()
    {
        uint[] idx = [0, 1, 2];
        GltfMeshPipeline.ExpandTopology(4, idx, 0).Should().Equal(0u, 1u, 2u);
    }

    [Fact]
    public void ExpandTopology_Strip_WindsCorrectly()
    {
        uint[] idx = [0, 1, 2, 3];
        GltfMeshPipeline.ExpandTopology(5, idx, 0).Should().Equal(0u, 1u, 2u, 2u, 1u, 3u);
    }

    [Fact]
    public void ExpandTopology_Empty_GeneratesSequence()
    {
        GltfMeshPipeline.ExpandTopology(4, ReadOnlySpan<uint>.Empty, 3).Should().Equal(0u, 1u, 2u);
    }

    [Fact]
    public void ExpandTopology_BadMode_Throws()
    {
        Action act = () => GltfMeshPipeline.ExpandTopology(99, new uint[] { 0 }, 0);
        act.Should().Throw<GltfTopologyError>();
    }

    [Fact]
    public void Downcast_SmallIndices_Is16Bit()
    {
        uint[] idx = [0, 1, 65535];
        var (bytes, is16) = GltfMeshPipeline.DowncastIndices(idx);
        is16.Should().BeTrue();
        bytes.Length.Should().Be(6);
    }

    [Fact]
    public void Downcast_LargeIndices_Is32Bit()
    {
        uint[] idx = [0, 70000];
        var (bytes, is16) = GltfMeshPipeline.DowncastIndices(idx);
        is16.Should().BeFalse();
        bytes.Length.Should().Be(8);
    }

    [Fact]
    public void ComputeBounds_CoversPoints()
    {
        float[] pos = [1.0f, 2.0f, 3.0f, -1.0f, 0.0f, 4.0f];
        var b = GltfMeshPipeline.ComputeBounds(pos);
        b.Min.X.Should().Be(-1.0f);
        b.Max.Z.Should().Be(4.0f);
    }
}
