namespace Axrone.Physics.Core;

/// <summary>
/// Contact lifecycle mirroring the web physics-core contract.
/// Native worlds currently emit begin/stay/end; pre/post-solve hooks are
/// reserved for a solver-callback pass.
/// </summary>
public enum CollisionEventType : byte
{
    /// <summary>A new overlap appeared.</summary>
    Begin = 0,

    /// <summary>An overlap persisted.</summary>
    Stay = 1,

    /// <summary>A previous overlap is gone.</summary>
    End = 2,

    /// <summary>Pre-solve callback point (reserved).</summary>
    PreSolve = 3,

    /// <summary>Post-solve callback point (reserved).</summary>
    PostSolve = 4,
}

/// <summary>
/// Sensor overlap lifecycle mirroring the web physics-core contract.
/// </summary>
public enum SensorEventType : byte
{
    /// <summary>A body entered the trigger volume.</summary>
    Enter = 0,

    /// <summary>A body stayed inside the trigger volume.</summary>
    Stay = 1,

    /// <summary>A body exited the trigger volume.</summary>
    Exit = 2,
}

/// <summary>
/// Time-of-impact outcome mirroring the web physics-core contract.
/// </summary>
public enum ToiState : byte
{
    /// <summary>Not evaluated yet.</summary>
    Unknown = 0,

    /// <summary>Sweep failed to converge.</summary>
    Failed = 1,

    /// <summary>Shapes overlap at time zero.</summary>
    Overlapped = 2,

    /// <summary>Shapes touch within the sweep.</summary>
    Touching = 3,

    /// <summary>No touch within the sweep.</summary>
    Separated = 4,
}
