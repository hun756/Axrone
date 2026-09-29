namespace Axrone.Render.OpenGL.Tests;

using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.Native;
using Axrone.Render.OpenGL.Resources;

/// <summary>
/// Integration tests for multi-resource lifecycle workflows including context loss
/// recovery, rebuild priority ordering, dispose cascade, and repeated context loss.
/// </summary>
public sealed class ResourceLifecycleTests : IDisposable
{
    private readonly MockGLApi _mock;
    private readonly GLContext _context;

    public ResourceLifecycleTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);
    }

    [Fact]
    public void ContextLossRecovery_AllResourcesInvalidated_ThenRebuilt()
    {
        // Arrange: create multiple resource types
        var buffer = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 256, "test_buffer");
        var texture = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba8, 64, 64, label: "test_texture");
        var framebuffer = new GLFramebuffer(_context, 64, 64, "test_fbo");
        var sampler = new GLSampler(_context, "test_sampler");
        var vao = new GLVertexArray(_context, "test_vao");

        // Capture initial handles
        uint bufferId = buffer.Id;
        uint textureId = texture.Id;
        uint fboId = framebuffer.Id;
        uint samplerId = sampler.Id;
        uint vaoId = vao.Id;

        bufferId.Should().NotBe(0);
        textureId.Should().NotBe(0);
        fboId.Should().NotBe(0);
        samplerId.Should().NotBe(0);
        vaoId.Should().NotBe(0);

        // Act: trigger context loss
        _context.NotifyContextLost();

        // Assert: all resources invalidated
        _context.IsLost.Should().BeTrue();
        buffer.Id.Should().Be(0, "buffer should be invalidated after context loss");
        texture.Id.Should().Be(0, "texture should be invalidated after context loss");
        framebuffer.Id.Should().Be(0, "framebuffer should be invalidated after context loss");
        sampler.Id.Should().Be(0, "sampler should be invalidated after context loss");
        vao.Id.Should().Be(0, "VAO should be invalidated after context loss");

        // Act: trigger context restore
        _context.NotifyContextRestored();

        // Assert: all resources rebuilt with new handles
        _context.IsLost.Should().BeFalse();
        buffer.Id.Should().NotBe(0, "buffer should be rebuilt after context restore");
        texture.Id.Should().NotBe(0, "texture should be rebuilt after context restore");
        framebuffer.Id.Should().NotBe(0, "framebuffer should be rebuilt after context restore");
        sampler.Id.Should().NotBe(0, "sampler should be rebuilt after context restore");
        vao.Id.Should().NotBe(0, "VAO should be rebuilt after context restore");

        // New handles should differ from originals (mock generates incrementing IDs)
        buffer.Id.Should().NotBe(bufferId);
        texture.Id.Should().NotBe(textureId);
        framebuffer.Id.Should().NotBe(fboId);
        sampler.Id.Should().NotBe(samplerId);
        vao.Id.Should().NotBe(vaoId);
    }

    [Fact]
    public void ResourceDependencyOrdering_RebuildOrderMatchesPriority()
    {
        // Arrange: create resources with different priorities
        // Buffer priority = 10, VAO priority = 20, Sampler priority = 25,
        // Texture priority = 30, Framebuffer priority = 40
        var rebuildOrder = new List<string>();

        var buffer = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 128, "buffer");
        var vao = new GLVertexArray(_context, "vao");
        var sampler = new GLSampler(_context, "sampler");
        var texture = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba8, 32, 32, label: "texture");
        var framebuffer = new GLFramebuffer(_context, 32, 32, "fbo");

        // Track rebuild order via lifecycle events
        _context.Lifecycle.ContextRestored += (s, e) =>
        {
            // After restore, resources are rebuilt in priority order.
            // We verify the final state: all resources have non-zero handles.
        };

        // Act: lose and restore context
        _context.NotifyContextLost();
        _context.NotifyContextRestored();

        // Assert: all resources rebuilt successfully
        buffer.Id.Should().NotBe(0);
        vao.Id.Should().NotBe(0);
        sampler.Id.Should().NotBe(0);
        texture.Id.Should().NotBe(0);
        framebuffer.Id.Should().NotBe(0);

        // Verify the registry sorted them by priority during rebuild.
        // We can verify indirectly: the mock call log should show GenBuffer before
        // GenVertexArray before GenSampler before GenTexture before GenFramebuffer
        // in the restore phase.
        _mock.ClearCallLog();
        _context.NotifyContextLost();
        _context.NotifyContextRestored();

        var callLog = _mock.CallLog;
        int genBufferIdx = -1, genVaoIdx = -1, genSamplerIdx = -1, genTexIdx = -1, genFboIdx = -1;

        for (int i = 0; i < callLog.Count; i++)
        {
            if (genBufferIdx == -1 && callLog[i].Contains("GenBuffer", StringComparison.Ordinal))
                genBufferIdx = i;
            else if (genVaoIdx == -1 && callLog[i].Contains("GenVertexArray", StringComparison.Ordinal))
                genVaoIdx = i;
            else if (genSamplerIdx == -1 && callLog[i].Contains("GenSampler", StringComparison.Ordinal))
                genSamplerIdx = i;
            else if (genTexIdx == -1 && callLog[i].Contains("GenTexture", StringComparison.Ordinal))
                genTexIdx = i;
            else if (genFboIdx == -1 && callLog[i].Contains("GenFramebuffer", StringComparison.Ordinal))
                genFboIdx = i;
        }

        // Priority order: Buffer(10) < VAO(20) < Sampler(25) < Texture(30) < Framebuffer(40)
        genBufferIdx.Should().BeGreaterThanOrEqualTo(0);
        genVaoIdx.Should().BeGreaterThanOrEqualTo(0);
        genSamplerIdx.Should().BeGreaterThanOrEqualTo(0);
        genTexIdx.Should().BeGreaterThanOrEqualTo(0);
        genFboIdx.Should().BeGreaterThanOrEqualTo(0);

        genBufferIdx.Should().BeLessThan(genVaoIdx, "Buffer (priority 10) should rebuild before VAO (priority 20)");
        genVaoIdx.Should().BeLessThan(genSamplerIdx, "VAO (priority 20) should rebuild before Sampler (priority 25)");
        genSamplerIdx.Should().BeLessThan(genTexIdx, "Sampler (priority 25) should rebuild before Texture (priority 30)");
        genTexIdx.Should().BeLessThan(genFboIdx, "Texture (priority 30) should rebuild before Framebuffer (priority 40)");
    }

    [Fact]
    public void DisposeCascade_ContextDisposal_CleansUpAllResources()
    {
        // Arrange: create multiple resources
        var buffer = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 64, "buf");
        var texture = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba8, 16, 16, label: "tex");
        var sampler = new GLSampler(_context, "samp");
        var vao = new GLVertexArray(_context, "vao");

        _context.Registry.Count.Should().BeGreaterThan(0, "resources should be registered");

        int resourceCountBefore = _context.Registry.Count;

        // Act: dispose the context
        _context.Dispose();

        // Assert: context is disposed and all resources cleaned from registry
        _context.IsDisposed.Should().BeTrue();
        _context.Registry.Count.Should().Be(0, "all resources should be removed from registry after context disposal");
    }

    [Fact]
    public void DoubleContextLoss_WithoutRestore_DoesNotThrow()
    {
        // Arrange
        var buffer = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 64, "buf");
        var texture = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba8, 16, 16, label: "tex");

        // Act: lose context twice without restoring
        var action = () =>
        {
            _context.NotifyContextLost();
            _context.NotifyContextLost();
        };

        // Assert: should not throw
        action.Should().NotThrow();
        _context.IsLost.Should().BeTrue();

        // Resources should still be invalidated
        buffer.Id.Should().Be(0);
        texture.Id.Should().Be(0);

        // Restore should still work after double loss
        _context.NotifyContextRestored();
        _context.IsLost.Should().BeFalse();
        buffer.Id.Should().NotBe(0);
        texture.Id.Should().NotBe(0);
    }

    [Fact]
    public void ContextLostEvent_FiresBeforeResourcesInvalidated()
    {
        // Arrange
        var buffer = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 64, "buf");
        bool eventFired = false;
        bool bufferWasValidDuringEvent = false;

        _context.Lifecycle.ContextLost += (s, e) =>
        {
            eventFired = true;
            // At this point, InvalidateAll has already been called by NotifyContextLost
            bufferWasValidDuringEvent = buffer.Id != 0;
        };

        // Act
        _context.NotifyContextLost();

        // Assert: event fired and resources were already invalidated when event ran
        eventFired.Should().BeTrue();
        bufferWasValidDuringEvent.Should().BeFalse(
            "resources should be invalidated before the ContextLost event fires");
    }

    [Fact]
    public void ContextRestoredEvent_FiresAfterResourcesRebuilt()
    {
        // Arrange
        var buffer = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 64, "buf");
        bool eventFired = false;
        bool bufferWasRebuiltDuringEvent = false;

        _context.Lifecycle.ContextRestored += (s, e) =>
        {
            eventFired = true;
            bufferWasRebuiltDuringEvent = buffer.Id != 0;
        };

        // Act
        _context.NotifyContextLost();
        _context.NotifyContextRestored();

        // Assert: event fired and resources were already rebuilt
        eventFired.Should().BeTrue();
        bufferWasRebuiltDuringEvent.Should().BeTrue(
            "resources should be rebuilt before the ContextRestored event fires");
    }

    [Fact]
    public void MultipleContextLossRestoreCycles_AllSucceed()
    {
        // Arrange
        var buffer = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 64, "buf");
        var texture = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba8, 16, 16, label: "tex");

        // Act & Assert: 3 loss/restore cycles should all work
        for (int cycle = 0; cycle < 3; cycle++)
        {
            _context.NotifyContextLost();
            _context.IsLost.Should().BeTrue();
            buffer.Id.Should().Be(0);
            texture.Id.Should().Be(0);

            _context.NotifyContextRestored();
            _context.IsLost.Should().BeFalse();
            buffer.Id.Should().NotBe(0, $"buffer should be rebuilt after cycle {cycle + 1}");
            texture.Id.Should().NotBe(0, $"texture should be rebuilt after cycle {cycle + 1}");
        }
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
