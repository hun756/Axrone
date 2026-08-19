import { describe, expect, it } from 'vitest';
import { WebGLShaderCompiler } from '../shader/compiler';
import { StandardUnlitShader } from '../shader/templates/standard-shaders';
import {
    ShaderDataType,
    ShaderQualifier,
    ShaderStage,
    type IShaderConfiguration,
} from '../shader/interfaces';
import {
    generatePrecisionDirective,
    generateVersionDirective,
    validateUniformNaming,
    isValidShaderVariableName,
    generateVariantKey,
    hashShaderSource,
    SHADER_CACHE_LIMITS,
} from '../shader/utils';

// ---------------------------------------------------------------------------
// Helpers — production shader sources collected from across the engine
// ---------------------------------------------------------------------------

/**
 * Inline shader sources from scene-runtime components. These mirror the real
 * production shaders used by billboard, line, particle, terrain-splat and
 * terrain-foliage runtimes. We embed them as plain strings so the static
 * analysis tests can inspect them without pulling in cross-package imports.
 */

const BILLBOARD_VERTEX = `#version 300 es
layout(location = 0) in vec3 a_Position;
layout(location = 2) in vec2 a_UV0;
layout(location = 3) in vec4 a_Color0;
uniform mat4 u_ViewProjection;
uniform mat4 u_Model;
uniform float u_BillboardMode;
out vec2 v_UV;
out vec4 v_Color;
void main() {
    vec4 worldPosition = u_Model * vec4(a_Position, 1.0);
    gl_Position = u_ViewProjection * worldPosition;
    v_UV = a_UV0;
    v_Color = a_Color0;
}`;

const BILLBOARD_FRAGMENT = `#version 300 es
precision mediump float;
uniform sampler2D u_MainTexture;
uniform float u_UseTexture;
uniform float u_AlphaTest;
in vec2 v_UV;
in vec4 v_Color;
out vec4 o_Color;
void main() {
    vec4 color = v_Color;
    if (u_UseTexture > 0.5) {
        color *= texture(u_MainTexture, v_UV);
    }
    if (color.a < u_AlphaTest) {
        discard;
    }
    o_Color = color;
}`;

const LINE_VERTEX = `#version 300 es
layout(location = 0) in vec3 a_Position;
layout(location = 2) in vec2 a_UV0;
layout(location = 3) in vec4 a_Color0;
uniform mat4 u_ViewProjection;
uniform mat4 u_Model;
out vec2 v_UV;
out vec4 v_Color;
void main() {
    vec4 worldPosition = u_Model * vec4(a_Position, 1.0);
    gl_Position = u_ViewProjection * worldPosition;
    v_UV = a_UV0;
    v_Color = a_Color0;
}`;

const LINE_FRAGMENT = `#version 300 es
precision mediump float;
uniform sampler2D u_MainTexture;
uniform float u_UseTexture;
in vec2 v_UV;
in vec4 v_Color;
out vec4 o_Color;
void main() {
    vec4 color = v_Color;
    if (u_UseTexture > 0.5) {
        color *= texture(u_MainTexture, v_UV);
    }
    if (color.a < 0.004) {
        discard;
    }
    o_Color = color;
}`;

const PARTICLE_VERTEX = `#version 300 es
layout(location = 0) in vec3 a_Position;
layout(location = 1) in float a_Rotation;
layout(location = 2) in vec2 a_UV0;
layout(location = 3) in vec4 a_Color0;
uniform mat4 u_ViewProjection;
uniform mat4 u_Model;
uniform float u_PointScale;
uniform float u_MaxPointSize;
out vec4 v_Color;
out float v_Seed;
out float v_Rotation;
void main() {
    vec4 worldPosition = u_Model * vec4(a_Position, 1.0);
    gl_Position = u_ViewProjection * worldPosition;
    v_Color = a_Color0;
    v_Seed = a_UV0.y;
    v_Rotation = a_Rotation;
    float size = a_UV0.x;
    float attenuated = size * u_PointScale / max(gl_Position.w, 1e-4);
    gl_PointSize = clamp(attenuated, 1.0, u_MaxPointSize);
}`;

