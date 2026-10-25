namespace Axrone.Scene.Core.Tests;

using Axrone.Scene;

internal struct Health : ISceneDataComponent
{
    public int Value;
}

internal sealed class CounterBehaviour : SceneBehaviour
{
    public int AwakeCount;
    public int StartCount;
    public int EnableCount;
    public int DisableCount;
    public int DestroyCount;
    public int Updates;

    public override void Awake() => AwakeCount++;

    public override void OnEnable() => EnableCount++;

    public override void Start() => StartCount++;

    public override void Update(float deltaSeconds) => Updates++;

    public override void OnDisable() => DisableCount++;

    public override void OnDestroy() => DestroyCount++;
}

internal sealed class HighOrderBehaviour : SceneBehaviour
{
    private readonly List<string> _log;

    public HighOrderBehaviour(List<string> log) => _log = log;

    public override void Update(float deltaSeconds) => _log.Add("high");
}

internal sealed class LowOrderBehaviour : SceneBehaviour
{
    private readonly List<string> _log;

    public LowOrderBehaviour(List<string> log) => _log = log;

    public override void Update(float deltaSeconds) => _log.Add("low");
}

internal sealed class ThrowingBehaviour : SceneBehaviour
{
    public override void Update(float deltaSeconds) =>
        throw new InvalidOperationException("behaviour fault");
}

internal sealed class RecordingDiagnosticsSink : ISceneDiagnosticsSink
{
    public List<SceneDiagnostic> Diagnostics { get; } = new();

    public void Report(in SceneDiagnostic diagnostic) => Diagnostics.Add(diagnostic);
}

internal static class SceneWorldFixture
{
    public static SceneWorld Create3DWorld(
        out SceneComponentRegistry registry,
        ISceneDiagnosticsSink? diagnostics = null,
        SceneWorldErrorPolicy policy = SceneWorldErrorPolicy.IsolateBehaviours)
    {
        registry = new SceneComponentRegistry();
        registry.RegisterData<SceneTransform3D>(
            "Transform3D",
            SceneComponentTraits.Required | SceneComponentTraits.Singleton,
            priority: 1000);
        registry.RegisterData<Health>("Health", SceneComponentTraits.Serializable);
        registry.RegisterBehaviour<CounterBehaviour>("Counter", priority: 0);

        return new SceneWorld(
            registry,
            diagnostics,
            new SceneWorldOptions(initialCapacity: 4, errorPolicy: policy));
    }

    public static SceneWorld Create2DWorld(out SceneComponentRegistry registry)
    {
        registry = new SceneComponentRegistry();
        registry.RegisterData<SceneTransform2D>(
            "Transform2D",
            SceneComponentTraits.Required | SceneComponentTraits.Singleton,
            priority: 1000);

        return new SceneWorld(
            registry,
            diagnostics: null,
            new SceneWorldOptions(initialCapacity: 4));
    }
}
