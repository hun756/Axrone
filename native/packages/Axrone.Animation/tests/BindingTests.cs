namespace Axrone.Animation.Tests;

using Xunit;
using FluentAssertions;

public class BindingTests
{
    private static readonly BoneInfo[] s_bones = new BoneInfo[]
    {
        new() { Name = "root", ParentIndex = -1 },
        new() { Name = "spine", ParentIndex = 0 },
    };

    private static readonly float[] s_times = new float[] { 0.0f, 1.0f };
    private static readonly float[] s_singleTime = new float[] { 0.0f };
    private static readonly float[] s_singleTranslation = new float[] { 0, 0, 0 };
    private static readonly float[] s_translation = new float[] { 0, 0, 0, 1, 0, 0 };
    private static readonly float[] s_rotation = new float[] { 0, 0, 0, 1, 0, 0, 0, 1 };

    private static Rig CreateRig() => new(new RigId("hero"), s_bones.AsSpan());

    private static AnimationClip CreateClip() =>
        new(new ClipId("walk"), 1.0f, new AnimationChannel[]
        {
            new(0, ChannelTarget.Translation, InterpolationMode.Linear, s_times, s_translation),
            new(1, ChannelTarget.Rotation, InterpolationMode.Linear, s_times, s_rotation),
        });

    private static ParameterStore CreateParameters() =>
        new(new Dictionary<string, ParameterType> { ["speed"] = ParameterType.Float });

    private static AnimationController CreateController(Rig rig, ParameterStore parameters)
    {
        var machine = new StateMachineInstance(
            new AnimationState[]
            {
                new(new StateId("move"), new Blend1DMotionNode("speed", new (float, MotionNode)[]
                {
                    (0.0f, new ClipMotionNode(CreateClip())),
                    (1.0f, new ClipMotionNode(CreateClip())),
                })),
            },
            Array.Empty<StateTransition>(), 0);
        var controller = new AnimationController(rig, parameters, new Dictionary<CurveId, int>());
        controller.AddLayer(new AnimationLayer(new LayerId("base"), machine));
        return controller;
    }

    [Fact]
    public void BindAll_BoundAndUnboundEvaluateIdentically()
    {
        Rig rig = CreateRig();

        ParameterStore unboundParameters = CreateParameters();
        unboundParameters.SetFloat("speed", 0.7f);
        AnimationController unbound = CreateController(rig, unboundParameters);
        unbound.Update(0.016f, new List<ClipEvent>(), out Vector3 posUnbound, out Quaternion rotUnbound);

        ParameterStore boundParameters = CreateParameters();
        boundParameters.SetFloat("speed", 0.7f);
        AnimationController bound = CreateController(rig, boundParameters);
        bound.BindAll();
        bound.Update(0.016f, new List<ClipEvent>(), out Vector3 posBound, out Quaternion rotBound);

        posBound.Should().Be(posUnbound);
        rotBound.Should().Be(rotUnbound);
    }

    [Fact]
    public void Bind_MissingParameterThrowsCodedErrorAtBindTime()
    {
        var node = new Blend1DMotionNode("ghost", new (float, MotionNode)[]
        {
            (0.0f, new ClipMotionNode(CreateClip())),
        });
        ParameterStore parameters = CreateParameters();

        Action bind = () => node.Bind(new MotionBindingContext(parameters, null, null));
        bind.Should().Throw<StateMachineException>()
            .Where(ex => ex.Code == AnimationErrorCode.StateMachineParameterNotFound);
    }

    [Fact]
    public void Bind_ValidatesBoneRangeAtBindTime()
    {
        Rig rig = CreateRig();
        var clip = new AnimationClip(new ClipId("bad"), 1.0f, new AnimationChannel[]
        {
            new(99, ChannelTarget.Translation, InterpolationMode.Linear, s_singleTime, s_singleTranslation),
        });

        Action bind = () => clip.Bind(new MotionBindingContext(null, null, rig));
        bind.Should().Throw<ValidationException>()
            .Where(ex => ex.Code == AnimationErrorCode.ValidationClipMismatch);
    }

    [Fact]
    public void Update_AutoBindsParametersWithoutExplicitBind()
    {
        ParameterStore parameters = CreateParameters();
        var machine = new StateMachineInstance(
            new AnimationState[] { new(new StateId("move"), new ClipMotionNode(CreateClip())) },
            new StateTransition[]
            {
                new(0, 0.1f, new ParameterCondition[] { new("speed", ConditionOperator.GreaterThan, 5.0f) }),
            }, 0);

        var events = new List<ClipEvent>();
        machine.Update(0.016f, parameters, events, 1.0f);

        Action update = () => machine.Update(0.016f, parameters, events, 1.0f);
        update.Should().NotThrow();
    }
}
