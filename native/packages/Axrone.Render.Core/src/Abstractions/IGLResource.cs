namespace Axrone.Render.Core.Abstractions;

/// <summary>
/// Interface for GPU resources that participate in context-loss recovery.
/// Resources implement this to be tracked by the GL context
/// and receive lifecycle callbacks on context loss/restoration.
/// </summary>
public interface IGLResource : IDisposable
{
    /// <summary>Gets the registry sequence number (set when registered).</summary>
    int RegistrySequence { get; set; }

    /// <summary>Gets the rebuild priority (lower = rebuilt first on context restore).</summary>
    int RebuildPriority { get; }

    /// <summary>
    /// Invalidates the GPU handle immediately (sets it to 0).
    /// Called during context-loss notification before full <see cref="OnContextLost"/> processing.
    /// </summary>
    void Invalidate();

    /// <summary>
    /// Rebuilds the GPU resource from its snapshot/configuration.
    /// Called during context-restoration to re-create GPU handles.
    /// </summary>
    void Rebuild();

    /// <summary>Called when the GL context is lost. Resets handles to zero.</summary>
    void OnContextLost();

    /// <summary>Called when the GL context is restored. Re-creates GPU resources.</summary>
    void OnContextRestored();
}
