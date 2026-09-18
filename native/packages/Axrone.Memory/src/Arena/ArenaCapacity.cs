namespace Axrone.Memory.Arena;

public readonly record struct ArenaCapacity
{
    public nuint Value { get; }
    public nuint Mask => Value - 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ArenaCapacity(nuint value)
    {
        if (value < 2 || !BitOperations.IsPow2(value))
        {
            ThrowHelper.ThrowInvalidCapacity(value);
        }
        Value = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator nuint(ArenaCapacity capacity) => capacity.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ArenaCapacity(nuint value) => new(value);
}
