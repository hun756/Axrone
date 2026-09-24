using Axrone.Animation;
using BenchmarkDotNet.Attributes;

namespace Axrone.Animation.Benchmarks;

/// <summary>
/// Sampling and blending throughput over a 64-bone rig. Setup is excluded.
/// </summary>
[MemoryDiagnoser]
public class AnimationBenchmarks
{
    private Rig _rig = null!;
    private AnimationClip _clip = null!;
    private AnimationFrame _frame = null!;
    private AnimationFrame _base = null!;
    private AnimationFrame _overlay = null!;
    private float _time;

    [GlobalSetup]
    public void Setup()
    {
        var bones = new BoneInfo[64];
        for (int i = 0; i < bones.Length; i++)
        {
            bones[i] = new BoneInfo { Name = $"bone{i}", ParentIndex = i == 0 ? -1 : i - 1 };
        }

        _rig = new Rig(new RigId("bench"), bones);

        var channels = new AnimationChannel[64];
        for (int i = 0; i < channels.Length; i++)
        {
            channels[i] = new AnimationChannel(
                i, ChannelTarget.Translation, InterpolationMode.Linear,
                [0.0f, 1.0f], [0.0f, 0.0f, 0.0f, 1.0f, 1.0f, 1.0f]);
        }

        _clip = new AnimationClip(new ClipId("bench"), 1.0f, channels);
        var layout = new Dictionary<CurveId, int>();
        _frame = new AnimationFrame(64, layout);
        _base = new AnimationFrame(64, layout);
        _overlay = new AnimationFrame(64, layout);
        _overlay.GetTranslations()[32] = new System.Numerics.Vector3(1.0f, 0.0f, 0.0f);
        _time = 0.0f;
    }

    [Benchmark]
    public void SampleClip()
    {
        _time += 0.016f;
        if (_time >= 1.0f)
        {
            _time = 0.0f;
        }

        _clip.Sample(_time, _frame);
    }

    [Benchmark]
    public void BlendFrames()
    {
        BlendingKernels.BlendFrame(_frame, _base, _overlay, 0.5f);
    }
}
