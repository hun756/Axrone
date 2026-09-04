namespace Axrone.Memory.Lifetime;

public static class SingletonAuto<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> where T : class, new()
{
    private static readonly Lazy<T> s_lazy = new(static () => new T(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static T Instance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => s_lazy.Value;
    }

    public static bool IsValueCreated => s_lazy.IsValueCreated;
}
