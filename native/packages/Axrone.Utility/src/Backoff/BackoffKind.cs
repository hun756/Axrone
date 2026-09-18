namespace Axrone.Utility.Backoff;

public enum BackoffKind : byte
{
    None = 0,
    Spin = 1,
    Yield = 2,
    Sleep = 3,
    AsyncDelay = 4
}
