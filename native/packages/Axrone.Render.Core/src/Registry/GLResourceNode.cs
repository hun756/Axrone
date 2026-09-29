namespace Axrone.Render.Core;

/// <summary>
/// Unmanaged registry node for a GPU resource stored in the shared
/// <see cref="DescriptorTable{TDescriptor}"/> registry. The generational
/// <see cref="DescriptorHandle{TDescriptor}"/> issued at registration is the
/// lifecycle identity (ABA-safe slot reuse, status, reclamation); the managed
/// <c>IGLResource</c> lives in a slot-indexed sidecar owned by the registry.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct GLResourceNode : IEquatable<GLResourceNode>
{
    /// <summary>Rebuild order on context restore (lower = rebuilt first).</summary>
    public readonly int RebuildPriority;

    /// <summary>Monotonic registration sequence (disposal walks it in reverse).</summary>
    public readonly int Sequence;

    /// <summary>Creates a node.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public GLResourceNode(int rebuildPriority, int sequence)
    {
        RebuildPriority = rebuildPriority;
        Sequence = sequence;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(GLResourceNode other) =>
        RebuildPriority == other.RebuildPriority && Sequence == other.Sequence;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) =>
        obj is GLResourceNode other && Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(RebuildPriority, Sequence);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(GLResourceNode left, GLResourceNode right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(GLResourceNode left, GLResourceNode right) => !left.Equals(right);
}
