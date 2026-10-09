using Axrone.Batching;
using Axrone.Numeric;
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

    private Quat[] _left4 = [];
    private Quat[] _right4 = [];
    private Quat[] _scratchQuat = [];
    private Vec3[] _left3 = [];
    private Vec3[] _right3 = [];
    private Vec3[] _scratch3 = [];
    private Vec4[] _vectors4 = [];
    private Vec4[] _scratch4 = [];
    private float[] _scratchDot = [];

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(20260920);
        _left4 = new Quat[Count];
        _right4 = new Quat[Count];
        _scratchQuat = new Quat[Count];
        _left3 = new Vec3[Count];
        _right3 = new Vec3[Count];
        _scratch3 = new Vec3[Count];
        _vectors4 = new Vec4[Count];
        _scratch4 = new Vec4[Count];
        _scratchDot = new float[Count];

        float Next() => random.Next(-40, 41) / 4f;
        for (var i = 0; i < Count; i++)
        {
            _left4[i] = new Quat(Next() / 2f, Next() / 2f, Next() / 2f, Next() / 2f);
            _right4[i] = new Quat(Next() / 2f, Next() / 2f, Next() / 2f, Next() / 2f);
            _left3[i] = new Vec3(Next(), Next(), Next());
            _right3[i] = new Vec3(Next(), Next(), Next());
            _vectors4[i] = new Vec4(Next(), Next(), Next(), Next());
        }
    }

    [Benchmark(Baseline = true)]
    public void QuaternionMultiplyScalar()
    {
        for (var i = 0; i < Count; i++)
        {
            _scratchQuat[i] = Quat.Multiply(_left4[i], _right4[i]);
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
            _scratchDot[i] = Vec3.Dot(_left3[i], _right3[i]);
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
            _scratch3[i] = Vec3.Cross(_left3[i], _right3[i]);
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
            _scratch4[i] = v.LengthSquared() > 1e-12f ? Vec4.Normalize(v) : Vec4.Zero;
        }
    }

    [Benchmark]
    public void NormalizeVectorised() =>
        SimdBatchKernels.Normalize4(_vectors4, _scratch4);
}
