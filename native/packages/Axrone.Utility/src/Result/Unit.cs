using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Enterprise.Patterns.Result;

[StructLayout(LayoutKind.Sequential, Size = 1)]
public readonly record struct Unit : IEquatable<Unit>, IComparable<Unit>
{
    public static readonly Unit Value = default;
    public static readonly Unit Default = default;
    public static readonly Task<Unit> TaskValue = Task.FromResult(Value);
    public static readonly ValueTask<Unit> ValueTask = new(Value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(Unit other) => 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override string ToString() => "()";
}
