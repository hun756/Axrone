namespace Axrone.Utility.Strategies;

using Axrone.Utility.Descriptors;

/// <summary>Provenance of a registered strategy node.</summary>
public enum StrategyNodeKind : uint
{
    /// <summary>Instance strategy (dynamic dispatch).</summary>
    Instance = 0,

    /// <summary>Static strategy (monomorphized invoker).</summary>
    Static = 1,
}

/// <summary>
/// Unmanaged strategy node stored in the shared <see cref="DescriptorTable{TDescriptor}"/>
/// registry. The declared <see cref="StrategyId"/> is author-given metadata; the
/// generational <see cref="DescriptorHandle{TDescriptor}"/> issued at registration
/// is the lifecycle identity (ABA-safe slot reuse, status, reclamation).
/// Managed dispatch state (invoker, instance) lives in a slot-indexed sidecar.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly struct StrategyNode : IEquatable<StrategyNode>
{
    /// <summary>Author-declared strategy identity.</summary>
    public readonly uint DeclaredId;

    /// <summary>Node provenance.</summary>
    public readonly StrategyNodeKind Flags;

    /// <summary>Creates a node.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public StrategyNode(uint declaredId, StrategyNodeKind flags)
    {
        DeclaredId = declaredId;
        Flags = flags;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(StrategyNode other) =>
        DeclaredId == other.DeclaredId && Flags == other.Flags;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) =>
        obj is StrategyNode other && Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => HashCode.Combine(DeclaredId, (uint)Flags);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(StrategyNode left, StrategyNode right) => left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(StrategyNode left, StrategyNode right) => !left.Equals(right);
}
