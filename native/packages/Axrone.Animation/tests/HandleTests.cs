namespace Axrone.Animation.Tests;

using Xunit;
using FluentAssertions;

public class HandleTests
{
    private static ParameterStore CreateParameters() =>
        new(new Dictionary<string, ParameterType>
        {
            ["speed"] = ParameterType.Float,
            ["count"] = ParameterType.Int,
            ["grounded"] = ParameterType.Bool,
        });

    private static CurveStore CreateCurves() =>
        new(new Dictionary<CurveId, int> { [new CurveId("morph")] = 0, [new CurveId("blink")] = 1 });

    [Fact]
    public void ParameterHandle_ResolvesAndReadsWithoutLookup()
    {
        ParameterStore store = CreateParameters();
        ParameterHandle speed = store.ResolveHandle("speed");
        speed.IsValid.Should().BeTrue();

        store.SetFloat(in speed, 3.5f);
        store.GetFloat(in speed).Should().Be(3.5f);

        ParameterHandle count = store.ResolveHandle("count");
        store.SetInt(in count, 7);
        store.GetInt(in count).Should().Be(7);

        ParameterHandle grounded = store.ResolveHandle("grounded");
        store.SetBool(in grounded, true);
        store.GetBool(in grounded).Should().BeTrue();
    }

    [Fact]
    public void ParameterHandle_MissingNameThrowsCodedError()
    {
        ParameterStore store = CreateParameters();
        Action resolve = () => store.ResolveHandle("nope");
        resolve.Should().Throw<StateMachineException>()
            .Where(ex => ex.Code == AnimationErrorCode.StateMachineParameterNotFound);

        store.TryResolveHandle("nope", out ParameterHandle handle).Should().BeFalse();
        handle.Should().Be(ParameterHandle.Invalid);
    }

    [Fact]
    public void ParameterHandle_OutOfRangeFailsClosed()
    {
        ParameterStore store = CreateParameters();
        var foreign = new ParameterHandle(999);
        Action read = () => store.GetFloat(in foreign);
        read.Should().Throw<ValidationException>()
            .Where(ex => ex.Code == AnimationErrorCode.ValidationInvalidArgument);

        Action invalid = () => store.GetFloat(in ParameterHandle.Invalid);
        invalid.Should().Throw<ValidationException>()
            .Where(ex => ex.Code == AnimationErrorCode.ValidationInvalidArgument);
    }

    [Fact]
    public void CurveHandle_ResolvesAndReadsWithoutLookup()
    {
        CurveStore store = CreateCurves();
        CurveHandle morph = store.ResolveHandle(new CurveId("morph"));
        morph.IsValid.Should().BeTrue();

        store.Write(in morph, 0.75f);
        store.Read(in morph).Should().Be(0.75f);
    }

    [Fact]
    public void CurveHandle_MissingIdThrowsCurveCode()
    {
        CurveStore store = CreateCurves();
        Action resolve = () => store.ResolveHandle(new CurveId("nope"));
        resolve.Should().Throw<ResolutionException>()
            .Where(ex => ex.Code == AnimationErrorCode.ResolutionCurveNotFound);

        Action legacy = () => store.Read(new CurveId("nope"));
        legacy.Should().Throw<ResolutionException>()
            .Where(ex => ex.Code == AnimationErrorCode.ResolutionCurveNotFound);
    }

    [Fact]
    public void CurveHandle_OutOfRangeFailsClosed()
    {
        CurveStore store = CreateCurves();
        var foreign = new CurveHandle(999);
        Action write = () => store.Write(in foreign, 1.0f);
        write.Should().Throw<ValidationException>()
            .Where(ex => ex.Code == AnimationErrorCode.ValidationInvalidArgument);
    }
}
