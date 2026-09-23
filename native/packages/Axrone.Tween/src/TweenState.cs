namespace Axrone.Tween;

/// <summary>Tween lifecycle state.</summary>
public enum TweenState : byte
{
    Inactive = 0,
    Queued = 1,
    Playing = 2,
    Paused = 3,
    Completed = 4,
    Canceled = 5,
    Faulted = 6,
}
