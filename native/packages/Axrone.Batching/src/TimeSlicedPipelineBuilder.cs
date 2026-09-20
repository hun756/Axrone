namespace Axrone.Batching;

using System.Diagnostics.CodeAnalysis;
using Axrone.Utility.Builders;

/// <summary>Entry point for pipeline builders.</summary>
/// <remarks>
/// Zero reflection: every option is a typed method, validated at build time by the same guards
/// the pipeline itself uses.
/// </remarks>
public static class TimeSlicedPipeline
{
    /// <summary>Creates a builder.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="capacity">Elements per buffer slot.</param>
    public static TimeSlicedPipelineBuilder<T> Create<T>(int capacity)
        where T : unmanaged => new(capacity);
}

/// <summary>Fluent builder for <see cref="TimeSlicedPipeline{T}"/> instances.</summary>
/// <typeparam name="T">Element type.</typeparam>
public sealed class TimeSlicedPipelineBuilder<T> : BuilderBase<TimeSlicedPipelineBuilder<T>, TimeSlicedPipeline<T>>
    where T : unmanaged
{
    private int _capacity;
    private BatchStride _stride = BatchStride.Default;
    private string _meterName = "Axrone.Batching";

    internal TimeSlicedPipelineBuilder(int capacity) => _capacity = capacity;

    /// <summary>Sets slot capacity.</summary>
    /// <param name="capacity">Elements per buffer slot.</param>
    public TimeSlicedPipelineBuilder<T> WithCapacity(int capacity)
    {
        _capacity = capacity;
        return this;
    }

    /// <summary>Sets calibrator stride bounds.</summary>
    /// <param name="stride">Stride bounds.</param>
    public TimeSlicedPipelineBuilder<T> WithStride(BatchStride stride)
    {
        _stride = stride;
        return this;
    }

    /// <summary>Sets the telemetry meter name.</summary>
    /// <param name="meterName">OpenTelemetry meter name.</param>
    public TimeSlicedPipelineBuilder<T> WithTelemetry(string meterName)
    {
        _meterName = meterName;
        return this;
    }

    /// <inheritdoc/>
    protected override TimeSlicedPipelineBuilder<T> Self => this;

    /// <summary>Attempts to build, reporting the pipeline guard failure as a diagnostic.</summary>
    public override bool TryBuild([MaybeNullWhen(false)] out TimeSlicedPipeline<T> result, out BuilderDiagnostic diagnostic) =>
        TryCreate(Build, out result, out diagnostic);

    /// <summary>Builds the pipeline.</summary>
    public override TimeSlicedPipeline<T> Build() => new(_capacity, _stride, _meterName);
}
