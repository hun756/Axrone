namespace Axrone.Memory;

/// <summary>
/// Controls when pooled buffers are zeroed.
/// </summary>
public enum ClearMode : byte
{
    /// <summary>Buffers are never cleared. Retained data visible on next rent.</summary>
    Never = 0,

    /// <summary>Buffers are cleared when returned to the pool.</summary>
    OnReturn = 1,

    /// <summary>Buffers are cleared when rented from the pool.</summary>
    OnRent = 2,

    /// <summary>Buffers are cleared on both return and rent.</summary>
    Always = 3,
}
