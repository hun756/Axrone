namespace Axrone.Scene.Assets;

/// <summary>Stable error codes emitted by the scene asset layer.</summary>
public static class SceneAssetErrorCodes
{
    /// <summary>An asset id is not present in its registry.</summary>
    public const string NotFound = "scene.asset.not-found";

    /// <summary>An asset definition failed validation.</summary>
    public const string Invalid = "scene.asset.invalid";

    /// <summary>A definition was registered under a different id than it declares.</summary>
    public const string IdMismatch = "scene.asset.id-mismatch";
}

/// <summary>Base asset failure with a stable code.</summary>
public class SceneAssetException : SceneException
{
    /// <summary>Creates an asset failure.</summary>
    public SceneAssetException(string code, string message, Exception? inner = null)
        : base(code, message, inner)
    {
    }
}

/// <summary>An asset id does not resolve in its registry.</summary>
public sealed class SceneAssetNotFoundException<TTag> : SceneAssetException
    where TTag : struct
{
    /// <summary>Missing id.</summary>
    public SceneAssetId<TTag> Id { get; }

    /// <summary>Creates a not-found failure.</summary>
    public SceneAssetNotFoundException(SceneAssetId<TTag> id)
        : base(SceneAssetErrorCodes.NotFound, $"Asset {id} is not registered.")
    {
        Id = id;
    }
}
