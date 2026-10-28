namespace Axrone.Reactive;

/// <summary>
/// Notification priority for a subscription. Higher priority observers run first,
/// independently of registration order; equal priorities keep registration order.
/// </summary>
public enum ObservationPriority
{
    /// <summary>Runs before normal and low observers.</summary>
    High = 0,

    /// <summary>Default priority.</summary>
    Normal = 1,

    /// <summary>Runs after high and normal observers.</summary>
    Low = 2,
}
