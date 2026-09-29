namespace Axrone.Execution;

/// <summary>
/// A queued call: an opaque unmanaged state pointer plus the function to run
/// against it. Lets a caller hand already-formatted work to any pump or
/// executor without the ring knowing what the work means — the classic
/// "trampoline" shape for staged, CPU-bound work whose payload already lives
/// in native memory.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lifetime: the state must outlive the dequeue.</b> The ring copies this
/// struct by value, but it does not own, copy, extend or free
/// <see cref="State"/>. A packet whose state is freed, pooled or moved before
/// the consumer runs <see cref="Invoke"/> is a use-after-free, and the ring
/// cannot detect it. Retain the state in the same arena the frame's
/// allocations come from, and only recycle it after the drain that observed the
/// packet has completed.
/// </para>
/// <para>
/// <b>Callbacks must be static.</b> <see cref="Callback"/> is a bare function
/// pointer, so a lambda, a closure, or any instance method that captures
/// <c>this</c> cannot be stored in it — the compiler rejects the conversion at
/// the call site rather than producing a dangling target. State travels
/// through <see cref="State"/> instead.
/// </para>
/// <para>
/// The <c>unmanaged</c> calling convention is what makes the pointer safe to
/// cross a native or interop boundary: a method carrying
/// <see cref="UnmanagedCallersOnlyAttribute"/> has no managed frame to unwind,
/// so an exception escaping one would tear down the process rather than the
/// thread. Apply that attribute to any callback that is also handed to native
/// code, and keep the body free of allocations, of blocking, and of throws.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly unsafe struct WorkPacket : IEquatable<WorkPacket>
{
    /// <summary>Opaque payload handed back to <see cref="Callback"/>. Not owned by the ring.</summary>
    public readonly void* State;

    /// <summary>Function invoked with <see cref="State"/> when the packet is dequeued.</summary>
    public readonly delegate* unmanaged<void*, void> Callback;

    /// <summary>Creates a packet.</summary>
    /// <param name="state">Opaque payload; must outlive the dequeue.</param>
    /// <param name="callback">Static, preferably <see cref="UnmanagedCallersOnlyAttribute"/>, entry point.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WorkPacket(void* state, delegate* unmanaged<void*, void> callback)
    {
        State = state;
        Callback = callback;
    }

    /// <summary>Runs the callback against <see cref="State"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Invoke() => Callback(State);

    /// <summary>Identity comparison: same state pointer and same callback address.</summary>
    /// <param name="other">The packet to compare against.</param>
    /// <returns>Whether both packets address the same state with the same callback.</returns>
    /// <remarks>
    /// The callback is compared by address, not by target: two distinct
    /// function pointers may point at the same method (generic instantiations,
    /// thunks), and address identity is the property a packet round-trip needs.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(WorkPacket other) => State == other.State && (nint)Callback == (nint)other.Callback;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is WorkPacket other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine((nint)State, (nint)Callback);

    /// <summary>Equality operator.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>Whether both packets are identical.</returns>
    public static bool operator ==(WorkPacket left, WorkPacket right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>Whether the packets differ.</returns>
    public static bool operator !=(WorkPacket left, WorkPacket right) => !left.Equals(right);
}

/// <summary>
/// Default kernel for <see cref="WorkPacket"/>: invokes the packet and
/// discards the context, which lets a packet be pumped with no consumer-owned
/// state at all. Struct, so the call devirtualizes at the call site.
/// </summary>
/// <remarks>
/// Pairs with <see cref="CommandPump{TCommand, TContext, TProcessor, TBackoff, TTelemetry}"/>,
/// whose context may be a <c>ref struct</c> and can therefore be
/// <see cref="EmptyContext"/>. It is deliberately NOT
/// <see cref="WorkExecutor{TCommand, TContext, TProcessor, TBackoff, TTelemetry}"/>'s
/// worker kernel: the executor constructs its context and requires a class, so a
/// worker that dispatches packets wraps them in a class context of its own.
/// </remarks>
public readonly struct WorkPacketProcessor : ICommandProcessor<WorkPacket, WorkPacketProcessor.EmptyContext>, IEquatable<WorkPacketProcessor>
{
    /// <summary>
    /// Placeholder consumer context: a packet carries everything it needs, so
    /// the kernel has no consumer state to thread through.
    /// </summary>
    /// <remarks>
    /// Nested on purpose — it exists only as the context type argument of
    /// <see cref="WorkPacketProcessor"/>, and a top-level name would put a
    /// meaningless "empty" type into the package's public vocabulary.
    /// </remarks>
    [SuppressMessage("Design", "CA1034:Do not nest types", Justification = "Namespaced by its only consumer, WorkPacketProcessor.")]
    public readonly ref struct EmptyContext { }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Process(ref WorkPacket item, ref EmptyContext context) => item.Invoke();

    /// <summary>Stateless kernel: every instance is equivalent.</summary>
    /// <param name="other">The value to compare against.</param>
    /// <returns>Always <c>true</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(WorkPacketProcessor other) => true;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is WorkPacketProcessor;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => 0;

    /// <summary>Equality operator.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>Always <c>true</c>.</returns>
    public static bool operator ==(WorkPacketProcessor left, WorkPacketProcessor right) => true;

    /// <summary>Inequality operator.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>Always <c>false</c>.</returns>
    public static bool operator !=(WorkPacketProcessor left, WorkPacketProcessor right) => false;
}
