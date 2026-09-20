namespace Axrone.Batching;

using Axrone.Utility.Builders;

/// <summary>
/// Mutable accumulator for <see cref="TimeSlicedPipelineBuilder{T}"/>; materializes a
/// <see cref="TimeSlicedPipeline{T}"/>.
/// </summary>
/// <remarks>
/// Capacity is structurally provided by the builder entry points, so the required mask is empty
/// and <see cref="TryValidate"/> is the authoritative gate. The positivity bound mirrors the
/// pipeline guard; enforcement stays in the product.
/// </remarks>
public struct PipelineState<T> : IAggregateDefinition<PipelineState<T>, TimeSlicedPipeline<T>>,
    IEquatable<PipelineState<T>>
    where T : unmanaged
{
    /// <summary>Working defaults; capacity must still be provided.</summary>
    public static PipelineState<T> Default => new()
    {
        Stride = BatchStride.Default,
        MeterName = "Axrone.Batching",
    };

    public int Capacity { get; set; }
    public BatchStride Stride { get; set; }
    public string MeterName { get; set; }

    /// <inheritdoc/>
    public static TimeSlicedPipeline<T> Materialize(in PipelineState<T> state) =>
        new(state.Capacity, state.Stride, state.MeterName);

    /// <inheritdoc/>
    public static bool TryValidate(in PipelineState<T> state, out BuilderDiagnostic diagnostic)
    {
        if (state.Capacity <= 0)
        {
            diagnostic = BuilderDiagnostic.Fail(BuilderStatusCode.ValidationFailed, "Capacity must be positive.");
            return false;
        }

        diagnostic = BuilderDiagnostic.Ok;
        return true;
    }

    /// <inheritdoc/>
    public bool Equals(PipelineState<T> other) =>
        Capacity == other.Capacity &&
        Stride.Equals(other.Stride) &&
        MeterName == other.MeterName;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PipelineState<T> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Capacity, Stride, MeterName);

    public static bool operator ==(PipelineState<T> left, PipelineState<T> right) => left.Equals(right);

    public static bool operator !=(PipelineState<T> left, PipelineState<T> right) => !left.Equals(right);
}
