namespace Axrone.Scene;

/// <summary>
/// Base scene failure with a stable code. Message text is diagnostic; callers
/// match <see cref="Code"/> and the typed payload on the derived exception.
/// </summary>
public class SceneException : Exception
{
    /// <summary>Stable error code.</summary>
    public string Code { get; }

    /// <summary>Creates an error.</summary>
    public SceneException(string code, string message, Exception? inner = null)
        : base($"[{code}] {message}", inner)
    {
        Code = code;
    }
}

/// <summary>A lifecycle transition or request is invalid.</summary>
public sealed class SceneLifecycleException(string message, Exception? inner = null)
    : SceneException(SceneErrorCodes.LifecycleInvalid, message, inner);

/// <summary>A scene definition or option is outside its accepted domain.</summary>
public sealed class SceneConfigurationException(string message, Exception? inner = null)
    : SceneException(SceneErrorCodes.ConfigurationInvalid, message, inner);

/// <summary>A definition failed structural or semantic validation.</summary>
public sealed class SceneValidationException(string code, string message, Exception? inner = null)
    : SceneException(code, message, inner);

/// <summary>The active profile does not expose a component or subsystem a caller requires.</summary>
public sealed class SceneCapabilityException(string capability, string message)
    : SceneException(SceneErrorCodes.CapabilityMissing, $"Capability '{capability}' is unavailable: {message}")
{
    /// <summary>Missing capability name.</summary>
    public string Capability { get; } = capability;
}

/// <summary>An actor handle failed validation.</summary>
public class SceneActorException : SceneException
{
    /// <summary>Handle that failed validation.</summary>
    public SceneActor Actor { get; }

    /// <summary>Creates an actor failure.</summary>
    public SceneActorException(string code, in SceneActor actor, string message, Exception? inner = null)
        : base(code, $"{actor}: {message}", inner)
    {
        Actor = actor;
    }
}

/// <summary>An actor handle names a slot whose generation no longer matches.</summary>
public sealed class SceneStaleHandleException : SceneActorException
{
    /// <summary>Creates a stale-handle failure.</summary>
    public SceneStaleHandleException(in SceneActor actor)
        : base(SceneErrorCodes.ActorStale, actor, "handle is stale or no longer resolves.")
    {
    }
}

/// <summary>An actor handle does not resolve in the owning world.</summary>
public sealed class SceneActorNotFoundException : SceneActorException
{
    /// <summary>Creates a not-found failure.</summary>
    public SceneActorNotFoundException(in SceneActor actor)
        : base(SceneErrorCodes.ActorNotFound, actor, "actor does not exist.")
    {
    }
}

/// <summary>A component operation failed on a specific actor.</summary>
public class SceneComponentException : SceneActorException
{
    /// <summary>Component or behaviour type name.</summary>
    public string ComponentName { get; }

    /// <summary>Creates a component failure.</summary>
    public SceneComponentException(
        string code,
        in SceneActor actor,
        string componentName,
        string message,
        Exception? inner = null)
        : base(code, actor, $"component '{componentName}': {message}", inner)
    {
        ComponentName = componentName;
    }
}

/// <summary>A structural mutation was attempted while the world was iterating.</summary>
public sealed class SceneStructuralMutationException(string operation)
    : SceneException(
        SceneErrorCodes.StructuralMutation,
        $"Structural mutation '{operation}' is not allowed while scene iteration is active.")
{
    /// <summary>Rejected operation.</summary>
    public string Operation { get; } = operation;
}

/// <summary>The runtime reached a terminal fault and refuses further work.</summary>
public sealed class SceneFaultedException(string message, Exception? inner = null)
    : SceneException(SceneErrorCodes.LifecycleFaulted, message, inner);

/// <summary>The runtime has already been disposed.</summary>
public sealed class SceneDisposedException(string objectName)
    : SceneException(SceneErrorCodes.LifecycleDisposed, $"{objectName} has been disposed.");
