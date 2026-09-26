namespace Axrone.Animation.Tests;

public class RigTests
{
    private static BoneInfo[] Chain() =>
    [
        new() { Name = "root", ParentIndex = -1 },
        new() { Name = "mid", ParentIndex = 0, RestTranslation = new Vector3(0.0f, 1.0f, 0.0f) },
        new() { Name = "tip", ParentIndex = 1, RestTranslation = new Vector3(0.0f, 1.0f, 0.0f) },
    ];

    [Fact]
    public void ValidRig_BuildsParentFirstOrder()
    {
        var rig = new Rig(new RigId("chain"), Chain());
        rig.BoneCount.Should().Be(3);
        rig.EvaluationOrder[0].Should().Be(0);
        rig.EvaluationOrder.ToArray().Should().Equal(0, 1, 2);
        rig.RootIndices.ToArray().Should().Equal(0);
        rig.GetChildren(0).ToArray().Should().Equal(1);
        rig.GetChildren(2).ToArray().Should().BeEmpty();
        rig.FindBoneIndex("tip").Should().Be(2);
        rig.FindBoneIndex("ghost").Should().Be(-1);
    }

    [Fact]
    public void RestPalette_MatchesLiveKinematics()
    {
        var rig = new Rig(new RigId("chain"), Chain());
        Span<float> palette = stackalloc float[3 * 16];
        rig.CreateRestMatrixPalette(palette);

        // Chain: root at origin, mid/tip offset +1Y each with identity rotation/scale.
        // ComposeTransformMatrix layout carries translation in elements 3/7/11.
        palette[3].Should().BeApproximately(0.0f, 1e-6f);
        palette[7].Should().BeApproximately(0.0f, 1e-6f);
        palette[11].Should().BeApproximately(0.0f, 1e-6f);
        palette[16 + 7].Should().BeApproximately(1.0f, 1e-6f);
        palette[32 + 7].Should().BeApproximately(2.0f, 1e-6f);

        // RestWorldMatrices view exposes the same bytes without copying.
        rig.RestWorldMatrices.Length.Should().Be(3 * 16);
        rig.RestWorldMatrices[32 + 7].Should().BeApproximately(2.0f, 1e-6f);
    }

    [Fact]
    public void EmptyRig_Throws()
    {
        Action build = () => { _ = new Rig(new RigId("empty"), []); };
        build.Should().Throw<ValidationException>()
            .Where(e => e.Code == AnimationErrorCode.ValidationRigEmptyBones);
    }

    [Fact]
    public void DuplicateNames_Throw()
    {
        BoneInfo[] bones =
        [
            new() { Name = "a", ParentIndex = -1 },
            new() { Name = "a", ParentIndex = 0 },
        ];
        Action build = () => { _ = new Rig(new RigId("dup"), bones); };
        build.Should().Throw<ValidationException>()
            .Where(e => e.Code == AnimationErrorCode.ValidationRigDuplicateBoneName);
    }

    [Fact]
    public void InvalidParents_Throw()
    {
        BoneInfo[] selfParent =
        [
            new() { Name = "a", ParentIndex = -1 },
            new() { Name = "b", ParentIndex = 1 },
        ];
        Action self = () => { _ = new Rig(new RigId("self"), selfParent); };
        self.Should().Throw<ValidationException>();

        BoneInfo[] outOfRange =
        [
            new() { Name = "a", ParentIndex = -1 },
            new() { Name = "b", ParentIndex = 7 },
        ];
        Action range = () => { _ = new Rig(new RigId("range"), outOfRange); };
        range.Should().Throw<ValidationException>()
            .Where(e => e.Code == AnimationErrorCode.ValidationRigInvalidParent);
    }

    [Fact]
    public void Cycle_Throws()
    {
        BoneInfo[] bones =
        [
            new() { Name = "a", ParentIndex = 2 },
            new() { Name = "b", ParentIndex = 0 },
            new() { Name = "c", ParentIndex = 1 },
        ];
        Action build = () => { _ = new Rig(new RigId("cycle"), bones); };
        build.Should().Throw<ValidationException>()
            .Where(e => e.Code == AnimationErrorCode.ValidationRigCycleDetected
                || e.Code == AnimationErrorCode.ValidationRigInvalidParent);
    }

    [Fact]
    public void RestPalette_ComposesChainWorldMatrices()
    {
        var rig = new Rig(new RigId("chain"), Chain());
        Span<float> palette = stackalloc float[3 * 16];
        rig.CreateRestMatrixPalette(palette);

        palette[3].Should().BeApproximately(0.0f, 1e-6f);
        palette[7].Should().BeApproximately(0.0f, 1e-6f);
        palette[16 + 7].Should().BeApproximately(1.0f, 1e-6f);
        palette[32 + 7].Should().BeApproximately(2.0f, 1e-6f);
        palette[15].Should().Be(1.0f);
    }

    [Fact]
    public void RestPose_SanitizesDegenerateDefaults()
    {
        BoneInfo[] bones =
        [
            new() { Name = "a", ParentIndex = -1, RestRotation = default, RestScale = Vector3.Zero },
        ];
        var rig = new Rig(new RigId("sanitize"), bones);
        rig.RestPose[3].Should().Be(0.0f);
        rig.RestPose[6].Should().Be(1.0f);
        rig.RestPose[7].Should().Be(1.0f);
        rig.RestPose[9].Should().Be(1.0f);
    }
}
