namespace Axrone.Memory.Arena;

public readonly record struct ArenaCapacity
{
    public nuint Value { get; }
    public nuint Mask => Value - 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ArenaCapacity(nuint value)
    {
        if (!IsValid(value))
        {
            ThrowHelper.ThrowInvalidCapacity(value);
        }
        Value = value;
    }

    /// <summary>Capacity rule table shared by the ctor and state validation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsValid(nuint value) => value >= 2 && BitOperations.IsPow2(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator nuint(ArenaCapacity capacity) => capacity.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ArenaCapacity(nuint value) => new(value);
}
