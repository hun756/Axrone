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

public class AggregateBuilderTests
{
    private static class StubSlots
    {
        public const int Id = 0;
        public const int Capacity = 1;

        public static PropertyBitmask64 RequiredMask => new((1UL << Id) | (1UL << Capacity));
    }

    private struct StubAggregateState : IAggregateDefinition<StubAggregateState, StubAggregateDescriptor>
    {
        public ulong Id;
        public uint Capacity;
        public uint Flags;

        public static StubAggregateDescriptor Materialize(in StubAggregateState state) => new(state.Id, state.Capacity, state.Flags);

        public static bool TryValidate(in StubAggregateState state, out BuilderDiagnostic diagnostic)
        {
            if (state.Id == 0UL)
            {
                diagnostic = BuilderDiagnostic.Fail(BuilderStatusCode.ValidationFailed, "Id must be non-zero.");
                return false;
            }

            if (state.Capacity is 0 or > 1024U)
            {
                diagnostic = BuilderDiagnostic.Fail(BuilderStatusCode.ValidationFailed, "Capacity must be between 1 and 1024.");
                return false;
            }

            diagnostic = BuilderDiagnostic.Ok;
            return true;
        }
    }

    private sealed class StubAggregateDescriptor
    {
        public ulong Id { get; }
        public uint Capacity { get; }
        public uint Flags { get; }

        public StubAggregateDescriptor(ulong id, uint capacity, uint flags)
        {
            Id = id;
            Capacity = capacity;
            Flags = flags;
        }
    }

    private sealed class StubAggregateBuilder : AggregateBuilder<StubAggregateBuilder, StubAggregateState, StubAggregateDescriptor>
    {
        protected override StubAggregateBuilder Self => this;

        protected override PropertyBitmask64 RequiredMask => StubSlots.RequiredMask;

        public StubAggregateBuilder WithId(ulong id)
        {
            State.Id = id;
            MarkSet(StubSlots.Id);
            return this;
        }

        public StubAggregateBuilder WithCapacity(uint capacity)
        {
            State.Capacity = capacity;
            MarkSet(StubSlots.Capacity);
            return this;
        }

        public StubAggregateBuilder WithPreset(bool large)
        {
            State.Capacity = large ? 1024U : 64U;
            State.Flags = large ? 0x1U : 0U;
            MarkSet(StubSlots.Capacity);
            return this;
        }

        public override StubAggregateBuilder Fork() => CopyTo(new StubAggregateBuilder());
    }

    private sealed class TaggedDescriptor
    {
        public StubAggregateDescriptor Base { get; }
        public string Tag { get; }

        public TaggedDescriptor(StubAggregateDescriptor @base, string tag)
        {
            Base = @base;
            Tag = tag;
        }
    }

    private sealed class TaggingBuilder : IBuilder<TaggedDescriptor>
    {
        private readonly IBuilder<StubAggregateDescriptor> _inner;
        private readonly string _tag;

        public TaggingBuilder(IBuilder<StubAggregateDescriptor> inner, string tag)
        {
            _inner = inner;
            _tag = tag;
        }

        public TaggedDescriptor Build() => new(_inner.Build(), _tag);
    }

    private static StubAggregateBuilder StagingTemplate() =>
        new StubAggregateBuilder().WithPreset(large: false);

    [Fact]
    public void HappyPath_MaterializesDescriptor()
    {
        var descriptor = new StubAggregateBuilder()
            .WithId(7UL)
            .WithCapacity(128U)
            .Build();

        descriptor.Id.Should().Be(7UL);
        descriptor.Capacity.Should().Be(128U);
    }

    [Fact]
    public void MissingSlot_NamesMask()
    {
        var builder = new StubAggregateBuilder().WithId(7UL);

        builder.TryBuild(out var descriptor, out BuilderDiagnostic diagnostic).Should().BeFalse();
        descriptor.Should().BeNull();
        diagnostic.Code.Should().Be(BuilderStatusCode.MissingRequiredField);
        diagnostic.Message.Should().Contain("0x0000000000000002");
    }

    [Fact]
    public void DomainViolation_FailsAfterMask()
    {
        var builder = new StubAggregateBuilder().WithId(7UL).WithCapacity(2048U);

        builder.TryBuild(out _, out BuilderDiagnostic diagnostic).Should().BeFalse();
        diagnostic.Code.Should().Be(BuilderStatusCode.ValidationFailed);
    }

    [Fact]
    public void Composite_Preset_SetsMultipleFields()
    {
        var descriptor = new StubAggregateBuilder()
            .WithId(1UL)
            .WithPreset(large: true)
            .Build();

        descriptor.Capacity.Should().Be(1024U);
        descriptor.Flags.Should().Be(0x1U);
    }

    [Fact]
    public void Fork_DivergesIndependently()
    {
        var original = new StubAggregateBuilder().WithId(1UL).WithCapacity(64U);
        var fork = original.Fork().WithId(2UL).WithCapacity(128U);

        original.Build().Id.Should().Be(1UL);
        fork.Build().Id.Should().Be(2UL);
        fork.Build().Capacity.Should().Be(128U);
    }

    [Fact]
    public void Reset_ClearsToPristine()
    {
        var builder = new StubAggregateBuilder().WithId(1UL).WithCapacity(64U);
        builder.Reset();

        builder.TryBuild(out _, out BuilderDiagnostic diagnostic).Should().BeFalse();
        diagnostic.Code.Should().Be(BuilderStatusCode.MissingRequiredField);
    }

    [Fact]
    public void Decorator_ComposesOverInterface()
    {
        var tagged = new TaggingBuilder(
            new StubAggregateBuilder().WithId(3UL).WithCapacity(16U),
            "staging").Build();

        tagged.Base.Id.Should().Be(3UL);
        tagged.Tag.Should().Be("staging");
    }

    [Fact]
    public void Template_ForkAndFill()
    {
        var descriptor = StagingTemplate()
            .WithId(9UL)
            .Build();

        descriptor.Id.Should().Be(9UL);
        descriptor.Capacity.Should().Be(64U);
    }
}
