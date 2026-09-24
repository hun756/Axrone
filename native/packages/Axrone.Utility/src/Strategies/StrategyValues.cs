namespace Axrone.Utility.Strategies;

/// <summary>Disposition of a strategy execution.</summary>
public enum StrategyStatus : byte
{
    /// <summary>Executed successfully.</summary>
    Success = 0,

    /// <summary>Declined the input.</summary>
    Rejected = 1,

    /// <summary>Faulted during execution.</summary>
    Faulted = 2,

    /// <summary>Requests fallback handling.</summary>
    FallbackRequested = 3,
}

/// <summary>4-byte strategy identity token, register-promotable.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct StrategyId(uint Value) : IComparable<StrategyId>
{
    /// <summary>Uninitialized sentinel; never a valid registration.</summary>
    public static readonly StrategyId Empty = new(0);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int CompareTo(StrategyId other) => Value.CompareTo(other.Value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// 16-byte strategy execution result: metric, code, and status packed for
/// register promotion (two 64-bit registers, no stack roundtrip).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly struct StrategyResult : IEquatable<StrategyResult>
{
    /// <summary>Caller-defined metric.</summary>
    public readonly ulong Metric;

    /// <summary>Reason or error code.</summary>
    public readonly uint Code;

    /// <summary>Disposition.</summary>
    public readonly StrategyStatus Status;

    private readonly byte _reserved0;
    private readonly ushort _reserved1;

    /// <summary>Creates a result.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public StrategyResult(StrategyStatus status, uint code, ulong metric)
    {
        Metric = metric;
        Code = code;
        Status = status;
        _reserved0 = 0;
        _reserved1 = 0;
    }

    /// <summary>Successful result with a metric.</summary>
    public static StrategyResult Success(ulong metric = 0) =>
        new(StrategyStatus.Success, 0, metric);

    /// <summary>Rejection with a reason code.</summary>
    public static StrategyResult Rejected(uint reasonCode) =>
        new(StrategyStatus.Rejected, reasonCode, 0);

    /// <summary>Fault with an error code.</summary>
    public static StrategyResult Faulted(uint errorCode) =>
        new(StrategyStatus.Faulted, errorCode, 0);

    /// <summary>Fallback request with a code.</summary>
    public static StrategyResult Fallback(uint fallbackCode = 0) =>
        new(StrategyStatus.FallbackRequested, fallbackCode, 0);

    /// <summary>Whether the execution succeeded.</summary>
    public bool IsSuccess
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Status == StrategyStatus.Success;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(StrategyResult other) =>
        Metric == other.Metric &&
        Code == other.Code &&
        Status == other.Status;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) =>
        obj is StrategyResult other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(Metric, Code, (byte)Status);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(StrategyResult left, StrategyResult right) => left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(StrategyResult left, StrategyResult right) => !left.Equals(right);
}
