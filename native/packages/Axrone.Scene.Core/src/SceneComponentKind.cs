namespace Axrone.Scene;

/// <summary>
/// Storage tier of a scene component. Data components are unmanaged columns;
/// behaviour components are managed lifecycle objects.
/// </summary>
public enum SceneComponentKind : byte
{
    /// <summary>Unmanaged payload stored in a dense per-actor column.</summary>
    Data = 0,

    /// <summary>Managed lifecycle object stored in the behaviour table.</summary>
    Behaviour = 1
}
