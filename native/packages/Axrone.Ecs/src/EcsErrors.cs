namespace Axrone.Ecs;

/// <summary>
/// Coded ECS error.
/// </summary>
public class EcsException(string code, string message) : Exception(message)
{
    /// <summary>Machine-readable code.</summary>
    public string Code { get; } = code;
}

/// <summary>
/// Unknown or stale entity.
/// </summary>
public sealed class EcsEntityException(Entity entity, string message)
    : EcsException("ecs.entity-not-found", message)
{
    /// <summary>Offending entity.</summary>
    public Entity Entity { get; } = entity;
}

/// <summary>
/// Missing component on an entity.
/// </summary>
public sealed class EcsComponentException(string? componentName, string message)
    : EcsException("ecs.component-not-found", message)
{
    /// <summary>Component name when known.</summary>
    public string? ComponentName { get; } = componentName;
}

/// <summary>
/// Invalid ECS configuration.
/// </summary>
public sealed class EcsConfigurationException(string code, string message) : EcsException(code, message);

/// <summary>
/// Centralized ECS throw helpers; throwing paths stay out of hot methods.
/// </summary>
internal static class EcsThrowHelper
{
    /// <summary>Throws when an entity handle is unknown or stale.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowEntityNotFound(in Entity entity) =>
        throw new EcsEntityException(entity, $"Entity {entity} does not exist or its version is stale.");

    /// <summary>Throws when a component is missing on an entity.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowComponentNotFound(string name) =>
        throw new EcsComponentException(name, $"Component '{name}' is not present on the entity.");

    /// <summary>Throws when a component id has no registered column factory.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowUnknownComponentType(ComponentId id) =>
        throw new EcsConfigurationException("ecs.unknown-component", $"Component id {id.Value} has no registered column factory.");

    /// <summary>Throws when the component type budget is exhausted.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowMaxComponentsExceeded() =>
        throw new EcsConfigurationException("ecs.max-components", "Component type limit of 256 exceeded.");

    /// <summary>Throws on non-exhaustive discriminated-union dispatch.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowExhaustiveMatchFailure() =>
        throw new EcsConfigurationException("ecs.exhaustive-match", "Non-exhaustive match on a discriminated union branch.");

    /// <summary>Throws when a removal journal is created with a non-positive capacity.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidJournalCapacity(int capacity) =>
        throw new EcsConfigurationException("ecs.invalid-journal-capacity", $"Removal journal capacity must be positive, but was {capacity}.");

    /// <summary>Throws when a command payload length does not match the component size.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidCommandPayload(int actual, int expected) =>
        throw new EcsConfigurationException(
            "ecs.invalid-command-payload",
            $"Command payload length {actual} does not match the component size {expected}.");

    /// <summary>Throws when a capacity or count is outside its valid range.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentOutOfRange(string paramName) =>
        throw new ArgumentOutOfRangeException(paramName);

    /// <summary>Throws when a nested parallel job would deadlock on the shared pool.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowParallelReentrancy() =>
        throw new EcsConfigurationException(
            "ecs.parallel-reentrancy",
            "A chunk-parallel job cannot run from inside a parallel system batch; mark the system structural.");

    /// <summary>Throws when fewer command buffers than workers were supplied.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidCommandBufferCount(int actual, int required) =>
        throw new EcsConfigurationException(
            "ecs.command-buffer-count",
            $"Parallel command playback requires at least {required} buffers, but received {actual}.");

    /// <summary>Throws when a system id is the invalid sentinel.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidSystemId() =>
        throw new EcsConfigurationException("ecs.invalid-system-id", "A system id must be non-zero.");

    /// <summary>Throws when a system id is registered twice.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowDuplicateSystem(SystemId id) =>
        throw new EcsConfigurationException(
            "ecs.duplicate-system",
            $"System id {id.Value} is already registered.");

    /// <summary>Throws when a system dependency was never registered.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowMissingSystemDependency(SystemId system, SystemId dependency) =>
        throw new EcsConfigurationException(
            "ecs.system-missing-dependency",
            $"System {system.Value} depends on unregistered system {dependency.Value}.");

    /// <summary>Throws when system dependencies contain a cycle.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowSystemCycle(SystemId system) =>
        throw new EcsConfigurationException(
            "ecs.system-cycle",
            $"System dependency cycle detected at system {system.Value}.");

    /// <summary>Throws when a dependency is scheduled in a later phase.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowSystemPhaseDependency(SystemId system, SystemId dependency) =>
        throw new EcsConfigurationException(
            "ecs.system-phase-dependency",
            $"System {system.Value} cannot depend on {dependency.Value} in a later phase.");
}
