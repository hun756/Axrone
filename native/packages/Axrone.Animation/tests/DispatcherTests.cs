namespace Axrone.Animation.Tests;

using Xunit;
using FluentAssertions;

public class DispatcherTests
{
    private static readonly BoneInfo[] s_bones = new BoneInfo[]
    {
        new() { Name = "root", ParentIndex = -1 },
    };

    private static readonly float[] s_times = new float[] { 0.0f, 1.0f };
    private static readonly float[] s_values = new float[] { 0, 0, 0, 1, 0, 0 };

    private static (Rig Rig, ParameterStore Parameters, AnimationFrame Frame, FrameArena Arena) CreateWorld()
    {
        var rig = new Rig(new RigId("r"), s_bones.AsSpan());
        var parameters = new ParameterStore(new Dictionary<string, ParameterType> { ["w"] = ParameterType.Float });
        var layout = new Dictionary<CurveId, int>();
        var frame = new AnimationFrame(1, layout);
        var arena = new FrameArena(1, layout, 8);
        return (rig, parameters, frame, arena);
    }

    private static AnimationClip CreateClip() =>
        new(new ClipId("c"), 1.0f, new AnimationChannel[]
        {
            new(0, ChannelTarget.Translation, InterpolationMode.Linear, s_times, s_values),
        });

    [Fact]
    public void Dispatcher_RoutesAllKindsWithoutThrowing()
    {
        var (rig, parameters, frame, arena) = CreateWorld();
        MotionNode[] nodes = new MotionNode[]
        {
            new ClipMotionNode(CreateClip()),
            new Blend1DMotionNode("w", new (float, MotionNode)[] { (0.0f, new ClipMotionNode(CreateClip())) }),
            new Blend2DMotionNode("w", "w", new (Vector2, MotionNode)[] { (Vector2.Zero, new ClipMotionNode(CreateClip())) }),
            new DirectMotionNode(new (string, MotionNode)[] { ("w", new ClipMotionNode(CreateClip())) }),
            new AdditiveMotionNode(new ClipMotionNode(CreateClip()), new ClipMotionNode(CreateClip()), "w"),
        };

        foreach (MotionNode node in nodes)
        {
            MotionDispatcher.GetDuration(node).Should().BeGreaterThanOrEqualTo(0.0f);
            Action evaluate = () => MotionDispatcher.Evaluate(node, 0.5f, frame, arena, rig, parameters, 0);
            evaluate.Should().NotThrow();
            Action events = () => MotionDispatcher.CollectEvents(node, 0.0f, 0.5f, 1.0f, new List<ClipEvent>());
            events.Should().NotThrow();
            Action delta = () => MotionDispatcher.ComputeRootDelta(node, 0.0f, 0.5f, rig, out _, out _);
            delta.Should().NotThrow();
            arena.Reset();
        }
    }
}
