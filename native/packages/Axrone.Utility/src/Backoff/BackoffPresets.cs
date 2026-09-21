namespace Axrone.Utility.Backoff;

public static class BackoffPresets
{
    public static BackoffConfiguration LockAcquisition() => new(
        minDuration: BackoffDuration.FromNanoseconds(50),
        maxDuration: BackoffDuration.FromMicroseconds(100),
        stepIncrement: BackoffDuration.FromNanoseconds(100),
        spinIterationsThreshold: 20,
        yieldIterationsThreshold: 40,
        multiplier: 2.0,
        jitterRatio: 0.0,
        maxRetryLimit: uint.MaxValue);

    public static BackoffConfiguration NetworkRetry() => new(
        minDuration: BackoffDuration.FromMicroseconds(50),
        maxDuration: BackoffDuration.FromSeconds(30),
        stepIncrement: BackoffDuration.FromMilliseconds(100),
        spinIterationsThreshold: 0,
        yieldIterationsThreshold: 0,
        multiplier: 2.0,
        jitterRatio: 1.0,
        maxRetryLimit: 10);

    public static BackoffConfiguration AssetStreaming() => new(
        minDuration: BackoffDuration.FromMicroseconds(100),
        maxDuration: BackoffDuration.FromMilliseconds(2000),
        stepIncrement: BackoffDuration.FromMilliseconds(5),
        spinIterationsThreshold: 4,
        yieldIterationsThreshold: 8,
        multiplier: 2.0,
        jitterRatio: 0.1,
        maxRetryLimit: 50);

    public static BackoffConfiguration AudioThread() => new(
        minDuration: BackoffDuration.FromNanoseconds(100),
        maxDuration: BackoffDuration.FromMicroseconds(1000),
        stepIncrement: BackoffDuration.FromNanoseconds(500),
        spinIterationsThreshold: 32,
        yieldIterationsThreshold: 48,
        multiplier: 2.0,
        jitterRatio: 0.0,
        maxRetryLimit: uint.MaxValue);

    public static BackoffConfiguration GpuSubmission() => new(
        minDuration: BackoffDuration.FromMicroseconds(10),
        maxDuration: BackoffDuration.FromMilliseconds(5),
        stepIncrement: BackoffDuration.FromMicroseconds(50),
        spinIterationsThreshold: 12,
        yieldIterationsThreshold: 24,
        multiplier: 2.0,
        jitterRatio: 0.15,
        maxRetryLimit: 100);

    public static BackoffConfiguration TaskStealing() => new(
        minDuration: BackoffDuration.FromNanoseconds(50),
        maxDuration: BackoffDuration.FromMicroseconds(50),
        stepIncrement: BackoffDuration.FromNanoseconds(200),
        spinIterationsThreshold: 16,
        yieldIterationsThreshold: 32,
        multiplier: 2.0,
        jitterRatio: 0.0,
        maxRetryLimit: uint.MaxValue);
}
