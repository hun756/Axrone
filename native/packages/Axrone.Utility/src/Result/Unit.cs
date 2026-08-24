using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Enterprise.Patterns.Result;

/// <summary>Represents a void-equivalent value for use in <see cref="Result{TValue}"/> when no value is produced.</summary>
[StructLayout(LayoutKind.Sequential, Size = 1)]
public readonly record struct Unit : IEquatable<Unit>, IComparable<Unit>
{
    /// <summary>Default <see cref="Unit"/> value.</summary>
    public static readonly Unit Value = default;

    /// <summary>Alias for <see cref="Value"/>.</summary>
    public static readonly Unit Default = default;

    /// <summary>Pre-completed <see cref="Task{TResult}"/> wrapping <see cref="Value"/>.</summary>
    public static readonly Task<Unit> TaskValue = Task.FromResult(Value);

    /// <summary>Pre-completed <see cref="ValueTask{TResult}"/> wrapping <see cref="Value"/>.</summary>
    public static readonly ValueTask<Unit> CompletedValueTask = new(Value);

    /// <summary>Always returns 0; all <see cref="Unit"/> values are equal.</summary>
    /// <param name="other">The other <see cref="Unit"/> to compare to.</param>
    /// <returns>Always 0.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(Unit other) => 0;

    /// <summary>Returns the string representation <c>"()"</c>.</summary>
    /// <returns>The string <c>"()"</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override string ToString() => "()";
}
