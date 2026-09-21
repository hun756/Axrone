namespace Axrone.Batching;

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

/// <summary>Fluent builder for <see cref="TimeSlicedPipeline{T}"/> instances over a mutable accumulator state.</summary>
/// <typeparam name="T">Element type.</typeparam>
/// <remarks>
/// Capacity is structurally required via the entry points, so the required mask is empty and
/// domain validation is authoritative. Supports fork/reset for template workflows.
/// </remarks>
public sealed class TimeSlicedPipelineBuilder<T>
    : AggregateBuilder<TimeSlicedPipelineBuilder<T>, PipelineState<T>, TimeSlicedPipeline<T>>
    where T : unmanaged
{
    internal TimeSlicedPipelineBuilder(int capacity)
    {
        State = PipelineState<T>.Default;
        State.Capacity = capacity;
    }

    /// <summary>Sets slot capacity.</summary>
    /// <param name="capacity">Elements per buffer slot.</param>
    public TimeSlicedPipelineBuilder<T> WithCapacity(int capacity)
    {
        State.Capacity = capacity;
        return this;
    }

    /// <summary>Sets calibrator stride bounds.</summary>
    /// <param name="stride">Stride bounds.</param>
    public TimeSlicedPipelineBuilder<T> WithStride(BatchStride stride)
    {
        State.Stride = stride;
        return this;
    }

    /// <summary>Sets the telemetry meter name.</summary>
    /// <param name="meterName">OpenTelemetry meter name.</param>
    public TimeSlicedPipelineBuilder<T> WithTelemetry(string meterName)
    {
        State.MeterName = meterName;
        return this;
    }

    /// <inheritdoc/>
    protected override TimeSlicedPipelineBuilder<T> Self => this;

    /// <inheritdoc/>
    protected override PropertyBitmask64 RequiredMask => PropertyBitmask64.None;

    /// <summary>Builds the pipeline; preserves the product's throw contract.</summary>
    public override TimeSlicedPipeline<T> Build() => PipelineState<T>.Materialize(in State);

    /// <inheritdoc/>
    public override void Reset() => State = PipelineState<T>.Default;

    /// <inheritdoc/>
    public override TimeSlicedPipelineBuilder<T> Fork() => CopyTo(new TimeSlicedPipelineBuilder<T>(State.Capacity));
}
