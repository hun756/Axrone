using System.Numerics;
using Axrone.Batching;
using BenchmarkDotNet.Attributes;

namespace Axrone.Batching.Benchmarks;

/// <summary>
/// Bound and spatial-index kernels against a plain scalar loop over the same spans.
/// </summary>
/// <remarks>
/// The scalar side is the fallback these kernels already carry in their tails, so a case where the
/// vectorised path does not win is a case where it should be deleted rather than kept for pride.
/// </remarks>
[MemoryDiagnoser]
public class BoundsSpatialBenchmarks
{
    [Params(64, 1024, 16384)]
    public int Count { get; set; }

    private Aabb[] _boxes = [];
    private Aabb[] _scratchBoxes = [];
    private Vector4[] _spheres = [];
    private Vector4[] _scratchSpheres = [];
    private uint[] _x = [];
    private uint[] _y = [];
    private uint[] _z = [];
    private uint[] _scratchMorton = [];
    private float[] _scanSource = [];
    private int[] _scratchIndices = [];
    private Matrix4x4 _matrix;

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(20260921);
        _boxes = new Aabb[Count];
        _scratchBoxes = new Aabb[Count];
        _spheres = new Vector4[Count];
        _scratchSpheres = new Vector4[Count];
        _x = new uint[Count];
        _y = new uint[Count];
        _z = new uint[Count];
        _scratchMorton = new uint[Count];
        _scanSource = new float[Count];
        _scratchIndices = new int[Count];

        float Next() => random.Next(-40, 41) / 4f;
        for (var i = 0; i < Count; i++)
        {
            var minX = Next();
            var minY = Next();
            var minZ = Next();
            _boxes[i] = new Aabb(minX, minY, minZ,
                minX + random.Next(0, 41) / 4f, minY + random.Next(0, 41) / 4f, minZ + random.Next(0, 41) / 4f);
            _spheres[i] = new Vector4(Next(), Next(), Next(), random.Next(0, 41) / 4f);
            _x[i] = (uint)random.Next();
            _y[i] = (uint)random.Next();
            _z[i] = (uint)random.Next();
            _scanSource[i] = i % 5 == 0 ? float.NaN : Next();
        }

        _matrix = new Matrix4x4(
            1.4f, 0.3f, -0.2f, 0f,
            0.1f, 0.7f, 0.4f, 0f,
            0.5f, -0.1f, 2.1f, 0f,
            12f, -3f, 8f, 1f);
    }

    [Benchmark(Baseline = true)]
    public void TransformAabbScalar()
    {
        for (var i = 0; i < Count; i++)
        {
            var center = Vector3.Transform(_boxes[i].Center, _matrix);
            var extent = _boxes[i].Extent;
            var transformed = new Vector3(
                (MathF.Abs(_matrix.M11) * extent.X) + (MathF.Abs(_matrix.M21) * extent.Y) + (MathF.Abs(_matrix.M31) * extent.Z),
                (MathF.Abs(_matrix.M12) * extent.X) + (MathF.Abs(_matrix.M22) * extent.Y) + (MathF.Abs(_matrix.M32) * extent.Z),
                (MathF.Abs(_matrix.M13) * extent.X) + (MathF.Abs(_matrix.M23) * extent.Y) + (MathF.Abs(_matrix.M33) * extent.Z));
            _scratchBoxes[i] = Aabb.FromCenterExtent(center, transformed);
        }
    }

    [Benchmark]
    public void TransformAabbVectorised() =>
        SimdBatchKernels.TransformAabb(_boxes, _scratchBoxes, _matrix);

    [Benchmark]
    public void TransformSpheresScalar()
    {
        const float maxScale = 1.5f;
        for (var i = 0; i < Count; i++)
        {
            var center = Vector3.Transform(new Vector3(_spheres[i].X, _spheres[i].Y, _spheres[i].Z), _matrix);
            _scratchSpheres[i] = new Vector4(center.X, center.Y, center.Z, _spheres[i].W * maxScale);
        }
    }

    [Benchmark]
    public void TransformSpheresVectorised() =>
        SimdBatchKernels.TransformBoundingSpheres(_spheres, _scratchSpheres, _matrix, 1.5f);

    [Benchmark]
    public void MortonEncodeScalar()
    {
        for (var i = 0; i < Count; i++)
        {
            _scratchMorton[i] = Spread(_x[i]) | (Spread(_y[i]) << 1) | (Spread(_z[i]) << 2);
        }

        static uint Spread(uint v)
        {
            v &= 0x000003FFu;
            v = (v | (v << 16)) & 0x030000FFu;
            v = (v | (v << 8)) & 0x0300F00Fu;
            v = (v | (v << 4)) & 0x030C30C3u;
            v = (v | (v << 2)) & 0x09249249u;
            return v;
        }
    }

    [Benchmark]
    public void MortonEncodeVectorised() =>
        SimdBatchKernels.MortonEncode3D(_x, _y, _z, _scratchMorton);

    [Benchmark]
    public void ScanNonFiniteScalar()
    {
        var found = 0;
        for (var i = 0; i < Count; i++)
        {
            if (!float.IsFinite(_scanSource[i]))
            {
                _scratchIndices[found++] = i;
            }
        }
    }

    [Benchmark]
    public void ScanNonFiniteVectorised() =>
        SimdBatchKernels.ScanNonFinite(_scanSource, _scratchIndices);
}
