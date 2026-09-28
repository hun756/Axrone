namespace Axrone.Render.OpenGL.Shading;

/// <summary>
/// Built-in GLSL 330 core shader sources for the Karis soft-knee mip-pyramid bloom:
/// prefilter, mip downsample, mip upsample and scene composite.
/// </summary>
/// <remarks>
/// <para>
/// All stages share <see cref="FullscreenTriangleVertex"/>, which synthesizes a
/// screen-covering triangle from <c>gl_VertexID</c> and needs no vertex buffer or VAO.
/// </para>
/// <para>
/// The scalar values these shaders expect are derived on the CPU by
/// <see cref="BloomMath"/>; the two must stay in sync — <c>BloomMath</c> is the
/// authority for the curve, these sources are its GPU evaluation.
/// </para>
/// </remarks>
public static class BloomShaders
{
    /// <summary>
    /// Fullscreen triangle vertex shader. Emits a single oversized triangle from
    /// <c>gl_VertexID</c> and carries normalized texture coordinates as the varying.
    /// </summary>
    public const string FullscreenTriangleVertex = """
        #version 330 core

        out vec2 v_TexCoord;

        void main()
        {
            vec2 p = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
            v_TexCoord = p;
            gl_Position = vec4(p * 2.0 - 1.0, 0.0, 1.0);
        }
        """;

    /// <summary>
    /// Prefilter fragment shader: Karis soft-knee bright-pass extraction with optional
    /// Karis luminance weighting.
    /// </summary>
    /// <remarks>
    /// Uniforms — all are skipped by the caller when the reflected location is <c>-1</c>:
    /// <list type="bullet">
    ///   <item><description><c>u_source</c> (sampler2D) — the HDR scene texture, bound at unit 0.</description></item>
    ///   <item><description><c>u_texelSize</c> (vec2) — <c>1.0 / sourceSize</c>. Unused by this stage; kept for a uniform block shared with the downsample.</description></item>
    ///   <item><description><c>u_curve</c> (vec4) — <c>x</c>: luminance threshold, <c>y</c>: knee width, <c>z</c>: Karis weight gain (<c>0</c> disables, <c>1</c> applies <c>1/(1+luma)</c>), <c>w</c>: input gain applied before thresholding.</description></item>
    /// </list>
    /// </remarks>
    public const string PrefilterFragment = """
        #version 330 core

        in vec2 v_TexCoord;

        uniform sampler2D u_source;
        uniform vec2 u_texelSize;
        uniform vec4 u_curve;

        out vec4 o_FragColor;

        const vec3 kLuma = vec3(0.2126, 0.7152, 0.0722);

        float luminance(vec3 color)
        {
            return dot(color, kLuma);
        }

        float karisWeight(vec3 color)
        {
            return 1.0 / (1.0 + max(luminance(color), 0.0));
        }

        // Quadratic soft knee: 0 at threshold - knee/4, 1 at threshold + knee/4,
        // 0.25 exactly at the threshold. A non-positive knee is a hard threshold.
        float softKnee(float x, float threshold, float knee)
        {
            if (knee <= 1e-5)
            {
                return x > threshold + 1e-5 ? 1.0 : 0.0;
            }

            float kneeQuadratic = knee * 0.25;
            if (x + kneeQuadratic < threshold)
            {
                return 0.0;
            }

            if (x - kneeQuadratic > threshold)
            {
                return 1.0;
            }

            float s = (x - threshold + kneeQuadratic) / (2.0 * kneeQuadratic);
            return s * s;
        }

        void main()
        {
            vec3 color = texture(u_source, v_TexCoord).rgb * u_curve.w;

            float luma = luminance(color);
            float contribution = softKnee(luma, u_curve.x, u_curve.y);
            float weight = mix(1.0, karisWeight(color), u_curve.z);

            o_FragColor = vec4(color * contribution * weight, 1.0);
        }
        """;

    /// <summary>
    /// Downsample fragment shader: the 13-tap tent downsample, grouped into five
    /// 2x2 blocks so each block can be Karis-averaged independently.
    /// </summary>
    /// <remarks>
    /// Uniforms — all are skipped by the caller when the reflected location is <c>-1</c>:
    /// <list type="bullet">
    ///   <item><description><c>u_source</c> (sampler2D) — the level to sample, bound at unit 0.</description></item>
    ///   <item><description><c>u_texelSize</c> (vec2) — <c>1.0 / sourceSize</c>, the tap step.</description></item>
    ///   <item><description><c>u_karis</c> (float) — Karis weight gain: <c>0</c> uses plain group averages, <c>1</c> weights each group by <c>1/(1+luma)</c>.</description></item>
    /// </list>
    /// </remarks>
    public const string DownsampleFragment = """
        #version 330 core

        in vec2 v_TexCoord;

        uniform sampler2D u_source;
        uniform vec2 u_texelSize;
        uniform float u_karis;

        out vec4 o_FragColor;

        const vec3 kLuma = vec3(0.2126, 0.7152, 0.0722);

        vec3 groupWeight(vec3 color)
        {
            float luma = dot(color, kLuma);
            return mix(vec3(1.0), 1.0 / (1.0 + max(luma, 0.0)), u_karis);
        }

        vec3 karisGroup(vec4 quad)
        {
            return quad.rgb * groupWeight(quad.rgb);
        }

        void main()
        {
            vec2 t = u_texelSize;

            // 9 taps on the integer texel grid (a 4x4 neighbourhood minus the far corners
            // that the two intermediate taps already cover).
            vec3 a = texture(u_source, v_TexCoord + t * vec2(-2.0,  2.0)).rgb;
            vec3 b = texture(u_source, v_TexCoord + t * vec2( 0.0,  2.0)).rgb;
            vec3 c = texture(u_source, v_TexCoord + t * vec2( 2.0,  2.0)).rgb;
            vec3 d = texture(u_source, v_TexCoord + t * vec2(-2.0,  0.0)).rgb;
            vec3 e = texture(u_source, v_TexCoord).rgb;
            vec3 f = texture(u_source, v_TexCoord + t * vec2( 2.0,  0.0)).rgb;
            vec3 g = texture(u_source, v_TexCoord + t * vec2(-2.0, -2.0)).rgb;
            vec3 h = texture(u_source, v_TexCoord + t * vec2( 0.0, -2.0)).rgb;
            vec3 i = texture(u_source, v_TexCoord + t * vec2( 2.0, -2.0)).rgb;

            // 4 taps on the half-texel grid, centred: the tent interior.
            vec3 j = texture(u_source, v_TexCoord + t * vec2(-1.0,  1.0)).rgb;
            vec3 k = texture(u_source, v_TexCoord + t * vec2( 1.0,  1.0)).rgb;
            vec3 l = texture(u_source, v_TexCoord + t * vec2(-1.0, -1.0)).rgb;
            vec3 m = texture(u_source, v_TexCoord + t * vec2( 1.0, -1.0)).rgb;

            vec3 corner0 = (karisGroup(vec4(a, b, d, e)) + karisGroup(vec4(b, c, e, f))
                          + karisGroup(vec4(d, e, g, h)) + karisGroup(vec4(e, f, h, i))) * 0.125;
            vec3 center = (j + k + l + m) * 0.125;

            o_FragColor = vec4(corner0 * 0.25 + center, 1.0);
        }
        """;

