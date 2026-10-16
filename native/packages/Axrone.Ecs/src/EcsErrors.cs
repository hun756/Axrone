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
}
