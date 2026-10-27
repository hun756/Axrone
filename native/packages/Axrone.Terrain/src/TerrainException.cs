namespace Axrone.Terrain;

/// <summary>Stable failure codes carried by <see cref="TerrainException"/>.</summary>
public enum TerrainErrorCode
{
    /// <summary>A grid or splat resolution outside the supported set was requested.</summary>
    InvalidResolution,

    /// <summary>A terrain dimension is non-finite or non-positive.</summary>
    InvalidDimensions,

    /// <summary>A height buffer length does not match the descriptor resolution.</summary>
    HeightmapSizeMismatch,

    /// <summary>A splat, density or foliage buffer length does not match its resolution.</summary>
    SplatSizeMismatch,

    /// <summary>A serialized payload is malformed or carries the wrong prefix.</summary>
    SourceDecodeFailed,

    /// <summary>A parameter is outside its documented domain.</summary>
    ValidationFailed,
}

/// <summary>
/// Coded exception thrown by every terrain entry point. The message is prefixed
/// with <c>Terrain: </c> so call sites can filter terrain failures cheaply.
/// </summary>
public sealed class TerrainException : Exception
{
    /// <summary>Initializes a coded terrain exception.</summary>
    /// <param name="message">Failure description without the package prefix.</param>
    /// <param name="code">Stable failure code.</param>
    public TerrainException(string message, TerrainErrorCode code)
        : base(FormattableString.Invariant($"Terrain: {message}"))
    {
        Code = code;
    }

    /// <summary>Initializes a coded terrain exception with an underlying cause.</summary>
    /// <param name="message">Failure description without the package prefix.</param>
    /// <param name="code">Stable failure code.</param>
    /// <param name="innerException">Underlying cause.</param>
    public TerrainException(string message, TerrainErrorCode code, Exception innerException)
        : base(FormattableString.Invariant($"Terrain: {message}"), innerException)
    {
        Code = code;
    }

    /// <summary>Gets the stable failure code.</summary>
    public TerrainErrorCode Code { get; }
}
