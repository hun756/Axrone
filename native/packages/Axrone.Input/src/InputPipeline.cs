namespace Axrone.Input;

/// <summary>
/// Frame pipeline: drains the transport queue into a snapshot, forwards it
/// plus typed text to the action engine and evaluates one frame. The host
/// owns <see cref="InputEngine{TSchema}.ClearFrameText"/> once per frame,
/// before feeding manual text.
/// </summary>
public static class InputPipeline
{
    /// <summary>Pumps one frame through a policy coordinator.</summary>
    /// <param name="coordinator">Frame coordinator.</param>
    /// <param name="engine">Action engine.</param>
    /// <param name="deltaMs">Frame time in milliseconds.</param>
    /// <typeparam name="TSchema">Action enum.</typeparam>
    /// <typeparam name="TBackoff">Queue contention backoff policy.</typeparam>
    /// <typeparam name="TMetrics">Queue telemetry sink.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PumpFrame<TSchema, TBackoff, TMetrics>(
        InputFrameCoordinator<TBackoff, TMetrics> coordinator,
        InputEngine<TSchema> engine,
        float deltaMs)
        where TSchema : unmanaged, Enum
        where TBackoff : struct, IBackoffPolicy
        where TMetrics : struct, IInputMetricsSink
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(engine);

        coordinator.NewFrame();
        engine.SetSnapshot(coordinator.CurrentSnapshot);
        ForwardText(coordinator.FrameText, engine);
        engine.Update(deltaMs);
    }

    /// <summary>Pumps one frame through the default coordinator.</summary>
    /// <param name="coordinator">Frame coordinator.</param>
    /// <param name="engine">Action engine.</param>
    /// <param name="deltaMs">Frame time in milliseconds.</param>
    /// <typeparam name="TSchema">Action enum.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PumpFrame<TSchema>(
        InputFrameCoordinator coordinator,
        InputEngine<TSchema> engine,
        float deltaMs)
        where TSchema : unmanaged, Enum
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(engine);

        coordinator.NewFrame();
        engine.SetSnapshot(coordinator.CurrentSnapshot);
        ForwardText(coordinator.FrameText, engine);
        engine.Update(deltaMs);
    }

    /// <summary>Forwards typed characters to the engine.</summary>
    /// <param name="frameText">Characters drained this frame.</param>
    /// <param name="engine">Action engine.</param>
    /// <typeparam name="TSchema">Action enum.</typeparam>
    private static void ForwardText<TSchema>(IReadOnlyList<char> frameText, InputEngine<TSchema> engine)
        where TSchema : unmanaged, Enum
    {
        for (int i = 0; i < frameText.Count; i++)
        {
            char character = frameText[i];
            engine.PushText(new InputTextEntry(character, character));
        }
    }
}
