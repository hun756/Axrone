namespace Axrone.Reactive.Ecs;

using Axrone.Ecs;

/// <summary>Entity lifecycle kind reported by the bridge.</summary>
public enum EntityLifecycleKind
{
    /// <summary>Entity was created.</summary>
    Created = 0,

    /// <summary>Entity was destroyed.</summary>
    Destroyed = 1,
}

/// <summary>Entity lifecycle event.</summary>
public readonly record struct EntityLifecycleEvent(Entity Entity, EntityLifecycleKind Kind);

/// <summary>Component was added after the tracking checkpoint.</summary>
public readonly record struct ComponentAddedEvent<T>(Entity Entity, T Value, int Tick)
    where T : unmanaged;

/// <summary>Existing component value changed after the tracking checkpoint.</summary>
public readonly record struct ComponentChangedEvent<T>(Entity Entity, T Value, int Tick)
    where T : unmanaged;

/// <summary>Component was removed after the tracking checkpoint.</summary>
public readonly record struct ComponentRemovedEvent<T>(Entity Entity, int Tick)
    where T : unmanaged;

/// <summary>A scheduler frame boundary.</summary>
public readonly record struct FrameEvent(int Tick, long Frame, float DeltaTime);

/// <summary>One scheduler system execution boundary.</summary>
public readonly record struct SystemEvent(
    SystemId Id,
    SystemPhase Phase,
    int Priority,
    int Tick,
    long Frame,
    float DeltaTime);

/// <summary>A system fault, followed by the original exception rethrown by the scheduler.</summary>
public readonly record struct SystemFaultEvent(SystemEvent Event, Exception Error);
