namespace Axrone.Render.OpenGL.Tests;

using Axrone.Geometry;
using Axrone.Render.Core.Abstractions;
using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.FrameGraph;
using Axrone.Render.OpenGL.FrameGraph.Passes;
using Axrone.Render.OpenGL.Mesh;
using Axrone.Render.OpenGL.Native;
using Axrone.Render.OpenGL.Resources;
using Axrone.Render.OpenGL.Shading;

/// <summary>
/// Regression suite for the OpenGL backend audit findings (#1-#12). Every test is written to be
/// discriminating: it fails against the pre-fix behaviour and passes against the fix, and several
/// include a paired negative control so an over-correction (e.g. "never issue the GL call") also
/// fails. Finding #10 (TransformFeedbackVaryings) is intentionally not asserted here: the fix lives
/// in the real Silk.NET binding, so a mock assertion would be vacuous — it is verified by compiling
/// against the real 4-argument Silk.NET overload.
/// </summary>
public sealed class RendererAuditRegressionTests
{
    // ---------------------------------------------------------------------
    // #1 — state cache deduplication must be active on a freshly built cache.
    // ---------------------------------------------------------------------

    [Fact]
    public void Finding01_StateCache_FreshConstruction_DeduplicatesRedundantCall()
    {
        var mock = new MockGLApi();
        var cache = new GLStateCache(mock);

        cache.BindArrayBuffer(7);
        mock.ClearCallLog();

        // Same value again: with dedup active this must NOT reach the driver.
        cache.BindArrayBuffer(7);

        mock.CallLog.Should().NotContain(c => c.Contains("BindBuffer", StringComparison.Ordinal));
    }

