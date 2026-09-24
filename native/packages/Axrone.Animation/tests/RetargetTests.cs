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
