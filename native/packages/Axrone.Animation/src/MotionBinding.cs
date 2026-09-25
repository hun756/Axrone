namespace Axrone.Animation;

/// <summary>
/// Partial binding context: whichever parts are present get resolved, the rest
/// is skipped. Binding is idempotent — re-running overwrites the same handles.
/// Unbound nodes keep working through the compat (string/dictionary) path, so
/// binding is a performance upgrade, never a correctness requirement.
/// </summary>
public readonly record struct MotionBindingContext(
    ParameterStore? Parameters,
    IReadOnlyDictionary<CurveId, int>? CurveLayout,
    Rig? Rig);