    /// <summary>
    /// Upsample fragment shader: 3x3 tent upsample with a texel-step radius and a
    /// per-level scatter weight, accumulated additively onto the destination level.
    /// </summary>
    /// <remarks>
    /// Uniforms — all are skipped by the caller when the reflected location is <c>-1</c>:
    /// <list type="bullet">
    ///   <item><description><c>u_source</c> (sampler2D) — the coarser level, bound at unit 0.</description></item>
    ///   <item><description><c>u_texelSize</c> (vec2) — <c>1.0 / sourceSize</c>, the tent step.</description></item>
    ///   <item><description><c>u_radius</c> (float) — tent radius in texels.</description></item>
    ///   <item><description><c>u_scatter</c> (float) — contribution weight: the RGB output and the alpha channel are both scaled by it, so the caller can accumulate with a pure additive RGB blend while weighting the level's contribution.</description></item>
    /// </list>
    /// </remarks>
    public const string UpsampleFragment = """
        #version 330 core

        in vec2 v_TexCoord;

        uniform sampler2D u_source;
        uniform vec2 u_texelSize;
        uniform float u_radius;
        uniform float u_scatter;

        out vec4 o_FragColor;

        void main()
        {
            vec2 t = u_texelSize * u_radius;
            vec2 uv = v_TexCoord;

            // 3x3 tent, corners at 1/16, edges at 2/16, centre at 4/16.
            vec3 sum = texture(u_source, uv + vec2(-t.x,  t.y)).rgb;
            sum += texture(u_source, uv + vec2(0.0,  t.y)).rgb * 2.0;
            sum += texture(u_source, uv + vec2( t.x,  t.y)).rgb;
            sum += texture(u_source, uv + vec2(-t.x, 0.0)).rgb * 2.0;
            sum += texture(u_source, uv).rgb * 4.0;
            sum += texture(u_source, uv + vec2( t.x, 0.0)).rgb * 2.0;
            sum += texture(u_source, uv + vec2(-t.x, -t.y)).rgb;
            sum += texture(u_source, uv + vec2(0.0, -t.y)).rgb * 2.0;
            sum += texture(u_source, uv + vec2( t.x, -t.y)).rgb;

            o_FragColor = vec4(sum * (1.0 / 16.0) * u_scatter, u_scatter);
        }
        """;

    /// <summary>
    /// Composite fragment shader: additive bloom over the scene, alpha passed through.
    /// </summary>
    /// <remarks>
    /// Uniforms — all are skipped by the caller when the reflected location is <c>-1</c>:
    /// <list type="bullet">
    ///   <item><description><c>u_scene</c> (sampler2D) — the scene texture, bound at unit 0.</description></item>
    ///   <item><description><c>u_bloom</c> (sampler2D) — the bloom pyramid level 0, bound at unit 1.</description></item>
    ///   <item><description><c>u_intensity</c> (float) — the bloom intensity. Preferred when present.</description></item>
    ///   <item><description><c>u_bloomIntensity</c> (float) — the legacy intensity name, used when <c>u_intensity</c> is absent. The caller uploads the same value to every name the program exposes, so the two never disagree.</description></item>
    /// </list>
    /// </remarks>
    public const string CompositeFragment = """
        #version 330 core

        in vec2 v_TexCoord;

        uniform sampler2D u_scene;
        uniform sampler2D u_bloom;
        uniform float u_intensity;
        uniform float u_bloomIntensity;

        out vec4 o_FragColor;

        void main()
        {
            vec4 scene = texture(u_scene, v_TexCoord);
            vec3 bloom = texture(u_bloom, v_TexCoord).rgb;

            // u_intensity wins when the program exposes it; a program that only
            // declares the legacy name leaves u_intensity optimized out at 0.
            float intensity = u_intensity != 0.0 ? u_intensity : u_bloomIntensity;

            o_FragColor = vec4(scene.rgb + bloom * intensity, scene.a);
        }
        """;
}
