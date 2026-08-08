namespace Axrone.Utility.Singleton.Internal;

/// <summary>
/// ThreadStatic nesting detector for singleton constructor guards.
/// Uses an <see cref="int"/> depth counter (not bool) to support nested singleton creation.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal static class NestingContext
{
    [ThreadStatic]
    private static int _depth;

    /// <summary>Whether we are currently inside a singleton creation scope.</summary>
    public static bool IsNested
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _depth) > 0;
    }

    /// <summary>Enter a nesting scope. Returns a disposable that exits the scope.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NestingScope EnterScope()
    {
        Volatile.Write(ref _depth, Volatile.Read(ref _depth) + 1);
        return new NestingScope();
    }

    /// <summary>Exit the current nesting scope.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ExitScope()
    {
        Volatile.Write(ref _depth, Volatile.Read(ref _depth) - 1);
    }

    /// <summary>RAII scope — decrements nesting depth on dispose.</summary>
    internal readonly struct NestingScope : IDisposable
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => ExitScope();
    }
}
