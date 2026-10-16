namespace Axrone.Ecs;

/// <summary>
/// Structural command discriminant.
/// </summary>
public enum CommandKind : byte
{
    /// <summary>Spawn (no-op: entity already exists).</summary>
    Spawn = 0,

    /// <summary>Destroy.</summary>
    Despawn = 1,

    /// <summary>Set component (reserved for a payload-carrying round).</summary>
    SetComponent = 2,
}

/// <summary>
/// Discriminated structural command.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public readonly struct EntityCommand
{
    /// <summary>Discriminant.</summary>
    [FieldOffset(0)]
    public readonly CommandKind Kind;

    /// <summary>Target entity.</summary>
    [FieldOffset(4)]
    public readonly Entity Entity;

    /// <summary>Component for set commands.</summary>
    [FieldOffset(12)]
    public readonly ComponentId ComponentId;

    /// <summary>Creates a spawn command.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityCommand Spawn(in Entity entity) =>
        new(CommandKind.Spawn, entity, default);

    /// <summary>Creates a despawn command.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityCommand Despawn(in Entity entity) =>
        new(CommandKind.Despawn, entity, default);

    /// <summary>Creates a set command.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityCommand Set(in Entity entity, ComponentId id) =>
        new(CommandKind.SetComponent, entity, id);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private EntityCommand(CommandKind kind, in Entity entity, ComponentId componentId)
    {
        Kind = kind;
        Entity = entity;
        ComponentId = componentId;
    }
}

/// <summary>
/// Deferred structural command buffer: enqueue during iteration,
/// playback against the world afterwards.
/// </summary>
public sealed class EntityCommandBuffer
{
    private EntityCommand[] _commands;
    private int _count;

    /// <summary>Creates a buffer.</summary>
    public EntityCommandBuffer(int initialCapacity = 256)
    {
        _commands = GC.AllocateUninitializedArray<EntityCommand>(initialCapacity);
        _count = 0;
    }

    /// <summary>Pending command count.</summary>
    public int Count => Volatile.Read(ref _count);

    /// <summary>Enqueues a command.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(in EntityCommand command)
    {
        if (_count == _commands.Length)
        {
            Array.Resize(ref _commands, _commands.Length * 2);
        }

        _commands[_count++] = command;
    }

    /// <summary>Replays and drains.</summary>
    public void Playback(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        ReadOnlySpan<EntityCommand> span = new(_commands, 0, _count);
        for (int i = 0; i < span.Length; i++)
        {
            ref readonly EntityCommand cmd = ref span[i];
            switch (cmd.Kind)
            {
                case CommandKind.Spawn:
                    break;
                case CommandKind.Despawn:
                    world.DestroyEntity(cmd.Entity);
                    break;
                case CommandKind.SetComponent:
                    break;
                default:
                    EcsThrowHelper.ThrowExhaustiveMatchFailure();
                    break;
            }
        }

        _count = 0;
    }
}
