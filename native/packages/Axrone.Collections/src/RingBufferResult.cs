namespace Axrone.Collections;

public enum RingBufferOperationStatus : byte
{
    Success = 0,
    Full = 1,
    Empty = 2,
    Timeout = 3,
    Canceled = 4,
    Disposed = 5
}

[StructLayout(LayoutKind.Auto)]
public readonly struct RingBufferResult<T>
{
    public readonly RingBufferOperationStatus Status;
    public readonly T? Value;

    public bool IsSuccess => Status == RingBufferOperationStatus.Success;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RingBufferResult(RingBufferOperationStatus status, T? value = default)
    {
        Status = status;
        Value = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RingBufferResult<T> Success(T value) => new(RingBufferOperationStatus.Success, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RingBufferResult<T> Failure(RingBufferOperationStatus status) => new(status, default);
}
