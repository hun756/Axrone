namespace Axrone.Render.OpenGL.FrameGraph;

/// <summary>
/// Identity of a render pass inside the frame graph.
/// </summary>
/// <remarks>
/// <para><b>Where the id comes from.</b> The graph assigns the id at
/// <c>AddPass</c> time, monotonically, from a counter that starts at zero, so a
/// <see cref="PassId"/> is also the pass's dense slot in the graph's storage. That
/// is the whole point of the type: every compile-time structure the graph builds
/// (in-degree table, adjacency spans, emission order) is indexed by the id
/// directly, so the walk never has to translate an id into a list position, and
/// two id-keyed structures can be zipped without a lookup.</para>
/// <para><b>Validity.</b> <see cref="Invalid"/> is the reserved sentinel
/// (<see cref="uint.MaxValue"/>), so a default-constructed id, a slot index and
/// "no pass" are three distinguishable states. Because the counter starts at
/// zero and ids are assigned densely, the sentinel can never collide with a real
/// pass, even for a graph with <see cref="uint.MaxValue"/> passes in theory.</para>
/// <para><b>Lifetime.</b> Ids are unique and monotonically increasing within one
/// build epoch. <c>Reset</c> clears the graph and restarts the counter at zero,
/// so a <see cref="PassId"/> must not be cached across a reset: it names a slot,
/// not a pass object. The slot, not the number, is the stable identity a caller
/// should keep.</para>
/// <para><b>Equality and ordering</b> are the compiler-generated record members
/// (<c>==</c>, <c>!=</c>, <see cref="GetHashCode"/>) plus the value comparison
/// operators below, so ids can be dictionary keys and can be sorted into a
/// deterministic order (which is what makes the compiled emission order
/// reproducible across runs).</para>
/// </remarks>
/// <param name="Value">The dense pass slot this id names.</param>
public readonly record struct PassId(uint Value) : IComparable<PassId>
{
    /// <summary>
    /// Gets the sentinel that names no pass. Never assigned by the graph.
    /// </summary>
    public static PassId Invalid => new(uint.MaxValue);

    /// <summary>
    /// Gets a value indicating whether this id names a real pass slot.
    /// </summary>
    public bool IsValid => Value != uint.MaxValue;

    /// <summary>
    /// Orders two pass ids by their slot, so a set of ids can be put in graph
    /// order without a bespoke comparer.
    /// </summary>
    /// <param name="other">The id to compare against.</param>
    /// <returns>A negative value, zero or a positive value as this id is less than, equal to or greater than <paramref name="other"/>.</returns>
    public int CompareTo(PassId other) => Value.CompareTo(other.Value);

    /// <summary>Compares two pass ids for an earlier slot.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><c>true</c> when <paramref name="left"/> names an earlier slot.</returns>
    public static bool operator <(PassId left, PassId right) => left.Value < right.Value;

    /// <summary>Compares two pass ids for a later slot.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><c>true</c> when <paramref name="left"/> names a later slot.</returns>
    public static bool operator >(PassId left, PassId right) => left.Value > right.Value;

    /// <summary>Compares two pass ids for an earlier or equal slot.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><c>true</c> when <paramref name="left"/> does not name a later slot.</returns>
    public static bool operator <=(PassId left, PassId right) => left.Value <= right.Value;

    /// <summary>Compares two pass ids for a later or equal slot.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><c>true</c> when <paramref name="left"/> does not name an earlier slot.</returns>
    public static bool operator >=(PassId left, PassId right) => left.Value >= right.Value;

    /// <summary>Returns the diagnostic form of this id, for example <c>PassId(3)</c>.</summary>
    /// <returns>The <c>PassId(n)</c> form.</returns>
    public override string ToString() => $"PassId({Value})";
}

/// <summary>
/// Identity of a declared render-graph resource.
/// </summary>
/// <remarks>
/// <para><b>Two planes, deliberately.</b> Resource names travel through the graph
/// on two independent paths and only one of them is interned:</para>
/// <list type="number">
/// <item><description>the <b>topology plane</b> — which pass must run before
/// which other pass — resolves names to <see cref="ResourceId"/> once, at
/// <c>AddPass</c> time, and every later step of the compile walk works on
/// integers. This is the hot path (Kahn over dense spans) and it must not pay
/// for string hashing or <see cref="StringComparer.OrdinalIgnoreCase"/>
/// comparisons;</description></item>
/// <item><description>the <b>resource plane</b> — the actual
/// <c>GLTexture</c> / <c>GLFramebuffer</c> / <c>GLBuffer</c> instances a pass
/// binds — stays string-keyed in <see cref="PassExecutionContext"/>, which is
/// the caller's own handle table.</description></item>
/// </list>
/// <para>The split is intentional. Interning buys a cheap, allocation-free walk
/// over ids; it must not leak into the resource plane, where a caller looks up
/// an instance by the same name it authored in the pass declaration and where
/// renaming or aliasing at runtime has to keep working. Interning the resource
/// plane would couple a graph-internal slot number to a caller-visible lookup
/// key for no measurable gain — the resolution happens once per pass, not once
/// per edge.</para>
/// <para>Like <see cref="PassId"/>, ids are dense from zero and restart at
/// <c>Reset</c>, and <see cref="Invalid"/> is the reserved sentinel that the
/// intern table never hands out.</para>
/// </remarks>
/// <param name="Value">The dense intern slot this id names.</param>
public readonly record struct ResourceId(uint Value) : IComparable<ResourceId>
{
    /// <summary>
    /// Gets the sentinel that names no resource. Never handed out by the intern
    /// table.
    /// </summary>
    public static ResourceId Invalid => new(uint.MaxValue);

    /// <summary>
    /// Gets a value indicating whether this id names an interned resource.
    /// </summary>
    public bool IsValid => Value != uint.MaxValue;

    /// <summary>
    /// Orders two resource ids by their intern slot.
    /// </summary>
    /// <param name="other">The id to compare against.</param>
    /// <returns>A negative value, zero or a positive value as this id is less than, equal to or greater than <paramref name="other"/>.</returns>
    public int CompareTo(ResourceId other) => Value.CompareTo(other.Value);

    /// <summary>Compares two resource ids for an earlier slot.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><c>true</c> when <paramref name="left"/> was interned first.</returns>
    public static bool operator <(ResourceId left, ResourceId right) => left.Value < right.Value;

    /// <summary>Compares two resource ids for a later slot.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><c>true</c> when <paramref name="left"/> was interned later.</returns>
    public static bool operator >(ResourceId left, ResourceId right) => left.Value > right.Value;

    /// <summary>Compares two resource ids for an earlier or equal slot.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><c>true</c> when <paramref name="left"/> was not interned later.</returns>
    public static bool operator <=(ResourceId left, ResourceId right) => left.Value <= right.Value;

    /// <summary>Compares two resource ids for a later or equal slot.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><c>true</c> when <paramref name="left"/> was not interned earlier.</returns>
    public static bool operator >=(ResourceId left, ResourceId right) => left.Value >= right.Value;

    /// <summary>Returns the diagnostic form of this id, for example <c>ResourceId(7)</c>.</summary>
    /// <returns>The <c>ResourceId(n)</c> form.</returns>
    public override string ToString() => $"ResourceId({Value})";
}
