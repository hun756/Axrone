namespace Axrone.Utility.Tests.Builders;

using System.Diagnostics.CodeAnalysis;
using Axrone.Utility.Builders;

public class BuilderBaseTests
{
    private sealed class StubProduct
    {
        public int Value;
    }

    private sealed class StubBuilder : BuilderBase<StubBuilder, StubProduct>
    {
        private int _value;
        private bool _hasValue;

        protected override StubBuilder Self => this;

        public StubBuilder WithValue(int value)
        {
            _value = value;
            _hasValue = true;
            return this;
        }

        public override bool TryBuild([MaybeNullWhen(false)] out StubProduct result, out BuilderDiagnostic diagnostic)
        {
            if (!_hasValue)
            {
                result = default!;
                diagnostic = BuilderDiagnostic.Fail(BuilderStatusCode.MissingRequiredField, "Value is required.");
                return false;
            }

            result = new StubProduct { Value = _value };
            diagnostic = BuilderDiagnostic.Ok;
            return true;
        }
    }

    private sealed class GuardedBuilder : BuilderBase<GuardedBuilder, StubProduct>
    {
        private readonly int _value;

        public GuardedBuilder(int value) => _value = value;

        protected override GuardedBuilder Self => this;

        public override bool TryBuild([MaybeNullWhen(false)] out StubProduct result, out BuilderDiagnostic diagnostic) =>
            TryCreate(Build, out result, out diagnostic);

        public override StubProduct Build() =>
            _value > 0 ? new StubProduct { Value = _value } : throw new ArgumentOutOfRangeException(nameof(_value));
    }

    [Fact]
    public void TryBuild_MissingField_ReportsDiagnostic()
    {
        var builder = new StubBuilder();

        builder.TryBuild(out StubProduct? result, out BuilderDiagnostic diagnostic).Should().BeFalse();
        result.Should().BeNull();
        diagnostic.IsSuccess.Should().BeFalse();
        diagnostic.Code.Should().Be(BuilderStatusCode.MissingRequiredField);
    }

    [Fact]
    public void Build_MissingField_ThrowsWithDiagnostic()
    {
        var act = () => new StubBuilder().Build();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MissingRequiredField*Value is required*");
    }

    [Fact]
    public void FluentChain_ConfiguresProduct()
    {
        var product = new StubBuilder()
            .WithValue(41)
            .When(true, b => b.WithValue(42))
            .Unless(true, b => b.WithValue(0))
            .Tap(b => b.Should().NotBeNull())
            .Build();

        product.Value.Should().Be(42);
    }

    [Fact]
    public void When_False_SkipsConfiguration()
    {
        var product = new StubBuilder()
            .When(false, b => b.WithValue(1))
            .WithValue(7)
            .Build();

        product.Value.Should().Be(7);
    }

    [Fact]
    public void TryCreate_Adapter_MapsGuardToDiagnostic()
    {
        var builder = new GuardedBuilder(-1);

        builder.TryBuild(out StubProduct? result, out BuilderDiagnostic diagnostic).Should().BeFalse();
        result.Should().BeNull();
        diagnostic.Code.Should().Be(BuilderStatusCode.ValidationFailed);
        diagnostic.Message.Should().Contain("ArgumentOutOfRangeException");
    }

    [Fact]
    public void Override_PreservesLegacyThrowContract()
    {
        var act = () => new GuardedBuilder(-1).Build();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TryCreate_Success_ReportsOk()
    {
        var builder = new GuardedBuilder(5);

        builder.TryBuild(out StubProduct? result, out BuilderDiagnostic diagnostic).Should().BeTrue();
        result.Should().NotBeNull();
        result!.Value.Should().Be(5);
        diagnostic.Should().Be(BuilderDiagnostic.Ok);
    }
}