    [Fact]
    public void Finding01_StateCache_FreshConstruction_FirstCallStillReachesDriver()
    {
        var mock = new MockGLApi();
        var cache = new GLStateCache(mock);
        mock.ClearCallLog();

        // Negative control: the sentinel seeding must still force the first real call through,
        // otherwise "dedup" would just mean "never call GL".
        cache.BindArrayBuffer(7);

        mock.CallLog.Should().Contain(c => c.Contains("BindBuffer", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------
    // #2 — GL_MAX_VIEWPORT_DIMS returns two integers from one pname.
    // ---------------------------------------------------------------------

    [Fact]
    public void Finding02_Capabilities_MaxViewportDims_ReadsWidthAndHeightFromSinglePname()
    {
        var mock = new MockGLApi { MaxViewportWidth = 32768, MaxViewportHeight = 16384 };

        var caps = new GLCapabilities(mock);

        caps.MaxViewportDims.Width.Should().Be(32768);
        caps.MaxViewportDims.Height.Should().Be(16384);
    }

    // ---------------------------------------------------------------------
    // #3 — a buffer's data operation must target its OWN binding point.
    // ---------------------------------------------------------------------

    [Fact]
    public void Finding03_ElementArrayBuffer_Update_WritesToItsOwnBinding_NotAnotherElementBuffer()
    {
        var mock = new MockGLApi();
        using var context = new GLContext(mock);

        // Two ELEMENT_ARRAY_BUFFER objects. Creating `other` leaves IT bound to the
        // ELEMENT_ARRAY_BUFFER point. If Update() binds to the wrong target (ARRAY_BUFFER),
        // the subsequent BufferSubData(ELEMENT_ARRAY_BUFFER, ...) silently lands in `other`
        // instead of `ibo` -- the exact corruption the audit flagged.
        var ibo = new GLBuffer(context, GLConst.ElementArrayBuffer, GLConst.StaticDraw, 4, "ibo");
        var other = new GLBuffer(context, GLConst.ElementArrayBuffer, GLConst.StaticDraw, 4, "other");

        byte[] data = [10, 20, 30, 40];
        ibo.Update(data);

        bool iboFound = mock.BufferStorage.TryGetValue(ibo.Id, out byte[]? inIbo);
        iboFound.Should().BeTrue("the index buffer's own storage must receive the uploaded data");
        inIbo.Should().Equal(data);

        // And the write must not have leaked into the other element-array buffer.
        mock.BufferStorage.ContainsKey(other.Id).Should().BeFalse();
    }

    // ---------------------------------------------------------------------
    // #4 — index element type must drive index count and draw type.
    // ---------------------------------------------------------------------

    [Fact]
    public void Finding04_Mesh_WithUShortIndices_ComputesCountAndDrawTypeFromIndexType()
    {
        var mock = new MockGLApi();
        using var context = new GLContext(mock);

        byte[] vertexBytes = new byte[128]; // 4 verts * 32-byte DefaultLayout stride
        byte[] indexBytes = new byte[12];   // 6 x ushort
        var mesh = new GLMesh(
            context,
            MeshGenerators.DefaultLayout,
            vertexBytes,
            indexBytes,
            Aabb3D.Empty,
            GLConst.Triangles,
            "m16",
            GLConst.UnsignedShort);

        // 12 bytes / sizeof(ushort) == 6 (pre-fix divided by sizeof(uint) -> 3).
        mesh.IndexCount.Should().Be(6);
        mesh.IndexType.Should().Be(GLConst.UnsignedShort);

        mock.ClearCallLog();
        mesh.Draw();

        mock.CallLog.Should().Contain(c =>
            c.Contains("DrawElements", StringComparison.Ordinal) &&
            c.Contains(GLConst.UnsignedShort.ToString(), StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------
    // #5 — framebuffer restore must re-attach a depth renderbuffer.
    // ---------------------------------------------------------------------

    [Fact]
    public void Finding05_Framebuffer_Restore_ReattachesDepthRenderbuffer()
    {
        var mock = new MockGLApi();
        using var context = new GLContext(mock);
        var fbo = new GLFramebuffer(context, 64, 64, "fbo");
        var depth = new GLRenderbuffer(context, GLConst.DepthComponent24, 64, 64, "depth");
        fbo.AttachDepthRenderbuffer(depth);

        context.NotifyContextLost();
        mock.ClearCallLog();
        context.NotifyContextRestored();

        mock.CallLog.Should().Contain(c =>
            c.Contains("FramebufferRenderbuffer", StringComparison.Ordinal) &&
            c.Contains(GLConst.DepthAttachment.ToString(), StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------
    // #6 — restore must rebuild resource CONTENT, not just handles.
    // ---------------------------------------------------------------------

    [Fact]
    public void Finding06_Texture_Restore_ReuploadsSnapshottedPixels()
    {
        var mock = new MockGLApi();
        using var context = new GLContext(mock);
        var tex = new GLTexture(context, GLConst.Texture2D, TextureFormat.Rgba8, 2, 2);
        byte[] pixels = new byte[2 * 2 * 4];
        pixels[0] = 0xAB;
        tex.UploadSubData2D(pixels, 0, 0, 2, 2); // full base-level upload -> snapshot

        context.NotifyContextLost();
        mock.ClearCallLog();
        context.NotifyContextRestored();

        mock.CallLog.Should().Contain(c => c.Contains("TexSubImage2D", StringComparison.Ordinal));
    }

    [Fact]
    public void Finding06_Texture_Restore_DoesNotUpload_WhenNeverCpuUploaded()
    {
        var mock = new MockGLApi();
        using var context = new GLContext(mock);
        var renderTarget = new GLTexture(context, GLConst.Texture2D, TextureFormat.Rgba8, 8, 8);

        context.NotifyContextLost();
        mock.ClearCallLog();
        context.NotifyContextRestored();

        // Negative control: render targets hold no CPU copy, so restore must re-allocate only.
        mock.CallLog.Should().Contain(c => c.Contains("TexStorage2D", StringComparison.Ordinal));
        mock.CallLog.Should().NotContain(c => c.Contains("TexSubImage2D", StringComparison.Ordinal));
    }

    [Fact]
    public void Finding06_VertexArray_Restore_ReappliesAttributeLayout()
    {
        var mock = new MockGLApi();
        using var context = new GLContext(mock);
        var vbo = new GLBuffer(context, GLConst.ArrayBuffer, GLConst.StaticDraw, 128, "vbo");
        var vao = new GLVertexArray(context, "vao");
        vao.ConfigureLayout(vbo, MeshGenerators.DefaultLayout.Attributes, 4);

        context.NotifyContextLost();
        mock.ClearCallLog();
        context.NotifyContextRestored();

        mock.CallLog.Should().Contain(c => c.Contains("VertexAttribPointer", StringComparison.Ordinal));
    }

    [Fact]
    public void Finding06_TransformFeedback_Restore_ReappliesBufferBindings()
    {
        var mock = new MockGLApi();
        using var context = new GLContext(mock);
        var tf = new GLTransformFeedback(context, "tf");
        var buffer = new GLBuffer(context, GLConst.TransformFeedbackBuffer, GLConst.StaticDraw, 256, "tfbuf");
        tf.Bind();
        tf.BindBuffer(0, buffer);

        context.NotifyContextLost();
        mock.ClearCallLog();
        context.NotifyContextRestored();

        mock.CallLog.Should().Contain(c => c.Contains("BindBufferBase", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------
    // #7 — sampler anisotropy must actually be applied (and clamped).
    // ---------------------------------------------------------------------

    [Fact]
    public void Finding07_Sampler_Anisotropy_AppliedWhenSupported()
    {
        var mock = new MockGLApi { MaxTextureMaxAnisotropy = 16 };
        using var context = new GLContext(mock);
        var sampler = new GLSampler(context, "s");
        sampler.SetAnisotropy(8f);
        mock.ClearCallLog();

        sampler.Apply();

        mock.CallLog.Should().Contain(c =>
            c.Contains("SamplerParameter", StringComparison.Ordinal) &&
            c.Contains(GLConst.TextureMaxAnisotropyExt.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public void Finding07_Sampler_Anisotropy_ClampedToDriverMax()
    {
        var mock = new MockGLApi { MaxTextureMaxAnisotropy = 4 };
        using var context = new GLContext(mock);
        var sampler = new GLSampler(context, "s");
        sampler.SetAnisotropy(16f);
        mock.ClearCallLog();

        sampler.Apply();

        // Requested 16 but the driver max is 4 -> the applied value must be clamped to 4.
        mock.CallLog.Should().Contain(c => c.Contains($"{GLConst.TextureMaxAnisotropyExt}, 4)", StringComparison.Ordinal));
    }

    [Fact]
    public void Finding07_Sampler_Anisotropy_SkippedWhenUnsupported()
    {
        var mock = new MockGLApi { MaxTextureMaxAnisotropy = 0 };
        using var context = new GLContext(mock);
        var sampler = new GLSampler(context, "s");
        sampler.SetAnisotropy(8f);
        mock.ClearCallLog();

        sampler.Apply();

        // Negative control: without the extension, setting the enum would raise GL_INVALID_ENUM.
        mock.CallLog.Should().NotContain(c => c.Contains(GLConst.TextureMaxAnisotropyExt.ToString(), StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------
    // #8 — timer query results are 64-bit and must not truncate.
    // ---------------------------------------------------------------------

    [Fact]
    public void Finding08_Query_GetResult_ReadsFull64BitValue()
    {
        var mock = new MockGLApi { QueryResult64 = 5_000_000_000UL }; // > int.MaxValue
        using var context = new GLContext(mock);
        var query = new GLQuery(context, QueryTarget.TimeElapsed, "timer");

        query.GetResult().Should().Be(5_000_000_000L);
    }

    // ---------------------------------------------------------------------
    // #9 — IRenderContext.BindTexture must honor a non-2D target.
    // ---------------------------------------------------------------------

    [Fact]
    public void Finding09_RenderContext_BindTexture_HonorsCubeMapTarget()
    {
        var mock = new MockGLApi();
        using var context = new GLContext(mock);
        var rc = new GLRenderContext(context);
        mock.ClearCallLog();

        rc.BindTexture(0, 42, GLConst.TextureCubeMap);

        mock.CallLog.Should().Contain(c => c.Contains($"BindTexture({GLConst.TextureCubeMap}, 42)", StringComparison.Ordinal));
    }

    [Fact]
    public void Finding09_RenderContext_BindTexture_2DPathStillWorks()
    {
        var mock = new MockGLApi();
        using var context = new GLContext(mock);
        var rc = new GLRenderContext(context);
        mock.ClearCallLog();

        rc.BindTexture(0, 42, GLConst.Texture2D);

        // Negative control: the common 2D path must remain on the cached per-unit bind.
        mock.CallLog.Should().Contain(c => c.Contains($"BindTexture({GLConst.Texture2D}, 42)", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------
    // #11 — compute write bindings must declare graph writes.
    // ---------------------------------------------------------------------

    [Fact]
    public void Finding11_ComputePass_WriteBinding_DeclaresGraphWrite()
    {
        var mock = new MockGLApi();
        using var context = new GLContext(mock);
        var program = new GLProgram(context, "vs", "fs");

        var pass = ComputePass.Create(
            "compute",
            program,
            bindings:
            [
                new ComputeResourceBinding(0, "ssbo", ComputeBindingType.ShaderStorageBuffer, ComputeBindingAccess.Write),
                new ComputeResourceBinding(1, "ubo", ComputeBindingType.UniformBuffer),
            ]);

        pass.GetWrittenResources().ToArray().Should().Contain("ssbo");
        pass.GetReadResources().ToArray().Should().Contain("ubo");
        pass.GetReadResources().ToArray().Should().NotContain("ssbo");
    }

    // ---------------------------------------------------------------------
    // #12 — transparent sort must be camera-relative, not world-origin-relative.
    // ---------------------------------------------------------------------

    [Fact]
    public void Finding12_TransparentPass_SortsBackToFrontRelativeToCamera()
    {
        var mock = new MockGLApi();
        using var context = new GLContext(mock);
        var program = new GLProgram(context, "vs", "fs");

        // Camera far along +X. A sits at the world origin (100 units from the camera);
        // B sits at x=90 (10 units from the camera). World-origin sorting would draw B first;
        // correct camera-relative back-to-front draws the farther A first.
        var pass = TransparentPass.Create("transparent", program, null, new Vector3(100, 0, 0));
        var meshA = MeshGenerators.CreatePlane(context, 1, 1, "A");
        var meshB = MeshGenerators.CreatePlane(context, 1, 1, "B");
        pass.Data.Entries.Add(new TransparentMeshEntry(meshA, new Vector3(0, 0, 0)));
        pass.Data.Entries.Add(new TransparentMeshEntry(meshB, new Vector3(90, 0, 0)));

        var passContext = new PassExecutionContext(context);
        var renderContext = new GLRenderContext(context, passContext);
        ((IRenderPass)pass).Execute(renderContext);

        pass.Data.Entries[0].WorldPosition.Should().Be(new Vector3(0, 0, 0));
        pass.Data.Entries[1].WorldPosition.Should().Be(new Vector3(90, 0, 0));
    }
}