const PARTICLE_FRAGMENT = `#version 300 es
precision mediump float;
uniform int u_SpriteMode;
uniform sampler2D u_Texture;
uniform float u_UseTexture;
uniform vec4 u_TexSheetParams;
uniform vec4 u_TexRegion;
in vec4 v_Color;
in float v_Seed;
in float v_Rotation;
out vec4 o_Color;
vec2 rotateUV(vec2 uv, float angle) {
    vec2 centered = uv - 0.5;
    float c = cos(angle);
    float s = sin(angle);
    return vec2(c * centered.x - s * centered.y, s * centered.x + c * centered.y) + 0.5;
}
vec2 computeTexUV(vec2 pointCoord) {
    float tilesX = u_TexSheetParams.x;
    float tilesY = u_TexSheetParams.y;
    float frameIndex = u_TexSheetParams.z;
    float totalFrames = u_TexSheetParams.w;
    if (totalFrames > 0.0) {
        float tileW = 1.0 / tilesX;
        float tileH = 1.0 / tilesY;
        float col = mod(frameIndex, tilesX);
        float row = floor(frameIndex / tilesX);
        vec2 tileOrigin = vec2(col * tileW, 1.0 - (row + 1.0) * tileH);
        return tileOrigin + pointCoord * vec2(tileW, tileH);
    }
    return u_TexRegion.xy + pointCoord * u_TexRegion.zw;
}
void main() {
    vec2 pointCoord = rotateUV(gl_PointCoord, v_Rotation);
    vec2 centered = pointCoord - 0.5;
    float dist = length(centered);
    if (u_UseTexture > 0.5) {
        vec2 texUV = computeTexUV(pointCoord);
        vec4 texel = texture(u_Texture, texUV);
        vec4 result = texel * v_Color;
        if (result.a < 0.012) { discard; }
        o_Color = result;
        return;
    }
    float mask = 0.0;
    if (u_SpriteMode == 0) {
        mask = pow(max(0.0, 1.0 - dist * 2.0), 2.1);
    } else if (u_SpriteMode == 1) {
        mask = 1.0 - smoothstep(0.40, 0.5, dist);
    } else if (u_SpriteMode == 2) {
        float diamond = abs(centered.x) + abs(centered.y);
        mask = pow(max(0.0, 1.0 - diamond * 2.0), 3.0)
             + pow(max(0.0, 1.0 - dist * 2.0), 2.0) * 0.35;
    } else {
        float crossMask = exp(-abs(centered.x) * 13.0) + exp(-abs(centered.y) * 13.0);
        mask = min(1.0, crossMask) * pow(max(0.0, 1.0 - dist * 2.0), 1.4);
    }
    if (mask < 0.012) { discard; }
    o_Color = vec4(v_Color.rgb * mask, v_Color.a * mask);
}`;

/**
 * Terrain splat fragment — reconstructed from the effect definition in
 * terrain-splat-shader.ts. The effect system generates the final GLSL from
 * the declarative definition; this is the logical equivalent.
 */
const TERRAIN_SPLAT_FRAGMENT = `#version 300 es
precision mediump float;
uniform sampler2D u_SplatMap;
uniform sampler2D u_LayerMap0;
uniform sampler2D u_LayerMap1;
uniform sampler2D u_LayerMap2;
uniform sampler2D u_LayerMap3;
uniform vec4 u_LayerTiling;
uniform float u_LayerCount;
uniform bool u_ReceiveLighting;
in vec3 v_Normal;
in vec2 v_UV;
out vec4 o_Color;
vec3 sampleTerrainLayer(sampler2D layerMap, float tiling, float layerSlot, float layerCount) {
    if (layerSlot >= layerCount) { return vec3(0.0); }
    return texture(layerMap, v_UV * tiling).rgb;
}
void main() {
    vec4 splat = texture(u_SplatMap, v_UV);
    vec3 albedo =
        sampleTerrainLayer(u_LayerMap0, u_LayerTiling.x, 0.0, u_LayerCount) * splat.r +
        sampleTerrainLayer(u_LayerMap1, u_LayerTiling.y, 1.0, u_LayerCount) * splat.g +
        sampleTerrainLayer(u_LayerMap2, u_LayerTiling.z, 2.0, u_LayerCount) * splat.b +
        sampleTerrainLayer(u_LayerMap3, u_LayerTiling.w, 3.0, u_LayerCount) * splat.a;
    o_Color = vec4(albedo, 1.0);
}`;

const TERRAIN_SPLAT_VERTEX = `#version 300 es
layout(location = 0) in vec3 a_Position;
layout(location = 1) in vec3 a_Normal;
layout(location = 2) in vec2 a_UV;
uniform mat4 u_Model;
uniform mat4 u_View;
uniform mat4 u_Projection;
out vec3 v_Normal;
out vec2 v_UV;
void main() {
    v_Normal = normalize(mat3(u_Model) * a_Normal);
    v_UV = a_UV;
    gl_Position = u_Projection * u_View * u_Model * vec4(a_Position, 1.0);
}`;

