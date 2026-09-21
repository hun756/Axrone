namespace Axrone.Batching.Tests;

/// <summary>
/// Coverage for Morton encoding and the non-finite scan.
/// </summary>
public class BatchSpatialKernelTests
{
    private static uint SpreadScalar(uint value)
    {
        value &= 0x000003FFu;
        value = (value | (value << 16)) & 0x030000FFu;
        value = (value | (value << 8)) & 0x0300F00Fu;
        value = (value | (value << 4)) & 0x030C30C3u;
        value = (value | (value << 2)) & 0x09249249u;
        return value;
    }

    [Fact]
    public void MortonEncode_KnownValues()
    {
        var x = new uint[] { 0, 1, 0, 0, 1, 0x3FF };
        var y = new uint[] { 0, 0, 1, 0, 1, 0x3FF };
        var z = new uint[] { 0, 0, 0, 1, 1, 0x3FF };
        var destination = new uint[6];

        SimdBatchKernels.MortonEncode3D(x, y, z, destination);

        destination[0].Should().Be(0u);
        destination[1].Should().Be(1u);
        destination[2].Should().Be(2u);
        destination[3].Should().Be(4u);
        destination[4].Should().Be(7u);
        destination[5].Should().Be(0x3FFFFFFFu);
    }

    [Fact]
    public void MortonEncode_HighBitsAreMasked()
    {
        var x = new uint[] { 0xFFFF_FFFF };
        var y = new uint[] { 0xFFFF_FFFF };
        var z = new uint[] { 0xFFFF_FFFF };
        var destination = new uint[1];

        SimdBatchKernels.MortonEncode3D(x, y, z, destination);

        destination[0].Should().Be(0x3FFFFFFFu);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(17)]
    [InlineData(100)]
    public void MortonEncode_MatchesScalarSpread(int count)
    {
        var rng = new SeededRng((ulong)count + 701);
        var x = new uint[count];
        var y = new uint[count];
        var z = new uint[count];
        for (var i = 0; i < count; i++)
        {
            x[i] = rng.NextUInt32();
            y[i] = rng.NextUInt32();
            z[i] = rng.NextUInt32();
        }

        var destination = new uint[count];

        SimdBatchKernels.MortonEncode3D(x, y, z, destination);

        for (var i = 0; i < count; i++)
        {
            var expected = SpreadScalar(x[i]) | (SpreadScalar(y[i]) << 1) | (SpreadScalar(z[i]) << 2);
            destination[i].Should().Be(expected, $"index {i}");
        }
    }

    [Fact]
    public void MortonEncode_MismatchedSpans_Throws()
    {
        var act = () => SimdBatchKernels.MortonEncode3D(
            new uint[4], new uint[4], new uint[3], new uint[4]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ScanNonFinite_AllFinite_WritesNothing()
    {
        var source = new float[] { 0f, 1f, -2.5f, 3.25f, float.Epsilon, float.MaxValue };

        SimdBatchKernels.ScanNonFinite(source, new int[4]).Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(20)]
    public void ScanNonFinite_FindsNaNAndInfinities(int count)
    {
        var source = new float[count];
        var expected = new System.Collections.Generic.List<int>();
        for (var i = 0; i < count; i++)
        {
            source[i] = i switch
            {
                _ when i % 5 == 0 => float.NaN,
                _ when i % 5 == 1 => float.PositiveInfinity,
                _ when i % 5 == 2 => float.NegativeInfinity,
                _ => i * 1.5f,
            };
            if (i % 5 <= 2)
            {
                expected.Add(i);
            }
        }

        var destination = new int[count];

        var written = SimdBatchKernels.ScanNonFinite(source, destination);

        written.Should().Be(expected.Count);
        destination[..written].Should().Equal(expected);
    }

    [Fact]
    public void ScanNonFinite_ShortDestination_Truncates()
    {
        var source = new float[] { float.NaN, float.NaN, float.NaN, 1f, float.PositiveInfinity };
        var destination = new int[2];

        var written = SimdBatchKernels.ScanNonFinite(source, destination);

        written.Should().Be(2);
        destination.Should().Equal(0, 1);
    }

    [Fact]
    public void ScanNonFinite_EmptySource_WritesNothing()
    {
        SimdBatchKernels.ScanNonFinite([], new int[4]).Should().Be(0);
    }
}
