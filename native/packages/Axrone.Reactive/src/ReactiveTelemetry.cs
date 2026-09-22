namespace Axrone.Reactive;

using System.Diagnostics.Metrics;

/// <summary>
/// Process-wide meters for subscription lifecycle. Cold paths only — subscribing, completing,
/// faulting. Per-notification counting would tax the hot path and is deliberately absent.
/// </summary>
internal static class ReactiveTelemetry
{
    private static readonly Meter s_meter = new("Axrone.Reactive", "1.0.0");
    private static readonly Counter<long> s_subscribed = s_meter.CreateCounter<long>("reactive.subscribed", "{subscriptions}");
    private static readonly Counter<long> s_completed = s_meter.CreateCounter<long>("reactive.completed", "{sources}");
    private static readonly Counter<long> s_faulted = s_meter.CreateCounter<long>("reactive.faulted", "{sources}");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Subscribed() => s_subscribed.Add(1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SourceCompleted() => s_completed.Add(1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SourceFaulted() => s_faulted.Add(1);
}
