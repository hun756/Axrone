namespace Axrone.Utility.Backoff;

public interface IBackoffPolicy<TSelf> where TSelf : struct, IBackoffPolicy<TSelf>
{
    static abstract BackoffDuration ComputeDuration(uint step, in BackoffConfiguration config, ref ulong rngState);
    static abstract BackoffKind ClassifyKind(uint step, in BackoffConfiguration config);
}