/**
 * The StandardUnlitShader fragment body from templates/standard-shaders.ts.
 * The compiler prepends #version and precision directives automatically.
 */
const UNLIT_FRAGMENT_BODY = `
out vec4 o_FragColor;
void main() {
    vec4 color = u_Color;
    #ifdef MAIN_TEXTURE
        color *= texture(u_MainTexture, v_TexCoord);
    #endif
    o_FragColor = color;
}`;

const UNLIT_VERTEX_BODY = `
void main() {
    gl_Position = u_MVPMatrix * vec4(a_Position, 1.0);
    v_TexCoord = a_TexCoord;
}`;

// ---------------------------------------------------------------------------
// Aggregate all production shader sources for static analysis
// ---------------------------------------------------------------------------

interface ProductionShaderEntry {
    readonly name: string;
    readonly vertex: string;
    readonly fragment: string;
}

const PRODUCTION_SHADERS: readonly ProductionShaderEntry[] = [
    { name: 'Standard/Unlit (vertex body)', vertex: UNLIT_VERTEX_BODY, fragment: UNLIT_FRAGMENT_BODY },
    { name: 'scene/billboard', vertex: BILLBOARD_VERTEX, fragment: BILLBOARD_FRAGMENT },
    { name: 'scene/line-ribbon', vertex: LINE_VERTEX, fragment: LINE_FRAGMENT },
    { name: 'scene/particle-point', vertex: PARTICLE_VERTEX, fragment: PARTICLE_FRAGMENT },
    { name: 'Scene/TerrainSplat', vertex: TERRAIN_SPLAT_VERTEX, fragment: TERRAIN_SPLAT_FRAGMENT },
] as const;

// ---------------------------------------------------------------------------
// Static analysis helpers
// ---------------------------------------------------------------------------

