using System.Numerics;
using Axrone.Batching;
using BenchmarkDotNet.Attributes;

namespace Axrone.Batching.Benchmarks;

/// <summary>
/// Geometry kernels against a plain scalar loop over the same spans.
/// </summary>
/// <remarks>
/// The scalar side is the fallback these kernels already carry in their tails, so a case where the
/// vectorised path does not win is a case where it should be deleted rather than kept for pride.
/// </remarks>
[MemoryDiagnoser]
public class GeometryKernelBenchmarks
{
    [Params(64, 1024, 16384)]
    public int Count { get; set; }

    private Vector3[] _positions3 = [];
    private Vector3[] _velocities3 = [];
    private Vector3[] _scratch3 = [];
    private Vector4[] _vectors4 = [];
    private Vector4[] _scratch4 = [];
    private Matrix4x4 _matrix;

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(20260919);
        _positions3 = new Vector3[Count];
        _velocities3 = new Vector3[Count];
        _scratch3 = new Vector3[Count];
        _vectors4 = new Vector4[Count];
        _scratch4 = new Vector4[Count];

        for (var i = 0; i < Count; i++)
        {
            _positions3[i] = new Vector3(random.Next(-40, 41) / 4f, random.Next(-40, 41) / 4f, random.Next(-40, 41) / 4f);
            _velocities3[i] = new Vector3(random.Next(-40, 41) / 4f, random.Next(-40, 41) / 4f, random.Next(-40, 41) / 4f);
            _vectors4[i] = new Vector4(random.Next(-40, 41) / 4f, random.Next(-40, 41) / 4f,
                                       random.Next(-40, 41) / 4f, random.Next(-40, 41) / 4f);
        }

        _matrix = new Matrix4x4(
            1.4f, 0.3f, -0.2f, 0f,
            0.1f, 0.7f, 0.4f, 0f,
            0.5f, -0.1f, 2.1f, 0f,
            12f, -3f, 8f, 1f);
    }

    [Benchmark(Baseline = true)]
    public void TransformPositionsScalar()
    {
        for (var i = 0; i < Count; i++)
        {
            _scratch3[i] = Vector3.Transform(_positions3[i], _matrix);
        }
    }

    [Benchmark]
    public void TransformPositionsVectorised() =>
        SimdBatchKernels.TransformPositions3D(_positions3, _scratch3, _matrix);

    [Benchmark]
    public void TransformAffineScalar()
    {
        for (var i = 0; i < Count; i++)
        {
            _scratch4[i] = Vector4.Transform(_vectors4[i], _matrix);
        }
    }

    [Benchmark]
    public void TransformAffineVectorised() =>
        SimdBatchKernels.TransformAffine(_vectors4, _scratch4, _matrix);

    [Benchmark]
    public void IntegrateVelocityScalar()
    {
        const float dt = 0.016f;
        for (var i = 0; i < Count; i++)
        {
            _scratch3[i] = _positions3[i] + (_velocities3[i] * dt);
        }
    }

    [Benchmark]
    public void IntegrateVelocityVectorised()
    {
        Array.Copy(_positions3, _scratch3, Count);
        SimdBatchKernels.IntegrateVelocity(_scratch3, _velocities3, 0.016f);
    }
}
