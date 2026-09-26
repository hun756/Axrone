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
        rig.FindBoneIndex("tip").Should().Be(new BoneHandle(2));
        rig.FindBoneIndex("tip").Index.Should().Be(2);
        rig.FindBoneIndex("ghost").Should().Be(BoneHandle.Invalid);
        BoneHandle root = rig.FindBoneIndex("root");
        rig.GetChildren(root).ToArray().Should().Equal(1);
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
    public void EvaluatePose_ComposesLivePoseToPalette()
    {
        var rig = new Rig(new RigId("chain"), Chain());
        var locals = new LocalTransform[3];
        locals[0] = LocalTransform.Identity;
        locals[1] = new LocalTransform(new Vector3(0.0f, 1.0f, 0.0f), Quaternion.Identity, Vector3.One);
        locals[2] = new LocalTransform(new Vector3(0.0f, 1.0f, 0.0f), Quaternion.Identity, Vector3.One);

        var palette = new Matrix4x4[3];
        rig.EvaluatePose(locals, palette);

        // Column-major lane: translation rides M14/M24/M34.
        palette[0].M24.Should().BeApproximately(0.0f, 1e-6f);
        palette[1].M24.Should().BeApproximately(1.0f, 1e-6f);
        palette[2].M24.Should().BeApproximately(2.0f, 1e-6f);

        Action shortLocals = () => rig.EvaluatePose(Array.Empty<LocalTransform>(), palette);
        shortLocals.Should().Throw<ValidationException>()
            .Where(ex => ex.Code == AnimationErrorCode.SamplingOutOfBounds);
    }

    [Fact]
    public void LocalTransform_SanitizesInConstructor()
    {
        var degenerate = new LocalTransform(Vector3.Zero, new Quaternion(0, 0, 0, 0), Vector3.Zero);
        degenerate.Rotation.Should().Be(Quaternion.Identity);
        degenerate.Scale.Should().Be(Vector3.One);
    }

    private struct TranslationBoundsVisitor : IRigPaletteVisitor<BoundsAccumulator>
    {
        public void Visit(ref BoundsAccumulator context, ReadOnlySpan<Matrix4x4> worldMatrices)
        {
            for (int i = 0; i < worldMatrices.Length; i++)
            {
                Vector3 t = new(worldMatrices[i].M14, worldMatrices[i].M24, worldMatrices[i].M34);
                context.Min = Vector3.Min(context.Min, t);
                context.Max = Vector3.Max(context.Max, t);
            }
        }
    }

    private struct BoundsAccumulator
    {
        public Vector3 Min;
        public Vector3 Max;
    }

    [Fact]
    public void AcceptPalette_RunsZeroAllocVisitor()
    {
        var rig = new Rig(new RigId("chain"), Chain());
        var bounds = new BoundsAccumulator { Min = new Vector3(float.MaxValue), Max = new Vector3(float.MinValue) };
        rig.AcceptPalette<TranslationBoundsVisitor, BoundsAccumulator>(ref bounds);

        bounds.Min.Should().Be(new Vector3(0.0f, 0.0f, 0.0f));
        bounds.Max.Should().Be(new Vector3(0.0f, 2.0f, 0.0f));
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
