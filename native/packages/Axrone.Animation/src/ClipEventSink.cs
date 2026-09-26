namespace Axrone.Animation;

/// <summary>
/// Zero-allocation clip-event consumer contract (counters, span writers, test probes).
/// Consumed through generic <c>CollectEvents&lt;TSink&gt;</c> overloads constrained to
/// this interface, so calls stay devirtualized with no boxing. Implementations must
/// be value types accumulating into themselves; a plain struct (not ref struct —
/// the language forbids interface implementation on ref structs) is required.
/// <see cref="CollectionEventSinkAdapter"/> bridges legacy <c>ICollection</c> callers.
/// </summary>
public interface IClipEventSink
{
    /// <summary>Emits one event.</summary>
    void Emit(in ClipEvent clipEvent);
}

/// <summary>Bridges heap collections to the zero-allocation sink contract.</summary>
public readonly record struct CollectionEventSinkAdapter : IClipEventSink
{
    private readonly ICollection<ClipEvent> _collection;

    /// <summary>Creates an adapter.</summary>
    public CollectionEventSinkAdapter(ICollection<ClipEvent> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        _collection = collection;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Emit(in ClipEvent clipEvent) => _collection.Add(clipEvent);
}
