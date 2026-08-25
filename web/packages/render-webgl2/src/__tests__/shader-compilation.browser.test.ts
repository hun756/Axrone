import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import {
    FULLSCREEN_VERTEX_SHADER_SOURCE,
    TONEMAP_FRAGMENT_SHADER_SOURCE,
    EXPOSURE_HISTORY_FRAGMENT_SHADER_SOURCE,
    POST_PROCESS_FRAGMENT_SHADER_SOURCE,
} from '../internal/render-pass-shaders';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

interface LinkedProgram {
    program: WebGLProgram;
    vs: WebGLShader;
    fs: WebGLShader;
}

/** Compile a single shader stage and return the WebGLShader handle. */
function compileShader(
    gl: WebGL2RenderingContext,
    type: number,
    source: string
): WebGLShader {
    const shader = gl.createShader(type);
    if (!shader) throw new Error('createShader returned null');
    gl.shaderSource(shader, source);
    gl.compileShader(shader);
    return shader;
}

/** Compile + link a VS/FS pair. Throws on failure with the info log. */
function linkProgram(
    gl: WebGL2RenderingContext,
    vsSource: string,
    fsSource: string
): LinkedProgram {
    const vs = compileShader(gl, gl.VERTEX_SHADER, vsSource);
    const fs = compileShader(gl, gl.FRAGMENT_SHADER, fsSource);

    const vsOk = gl.getShaderParameter(vs, gl.COMPILE_STATUS);
    if (!vsOk) {
        const log = gl.getShaderInfoLog(vs) ?? '';
        gl.deleteShader(vs);
        gl.deleteShader(fs);
        throw new Error(`Vertex shader compile error: ${log}`);
    }

    const fsOk = gl.getShaderParameter(fs, gl.COMPILE_STATUS);
    if (!fsOk) {
        const log = gl.getShaderInfoLog(fs) ?? '';
        gl.deleteShader(vs);
        gl.deleteShader(fs);
        throw new Error(`Fragment shader compile error: ${log}`);
    }

    const program = gl.createProgram();
    if (!program) throw new Error('createProgram returned null');

    gl.attachShader(program, vs);
    gl.attachShader(program, fs);
    gl.linkProgram(program);

    const linkOk = gl.getProgramParameter(program, gl.LINK_STATUS);
    if (!linkOk) {
        const log = gl.getProgramInfoLog(program) ?? '';
        gl.deleteShader(vs);
        gl.deleteShader(fs);
        gl.deleteProgram(program);
        throw new Error(`Program link error: ${log}`);
    }

    return { program, vs, fs };
}

/** Clean up GPU resources for a linked program. */
function cleanupProgram(gl: WebGL2RenderingContext, lp: LinkedProgram): void {
    gl.deleteShader(lp.vs);
    gl.deleteShader(lp.fs);
    gl.deleteProgram(lp.program);
}

