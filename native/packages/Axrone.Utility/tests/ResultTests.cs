using Axrone.Utility.Result;

namespace Axrone.Utility.Tests.Result;

public class ResultTests
{
    [Fact]
    public void Ok_IsOk_ReturnsTrue()
    {
        var result = ResultModule.Ok<int, string>(42);
        result.IsOk.Should().BeTrue();
        result.IsErr.Should().BeFalse();
    }

    [Fact]
    public void Err_IsErr_ReturnsTrue()
    {
        var result = ResultModule.Err<int, string>("fail");
        result.IsErr.Should().BeTrue();
        result.IsOk.Should().BeFalse();
    }

    [Fact]
    public void Unwrap_Ok_ReturnsValue()
    {
        var result = ResultModule.Ok<int, string>(42);
        result.Unwrap().Should().Be(42);
    }

    [Fact]
    public void Unwrap_Err_ThrowsResultException()
    {
        var result = ResultModule.Err<int, string>("fail");
        var act = () => result.Unwrap();
        act.Should().Throw<ResultException>();
    }

    [Fact]
    public void UnwrapOr_Ok_ReturnsValue()
    {
        var result = ResultModule.Ok<int, string>(42);
        result.UnwrapOr(0).Should().Be(42);
    }

    [Fact]
    public void UnwrapOr_Err_ReturnsDefault()
    {
        var result = ResultModule.Err<int, string>("fail");
        result.UnwrapOr(99).Should().Be(99);
    }

    [Fact]
    public void Map_Ok_TransformsValue()
    {
        var result = ResultModule.Ok<int, string>(42);
        var mapped = result.Map(v => v.ToString());
        mapped.Unwrap().Should().Be("42");
    }

    [Fact]
    public void Map_Err_PropagatesError()
    {
        var result = ResultModule.Err<int, string>("fail");
        var mapped = result.Map(v => v.ToString());
        mapped.IsErr.Should().BeTrue();
    }

    [Fact]
    public void AndThen_Ok_ChainsOperation()
    {
        var result = ResultModule.Ok<int, string>(42);
        var chained = result.AndThen(v =>
            v > 0 ? ResultModule.Ok<string, string>($"pos:{v}") : ResultModule.Err<string, string>("neg"));
        chained.Unwrap().Should().Be("pos:42");
    }

    [Fact]
    public void AndThen_Err_ShortCircuits()
    {
        var result = ResultModule.Err<int, string>("fail");
        var chained = result.AndThen(v => ResultModule.Ok<string, string>($"val:{v}"));
        chained.IsErr.Should().BeTrue();
    }

    [Fact]
    public void Fold_Ok_CallsOnOk()
    {
        var result = ResultModule.Ok<int, string>(42);
        var folded = result.Fold(v => $"ok:{v}", e => $"err:{e}");
        folded.Should().Be("ok:42");
    }

    [Fact]
    public void Fold_Err_CallsOnErr()
    {
        var result = ResultModule.Err<int, string>("fail");
        var folded = result.Fold(v => $"ok:{v}", e => $"err:{e}");
        folded.Should().Be("err:fail");
    }

    [Fact]
    public void Try_Success_ReturnsOk()
    {
        var result = ResultModule.Try(() => 42);
        result.IsOk.Should().BeTrue();
        result.Unwrap().Should().Be(42);
    }

    [Fact]
    public void Try_Exception_ReturnsErr()
    {
        var result = ResultModule.Try<int>(() => throw new InvalidOperationException("boom"));
        result.IsErr.Should().BeTrue();
    }

    [Fact]
    public void Tap_Ok_ExecutesSideEffect()
    {
        var sideEffect = 0;
        var result = ResultModule.Ok<int, string>(42);
        var returned = result.Tap(v => sideEffect = v);
        sideEffect.Should().Be(42);
        returned.Should().BeSameAs(result);
    }

    [Fact]
    public void Tap_Err_DoesNotExecuteSideEffect()
    {
        var sideEffect = 0;
        var result = ResultModule.Err<int, string>("fail");
        var returned = result.Tap(v => sideEffect = v);
        sideEffect.Should().Be(0);
        returned.Should().BeSameAs(result);
    }

    [Fact]
    public void Equals_SameOkValues_AreEqual()
    {
        var a = ResultModule.Ok<int, string>(42);
        var b = ResultModule.Ok<int, string>(42);
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentOkValues_AreNotEqual()
    {
        var a = ResultModule.Ok<int, string>(42);
        var b = ResultModule.Ok<int, string>(99);
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_OkAndErr_AreNotEqual()
    {
        var a = ResultModule.Ok<int, int>(42);
        var b = ResultModule.Err<int, int>(42);
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Expect_Ok_ReturnsValue()
    {
        var result = ResultModule.Ok<int, string>(42);
        result.Expect("should not fail").Should().Be(42);
    }

    [Fact]
    public void Expect_Err_ThrowsWithMessage()
    {
        var result = ResultModule.Err<int, string>("fail");
        var act = () => result.Expect("custom message");
        act.Should().Throw<ResultException>().WithMessage("custom message");
    }

    [Fact]
    public void OrElse_Err_RecoverWithAlternative()
    {
        var result = ResultModule.Err<int, string>("fail");
        var recovered = result.OrElse(e => ResultModule.Ok<int, int>(e.Length));
        recovered.IsOk.Should().BeTrue();
        recovered.Unwrap().Should().Be(4);
    }

    [Fact]
    public void MapErr_Err_TransformsError()
    {
        var result = ResultModule.Err<int, string>("fail");
        var mapped = result.MapErr(e => e.Length);
        mapped.IsErr.Should().BeTrue();
    }

    [Fact]
    public void ToTuple_Ok_ReturnsSuccessTrue()
    {
        var result = ResultModule.Ok<int, string>(42);
        var (success, value) = result.ToTuple();
        success.Should().BeTrue();
        value.Should().Be(42);
    }

    [Fact]
    public void ToTuple_Err_ReturnsSuccessFalse()
    {
        var result = ResultModule.Err<int, string>("fail");
        var (success, _) = result.ToTuple();
        success.Should().BeFalse();
    }
}
