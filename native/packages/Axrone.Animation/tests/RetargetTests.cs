namespace Axrone.Animation.Tests;

public class RetargetTests
{
    private static Rig SourceRig() => new(new RigId("src"), [
        new BoneInfo { Name = "root", ParentIndex = -1 },
        new BoneInfo { Name = "arm", ParentIndex = 0, RestTranslation = new Vector3(1.0f, 0.0f, 0.0f) },
    ]);

    private static Rig ScaledTargetRig() => new(new RigId("dst"), [
        new BoneInfo { Name = "root", ParentIndex = -1 },
        new BoneInfo { Name = "arm", ParentIndex = 0, RestTranslation = new Vector3(2.0f, 0.0f, 0.0f) },
    ]);

    private static Dictionary<CurveId, int> NoCurves() => new();

    [Fact]
    public void NameMatching_ResolvesAllBones()
    {
        var profile = new RetargetProfile(SourceRig(), ScaledTargetRig());
        profile.SourceToTargetMap.ToArray().Should().Equal(0, 1);
    }

    [Fact]
    public void ExplicitMapping_PartialOk()
    {
        var profile = new RetargetProfile(SourceRig(), ScaledTargetRig(), [("arm", "arm")]);
        profile.SourceToTargetMap.ToArray().Should().Equal(-1, 1);
    }

    [Fact]
    public void Bindings_ContainMappedBonesOnly()
    {
        var full = new RetargetProfile(SourceRig(), ScaledTargetRig());
        full.Bindings.Length.Should().Be(2);

        var partial = new RetargetProfile(SourceRig(), ScaledTargetRig(), [("arm", "arm")]);
        partial.Bindings.Length.Should().Be(1);
        partial.Bindings[0].SourceIndex.Should().Be(1);
        partial.Bindings[0].TargetIndex.Should().Be(1);
        partial.Bindings[0].LengthRatio.Should().BeApproximately(2.0f, 1e-6f);
    }

    [Fact]
    public void AllModePairs_ExecuteWithoutThrowing()
    {
        Rig source = SourceRig();
        Rig target = ScaledTargetRig();
        var translationModes = new RetargetTranslationMode[]
        {
            RetargetTranslationMode.None,
            RetargetTranslationMode.Absolute,
            RetargetTranslationMode.Scaled,
        };
        var rotationModes = new RetargetRotationMode[]
        {
            RetargetRotationMode.Copy,
            RetargetRotationMode.Offset,
        };

        foreach (RetargetTranslationMode translation in translationModes)
        {
            foreach (RetargetRotationMode rotation in rotationModes)
            {
                var profile = new RetargetProfile(source, target)
                {
                    TranslationMode = translation,
                    RotationMode = rotation,
                };
                var sourceFrame = new AnimationFrame(2, NoCurves());
                var targetFrame = new AnimationFrame(2, NoCurves());
                Action retarget = () => profile.RetargetFrame(sourceFrame, targetFrame);
                retarget.Should().NotThrow();
            }
        }
    }

    [Fact]
    public void RetargetView_WorksOverStackLanes()
    {
        var profile = new RetargetProfile(SourceRig(), ScaledTargetRig());

        Span<Vector3> srcT = stackalloc Vector3[2];
        Span<Quaternion> srcR = stackalloc Quaternion[2];
        Span<Vector3> srcS = stackalloc Vector3[2];
        srcR[0] = Quaternion.Identity;
        srcR[1] = Quaternion.Identity;
        srcS[0] = Vector3.One;
        srcS[1] = Vector3.One;
        srcT[1] = new Vector3(3.0f, 0.0f, 0.0f);

        Span<Vector3> dstT = stackalloc Vector3[2];
        Span<Quaternion> dstR = stackalloc Quaternion[2];
        Span<Vector3> dstS = stackalloc Vector3[2];

        var source = new AnimationFrameView(srcT, srcR, srcS);
        var target = new AnimationFrameView(dstT, dstR, dstS);
        profile.RetargetView(in source, in target);

        dstT[1].X.Should().BeApproximately(6.0f, 1e-5f);
    }

    [Fact]
    public void ZeroMappings_Throw()
    {
        var other = new Rig(new RigId("other"), [
            new BoneInfo { Name = "x", ParentIndex = -1 },
        ]);
        Action build = () => { _ = new RetargetProfile(SourceRig(), other); };
        build.Should().Throw<RetargetingException>()
            .Where(e => e.Code == AnimationErrorCode.RetargetingNoMapping);
    }

    [Fact]
    public void ScaledMode_ScalesTranslationByRestRatio()
    {
        Rig source = SourceRig();
        Rig target = ScaledTargetRig();
        var profile = new RetargetProfile(source, target) { TranslationMode = RetargetTranslationMode.Scaled };

        var sourceFrame = new AnimationFrame(2, NoCurves());
        sourceFrame.GetTranslations()[1] = new Vector3(3.0f, 0.0f, 0.0f);
        sourceFrame.GetRotations()[1] = Quaternion.Identity;
        var targetFrame = new AnimationFrame(2, NoCurves());

        profile.RetargetFrame(sourceFrame, targetFrame);
        targetFrame.ReadTranslations()[1].X.Should().BeApproximately(6.0f, 1e-5f);
    }

    [Fact]
    public void CopyMode_PassesRotationThrough()
    {
        Rig source = SourceRig();
        Rig target = ScaledTargetRig();
        var profile = new RetargetProfile(source, target) { RotationMode = RetargetRotationMode.Copy };

        var sourceFrame = new AnimationFrame(2, NoCurves());
        var twisted = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1.0f);
        sourceFrame.GetRotations()[1] = twisted;
        var targetFrame = new AnimationFrame(2, NoCurves());

        profile.RetargetFrame(sourceFrame, targetFrame);
        Quaternion result = targetFrame.ReadRotations()[1];
        result.X.Should().BeApproximately(twisted.X, 1e-6f);
        result.W.Should().BeApproximately(twisted.W, 1e-6f);
    }
}
