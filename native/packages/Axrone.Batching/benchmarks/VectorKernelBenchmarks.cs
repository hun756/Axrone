using System.Numerics;
using Axrone.Batching;
using BenchmarkDotNet.Attributes;

namespace Axrone.Batching.Benchmarks;

/// <summary>
/// Vector kernels against a plain scalar loop over the same spans.
/// </summary>
/// <remarks>
/// The scalar side is the fallback these kernels already carry in their tails, so a case where the
/// vectorised path does not win is a case where it should be deleted rather than kept for pride.
/// </remarks>
[MemoryDiagnoser]
public class VectorKernelBenchmarks
{
    [Params(64, 1024, 16384)]
    public int Count { get; set; }

    private Quaternion[] _left4 = [];
    private Quaternion[] _right4 = [];
    private Quaternion[] _scratchQuat = [];
    private Vector3[] _left3 = [];
    private Vector3[] _right3 = [];
    private Vector3[] _scratch3 = [];
    private Vector4[] _vectors4 = [];
    private Vector4[] _scratch4 = [];
    private float[] _scratchDot = [];

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(20260920);
        _left4 = new Quaternion[Count];
        _right4 = new Quaternion[Count];
        _scratchQuat = new Quaternion[Count];
        _left3 = new Vector3[Count];
        _right3 = new Vector3[Count];
        _scratch3 = new Vector3[Count];
        _vectors4 = new Vector4[Count];
        _scratch4 = new Vector4[Count];
        _scratchDot = new float[Count];

        float Next() => random.Next(-40, 41) / 4f;
        for (var i = 0; i < Count; i++)
        {
            _left4[i] = new Quaternion(Next() / 2f, Next() / 2f, Next() / 2f, Next() / 2f);
            _right4[i] = new Quaternion(Next() / 2f, Next() / 2f, Next() / 2f, Next() / 2f);
            _left3[i] = new Vector3(Next(), Next(), Next());
            _right3[i] = new Vector3(Next(), Next(), Next());
            _vectors4[i] = new Vector4(Next(), Next(), Next(), Next());
        }
    }

    [Benchmark(Baseline = true)]
    public void QuaternionMultiplyScalar()
    {
        for (var i = 0; i < Count; i++)
        {
            _scratchQuat[i] = Quaternion.Multiply(_left4[i], _right4[i]);
        }
    }

    [Benchmark]
    public void QuaternionMultiplyVectorised() =>
        SimdBatchKernels.QuaternionMultiply(_left4, _right4, _scratchQuat);

    [Benchmark]
    public void DotScalar()
    {
        for (var i = 0; i < Count; i++)
        {
            _scratchDot[i] = Vector3.Dot(_left3[i], _right3[i]);
        }
    }

    [Benchmark]
    public void DotVectorised() =>
        SimdBatchKernels.BatchDot3(_left3, _right3, _scratchDot);

    [Benchmark]
    public void CrossScalar()
    {
        for (var i = 0; i < Count; i++)
        {
            _scratch3[i] = Vector3.Cross(_left3[i], _right3[i]);
        }
    }

    [Benchmark]
    public void CrossVectorised() =>
        SimdBatchKernels.BatchCross3(_left3, _right3, _scratch3);

    [Benchmark]
    public void NormalizeScalar()
    {
        for (var i = 0; i < Count; i++)
        {
            var v = _vectors4[i];
            _scratch4[i] = v.LengthSquared() > 1e-12f ? Vector4.Normalize(v) : Vector4.Zero;
        }
    }

    [Benchmark]
    public void NormalizeVectorised() =>
        SimdBatchKernels.Normalize4(_vectors4, _scratch4);
}
