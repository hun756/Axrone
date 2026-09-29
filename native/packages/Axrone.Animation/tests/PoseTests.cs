namespace Axrone.Animation.Tests;

public class PoseTests
{
    private static Dictionary<CurveId, int> Layout() => new()
    {
        [new CurveId("weight")] = 0,
        [new CurveId("mood")] = 1,
    };

    [Fact]
    public void Mask_DefaultIncludesOutOfRangeExcludes()
    {
        AnimationMask @default = default;
        @default.IsEnabled(0).Should().BeTrue();
        @default.IsEnabled(999).Should().BeTrue();

        var mask = new AnimationMask(65);
        mask.IsEnabled(0).Should().BeTrue();
        mask.IsEnabled(64).Should().BeTrue();
        mask.IsEnabled(65).Should().BeFalse();

        mask.Set(1, false);
        mask.IsEnabled(1).Should().BeFalse();
        mask.IsEnabled(0).Should().BeTrue();
        mask.Set(65, true);
    }

    [Fact]
    public void Curves_ReadWriteCopy()
    {
        var store = new CurveStore(Layout());
        store.Write(new CurveId("weight"), 0.5f);
        store.Read(new CurveId("weight")).Should().Be(0.5f);

        Action missing = () => store.Read(new CurveId("ghost"));
        missing.Should().Throw<ResolutionException>()
            .Where(ex => ex.Code == AnimationErrorCode.ResolutionCurveNotFound);

        var other = new CurveStore(Layout());
        other.CopyFrom(store);
        other.Read(new CurveId("weight")).Should().Be(0.5f);
    }

    [Fact]
    public void Frame_LaneAccessCopyReset()
    {
        var rig = new Rig(new RigId("r"), [
            new BoneInfo { Name = "root", ParentIndex = -1 },
            new BoneInfo { Name = "child", ParentIndex = 0 },
        ]);
        var frame = new AnimationFrame(2, Layout());
        frame.GetTranslations()[1] = new Vector3(1.0f, 2.0f, 3.0f);
        frame.GetRotations()[0] = new Quaternion(0.0f, 0.0f, 0.0f, 1.0f);

        var copy = new AnimationFrame(2, Layout());
        copy.CopyFrom(frame);
        copy.ReadTranslations()[1].Should().Be(new Vector3(1.0f, 2.0f, 3.0f));

        copy.ResetToRest(rig);
        copy.ReadTranslations()[1].Should().Be(Vector3.Zero);
        copy.ReadScales()[1].Should().Be(Vector3.One);
    }

    [Fact]
    public void Arena_RentsLifoWithinCapacity()
    {
        var arena = new FrameArena(2, Layout(), capacity: 2);
        AnimationFrame first = arena.Alloc();
        AnimationFrame second = arena.Alloc();
        Action overflow = () => arena.Alloc();
        overflow.Should().Throw<EvaluationException>();

        arena.Free();
        arena.Alloc().Should().BeSameAs(second);
        first.Should().NotBeSameAs(second);

        arena.Reset();
        arena.Alloc().Should().BeSameAs(first);
    }
}