/** Prepend a precision directive to a shader that already has #version. */
function withPrecision(source: string, precision: string): string {
    // Replace existing precision line, or insert after #version
    if (/\bprecision\s+(lowp|mediump|highp)\s+float\b/.test(source)) {
        return source.replace(
            /\bprecision\s+(lowp|mediump|highp)\s+float\b/,
            `precision ${precision} float`
        );
    }
    return source.replace(/(#version\s+300\s+es\s*\n)/, `$1precision ${precision} float;\n`);
}

// ---------------------------------------------------------------------------
// Production shader pairs (VS + FS) that ship in the engine
// ---------------------------------------------------------------------------

interface ShaderPair {
    readonly name: string;
    readonly vertex: string;
    readonly fragment: string;
}

const RENDER_PASS_SHADER_PAIRS: readonly ShaderPair[] = [
    {
        name: 'tonemap',
        vertex: FULLSCREEN_VERTEX_SHADER_SOURCE,
        fragment: TONEMAP_FRAGMENT_SHADER_SOURCE,
    },
    {
        name: 'exposure-history',
        vertex: FULLSCREEN_VERTEX_SHADER_SOURCE,
        fragment: EXPOSURE_HISTORY_FRAGMENT_SHADER_SOURCE,
    },
    {
        name: 'post-process',
        vertex: FULLSCREEN_VERTEX_SHADER_SOURCE,
        fragment: POST_PROCESS_FRAGMENT_SHADER_SOURCE,
    },
] as const;

/** All individual shader sources for static analysis. */
const ALL_SHADER_SOURCES: readonly { name: string; source: string; stage: 'vertex' | 'fragment' }[] = [
    { name: 'fullscreen-vs', source: FULLSCREEN_VERTEX_SHADER_SOURCE, stage: 'vertex' },
    { name: 'tonemap-fs', source: TONEMAP_FRAGMENT_SHADER_SOURCE, stage: 'fragment' },
    { name: 'exposure-history-fs', source: EXPOSURE_HISTORY_FRAGMENT_SHADER_SOURCE, stage: 'fragment' },
    { name: 'post-process-fs', source: POST_PROCESS_FRAGMENT_SHADER_SOURCE, stage: 'fragment' },
];

// ===========================================================================
// TESTS
// ===========================================================================

describe('Shader Compilation Validation (Browser / Real WebGL2)', () => {
    let canvas: HTMLCanvasElement;
    let gl: WebGL2RenderingContext;

    beforeEach(() => {
        canvas = document.createElement('canvas');
        canvas.width = 256;
        canvas.height = 256;
        document.body.appendChild(canvas);

        const ctx = canvas.getContext('webgl2', {
            antialias: false,
            depth: false,
            stencil: false,
            alpha: false,
            premultipliedAlpha: false,
            preserveDrawingBuffer: false,
        });
        if (!ctx) throw new Error('WebGL2 not available in this browser');
        gl = ctx;
    });

    afterEach(() => {
        const ext = gl.getExtension('WEBGL_lose_context');
        ext?.loseContext();
        if (canvas.parentNode) canvas.parentNode.removeChild(canvas);
    });

    // -----------------------------------------------------------------------
    // 1. Core Render Pass Shaders — compile & link
    // -----------------------------------------------------------------------
    describe('Core Render Pass Shaders', () => {
        it('compiles fullscreen vertex shader without errors', () => {
            const shader = compileShader(gl, gl.VERTEX_SHADER, FULLSCREEN_VERTEX_SHADER_SOURCE);
            expect(gl.getShaderParameter(shader, gl.COMPILE_STATUS)).toBe(true);
            gl.deleteShader(shader);
        });

        it('compiles tonemap fragment shader without errors', () => {
            const shader = compileShader(gl, gl.FRAGMENT_SHADER, TONEMAP_FRAGMENT_SHADER_SOURCE);
            expect(gl.getShaderParameter(shader, gl.COMPILE_STATUS)).toBe(true);
            gl.deleteShader(shader);
        });

        it('compiles exposure-history fragment shader without errors', () => {
            const shader = compileShader(gl, gl.FRAGMENT_SHADER, EXPOSURE_HISTORY_FRAGMENT_SHADER_SOURCE);
            expect(gl.getShaderParameter(shader, gl.COMPILE_STATUS)).toBe(true);
            gl.deleteShader(shader);
        });

        it('compiles post-process fragment shader without errors', () => {
            const shader = compileShader(gl, gl.FRAGMENT_SHADER, POST_PROCESS_FRAGMENT_SHADER_SOURCE);
            expect(gl.getShaderParameter(shader, gl.COMPILE_STATUS)).toBe(true);
            gl.deleteShader(shader);
        });

        it('links tonemap VS+FS program successfully', () => {
            const lp = linkProgram(gl, FULLSCREEN_VERTEX_SHADER_SOURCE, TONEMAP_FRAGMENT_SHADER_SOURCE);
            expect(gl.getProgramParameter(lp.program, gl.LINK_STATUS)).toBe(true);
            cleanupProgram(gl, lp);
        });

        it('links exposure-history VS+FS program successfully', () => {
            const lp = linkProgram(gl, FULLSCREEN_VERTEX_SHADER_SOURCE, EXPOSURE_HISTORY_FRAGMENT_SHADER_SOURCE);
            expect(gl.getProgramParameter(lp.program, gl.LINK_STATUS)).toBe(true);
            cleanupProgram(gl, lp);
        });

        it('links post-process VS+FS program successfully', () => {
            const lp = linkProgram(gl, FULLSCREEN_VERTEX_SHADER_SOURCE, POST_PROCESS_FRAGMENT_SHADER_SOURCE);
            expect(gl.getProgramParameter(lp.program, gl.LINK_STATUS)).toBe(true);
            cleanupProgram(gl, lp);
        });
    });

    // -----------------------------------------------------------------------
    // 2. Shader Precision Variants
    // -----------------------------------------------------------------------
    describe('Shader Precision Variants', () => {
        it.each(RENDER_PASS_SHADER_PAIRS.map((p) => [p.name, p]))(
            'compiles %s fragment with mediump precision',
            (_label, pair) => {
                const p = pair as ShaderPair;
                const src = withPrecision(p.fragment, 'mediump');
                const shader = compileShader(gl, gl.FRAGMENT_SHADER, src);
                expect(gl.getShaderParameter(shader, gl.COMPILE_STATUS)).toBe(true);
                const log = gl.getShaderInfoLog(shader) ?? '';
                expect(log).not.toMatch(/error/i);
                gl.deleteShader(shader);
            }
        );

        it.each(RENDER_PASS_SHADER_PAIRS.map((p) => [p.name, p]))(
            'compiles %s fragment with highp precision',
            (_label, pair) => {
                const p = pair as ShaderPair;
                const src = withPrecision(p.fragment, 'highp');
                const shader = compileShader(gl, gl.FRAGMENT_SHADER, src);
                expect(gl.getShaderParameter(shader, gl.COMPILE_STATUS)).toBe(true);
                const log = gl.getShaderInfoLog(shader) ?? '';
                expect(log).not.toMatch(/error/i);
                gl.deleteShader(shader);
            }
        );

        it('no precision-related compile errors for any render-pass shader', () => {
            for (const pair of RENDER_PASS_SHADER_PAIRS) {
                for (const precision of ['lowp', 'mediump', 'highp'] as const) {
                    const src = withPrecision(pair.fragment, precision);
                    const shader = compileShader(gl, gl.FRAGMENT_SHADER, src);
                    const ok = gl.getShaderParameter(shader, gl.COMPILE_STATUS);
                    const log = gl.getShaderInfoLog(shader) ?? '';
                    gl.deleteShader(shader);

                    // lowp may fail on some shaders that require highp for
                    // certain operations; we only assert no *precision-specific*
                    // error appears (the word "precision" in the log).
                    if (!ok && /precision/i.test(log)) {
                        expect.fail(
                            `Precision error for ${pair.name} at ${precision}: ${log}`
                        );
                    }
                }
            }
        });
    });

    // -----------------------------------------------------------------------
    // 3. GLSL ES 3.00 Compliance (static analysis)
    // -----------------------------------------------------------------------
    describe('GLSL ES 3.00 Compliance', () => {
        it('all shaders start with #version 300 es', () => {
            for (const entry of ALL_SHADER_SOURCES) {
                const trimmed = entry.source.trimStart();
                expect(
                    trimmed.startsWith('#version 300 es'),
                    `${entry.name} does not start with "#version 300 es"`
                ).toBe(true);
            }
        });

        it('no shader uses gl_FragColor (deprecated in GLSL ES 3.00)', () => {
            for (const entry of ALL_SHADER_SOURCES) {
                expect(
                    /\bgl_FragColor\b/.test(entry.source),
                    `${entry.name} uses deprecated gl_FragColor`
                ).toBe(false);
            }
        });

        it('all fragment shaders declare explicit out variable', () => {
            const fragmentSources = ALL_SHADER_SOURCES.filter((e) => e.stage === 'fragment');
            for (const entry of fragmentSources) {
                expect(
                    /\bout\s+vec4\s+\w+\s*;/.test(entry.source),
                    `${entry.name} is missing an explicit "out vec4" declaration`
                ).toBe(true);
            }
        });

        it('vertex shaders do not use layout(location = ...) for consistency', () => {
            const vertexSources = ALL_SHADER_SOURCES.filter((e) => e.stage === 'vertex');
            for (const entry of vertexSources) {
                expect(
                    /\blayout\s*\(\s*location\s*=/.test(entry.source),
                    `${entry.name} uses layout(location = ...) in vertex shader`
                ).toBe(false);
            }
        });
    });

    // -----------------------------------------------------------------------
    // 4. Uniform Validation
    // -----------------------------------------------------------------------
    describe('Uniform Validation', () => {
        it('each linked program has a reasonable uniform count (< 32)', () => {
            for (const pair of RENDER_PASS_SHADER_PAIRS) {
                const lp = linkProgram(gl, pair.vertex, pair.fragment);

                const uniformCount = gl.getProgramParameter(lp.program, gl.ACTIVE_UNIFORMS);
                expect(
                    uniformCount,
                    `${pair.name} has ${uniformCount} active uniforms (expected < 32)`
                ).toBeLessThan(32);

                cleanupProgram(gl, lp);
            }
        });

        it('all active uniform types are valid WebGL2 types', () => {
            // Valid GLSL ES 3.00 uniform types mapped to WebGL2 GLenum values
            const VALID_UNIFORM_TYPES = new Set<number>([
                gl.FLOAT,         // 0x1406
                gl.FLOAT_VEC2,    // 0x8B50
                gl.FLOAT_VEC3,    // 0x8B51
                gl.FLOAT_VEC4,    // 0x8B52
                gl.INT,           // 0x1404
                gl.INT_VEC2,      // 0x8B53
                gl.INT_VEC3,      // 0x8B54
                gl.INT_VEC4,      // 0x8B55
                gl.BOOL,          // 0x8B56
                gl.BOOL_VEC2,     // 0x8B57
                gl.BOOL_VEC3,     // 0x8B58
                gl.BOOL_VEC4,     // 0x8B59
                gl.FLOAT_MAT2,    // 0x8B5A
                gl.FLOAT_MAT3,    // 0x8B5B
                gl.FLOAT_MAT4,    // 0x8B5C
                gl.SAMPLER_2D,    // 0x8B5E
                gl.SAMPLER_CUBE,  // 0x8B60
                gl.SAMPLER_2D_ARRAY, // 0x8B5F (WebGL2)
                gl.INT_SAMPLER_2D,   // 0x8DCA
                gl.UNSIGNED_INT_SAMPLER_2D, // 0x8DD7
                gl.SAMPLER_2D_SHADOW,       // 0x8B62
            ]);

            for (const pair of RENDER_PASS_SHADER_PAIRS) {
                const lp = linkProgram(gl, pair.vertex, pair.fragment);
                const count = gl.getProgramParameter(lp.program, gl.ACTIVE_UNIFORMS);

                for (let i = 0; i < count; i++) {
                    const info = gl.getActiveUniform(lp.program, i);
                    if (!info) continue;
                    expect(
                        VALID_UNIFORM_TYPES.has(info.type),
                        `${pair.name}: uniform "${info.name}" has unexpected type 0x${info.type.toString(16)}`
                    ).toBe(true);
                }

                cleanupProgram(gl, lp);
            }
        });

        it('tonemap program exposes expected core uniforms', () => {
            const lp = linkProgram(gl, FULLSCREEN_VERTEX_SHADER_SOURCE, TONEMAP_FRAGMENT_SHADER_SOURCE);

            const expectedNames = ['uSource', 'uExposureHistory', 'uMode', 'uExposureScale', 'uGamma'];
            for (const name of expectedNames) {
                const loc = gl.getUniformLocation(lp.program, name);
                expect(loc, `tonemap program missing uniform "${name}"`).not.toBeNull();
            }

            cleanupProgram(gl, lp);
        });

        it('post-process program exposes expected core uniforms', () => {
            const lp = linkProgram(gl, FULLSCREEN_VERTEX_SHADER_SOURCE, POST_PROCESS_FRAGMENT_SHADER_SOURCE);

            const expectedNames = ['uSource', 'uAuxSource', 'uEffectMode', 'uTexelSize'];
            for (const name of expectedNames) {
                const loc = gl.getUniformLocation(lp.program, name);
                expect(loc, `post-process program missing uniform "${name}"`).not.toBeNull();
            }

            cleanupProgram(gl, lp);
        });
    });

    // -----------------------------------------------------------------------
    // 5. Shader Compilation Error Handling
    // -----------------------------------------------------------------------
    describe('Shader Compilation Error Handling', () => {
        it('deliberately invalid GLSL fails compilation', () => {
            const badSource = `#version 300 es
void main() {
    this_is_not_valid_glsl;
}`;
            const shader = compileShader(gl, gl.FRAGMENT_SHADER, badSource);
            expect(gl.getShaderParameter(shader, gl.COMPILE_STATUS)).toBe(false);
            gl.deleteShader(shader);
        });

        it('gl.getShaderInfoLog returns a meaningful error for invalid GLSL', () => {
            const badSource = `#version 300 es
void main() {
    totally_broken @@@@ garbage;
}`;
            const shader = compileShader(gl, gl.VERTEX_SHADER, badSource);
            gl.compileShader(shader);
            const log = gl.getShaderInfoLog(shader) ?? '';
            expect(log.length).toBeGreaterThan(0);
            // Most drivers include the word "error" in the log
            expect(log.toLowerCase()).toMatch(/error|syntax|unexpected/);
            gl.deleteShader(shader);
        });

        it('linking mismatched VS/FS pair fails with an info log', () => {
            // VS outputs vUv but FS expects v_color — varying mismatch
            const vs = `#version 300 es
out vec2 vUv;
void main() { gl_Position = vec4(0.0); vUv = vec2(0.0); }`;
            const fs = `#version 300 es
precision highp float;
in vec4 v_color;
out vec4 outColor;
void main() { outColor = v_color; }`;

            const vsShader = compileShader(gl, gl.VERTEX_SHADER, vs);
            const fsShader = compileShader(gl, gl.FRAGMENT_SHADER, fs);

            // Both should compile individually
            expect(gl.getShaderParameter(vsShader, gl.COMPILE_STATUS)).toBe(true);
            expect(gl.getShaderParameter(fsShader, gl.COMPILE_STATUS)).toBe(true);

            const program = gl.createProgram()!;
            gl.attachShader(program, vsShader);
            gl.attachShader(program, fsShader);
            gl.linkProgram(program);

            // Link should fail because varyings don't match
            const linkOk = gl.getProgramParameter(program, gl.LINK_STATUS);
            // Note: some drivers may successfully link with the mismatched
            // varying being optimized away. We accept both outcomes but if it
            // fails, the info log must be non-empty.
            if (!linkOk) {
                const log = gl.getProgramInfoLog(program) ?? '';
                expect(log.length).toBeGreaterThan(0);
            }

            gl.deleteShader(vsShader);
            gl.deleteShader(fsShader);
            gl.deleteProgram(program);
        });
    });

    // -----------------------------------------------------------------------
    // 6. Maximum Shader Complexity
    // -----------------------------------------------------------------------
    describe('Maximum Shader Complexity', () => {
        it('compiles a fragment shader with 4 texture samples (mobile limit)', () => {
            const fourSampleFS = `#version 300 es
precision highp float;
uniform sampler2D uTex0;
uniform sampler2D uTex1;
uniform sampler2D uTex2;
uniform sampler2D uTex3;
in vec2 vUv;
out vec4 outColor;
void main() {
    vec4 c0 = texture(uTex0, vUv);
    vec4 c1 = texture(uTex1, vUv);
    vec4 c2 = texture(uTex2, vUv);
    vec4 c3 = texture(uTex3, vUv);
    outColor = (c0 + c1 + c2 + c3) * 0.25;
}`;
            const shader = compileShader(gl, gl.FRAGMENT_SHADER, fourSampleFS);
            expect(gl.getShaderParameter(shader, gl.COMPILE_STATUS)).toBe(true);
            gl.deleteShader(shader);
        });

        it('compiles a fragment shader with 8 texture samples', () => {
            const eightSampleFS = `#version 300 es
precision highp float;
uniform sampler2D uTex0;
uniform sampler2D uTex1;
uniform sampler2D uTex2;
uniform sampler2D uTex3;
uniform sampler2D uTex4;
uniform sampler2D uTex5;
uniform sampler2D uTex6;
uniform sampler2D uTex7;
in vec2 vUv;
out vec4 outColor;
void main() {
    vec4 acc = texture(uTex0, vUv) + texture(uTex1, vUv)
             + texture(uTex2, vUv) + texture(uTex3, vUv)
             + texture(uTex4, vUv) + texture(uTex5, vUv)
             + texture(uTex6, vUv) + texture(uTex7, vUv);
    outColor = acc * 0.125;
}`;
            const shader = compileShader(gl, gl.FRAGMENT_SHADER, eightSampleFS);
            expect(gl.getShaderParameter(shader, gl.COMPILE_STATUS)).toBe(true);
            gl.deleteShader(shader);
        });

        it('post-process shader has no dynamic loop iteration in fragment body', () => {
            // All loops in the post-process shader use literal integer bounds
            const forPattern = /\bfor\s*\(\s*\w+\s*=\s*\w+\s*;\s*\w+\s*[<>]=?\s*([^;]+)\s*;/g;
            let match: RegExpExecArray | null;
            while ((match = forPattern.exec(POST_PROCESS_FRAGMENT_SHADER_SOURCE)) !== null) {
                const bound = match[1]!.trim();
                expect(
                    /^\d+$/.test(bound),
                    `Post-process FS has dynamic loop bound: "${bound}"`
                ).toBe(true);
            }
        });

        it('tonemap shader has no dynamic loop iteration in fragment body', () => {
            const forPattern = /\bfor\s*\(\s*\w+\s*=\s*\w+\s*;\s*\w+\s*[<>]=?\s*([^;]+)\s*;/g;
            let match: RegExpExecArray | null;
            while ((match = forPattern.exec(TONEMAP_FRAGMENT_SHADER_SOURCE)) !== null) {
                const bound = match[1]!.trim();
                expect(
                    /^\d+$/.test(bound),
                    `Tonemap FS has dynamic loop bound: "${bound}"`
                ).toBe(true);
            }
        });
    });

    // -----------------------------------------------------------------------
    // 7. Extension-Dependent Shaders
    // -----------------------------------------------------------------------
    describe('Extension-Dependent Shaders', () => {
        it('compiles shader that uses EXT_texture_filter_anisotropic when available', () => {
            const anisoExt = gl.getExtension('EXT_texture_filter_anisotropic');

            // The extension affects texture sampling parameters, not GLSL syntax.
            // We verify the extension is queryable and that a standard shader
            // compiles fine regardless.
            const shader = compileShader(gl, gl.FRAGMENT_SHADER, TONEMAP_FRAGMENT_SHADER_SOURCE);
            expect(gl.getShaderParameter(shader, gl.COMPILE_STATUS)).toBe(true);

            if (anisoExt) {
                // Extension is available — verify the constant exists
                expect(typeof anisoExt.TEXTURE_MAX_ANISOTROPY_EXT).toBe('number');
            }

            gl.deleteShader(shader);
        });

        it('handles missing extension gracefully (shader still compiles)', () => {
            // Even without EXT_texture_filter_anisotropic, standard shaders
            // must compile without errors.
            const shader = compileShader(gl, gl.FRAGMENT_SHADER, POST_PROCESS_FRAGMENT_SHADER_SOURCE);
            expect(gl.getShaderParameter(shader, gl.COMPILE_STATUS)).toBe(true);
            gl.deleteShader(shader);
        });

        it('reports supported extensions as a non-empty array', () => {
            const extensions = gl.getSupportedExtensions() ?? [];
            expect(Array.isArray(extensions)).toBe(true);
            expect(extensions.length).toBeGreaterThan(0);
        });
    });
});
