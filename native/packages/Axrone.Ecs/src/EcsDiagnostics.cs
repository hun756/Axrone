namespace Axrone.Ecs;

/// <summary>
/// Receives world lifecycle callbacks.
/// </summary>
public interface IDiagnosticsSink
{
    /// <summary>Raised when an archetype is created.</summary>
    void OnArchetypeCreated(ArchetypeId id, in ComponentMask mask);

    /// <summary>Raised when an entity is created.</summary>
    void OnEntityCreated(Entity entity);

    /// <summary>Raised when an entity is destroyed.</summary>
    void OnEntityDestroyed(Entity entity);
}

/// <summary>
/// Diagnostics sink that discards every callback.
/// </summary>
public sealed class NullDiagnosticsSink : IDiagnosticsSink
{
    /// <summary>Shared instance.</summary>
    public static readonly NullDiagnosticsSink Instance = new();

    private NullDiagnosticsSink()
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnArchetypeCreated(ArchetypeId id, in ComponentMask mask)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnEntityCreated(Entity entity)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnEntityDestroyed(Entity entity)
    {
    }
}
