namespace Axrone.Render.Effects.Tests;

/// <summary>
/// Tests for the mip-pyramid half of <see cref="BloomPassExecutor"/>: stage order,
/// attachment invalidation discipline, blend-state restoration, uniform upload and
/// configuration validation, plus the untouched legacy Gaussian path.
/// </summary>
public sealed class BloomPyramidTests : IDisposable
{
    private const string SourceName = "scene_hdr";
    private const string OutputName = "scene_bloom";
    private const uint GLFrameBuffer = GLConst.Framebuffer;

    private readonly MockGLApi _mock;
    private readonly GLContext _context;
    private readonly GLProgram _bright;
    private readonly GLProgram _blur;
    private readonly GLProgram _composite;
    private readonly GLTexture _source;
    private readonly GLTexture _output;

    public BloomPyramidTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);

        _bright = new GLProgram(_context, BloomShaders.FullscreenTriangleVertex, BloomShaders.PrefilterFragment);
        _blur = new GLProgram(_context, BloomShaders.FullscreenTriangleVertex, BloomShaders.DownsampleFragment);
        _composite = new GLProgram(_context, BloomShaders.FullscreenTriangleVertex, BloomShaders.CompositeFragment);

        _source = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba16f, 64, 64, label: "test_scene");
        _output = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba16f, 64, 64, label: "test_bloom");
    }

    public void Dispose()
    {
        _source.Dispose();
        _output.Dispose();
        _bright.Dispose();
        _blur.Dispose();
        _composite.Dispose();
        _context.Dispose();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private IReadOnlyList<string> Log => _mock.CallLog;

    private BloomPassExecutor CreatePass() =>
        new("bloom", _bright, _blur, _composite, SourceName, OutputName);

    private PassExecutionContext CreateExecutionContext()
    {
        var ctx = new PassExecutionContext(_context);
        ctx.SetResource(SourceName, _source);
        ctx.SetResource(OutputName, _output);
        return ctx;
    }

    /// <summary>
    /// Builds a supplied three-level chain (32x32, 16x16, 8x8) so the stage count and
    /// the viewport order are fully determined.
    /// </summary>
    private BloomMipLevel[] CreateChain(int count = 3)
    {
        var levels = new BloomMipLevel[count];
        for (int i = 0; i < count; i++)
        {
            int size = Math.Max(1, 32 >> i);
            var texture = new GLTexture(_context, GLConst.Texture2D, TextureFormat.Rgba16f, size, size, label: "chain" + i);
            var framebuffer = new GLFramebuffer(_context, size, size, "chain_fbo" + i);
            framebuffer.AttachColor(texture, 0);
            levels[i] = new BloomMipLevel(framebuffer, texture);
        }

        return levels;
    }

    private static void DisposeChain(BloomMipLevel[] levels)
    {
        foreach (BloomMipLevel level in levels)
        {
            level.Framebuffer?.Dispose();
            level.Texture?.Dispose();
        }
    }

    private List<int> IndicesOf(string prefix)
    {
        var indices = new List<int>();
        for (int i = 0; i < Log.Count; i++)
        {
            if (Log[i].StartsWith(prefix, StringComparison.Ordinal))
            {
                indices.Add(i);
            }
        }

        return indices;
    }

    private int CountOf(string prefix) => IndicesOf(prefix).Count;

    private List<int> DrawIndices() => IndicesOf("DrawArrays(");

    private int LastIndexBefore(string prefix, int exclusive)
    {
        for (int i = exclusive - 1; i >= 0; i--)
        {
            if (Log[i].StartsWith(prefix, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Viewport width in effect at each draw, in call order.</summary>
    private List<int> ViewportWidthPerDraw()
    {
        var widths = new List<int>();
        int current = -1;
        foreach (string call in Log)
        {
            if (call.StartsWith("Viewport(", StringComparison.Ordinal))
            {
                // "Viewport({x}, {y}, {width}, {height})"
                string[] parts = call.Split(", ");
                current = int.Parse(parts[2]);
            }
            else if (call.StartsWith("DrawArrays(", StringComparison.Ordinal))
            {
                widths.Add(current);
            }
        }

        return widths;
    }

    // =========================================================================
    // Stage order
    // =========================================================================

    [Fact]
    public void Pyramid_ExecutesPrefilterDownsampleUpsampleCompositeInOrder()
    {
        BloomMipLevel[] chain = CreateChain();
        try
        {
            BloomPassExecutor pass = CreatePass().WithMipPyramid(chain);
            _mock.ClearCallLog();

            pass.Execute(_context, CreateExecutionContext());

            // 1 prefilter + 2 downsample + 2 upsample + 1 composite.
            List<int> draws = DrawIndices();
            draws.Should().HaveCount(6);

            // Viewports prove which level each draw targets: 32, 16, 8 halving down,
            // then 16, 32 accumulating back up, then the 64x64 output.
            ViewportWidthPerDraw().Should().Equal(32, 16, 8, 16, 32, 64);

            // Program switches mark the stage boundaries. With state-cache deduplication active,
            // consecutive binds of the same program collapse to a single GL call: the prefilter
            // binds _bright, the two downsamples and two upsamples all share _blur (one bind),
            // and the composite binds _composite -> three distinct binds. The stage ORDER is
            // still fully proven by the six draws and their viewport widths asserted above.
            List<int> useProgram = IndicesOf("UseProgram(");
            useProgram.Should().HaveCount(3);
            Log[useProgram[0]].Should().Be($"UseProgram({_bright.Id})");
            Log[useProgram[^1]].Should().Be($"UseProgram({_composite.Id})");
            for (int i = 1; i < useProgram.Count - 1; i++)
            {
                Log[useProgram[i]].Should().Be($"UseProgram({_blur.Id})");
            }

            useProgram[0].Should().BeLessThan(draws[0]);
            useProgram[^1].Should().BeGreaterThan(draws[4]);

            // The composite is the final draw and blending is off by then.
            Log[draws[5]].Should().Be("DrawArrays(4, 0, 3)");
            IndicesOf("Disable(3042)")[0].Should().BeLessThan(draws[5]);
        }
        finally
        {
            DisposeChain(chain);
        }
    }

    [Fact]
    public void Pyramid_DefaultChainIsDerivedFromTheSourceResolution()
    {
        BloomPassExecutor pass = CreatePass();
        _mock.ClearCallLog();

        pass.Execute(_context, CreateExecutionContext());

        // A 64x64 source halves to 32, 16, 8, 4, 2, 1: six levels.
        pass.LevelCount.Should().Be(6);
        pass.PyramidWidth.Should().Be(32);
        pass.PyramidHeight.Should().Be(32);
        pass.OwnsMipPyramid.Should().BeTrue();

        // 1 prefilter + 5 downsample + 5 upsample + 1 composite.
        DrawIndices().Should().HaveCount(12);

        pass.Dispose();
        pass.LevelCount.Should().Be(0, "disposal drops the derived chain");
    }

    [Fact]
    public void Pyramid_MipCountOverridesTheDerivedChain()
    {
        BloomPassExecutor pass = CreatePass().WithMipCount(3);

        pass.Execute(_context, CreateExecutionContext());

        pass.LevelCount.Should().Be(3);
        pass.MipCount.Should().Be(3);
        pass.GetPyramidLevel(2).Framebuffer.Width.Should().Be(8);

        pass.Dispose();
    }

    [Fact]
    public void Pyramid_SuppliedChainIsUsedVerbatimAndNeverDisposed()
    {
        BloomMipLevel[] chain = CreateChain();
        try
        {
            BloomPassExecutor pass = CreatePass().WithMipPyramid(chain);

            pass.Execute(_context, CreateExecutionContext());

            pass.LevelCount.Should().Be(3);
            pass.PyramidWidth.Should().Be(32);
            pass.GetPyramidLevel(1).Texture.Should().BeSameAs(chain[1].Texture);
            pass.OwnsMipPyramid.Should().BeFalse();

            pass.Dispose();

            foreach (BloomMipLevel level in chain)
            {
                level.Texture.IsDisposed.Should().BeFalse("a supplied chain is never disposed by the pass");
                level.Framebuffer.IsDisposed.Should().BeFalse();
            }
        }
        finally
        {
            DisposeChain(chain);
        }
    }

    [Fact]
    public void Pyramid_InternalChainIsDisposedWithThePass()
    {
        BloomPassExecutor pass = CreatePass().WithMipCount(2);
        pass.Execute(_context, CreateExecutionContext());

        BloomMipLevel level0 = pass.GetPyramidLevel(0);
        BloomMipLevel level1 = pass.GetPyramidLevel(1);

        pass.Dispose();

        level0.Texture.IsDisposed.Should().BeTrue();
        level1.Framebuffer.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void GetPyramidLevel_RejectsUnbuiltAndOutOfRangeIndices()
    {
        BloomPassExecutor pass = CreatePass();

        Action notBuilt = () => pass.GetPyramidLevel(0);
        notBuilt.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidOperation);

        pass.WithMipCount(2);
        pass.Execute(_context, CreateExecutionContext());

        Action tooHigh = () => pass.GetPyramidLevel(2);
        tooHigh.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidValue);

        Action negative = () => pass.GetPyramidLevel(-1);
        negative.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidValue);

        pass.Dispose();
    }

    // =========================================================================
    // Invalidation discipline
    // =========================================================================

    [Fact]
    public void Pyramid_PrefilterAndDownsampleInvalidateDontCare()
    {
        BloomMipLevel[] chain = CreateChain();
        try
        {
            BloomPassExecutor pass = CreatePass().WithMipPyramid(chain);
            _mock.ClearCallLog();

            pass.Execute(_context, CreateExecutionContext());

            CountOf($"InvalidateFramebuffer({GLFrameBuffer}, 1)").Should().Be(3,
                "prefilter plus two downsamples overwrite their target completely");
        }
        finally
        {
            DisposeChain(chain);
        }
    }

    [Fact]
    public void Pyramid_UpsampleLoadsInsteadOfInvalidatingItsAccumulationBase()
    {
        BloomMipLevel[] chain = CreateChain();
        try
        {
            BloomPassExecutor pass = CreatePass().WithMipPyramid(chain);
            _mock.ClearCallLog();

            pass.Execute(_context, CreateExecutionContext());

            CountOf($"InvalidateFramebuffer({GLFrameBuffer}, 0) []").Should().Be(2,
                "each upsample loads the destination level instead of discarding it");

            List<int> draws = DrawIndices();
            int firstBlendEnable = IndicesOf("Enable(3042)")[0];

            // Draws 3 and 4 are the upsample accumulations: the last invalidate before
            // each of them is the load, never a DontCare.
            foreach (int draw in new[] { draws[3], draws[4] })
            {
                Log[LastIndexBefore("InvalidateFramebuffer(", draw)]
                    .Should().Be($"InvalidateFramebuffer({GLFrameBuffer}, 0) []");
                draw.Should().BeGreaterThan(firstBlendEnable);
            }
        }
        finally
        {
            DisposeChain(chain);
        }
    }

    // =========================================================================
    // Blend state save / restore
    // =========================================================================

    [Fact]
    public void Pyramid_RestoresTheBlendStateItOverrode()
    {
        const uint ReverseSubtract = 0x8003; // GL_FUNC_REVERSE_SUBTRACT, distinct from FuncAdd

        BloomMipLevel[] chain = CreateChain();
        try
        {
            BloomPassExecutor pass = CreatePass().WithMipPyramid(chain);

            // The prefilter and downsample stages own the blend enable, but the
            // function and equation the caller had installed must survive the pass.
            _context.State.SetBlend(false);
            _context.State.SetBlendEquationSeparate(ReverseSubtract, ReverseSubtract);
            _context.State.SetBlendFuncSeparate(GLConst.SrcAlpha, GLConst.OneMinusSrcAlpha, GLConst.One, GLConst.Zero);
            _mock.ClearCallLog();

            pass.Execute(_context, CreateExecutionContext());

            _context.State.BlendEnabled.Should().BeFalse();
            _context.State.BlendEquationRGB.Should().Be(ReverseSubtract);
            _context.State.BlendEquationAlpha.Should().Be(ReverseSubtract);
            _context.State.BlendSrcRGB.Should().Be(GLConst.SrcAlpha);
            _context.State.BlendDstRGB.Should().Be(GLConst.OneMinusSrcAlpha);
            _context.State.BlendSrcAlpha.Should().Be(GLConst.One);
            _context.State.BlendDstAlpha.Should().Be(GLConst.Zero);

            _mock.CallLog.Should().Contain($"BlendFuncSeparate({GLConst.One}, {GLConst.One}, {GLConst.One}, {GLConst.OneMinusSrcAlpha})");
            _mock.CallLog.Should().Contain($"BlendEquationSeparate({GLConst.FuncAdd}, {GLConst.FuncAdd})");
            _mock.CallLog.Should().Contain($"BlendFuncSeparate({GLConst.SrcAlpha}, {GLConst.OneMinusSrcAlpha}, {GLConst.One}, {GLConst.Zero})");
            _mock.CallLog.Should().Contain($"BlendEquationSeparate({ReverseSubtract}, {ReverseSubtract})");
        }
        finally
        {
            DisposeChain(chain);
        }
    }

    [Fact]
    public void Pyramid_LeavesBlendingDisabledWhenItWasDisabledBefore()
    {
        BloomMipLevel[] chain = CreateChain();
        try
        {
            BloomPassExecutor pass = CreatePass().WithMipPyramid(chain);

            _context.State.SetBlend(false);
            _mock.ClearCallLog();

            pass.Execute(_context, CreateExecutionContext());

            _context.State.BlendEnabled.Should().BeFalse();
            _mock.CallLog.Should().Contain("Disable(3042)");
        }
        finally
        {
            DisposeChain(chain);
        }
    }

    // =========================================================================
    // Uniform upload
    // =========================================================================

    [Fact]
    public void Pyramid_UploadsIntensityToBothCompositeUniformNames()
    {
        _mock.SetActiveUniforms(_composite.Id,
        [
            ("u_intensity", 1, 0x1406),
            ("u_bloomIntensity", 1, 0x1406)
        ]);

        BloomPassExecutor pass = CreatePass().WithMipCount(2).WithBloomIntensity(0.75f);
        _mock.ClearCallLog();

        pass.Execute(_context, CreateExecutionContext());

        int intensity = _composite.GetUniformLocation("u_intensity");
        int legacy = _composite.GetUniformLocation("u_bloomIntensity");
        intensity.Should().NotBe(-1);
        legacy.Should().NotBe(-1);

        _mock.CallLog.Should().Contain($"Uniform1({intensity}, 0.75)");
        _mock.CallLog.Should().Contain($"Uniform1({legacy}, 0.75)");

        pass.Dispose();
    }

    [Fact]
    public void Pyramid_RereadsLocationsAfterAProgramRebuild()
    {
        _mock.SetActiveUniforms(_composite.Id, [("u_intensity", 1, 0x1406)]);
        BloomPassExecutor pass = CreatePass().WithMipCount(2).WithBloomIntensity(0.5f);
        PassExecutionContext ctx = CreateExecutionContext();
        pass.Execute(_context, ctx);
        _mock.ClearCallLog();

        // A rebuild recycles the driver's location allocation and drops the reflection
        // cache, which is why locations are re-read on every execution.
        _composite.Rebuild();
        _mock.SetActiveUniforms(_composite.Id, [("u_intensity", 1, 0x1406)]);

        pass.Execute(_context, ctx);

        _mock.CallLog.Should().Contain(e => e.StartsWith("GetUniformLocation(", StringComparison.Ordinal)
            && e.Contains("u_intensity", StringComparison.Ordinal));
        _mock.CallLog.Should().Contain($"Uniform1({_composite.GetUniformLocation("u_intensity")}, 0.5)");

        pass.Dispose();
    }

    [Fact]
    public void Pyramid_UploadsTexelSizeKarisRadiusAndScatter()
    {
        _mock.SetActiveUniforms(_blur.Id,
        [
            ("u_source", 1, 0x8B5E),
            ("u_texelSize", 1, 0x8B50),
            ("u_karis", 1, 0x1406),
            ("u_radius", 1, 0x1406),
            ("u_scatter", 1, 0x1406)
        ]);

        BloomPassExecutor pass = CreatePass().WithMipCount(4).WithRadius(2.5f).WithScatter(0.5f);
        _mock.ClearCallLog();

        pass.Execute(_context, CreateExecutionContext());

        int texelSize = _blur.GetUniformLocation("u_texelSize");
        int karis = _blur.GetUniformLocation("u_karis");

        _mock.CallLog.Should().Contain($"Uniform2({texelSize}, 0.03125, 0.03125)", "1/32 for the first halving");
        _mock.CallLog.Should().Contain($"Uniform2({texelSize}, 0.125, 0.125)");
        _mock.CallLog.Should().Contain($"Uniform1({karis}, 0)", "the first halving must not re-weight an already weighted level");
        _mock.CallLog.Should().Contain($"Uniform1({karis}, 1)");
        _mock.CallLog.Should().Contain($"Uniform1({_blur.GetUniformLocation("u_radius")}, 2.5)");
        _mock.CallLog.Should().Contain($"Uniform1({_blur.GetUniformLocation("u_scatter")}, 0.5)");

        pass.Dispose();
    }

    [Fact]
    public void Pyramid_UploadsPrefilterCurveAndTexelSize()
    {
        _mock.SetActiveUniforms(_bright.Id,
        [
            ("u_source", 1, 0x8B5E),
            ("u_texelSize", 1, 0x8B50),
            ("u_curve", 1, 0x8B52)
        ]);

        BloomPassExecutor pass = CreatePass().WithMipCount(2).WithThreshold(2f).WithSoftKnee(0.5f);
        _mock.ClearCallLog();

        pass.Execute(_context, CreateExecutionContext());

        // threshold 2, knee = 2 * 0.5 = 1, Karis gain 1, input gain 1.
        _mock.CallLog.Should().Contain($"Uniform4({_bright.GetUniformLocation("u_curve")}, 2, 1, 1, 1)");
        _mock.CallLog.Should().Contain($"Uniform2({_bright.GetUniformLocation("u_texelSize")}, 0.015625, 0.015625)", "1/64: the prefilter samples the scene, not level 0");
        _mock.CallLog.Should().Contain($"Uniform1({_bright.GetUniformLocation("u_source")}, 0)");

        pass.Dispose();
    }

    [Fact]
    public void Pyramid_SkipsUniformsTheProgramDoesNotExpose()
    {
        // Nothing seeded: every reflected location is -1, so no uniform may be uploaded.
        BloomPassExecutor pass = CreatePass().WithMipCount(2);
        _mock.ClearCallLog();

        Action execute = () => pass.Execute(_context, CreateExecutionContext());

        execute.Should().NotThrow();
        _mock.CallLog.Should().NotContain(e => e.StartsWith("Uniform", StringComparison.Ordinal));
        DrawIndices().Should().HaveCount(4, "1 prefilter + 1 downsample + 1 upsample + 1 composite");

        pass.Dispose();
    }

    // =========================================================================
    // Validation
    // =========================================================================

    [Theory]
    [InlineData(2.0f)]
    [InlineData(-0.1f)]
    public void Validate_ThrowsWhenSoftKneeOutOfRange(float softKnee)
    {
        BloomPassExecutor pass = CreatePass().WithSoftKnee(softKnee);

        Action validate = () => pass.Validate();

        validate.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(4.5f)]
    public void Validate_ThrowsWhenRadiusOutOfRange(float radius)
    {
        BloomPassExecutor pass = CreatePass().WithRadius(radius);

        Action validate = () => pass.Validate();

        validate.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);
    }

    [Theory]
    [InlineData(-0.5f)]
    [InlineData(4.5f)]
    public void Validate_ThrowsWhenScatterOutOfRange(float scatter)
    {
        BloomPassExecutor pass = CreatePass().WithScatter(scatter);

        Action validate = () => pass.Validate();

        validate.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    [InlineData(-2)]
    public void Validate_ThrowsWhenMipCountOutOfRange(int mipCount)
    {
        BloomPassExecutor pass = CreatePass().WithMipCount(mipCount);

        Action validate = () => pass.Validate();

        validate.Should().Throw<RenderException>()
            .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);
    }

    [Fact]
    public void Validate_ThrowsWhenSuppliedChainIsTooShort()
    {
        BloomMipLevel[] chain = CreateChain();
        try
        {
            BloomPassExecutor pass = CreatePass().WithMipPyramid(chain.AsSpan(0, 1));

            Action validate = () => pass.Validate();

            validate.Should().Throw<RenderException>()
                .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);
        }
        finally
        {
            DisposeChain(chain);
        }
    }

    [Fact]
    public void Validate_ThrowsWhenSuppliedLevelIsUninitialized()
    {
        BloomMipLevel[] chain = CreateChain();
        try
        {
            chain[1] = default;
            BloomPassExecutor pass = CreatePass().WithMipPyramid(chain);

            Action validate = () => pass.Validate();

            validate.Should().Throw<RenderException>()
                .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);
        }
        finally
        {
            DisposeChain(chain);
        }
    }

    [Fact]
    public void Validate_ThrowsWhenSuppliedLevelSizeMismatchesItsFramebuffer()
    {
        BloomMipLevel[] chain = CreateChain();
        try
        {
            var mismatched = new GLFramebuffer(_context, 4, 4, "mismatch");
            chain[2] = new BloomMipLevel(mismatched, chain[2].Texture);
            BloomPassExecutor pass = CreatePass().WithMipPyramid(chain);

            Action validate = () => pass.Validate();

            validate.Should().Throw<RenderException>()
                .Where(e => e.Code == RenderErrorCode.InvalidPassConfiguration);
        }
        finally
        {
            DisposeChain(chain);
        }
    }

    [Fact]
    public void Validate_AcceptsThePyramidDefaults()
    {
        BloomPassExecutor pass = CreatePass();

        Action validate = () => pass.Validate();

        validate.Should().NotThrow();
        pass.SoftKnee.Should().Be(0.5f);
        pass.Radius.Should().Be(1f);
        pass.Scatter.Should().Be(1f);
        pass.MipCount.Should().Be(0);
        pass.UseLegacyGaussian.Should().BeFalse();
    }

    [Fact]
    public void Validate_AcceptsThePyramidBounds()
    {
        BloomPassExecutor pass = CreatePass()
            .WithSoftKnee(0f)
            .WithSoftKnee(1f)
            .WithRadius(BloomPassExecutor.MaxRadius)
            .WithScatter(BloomPassExecutor.MaxScatter)
            .WithMipCount(BloomPassExecutor.MinMipCount)
            .WithMipCount(BloomPassExecutor.MaxMipCount);

        Action validate = () => pass.Validate();

        validate.Should().NotThrow();
    }

    [Fact]
    public void WithMipPyramid_EmptySpanRestoresTheInternalChain()
    {
        BloomMipLevel[] chain = CreateChain();
        try
        {
            BloomPassExecutor pass = CreatePass().WithMipPyramid(chain);
            pass.Execute(_context, CreateExecutionContext());
            pass.LevelCount.Should().Be(3);
            pass.OwnsMipPyramid.Should().BeFalse();

            pass.WithMipPyramid(ReadOnlySpan<BloomMipLevel>.Empty);
            pass.LevelCount.Should().Be(0, "switching back drops the previous derivation");
            pass.OwnsMipPyramid.Should().BeTrue();

            pass.WithMipCount(4);
            pass.Execute(_context, CreateExecutionContext());
            pass.LevelCount.Should().Be(4);

            pass.Dispose();
        }
        finally
        {
            DisposeChain(chain);
        }
    }

    [Fact]
    public void WithMipPyramid_TruncatesChainsLongerThanTheBound()
    {
        BloomMipLevel[] chain = CreateChain(BloomPassExecutor.MaxMipCount + 4);
        try
        {
            BloomPassExecutor pass = CreatePass().WithMipPyramid(chain);

            pass.Execute(_context, CreateExecutionContext());

            pass.LevelCount.Should().Be(BloomPassExecutor.MaxMipCount);

            Action validate = () => pass.Validate();
            validate.Should().NotThrow();

            pass.Dispose();
        }
        finally
        {
            DisposeChain(chain);
        }
    }

    // =========================================================================
    // Legacy Gaussian path
    // =========================================================================

    [Fact]
    public void LegacyGaussian_ExecutesBrightPassBlurLoopAndComposite()
    {
        _mock.SetActiveUniforms(_bright.Id, [("u_threshold", 1, 0x1406)]);
        _mock.SetActiveUniforms(_blur.Id, [("u_direction", 1, 0x8B50)]);
        _mock.SetActiveUniforms(_composite.Id, [("u_bloomIntensity", 1, 0x1406)]);

        BloomPassExecutor pass = CreatePass()
            .WithLegacyGaussian(true)
            .WithBlurIterations(3)
            .WithThreshold(1.5f)
            .WithBloomIntensity(0.25f);
        _mock.ClearCallLog();

        pass.Execute(_context, CreateExecutionContext());

        // 1 bright pass + 2 * 3 blur passes + 1 composite.
        DrawIndices().Should().HaveCount(8);
        _mock.CallLog.Should().Contain($"Uniform1({_bright.GetUniformLocation("u_threshold")}, 1.5)");
        _mock.CallLog.Should().Contain($"Uniform1({_composite.GetUniformLocation("u_bloomIntensity")}, 0.25)");
        _mock.CallLog.Should().Contain($"Uniform2({_blur.GetUniformLocation("u_direction")}, 1, 0)");
        _mock.CallLog.Should().Contain($"Uniform2({_blur.GetUniformLocation("u_direction")}, 0, 1)");

        // The Gaussian path neither builds a pyramid nor touches blend or invalidation.
        pass.LevelCount.Should().Be(0);
        pass.PyramidWidth.Should().Be(0);
        _mock.CallLog.Should().NotContain(e => e.StartsWith("InvalidateFramebuffer(", StringComparison.Ordinal));
        _mock.CallLog.Should().NotContain("Enable(3042)");
        _mock.CallLog.Should().Contain("BindVertexArray(0)");

        pass.Dispose();
    }

    [Fact]
    public void LegacyGaussian_AndPyramidIssueDifferentDrawCounts()
    {
        BloomPassExecutor legacy = CreatePass().WithLegacyGaussian(true).WithBlurIterations(2);
        BloomPassExecutor pyramid = CreatePass().WithMipCount(3);
        PassExecutionContext ctx = CreateExecutionContext();

        _mock.ClearCallLog();
        legacy.Execute(_context, ctx);
        int legacyDraws = DrawIndices().Count;

        _mock.ClearCallLog();
        pyramid.Execute(_context, ctx);
        int pyramidDraws = DrawIndices().Count;

        legacyDraws.Should().Be(6);   // 1 bright + 2 * 2 blur + 1 composite
        pyramidDraws.Should().Be(6);  // 1 prefilter + 2 down + 2 up + 1 composite
        ViewportWidthPerDraw().Should().Equal(32, 16, 8, 16, 32, 64);

        legacy.Dispose();
        pyramid.Dispose();
    }

    [Fact]
    public void Dispose_IsIdempotentAndDropsTheChain()
    {
        BloomPassExecutor pass = CreatePass().WithMipCount(2);
        pass.Execute(_context, CreateExecutionContext());
        pass.LevelCount.Should().Be(2);

        pass.Dispose();
        pass.Dispose();

        pass.LevelCount.Should().Be(0);
        pass.PyramidWidth.Should().Be(0);
        pass.PyramidHeight.Should().Be(0);
    }
}