/** Count `texture(...)` calls in a fragment shader source. */
const countTextureSamples = (source: string): number => {
    const matches = source.match(/\btexture\s*\(/g);
    return matches ? matches.length : 0;
};

/** Check whether a source contains a `precision` declaration. */
const hasPrecisionDirective = (source: string): boolean =>
    /\bprecision\s+(lowp|mediump|highp)\s+float\b/.test(source);

/** Check whether a source contains `#version` directive. */
const hasVersionDirective = (source: string): boolean =>
    /^\s*#version\s+/m.test(source);

/** Detect `gl_FragColor` usage (deprecated in GLSL ES 3.00). */
const usesGlFragColor = (source: string): boolean =>
    /\bgl_FragColor\b/.test(source);

/** Detect `layout(location = ...)` — not valid in WebGL1-compatible shaders. */
const usesLayoutLocation = (source: string): boolean =>
    /\blayout\s*\(\s*location\s*=/.test(source);

/**
 * Detect dynamic loop bounds in fragment shaders. We look for `for` loops
 * where the bound is not a literal integer constant.
 */
const hasDynamicLoopBounds = (source: string): boolean => {
    const forPattern = /\bfor\s*\(\s*\w+\s*=\s*\w+\s*;\s*\w+\s*[<>]=?\s*([^;]+)\s*;/g;
    let match: RegExpExecArray | null;
    while ((match = forPattern.exec(source)) !== null) {
        const bound = match[1]!.trim();
        // If the bound is a literal number, it's static
        if (!/^\d+$/.test(bound)) {
            return true;
        }
    }
    return false;
};

/**
 * Detect `if/else` branching on per-fragment varying values (interpolated
 * inputs). Varyings typically start with `v_` in this codebase.
 */
const branchesOnVarying = (source: string): boolean => {
    const ifPattern = /\bif\s*\([^)]*\b(v_\w+)\b[^)]*\)/g;
    return ifPattern.test(source);
};

/** Extract the precision qualifier from a fragment shader source. */
const extractPrecision = (source: string): 'lowp' | 'mediump' | 'highp' | null => {
    const match = source.match(/\bprecision\s+(lowp|mediump|highp)\s+float\b/);
    return match ? (match[1] as 'lowp' | 'mediump' | 'highp') : null;
};

// ---------------------------------------------------------------------------
// Minimal WebGL mock (same pattern as existing compiler.test.ts)
// ---------------------------------------------------------------------------

function createMinimalGL(): WebGL2RenderingContext {
    const programs: object[] = [];
    const shaders: object[] = [];

    const gl = new Proxy(
        {
            VERTEX_SHADER: 0x8b31,
            FRAGMENT_SHADER: 0x8b30,
            COMPILE_STATUS: 0x8b81,
            LINK_STATUS: 0x8b82,
            INVALID_INDEX: 0xffffffff,
            FLOAT: 0x1406,
            _programs: programs,
            _shaders: shaders,
            createProgram() {
                const p = { _type: 'program' };
                programs.push(p);
                return p;
            },
            createShader() {
                const s = { _type: 'shader' };
                shaders.push(s);
                return s;
            },
            shaderSource() {},
            compileShader() {},
            getShaderParameter() { return true; },
            getShaderInfoLog() { return ''; },
            attachShader() {},
            linkProgram() {},
            getProgramParameter() { return true; },
            getProgramInfoLog() { return ''; },
            deleteProgram() {},
            deleteShader() {},
            getUniformLocation() { return {}; },
            getAttribLocation() { return 0; },
            getUniformBlockIndex() { return 0; },
            getAttachedShaders() { return []; },
            getShaderSource() { return 'source'; },
            useProgram() {},
        },
        {
            get: (target, property) => {
                if (property in target) return (target as Record<string, unknown>)[property as string];
                return 0;
            },
        }
    );
    return gl as unknown as WebGL2RenderingContext;
}

/**
 * Create a GL mock that simulates a compilation failure for the shader.
 * Used to test error handling paths.
 */
function createFailingGL(errorMessage: string): WebGL2RenderingContext {
    const base = createMinimalGL();
    const origGetShaderParameter = (base as unknown as Record<string, unknown>).getShaderParameter;

    // Override getShaderParameter to return false for COMPILE_STATUS
    Object.assign(base as unknown as Record<string, unknown>, {
        getShaderParameter(_shader: unknown, status: number) {
            if (status === 0x8b81) return false; // COMPILE_STATUS
            return (origGetShaderParameter as (_s: unknown, _st: number) => boolean)(_shader, status);
        },
        getShaderInfoLog() { return errorMessage; },
    });

    return base;
}

// ---------------------------------------------------------------------------
// Helper to build a valid IShaderConfiguration for compilation tests
// ---------------------------------------------------------------------------

function createValidConfig(
    overrides: Partial<IShaderConfiguration> = {}
): IShaderConfiguration {
    return {
        name: 'test-shader',
        version: '1.0',
        attributes: [
            {
                name: 'a_position',
                type: ShaderDataType.VEC3,
                qualifier: ShaderQualifier.IN,
                binding: 0,
            },
        ],
        uniforms: [
            {
                name: 'u_modelViewProjection',
                type: ShaderDataType.MAT4,
                qualifier: ShaderQualifier.UNIFORM,
            },
        ],
        textures: [
            {
                name: 'u_diffuseMap',
                type: 'texture2D' as unknown as ShaderDataType,
                slot: 0,
            },
        ],
        passes: [
            {
                name: 'main',
                stage: [ShaderStage.VERTEX, ShaderStage.FRAGMENT],
                vertexShader: 'void main() { gl_Position = vec4(0); }',
                fragmentShader: 'out vec4 o_FragColor; void main() { o_FragColor = vec4(1); }',
                renderState: {},
            },
        ],
        ...overrides,
    };
}

// ===========================================================================
// TESTS
// ===========================================================================

describe('T-14: Shader Compilation Validation', () => {
    // -----------------------------------------------------------------------
    // 1. Shader Source Validation (static analysis)
    // -----------------------------------------------------------------------
    describe('Shader Source Validation', () => {
        it('all production vertex shaders that include #version also contain required structure', () => {
            const shadersWithVersion = PRODUCTION_SHADERS.filter((s) =>
                hasVersionDirective(s.vertex)
            );
            expect(shadersWithVersion.length).toBeGreaterThan(0);

            for (const shader of shadersWithVersion) {
                // Every versioned vertex shader must have a main() function
                expect(shader.vertex).toContain('void main()');
                // Every vertex shader must write to gl_Position
                expect(shader.vertex).toContain('gl_Position');
            }
        });

        it('all production fragment shaders with #version have a precision declaration', () => {
            const fragmentShadersWithVersion = PRODUCTION_SHADERS.filter((s) =>
                hasVersionDirective(s.fragment)
            );
            expect(fragmentShadersWithVersion.length).toBeGreaterThan(0);

            for (const shader of fragmentShadersWithVersion) {
                expect(
                    hasPrecisionDirective(shader.fragment),
                    `Fragment shader "${shader.name}" is missing a precision declaration`
                ).toBe(true);
            }
        });

        it('no production fragment shader exceeds 8 texture samples (mobile-safe limit)', () => {
            const MAX_MOBILE_TEXTURE_SAMPLES = 8;

            for (const shader of PRODUCTION_SHADERS) {
                const sampleCount = countTextureSamples(shader.fragment);
                expect(
                    sampleCount,
                    `Fragment shader "${shader.name}" has ${sampleCount} texture samples (limit: ${MAX_MOBILE_TEXTURE_SAMPLES})`
                ).toBeLessThanOrEqual(MAX_MOBILE_TEXTURE_SAMPLES);
            }
        });

        it('no vertex shader uses gl_FragColor (deprecated in GLSL ES 3.00)', () => {
            for (const shader of PRODUCTION_SHADERS) {
                expect(
                    usesGlFragColor(shader.vertex),
                    `Vertex shader "${shader.name}" uses deprecated gl_FragColor`
                ).toBe(false);
            }
        });

        it('all versioned fragment shaders use mediump or document highp with a comment', () => {
            for (const shader of PRODUCTION_SHADERS) {
                if (!hasVersionDirective(shader.fragment)) continue;

                const precision = extractPrecision(shader.fragment);
                if (precision === 'highp') {
                    // highp is allowed if there's a comment justifying it
                    const hasJustification =
                        /\/\/.*highp|\/\*.*highp|highp.*required|highp.*needed/i.test(
                            shader.fragment
                        );
                    // We accept highp only when justified — for now all our
                    // production shaders use mediump, so this should pass.
                    expect(
                        hasJustification || precision === 'mediump',
                        `Fragment shader "${shader.name}" uses highp without justification`
                    ).toBe(true);
                } else {
                    // mediump or lowp are both fine
                    expect(precision).toBe('mediump');
                }
            }
        });
    });

    // -----------------------------------------------------------------------
    // 2. Shader Compilation (mock WebGL2 context)
    // -----------------------------------------------------------------------
    describe('Shader Compilation', () => {
        it('basic unlit shader configuration compiles without errors via mock GL', async () => {
            const gl = createMinimalGL();
            const compiler = new WebGLShaderCompiler(gl);
            const config = createValidConfig({
                name: 'Standard/Unlit',
                passes: [
                    {
                        name: 'ForwardBase',
                        stage: [ShaderStage.VERTEX, ShaderStage.FRAGMENT],
                        vertexShader: UNLIT_VERTEX_BODY,
                        fragmentShader: UNLIT_FRAGMENT_BODY,
                        renderState: {},
                    },
                ],
            });

            const result = compiler.validateConfiguration(config);
            expect(result.isValid).toBe(true);
            expect(result.errors).toHaveLength(0);

            const compiled = await compiler.compile(config);
            expect(compiled.name).toBe('Standard/Unlit');
            // bytecodeSize is calculated from attached shaders in the mock GL;
            // the mock returns empty attached shaders so size is 0 — we verify
            // the compilation succeeded and the field exists.
            expect(typeof compiled.bytecodeSize).toBe('number');
            expect(compiled.compilationTime).toBeGreaterThanOrEqual(0);
        });

        it('PBR-style metallic-roughness shader configuration validates', () => {
            const gl = createMinimalGL();
            const compiler = new WebGLShaderCompiler(gl);

            const pbrConfig = createValidConfig({
                name: 'Standard/PBR_MetallicRoughness',
                uniforms: [
                    { name: 'u_MVPMatrix', type: ShaderDataType.MAT4, qualifier: ShaderQualifier.UNIFORM },
                    { name: 'u_BaseColorFactor', type: ShaderDataType.VEC4, qualifier: ShaderQualifier.UNIFORM },
                    { name: 'u_MetallicFactor', type: ShaderDataType.FLOAT, qualifier: ShaderQualifier.UNIFORM },
                    { name: 'u_RoughnessFactor', type: ShaderDataType.FLOAT, qualifier: ShaderQualifier.UNIFORM },
                ],
                textures: [
                    { name: 'u_BaseColorTexture', type: 'texture2D' as unknown as ShaderDataType, slot: 0 },
                    { name: 'u_MetallicRoughnessTexture', type: 'texture2D' as unknown as ShaderDataType, slot: 1 },
                    { name: 'u_NormalMap', type: 'texture2D' as unknown as ShaderDataType, slot: 2 },
                ],
                passes: [
                    {
                        name: 'PBR',
                        stage: [ShaderStage.VERTEX, ShaderStage.FRAGMENT],
                        vertexShader: 'void main() { gl_Position = u_MVPMatrix * vec4(0); }',
                        fragmentShader: `out vec4 o_FragColor;
void main() {
    vec4 baseColor = u_BaseColorFactor;
    float metallic = u_MetallicFactor;
    float roughness = u_RoughnessFactor;
    o_FragColor = baseColor * vec4(metallic, roughness, 0.0, 1.0);
}`,
                        renderState: {},
                    },
                ],
            });

            const result = compiler.validateConfiguration(pbrConfig);
            expect(result.isValid).toBe(true);
        });

        it('normal-mapped shader configuration validates with correct sampler types', () => {
            const gl = createMinimalGL();
            const compiler = new WebGLShaderCompiler(gl);

            const normalMapConfig = createValidConfig({
                name: 'Standard/NormalMapped',
                textures: [
                    { name: 'u_AlbedoMap', type: 'texture2D' as unknown as ShaderDataType, slot: 0 },
                    { name: 'u_NormalMap', type: 'texture2D' as unknown as ShaderDataType, slot: 1 },
                ],
                passes: [
                    {
                        name: 'Forward',
                        stage: [ShaderStage.VERTEX, ShaderStage.FRAGMENT],
                        vertexShader: 'void main() { gl_Position = vec4(0); }',
                        fragmentShader: `out vec4 o_FragColor;
void main() { o_FragColor = vec4(1); }`,
                        renderState: {},
                    },
                ],
            });

            const result = compiler.validateConfiguration(normalMapConfig);
            expect(result.isValid).toBe(true);
        });

        it('skinned mesh shader configuration validates with extra attributes', () => {
            const gl = createMinimalGL();
            const compiler = new WebGLShaderCompiler(gl);

            const skinnedConfig = createValidConfig({
                name: 'Standard/Skinned',
                attributes: [
                    { name: 'a_position', type: ShaderDataType.VEC3, qualifier: ShaderQualifier.IN, binding: 0 },
                    { name: 'a_normal', type: ShaderDataType.VEC3, qualifier: ShaderQualifier.IN, binding: 1 },
                    { name: 'a_joints0', type: ShaderDataType.IVEC4, qualifier: ShaderQualifier.IN, binding: 2 },
                    { name: 'a_weights0', type: ShaderDataType.VEC4, qualifier: ShaderQualifier.IN, binding: 3 },
                ],
                passes: [
                    {
                        name: 'SkinnedForward',
                        stage: [ShaderStage.VERTEX, ShaderStage.FRAGMENT],
                        vertexShader: 'void main() { gl_Position = vec4(0); }',
                        fragmentShader: 'out vec4 o_FragColor; void main() { o_FragColor = vec4(1); }',
                        renderState: {},
                    },
                ],
            });

            const result = compiler.validateConfiguration(skinnedConfig);
            expect(result.isValid).toBe(true);
            expect(result.errors).toHaveLength(0);
        });

        it('terrain splat shader configuration validates with multiple texture slots', () => {
            const gl = createMinimalGL();
            const compiler = new WebGLShaderCompiler(gl);

            const terrainConfig = createValidConfig({
                name: 'Scene/TerrainSplat',
                textures: [
                    { name: 'u_SplatMap', type: 'texture2D' as unknown as ShaderDataType, slot: 0 },
                    { name: 'u_LayerMap0', type: 'texture2D' as unknown as ShaderDataType, slot: 1 },
                    { name: 'u_LayerMap1', type: 'texture2D' as unknown as ShaderDataType, slot: 2 },
                    { name: 'u_LayerMap2', type: 'texture2D' as unknown as ShaderDataType, slot: 3 },
                    { name: 'u_LayerMap3', type: 'texture2D' as unknown as ShaderDataType, slot: 4 },
                ],
                passes: [
                    {
                        name: 'TerrainPass',
                        stage: [ShaderStage.VERTEX, ShaderStage.FRAGMENT],
                        vertexShader: TERRAIN_SPLAT_VERTEX,
                        fragmentShader: TERRAIN_SPLAT_FRAGMENT,
                        renderState: {},
                    },
                ],
            });

            const result = compiler.validateConfiguration(terrainConfig);
            expect(result.isValid).toBe(true);
        });
    });

    // -----------------------------------------------------------------------
    // 3. Shader Uniform Validation
    // -----------------------------------------------------------------------
    describe('Shader Uniform Validation', () => {
        it('all declared uniforms with u_ prefix pass naming validation', () => {
            const uniformNames = [
                'u_MVPMatrix',
                'u_Color',
                'u_MainTexture',
                'u_ViewProjection',
                'u_Model',
                'u_PointScale',
                'u_MaxPointSize',
                'u_SpriteMode',
                'u_TexSheetParams',
                'u_TexRegion',
                'u_LayerTiling',
                'u_LayerCount',
            ];

            for (const name of uniformNames) {
                const result = validateUniformNaming(name);
                expect(result.valid, `Uniform "${name}" should pass naming validation`).toBe(true);
            }
        });

        it('uniforms without u_ prefix generate a warning but remain valid', () => {
            const result = validateUniformNaming('modelMatrix');
            expect(result.valid).toBe(true);
            expect(result.warnings.some((w) => w.includes('u_'))).toBe(true);
        });

        it('uniform types match between vertex and fragment stages in StandardUnlitShader', () => {
            // The StandardUnlitShader declares u_MVPMatrix (mat4) and u_Color (vec4)
            // as uniforms used across both stages.
            const uniforms = StandardUnlitShader.uniforms;
            const mvpUniform = uniforms.find((u) => u.name === 'u_MVPMatrix');
            const colorUniform = uniforms.find((u) => u.name === 'u_Color');

            expect(mvpUniform).toBeDefined();
            expect(mvpUniform!.type).toBe(ShaderDataType.MAT4);

            expect(colorUniform).toBeDefined();
            expect(colorUniform!.type).toBe(ShaderDataType.VEC4);
        });

        it('sampler uniforms use correct sampler type (sampler2D vs samplerCube)', () => {
            // All texture properties in StandardUnlitShader should be texture2D
            for (const tex of StandardUnlitShader.textures) {
                expect(tex.type).toBe('texture2D');
            }

            // Billboard shader uses sampler2D
            const billboardSamplerMatch = BILLBOARD_FRAGMENT.match(/uniform\s+sampler2D\s+(\w+)/g);
            expect(billboardSamplerMatch).not.toBeNull();
            expect(billboardSamplerMatch!.length).toBeGreaterThan(0);
        });

        it('no uniform declarations use invalid variable names', () => {
            const allUniformNames = StandardUnlitShader.uniforms.map((u) => u.name);

            for (const name of allUniformNames) {
                expect(
                    isValidShaderVariableName(name),
                    `Uniform name "${name}" is not a valid GLSL variable name`
                ).toBe(true);
            }
        });
    });

    // -----------------------------------------------------------------------
    // 4. Shader Variant Tracking
    // -----------------------------------------------------------------------
    describe('Shader Variant Tracking', () => {
        it('variant key is deterministic for the same keywords and defines', () => {
            const key1 = generateVariantKey('test-shader', ['FOG', 'SHADOWS'], { MAIN_TEXTURE: true });
            const key2 = generateVariantKey('test-shader', ['SHADOWS', 'FOG'], { MAIN_TEXTURE: true });
            expect(key1).toBe(key2);
        });

        it('variant key differs for different keyword sets', () => {
            const key1 = generateVariantKey('test-shader', ['FOG'], {});
            const key2 = generateVariantKey('test-shader', ['SHADOWS'], {});
            expect(key1).not.toBe(key2);
        });

        it('shader variant count stays within reasonable limits (SHADER_CACHE_LIMITS)', () => {
            // MAX_VARIANTS_PER_SHADER is 64 — verify the constant is sane
            expect(SHADER_CACHE_LIMITS.MAX_VARIANTS_PER_SHADER).toBeLessThanOrEqual(128);
            expect(SHADER_CACHE_LIMITS.MAX_VARIANTS_PER_SHADER).toBeGreaterThan(0);
        });

        it('compilation cache returns the same shader for identical configurations', async () => {
            const gl = createMinimalGL();
            const compiler = new WebGLShaderCompiler(gl);
            const config = createValidConfig();

            const first = await compiler.compile(config);
            const second = await compiler.compile(config);

            // Same cache key → same object reference
            expect(first.id).toBe(second.id);
        });

        it('excessive variant count triggers cache stats growth', async () => {
            const gl = createMinimalGL();
            const compiler = new WebGLShaderCompiler(gl);
            const config = createValidConfig();

            const compiled = await compiler.compile(config);

            // Compile several variants
            for (let i = 0; i < 5; i++) {
                await compiler.compileVariant(compiled, [`KEYWORD_${i}`], {});
            }

            const stats = compiler.getCacheStats();
            expect(stats.variants).toBe(5);
            expect(stats.compiledShaders).toBe(1);
        });
    });

    // -----------------------------------------------------------------------
    // 5. Mobile GPU Compatibility
    // -----------------------------------------------------------------------
    describe('Mobile GPU Compatibility', () => {
        it('all versioned production fragment shaders use mediump precision', () => {
            for (const shader of PRODUCTION_SHADERS) {
                if (!hasVersionDirective(shader.fragment)) continue;
                const precision = extractPrecision(shader.fragment);
                expect(
                    precision,
                    `Fragment shader "${shader.name}" should use mediump for mobile compatibility`
                ).toBe('mediump');
            }
        });

        it('no production fragment shader has dynamic loop bounds', () => {
            for (const shader of PRODUCTION_SHADERS) {
                expect(
                    hasDynamicLoopBounds(shader.fragment),
                    `Fragment shader "${shader.name}" has dynamic loop bounds (mobile GPU hazard)`
                ).toBe(false);
            }
        });

        it('no production fragment shader branches on per-fragment varying values', () => {
            for (const shader of PRODUCTION_SHADERS) {
                expect(
                    branchesOnVarying(shader.fragment),
                    `Fragment shader "${shader.name}" branches on a varying value (mobile GPU hazard)`
                ).toBe(false);
            }
        });

        it('terrain splat fragment shader texture sample count is within mobile limits', () => {
            // The terrain splat shader has a helper function `sampleTerrainLayer`
            // that wraps a single `texture()` call, plus one direct `texture(u_SplatMap, ...)`
            // call. Static analysis counts 2 textual `texture(` occurrences — the
            // runtime expansion (4 layer calls) is not visible to the regex.
            const sampleCount = countTextureSamples(TERRAIN_SPLAT_FRAGMENT);
            expect(sampleCount).toBe(2);
            // Even with runtime expansion (5 actual samples), this is within
            // the mobile-safe limit of 8.
            expect(sampleCount).toBeLessThanOrEqual(8);
        });

        it('no WebGL1-compatible production shader uses layout(location = ...) in fragment stage', () => {
            // layout(location) is valid in vertex shaders for WebGL2 but should
            // not appear in fragment shaders for WebGL1 fallback compatibility.
            for (const shader of PRODUCTION_SHADERS) {
                expect(
                    usesLayoutLocation(shader.fragment),
                    `Fragment shader "${shader.name}" uses layout(location) — not WebGL1 compatible`
                ).toBe(false);
            }
        });
    });

    // -----------------------------------------------------------------------
    // 6. Shader Compilation Error Handling
    // -----------------------------------------------------------------------
    describe('Shader Compilation Error Handling', () => {
        it('throws when shader compilation fails in mock GL context', async () => {
            const gl = createFailingGL('ERROR: 0:1: syntax error');
            const compiler = new WebGLShaderCompiler(gl);
            const config = createValidConfig();

            await expect(compiler.compile(config)).rejects.toThrow('Shader compilation failed');
        });

        it('throws when configuration validation fails before compilation', async () => {
            const gl = createMinimalGL();
            const compiler = new WebGLShaderCompiler(gl);
            const config = createValidConfig({ name: '' });

            await expect(compiler.compile(config)).rejects.toThrow('Shader validation failed');
        });

        it('clearCache resets compilation and variant caches', async () => {
            const gl = createMinimalGL();
            const compiler = new WebGLShaderCompiler(gl);
            const config = createValidConfig();

            await compiler.compile(config);
            expect(compiler.getCacheStats().compiledShaders).toBe(1);

            compiler.clearCache();
            expect(compiler.getCacheStats().compiledShaders).toBe(0);
            expect(compiler.getCacheStats().variants).toBe(0);
        });
    });

    // -----------------------------------------------------------------------
    // 7. Shader Source Generation Utilities
    // -----------------------------------------------------------------------
    describe('Shader Source Generation Utilities', () => {
        it('generateVersionDirective produces correct GLSL ES 3.00 header', () => {
            const directive = generateVersionDirective('300 es');
            expect(directive).toBe('#version 300 es\n');
        });

        it('generatePrecisionDirective defaults to mediump', () => {
            const directive = generatePrecisionDirective();
            expect(directive).toBe('precision mediump float;\n');
        });

        it('generatePrecisionDirective supports highp when explicitly requested', () => {
            const directive = generatePrecisionDirective('highp');
            expect(directive).toBe('precision highp float;\n');
        });

        it('hashShaderSource produces stable hashes for identical input', () => {
            const source = 'void main() { gl_Position = vec4(0); }';
            const hash1 = hashShaderSource(source);
            const hash2 = hashShaderSource(source);
            expect(hash1).toBe(hash2);
        });

        it('hashShaderSource produces different hashes for different input', () => {
            const hash1 = hashShaderSource('void main() {}');
            const hash2 = hashShaderSource('void main() { gl_Position = vec4(1); }');
            expect(hash1).not.toBe(hash2);
        });
    });
});
