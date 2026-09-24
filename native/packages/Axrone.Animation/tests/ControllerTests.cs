namespace Axrone.Animation.Tests;

public class ControllerTests
{
    private static Rig TwoBone() => new(new RigId("r"), [
        new BoneInfo { Name = "root", ParentIndex = -1 },
        new BoneInfo { Name = "child", ParentIndex = 0 },
    ]);

    private static Dictionary<string, ParameterType> Params() => new()
    {
        ["go"] = ParameterType.Trigger,
    };

    private static Dictionary<CurveId, int> NoCurves() => new();

    private static AnimationClip SlideClip(float toX) => new(
        new ClipId($"slide-{toX}"), 1.0f,
        [new AnimationChannel(1, ChannelTarget.Translation, InterpolationMode.Linear, [0.0f, 1.0f], [0.0f, 0.0f, 0.0f, toX, 0.0f, 0.0f])]);

    private static AnimationController BaseController(Rig rig, ParameterStore parameters)
    {
        var idle = new AnimationState(new StateId("idle"), new ClipMotionNode(SlideClip(0.0f)));
        var machine = new StateMachineInstance([idle], [], 0);
        var controller = new AnimationController(rig, parameters, NoCurves());
        controller.AddLayer(new AnimationLayer(new LayerId("base"), machine));
        return controller;
    }

    [Fact]
    public void BaseLayer_DrivesCurrentFrame()
    {
        Rig rig = TwoBone();
        var parameters = new ParameterStore(Params());
        using var controller = BaseController(rig, parameters);
        var events = new List<ClipEvent>();

        controller.Update(0.5f, events, out Vector3 rootPos, out _);
        controller.CurrentFrame.ReadTranslations()[1].X.Should().Be(0.0f);
        rootPos.Should().Be(Vector3.Zero);
    }

    [Fact]
    public void OverrideLayer_BlendsOnTop()
    {
        Rig rig = TwoBone();
        var parameters = new ParameterStore(Params());
        using var controller = BaseController(rig, parameters);

        var overlay = new AnimationState(new StateId("over"), new ClipMotionNode(SlideClip(10.0f)));
        var overlayMachine = new StateMachineInstance([overlay], [], 0);
        controller.AddLayer(new AnimationLayer(new LayerId("upper"), overlayMachine) { Weight = 0.5f });

        var events = new List<ClipEvent>();
        controller.Update(0.5f, events, out _, out _);
        controller.CurrentFrame.ReadTranslations()[1].X.Should().BeApproximately(2.5f, 1e-4f);
    }

    [Fact]
    public void AdditiveLayer_AddsRestDelta()
    {
        Rig rig = TwoBone();
        var parameters = new ParameterStore(Params());
        using var controller = BaseController(rig, parameters);

        var additive = new AnimationState(new StateId("add"), new ClipMotionNode(SlideClip(4.0f)));
        var additiveMachine = new StateMachineInstance([additive], [], 0);
        controller.AddLayer(new AnimationLayer(new LayerId("add"), additiveMachine) { Mode = LayerMode.Additive, Weight = 1.0f });

        var events = new List<ClipEvent>();
        controller.Update(0.5f, events, out _, out _);
        controller.CurrentFrame.ReadTranslations()[1].X.Should().BeApproximately(2.0f, 1e-4f);
    }

    [Fact]
    public void EmptyController_RestsWithoutLayers()
    {
        Rig rig = TwoBone();
        var parameters = new ParameterStore(Params());
        using var controller = new AnimationController(rig, parameters, NoCurves());
        var events = new List<ClipEvent>();

        controller.Update(1.0f, events, out Vector3 rootPos, out Quaternion rootRot);
        controller.CurrentFrame.ReadTranslations()[1].Should().Be(Vector3.Zero);
        rootPos.Should().Be(Vector3.Zero);
        rootRot.Should().Be(Quaternion.Identity);
    }

    [Fact]
    public void RootMotion_ExtractedFromBase()
    {
        var rig = new Rig(new RigId("r"), [
            new BoneInfo { Name = "root", ParentIndex = -1 },
            new BoneInfo { Name = "child", ParentIndex = 0 },
        ]);
        var clip = new AnimationClip(
            new ClipId("move"), 1.0f,
            [new AnimationChannel(0, ChannelTarget.Translation, InterpolationMode.Linear, [0.0f, 1.0f], [0.0f, 0.0f, 0.0f, 3.0f, 0.0f, 0.0f])]);
        var state = new AnimationState(new StateId("move"), new ClipMotionNode(clip));
        var machine = new StateMachineInstance([state], [], 0);
        var parameters = new ParameterStore(Params());
        using var controller = new AnimationController(rig, parameters, NoCurves());
        controller.AddLayer(new AnimationLayer(new LayerId("base"), machine));

        var events = new List<ClipEvent>();
        controller.Update(0.5f, events, out Vector3 rootPos, out _);
        rootPos.X.Should().BeApproximately(1.5f, 1e-4f);
    }
}
