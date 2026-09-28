namespace Axrone.Render.OpenGL.FrameGraph.PassExecutors;

/// <summary>
/// One level of a bloom mip pyramid: the framebuffer the level renders into and the
/// texture its color attachment points at.
/// </summary>
/// <remarks>
/// A caller can hand a pre-allocated chain to
/// <see cref="BloomPassExecutor.WithMipPyramid"/> to share pyramid memory between
/// passes, multiple cameras or render targets. Supplied levels are never disposed by
/// the pass; their lifetime stays with the owner that created them.
/// </remarks>
/// <param name="Framebuffer">The framebuffer that renders this level.</param>
/// <param name="Texture">The color texture attached to <paramref name="Framebuffer"/>.</param>
public readonly record struct BloomMipLevel(GLFramebuffer Framebuffer, GLTexture Texture);
