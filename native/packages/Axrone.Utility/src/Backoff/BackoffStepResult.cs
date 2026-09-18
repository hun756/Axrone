namespace Axrone.Utility.Backoff;

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct BackoffStepResult(uint Step, BackoffKind Kind, BackoffDuration Duration);
