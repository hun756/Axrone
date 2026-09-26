namespace Axrone.Animation.Tests;

public class StateMachineTests
{
    private static Rig TwoBone() => new(new RigId("r"), [
        new BoneInfo { Name = "root", ParentIndex = -1 },
        new BoneInfo { Name = "child", ParentIndex = 0 },
    ]);

    private static Dictionary<CurveId, int> NoCurves() => new();

    private static Dictionary<string, ParameterType> Params() => new()
    {
        ["go"] = ParameterType.Trigger,
        ["speed"] = ParameterType.Float,
    };

    private static AnimationClip SlideClip(float toX) => new(
        new ClipId($"slide-{toX}"), 1.0f,
        [new AnimationChannel(1, ChannelTarget.Translation, InterpolationMode.Linear, [0.0f, 1.0f], [0.0f, 0.0f, 0.0f, toX, 0.0f, 0.0f])]);

    private static StateMachineInstance TwoState(out ParameterStore parameters)
    {
        parameters = new ParameterStore(Params());
        var idle = new AnimationState(new StateId("idle"), new ClipMotionNode(SlideClip(0.0f)));
        var run = new AnimationState(new StateId("run"), new ClipMotionNode(SlideClip(10.0f)));
        var transition = new StateTransition(1, 0.5f, [new ParameterCondition("go", ConditionOperator.Equal, 0.0f)]);
        idle = new AnimationState(new StateId("idle"), new ClipMotionNode(SlideClip(0.0f)), [transition]);
        return new StateMachineInstance([idle, run], [], 0);
    }

    private static readonly float[] s_halfTimes = new float[] { 0.0f, 1.0f };
    private static readonly float[] s_staticValues = new float[] { 0, 0, 0, 0, 0, 0 };
    private static readonly float[] s_movingValues = new float[] { 0, 0, 0, 2, 0, 0 };

    [Fact]
    public void RootDelta_MixesSourceAndTargetMidBlend()
    {
        var rig = new Rig(new RigId("r"), new BoneInfo[] { new() { Name = "root", ParentIndex = -1 } });
        var parameters = new ParameterStore(Params());
        var staticClip = new AnimationClip(
            new ClipId("still"), 1.0f,
            new AnimationChannel[] { new(0, ChannelTarget.Translation, InterpolationMode.Linear, s_halfTimes, s_staticValues) });
        var movingClip = new AnimationClip(
            new ClipId("move"), 1.0f,
            new AnimationChannel[] { new(0, ChannelTarget.Translation, InterpolationMode.Linear, s_halfTimes, s_movingValues) });
        var machine = new StateMachineInstance(
            new AnimationState[]
            {
                new(new StateId("still"), new ClipMotionNode(staticClip)),
                new(new StateId("move"), new ClipMotionNode(movingClip)),
            },
            Array.Empty<StateTransition>(), 0);

        machine.CrossFade(1, duration: 1.0f);
        machine.HasActiveTransition.Should().BeTrue();
        machine.Update(0.5f, parameters, new List<ClipEvent>(), 1.0f);

        machine.ExtractRootDelta(rig, out Vector3 delta, out _);
        delta.X.Should().BeApproximately(0.5f, 1e-4f);
        delta.Y.Should().BeApproximately(0.0f, 1e-6f);
    }

    [Fact]
    public void EntryState_EvaluatesRest()
    {
        Rig rig = TwoBone();
        StateMachineInstance machine = TwoState(out ParameterStore parameters);
        var frame = new AnimationFrame(2, NoCurves());
        var arena = new FrameArena(2, NoCurves(), capacity: 8);
        machine.Evaluate(frame, arena, rig, parameters);
        frame.ReadTranslations()[1].X.Should().Be(0.0f);
    }

    [Fact]
    public void TriggerTransition_BlendsToTarget()
    {
        Rig rig = TwoBone();
        StateMachineInstance machine = TwoState(out ParameterStore parameters);
        var frame = new AnimationFrame(2, NoCurves());
        var arena = new FrameArena(2, NoCurves(), capacity: 8);
        var events = new List<ClipEvent>();

        parameters.SetTrigger("go");
        machine.Update(0.25f, parameters, events, 1.0f);
        machine.Evaluate(frame, arena, rig, parameters);
        float mid = frame.ReadTranslations()[1].X;
        mid.Should().BeInRange(0.0f, 10.0f);

        machine.Update(0.5f, parameters, events, 1.0f);
        machine.Evaluate(frame, arena, rig, parameters);
        machine.CurrentStateIndex.Should().Be(1);
    }

    [Fact]
    public void ForceState_SnapsWithoutBlend()
    {
        StateMachineInstance machine = TwoState(out _);
        machine.ForceState(1, 0.5f);
        machine.CurrentStateIndex.Should().Be(1);
        machine.StateNormalizedTime.Should().Be(0.5f);
    }

    [Fact]
    public void CrossFade_CompletesOverDuration()
    {
        Rig rig = TwoBone();
        StateMachineInstance machine = TwoState(out ParameterStore parameters);
        var frame = new AnimationFrame(2, NoCurves());
        var arena = new FrameArena(2, NoCurves(), capacity: 8);
        var events = new List<ClipEvent>();

        machine.CrossFade(1, 1.0f);
        machine.Update(0.5f, parameters, events, 1.0f);
        machine.CurrentStateIndex.Should().Be(0);
        machine.Evaluate(frame, arena, rig, parameters);

        machine.Update(0.5f, parameters, events, 1.0f);
        machine.CurrentStateIndex.Should().Be(1);
    }

    [Fact]
    public void ExitTime_GatesTransition()
    {
        var parameters = new ParameterStore(Params());
        parameters.SetFloat("speed", 5.0f);
        var idle = new AnimationState(
            new StateId("idle"),
            new ClipMotionNode(SlideClip(0.0f)),
            [new StateTransition(1, 0.1f, [new ParameterCondition("speed", ConditionOperator.GreaterThan, 1.0f)]) { ExitTime = 0.9f }]);
        var run = new AnimationState(new StateId("run"), new ClipMotionNode(SlideClip(10.0f)));
        var machine = new StateMachineInstance([idle, run], [], 0);
        var events = new List<ClipEvent>();

        machine.Update(0.5f, parameters, events, 1.0f);
        machine.CurrentStateIndex.Should().Be(0);

        machine.Update(0.5f, parameters, events, 1.0f);
        machine.CurrentStateIndex.Should().Be(0);
    }
}
