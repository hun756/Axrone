import { describe, it, expect, beforeEach, afterEach } from 'vitest';

/**
 * Visual Regression Tests — Pixel-Level Assertions with Real WebGL2 Contexts
 *
 * These tests render geometry into a WebGL2 framebuffer and verify exact pixel
 * output via gl.readPixels(). No golden image library is used — all assertions
 * are programmatic with ±2 channel tolerance for 8-bit RGBA values.
 *
 * Test groups:
 *  1. Multi-Primitive Scene
 *  2. Overdraw Detection
 *  3. Depth Ordering
 *  4. Texture Sampling Accuracy
 *  5. Viewport Clipping
 *  6. Alpha Blending Modes
 *  7. Multi-Pass Rendering
 *  8. Stencil Testing
 */

// ---------------------------------------------------------------------------
// GLSL Shaders
// ---------------------------------------------------------------------------

/** Vertex shader: position + per-vertex color. */
const VERT_SOLID_COLOR = `#version 300 es
in vec2 a_position;
in vec4 a_color;
out vec4 v_color;
void main() {
    gl_Position = vec4(a_position, 0.0, 1.0);
    v_color = a_color;
}
`;

/** Fragment shader: outputs interpolated per-vertex color. */
const FRAG_SOLID_COLOR = `#version 300 es
precision mediump float;
in vec4 v_color;
out vec4 fragColor;
void main() {
    fragColor = v_color;
}
`;

/** Vertex shader: position + UV for texture sampling. */
const VERT_TEXTURED = `#version 300 es
in vec2 a_position;
in vec2 a_uv;
out vec2 v_uv;
void main() {
    gl_Position = vec4(a_position, 0.0, 1.0);
    v_uv = a_uv;
}
`;

/** Fragment shader: samples a 2D texture. */
const FRAG_TEXTURED = `#version 300 es
precision mediump float;
uniform sampler2D u_texture;
in vec2 v_uv;
out vec4 fragColor;
void main() {
    fragColor = texture(u_texture, v_uv);
}
`;

/** Vertex shader: position + uniform depth + uniform color. */
const VERT_DEPTH = `#version 300 es
in vec2 a_position;
uniform float u_depth;
void main() {
    gl_Position = vec4(a_position, u_depth, 1.0);
}
`;

/** Fragment shader: uniform color (no per-vertex input). */
const FRAG_UNIFORM_COLOR = `#version 300 es
precision mediump float;
uniform vec4 u_color;
out vec4 fragColor;
void main() {
    fragColor = u_color;
}
`;

/** Vertex shader: position only (no attributes beyond position). */
const VERT_POSITION_ONLY = `#version 300 es
in vec2 a_position;
void main() {
    gl_Position = vec4(a_position, 0.0, 1.0);
}
`;

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

interface CompiledProgram {
    program: WebGLProgram;
}

function compileShader(gl: WebGL2RenderingContext, type: number, src: string): WebGLShader {
    const s = gl.createShader(type)!;
    gl.shaderSource(s, src);
    gl.compileShader(s);
    if (!gl.getShaderParameter(s, gl.COMPILE_STATUS)) {
        const log = gl.getShaderInfoLog(s) || 'shader compile failed';
        gl.deleteShader(s);
        throw new Error(log);
    }
    return s;
}

function compileProgram(gl: WebGL2RenderingContext, vertSrc: string, fragSrc: string): CompiledProgram {
    const vs = compileShader(gl, gl.VERTEX_SHADER, vertSrc);
    const fs = compileShader(gl, gl.FRAGMENT_SHADER, fragSrc);
    const program = gl.createProgram()!;
    gl.attachShader(program, vs);
    gl.attachShader(program, fs);
    gl.linkProgram(program);
    gl.deleteShader(vs);
    gl.deleteShader(fs);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
        const log = gl.getProgramInfoLog(program) || 'program link failed';
        gl.deleteProgram(program);
        throw new Error(log);
    }
    return { program };
}

/** Read a single RGBA pixel at (x, y). */
function readPixel(gl: WebGL2RenderingContext, x: number, y: number): Uint8Array {
    const px = new Uint8Array(4);
    gl.readPixels(x, y, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, px);
    return px;
}

/** Read a rectangular region of pixels. Returns Uint8Array of size w*h*4. */
function readPixelRect(gl: WebGL2RenderingContext, x: number, y: number, w: number, h: number): Uint8Array {
    const px = new Uint8Array(w * h * 4);
    gl.readPixels(x, y, w, h, gl.RGBA, gl.UNSIGNED_BYTE, px);
    return px;
}

/** Assert that a pixel matches expected RGBA within ±tolerance per channel. */
function expectPixelNear(
    pixel: Uint8Array,
    r: number, g: number, b: number, a: number,
    tolerance = 2,
    label?: string
) {
    const prefix = label ? `${label}: ` : '';
    expect(Math.abs(pixel[0] - r), `${prefix}R`).toBeLessThanOrEqual(tolerance);
    expect(Math.abs(pixel[1] - g), `${prefix}G`).toBeLessThanOrEqual(tolerance);
    expect(Math.abs(pixel[2] - b), `${prefix}B`).toBeLessThanOrEqual(tolerance);
    expect(Math.abs(pixel[3] - a), `${prefix}A`).toBeLessThanOrEqual(tolerance);
}

/** Create a fullscreen quad VAO (clip-space [-1..1]) with position + UV. */
function createFullscreenQuad(gl: WebGL2RenderingContext, program: WebGLProgram): {
    vao: WebGLVertexArrayObject;
    cleanup: () => void;
} {
    const vertices = new Float32Array([
        // pos       uv
        -1, -1, 0, 0,
         1, -1, 1, 0,
         1,  1, 1, 1,
        -1,  1, 0, 1,
    ]);
    const vbo = gl.createBuffer()!;
    gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
    gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);

    const vao = gl.createVertexArray()!;
    gl.bindVertexArray(vao);

    const aPos = gl.getAttribLocation(program, 'a_position');
    const aUv = gl.getAttribLocation(program, 'a_uv');
    gl.enableVertexAttribArray(aPos);
    gl.vertexAttribPointer(aPos, 2, gl.FLOAT, false, 16, 0);
    if (aUv >= 0) {
        gl.enableVertexAttribArray(aUv);
        gl.vertexAttribPointer(aUv, 2, gl.FLOAT, false, 16, 8);
    }
    gl.bindVertexArray(null);

    return {
        vao,
        cleanup: () => { gl.deleteVertexArray(vao); gl.deleteBuffer(vbo); },
    };
}

/** Create a positioned quad VAO with position-only data (no UV). Use with shaders that only have a_position. */
function createPositionedPosOnly(
    gl: WebGL2RenderingContext,
    program: WebGLProgram,
    cx: number, cy: number,
    halfW: number, halfH: number
): { vao: WebGLVertexArrayObject; cleanup: () => void } {
    const x0 = cx - halfW, x1 = cx + halfW;
    const y0 = cy - halfH, y1 = cy + halfH;
    const vertices = new Float32Array([
        x0, y0,
        x1, y0,
        x1, y1,
        x0, y1,
    ]);
    const vbo = gl.createBuffer()!;
    gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
    gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);

    const vao = gl.createVertexArray()!;
    gl.bindVertexArray(vao);

    const aPos = gl.getAttribLocation(program, 'a_position');
    gl.enableVertexAttribArray(aPos);
    gl.vertexAttribPointer(aPos, 2, gl.FLOAT, false, 0, 0);
    gl.bindVertexArray(null);

    return {
        vao,
        cleanup: () => { gl.deleteVertexArray(vao); gl.deleteBuffer(vbo); },
    };
}

/** Create a positioned quad VAO with position + per-vertex color (no UV). */
function createPositionedColoredQuad(
    gl: WebGL2RenderingContext,
    program: WebGLProgram,
    cx: number, cy: number,
    halfW: number, halfH: number,
    r: number, g: number, b: number, a: number
): { vao: WebGLVertexArrayObject; cleanup: () => void } {
    const x0 = cx - halfW, x1 = cx + halfW;
    const y0 = cy - halfH, y1 = cy + halfH;
    // Interleaved: a_position (vec2) + a_color (vec4) = 6 floats = 24 bytes
    const vertices = new Float32Array([
        x0, y0, r, g, b, a,
        x1, y0, r, g, b, a,
        x1, y1, r, g, b, a,
        x0, y1, r, g, b, a,
    ]);
    const vbo = gl.createBuffer()!;
    gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
    gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);

    const vao = gl.createVertexArray()!;
    gl.bindVertexArray(vao);

    const aPos = gl.getAttribLocation(program, 'a_position');
    const aColor = gl.getAttribLocation(program, 'a_color');
    gl.enableVertexAttribArray(aPos);
    gl.vertexAttribPointer(aPos, 2, gl.FLOAT, false, 24, 0);
    if (aColor >= 0) {
        gl.enableVertexAttribArray(aColor);
        gl.vertexAttribPointer(aColor, 4, gl.FLOAT, false, 24, 8);
    }
    gl.bindVertexArray(null);

    return {
        vao,
        cleanup: () => { gl.deleteVertexArray(vao); gl.deleteBuffer(vbo); },
    };
}

/** Create a fullscreen quad with position only (no UV, no color). */
function createPositionOnlyQuad(gl: WebGL2RenderingContext, program: WebGLProgram): {
    vao: WebGLVertexArrayObject;
    cleanup: () => void;
} {
    const vertices = new Float32Array([-1, -1, 1, -1, 1, 1, -1, 1]);
    const vbo = gl.createBuffer()!;
    gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
    gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);

    const vao = gl.createVertexArray()!;
    gl.bindVertexArray(vao);
    const aPos = gl.getAttribLocation(program, 'a_position');
    gl.enableVertexAttribArray(aPos);
    gl.vertexAttribPointer(aPos, 2, gl.FLOAT, false, 0, 0);
    gl.bindVertexArray(null);

    return {
        vao,
        cleanup: () => { gl.deleteVertexArray(vao); gl.deleteBuffer(vbo); },
    };
}

/** Create an NxN RGBA8 texture from raw pixel data (row-major, bottom-to-top). */
function createTextureFromData(
    gl: WebGL2RenderingContext,
    size: number,
    data: Uint8Array
): WebGLTexture {
    const tex = gl.createTexture()!;
    gl.bindTexture(gl.TEXTURE_2D, tex);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, size, size, 0, gl.RGBA, gl.UNSIGNED_BYTE, data);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    gl.bindTexture(gl.TEXTURE_2D, null);
    return tex;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('Visual Regression (Pixel-Level)', () => {
    let canvas: HTMLCanvasElement;
    let gl!: WebGL2RenderingContext;
    const W = 128;
    const H = 128;

    beforeEach(() => {
        canvas = document.createElement('canvas');
        canvas.width = W;
        canvas.height = H;
        document.body.appendChild(canvas);

        const ctx = canvas.getContext('webgl2', {
            antialias: false,
            depth: true,
            stencil: true,
            alpha: true,
            premultipliedAlpha: false,
            preserveDrawingBuffer: true,
        });
        if (!ctx) throw new Error('WebGL2 not supported in this browser');
        gl = ctx;
    });

    afterEach(() => {
        if (canvas.parentNode) {
            canvas.parentNode.removeChild(canvas);
        }
    });

    // =========================================================================
    // 1. Multi-Primitive Scene
    // =========================================================================
    describe('Multi-Primitive Scene', () => {
        it('renders 3 colored quads at different positions with correct colors', () => {
            const prog = compileProgram(gl, VERT_SOLID_COLOR, FRAG_SOLID_COLOR);

            // Three quads in different screen regions
            const quads = [
                { cx: -0.6, cy: 0.0, hw: 0.25, hh: 0.3, r: 255, g: 0, b: 0, a: 255 },     // Red, left
                { cx: 0.0, cy: 0.5, hw: 0.25, hh: 0.2, r: 0, g: 255, b: 0, a: 255 },       // Green, top-center
                { cx: 0.6, cy: -0.4, hw: 0.2, hh: 0.25, r: 0, g: 0, b: 255, a: 255 },      // Blue, right-bottom
            ];

            const vaoList = quads.map((q) =>
                createPositionedColoredQuad(
                    gl, prog.program, q.cx, q.cy, q.hw, q.hh,
                    q.r / 255, q.g / 255, q.b / 255, q.a / 255
                )
            );

            gl.viewport(0, 0, W, H);
            gl.clearColor(0.1, 0.1, 0.1, 1.0); // Dark grey background
            gl.clear(gl.COLOR_BUFFER_BIT);

            gl.useProgram(prog.program);
            for (const v of vaoList) {
                gl.bindVertexArray(v.vao);
                gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            }
            gl.bindVertexArray(null);

            // Verify each quad center has the correct color
            const clipToPixel = (cx: number, cy: number): [number, number] => [
                Math.floor(((cx + 1) / 2) * W),
                Math.floor(((cy + 1) / 2) * H),
            ];

            const [rX, rY] = clipToPixel(-0.6, 0.0);
            expectPixelNear(readPixel(gl, rX, rY), 255, 0, 0, 255, 2, 'red quad center');

            const [gX, gY] = clipToPixel(0.0, 0.5);
            expectPixelNear(readPixel(gl, gX, gY), 0, 255, 0, 255, 2, 'green quad center');

            const [bX, bY] = clipToPixel(0.6, -0.4);
            expectPixelNear(readPixel(gl, bX, bY), 0, 0, 255, 255, 2, 'blue quad center');

            // Verify background pixel (between quads) is the clear color
            // (0.1 * 255 ≈ 26)
            const bgPixel = readPixel(gl, 2, 2);
            expectPixelNear(bgPixel, 26, 26, 26, 255, 2, 'background');

            vaoList.forEach((v) => v.cleanup());
            gl.deleteProgram(prog.program);
        });

        it('verifies background clear color is undisturbed between primitives', () => {
            const prog = compileProgram(gl, VERT_POSITION_ONLY, FRAG_UNIFORM_COLOR);
            const uColor = gl.getUniformLocation(prog.program, 'u_color')!;

            const quad = createPositionedPosOnly(gl, prog.program, 0.0, 0.0, 0.2, 0.2);

            gl.viewport(0, 0, W, H);
            gl.clearColor(0.0, 0.5, 1.0, 1.0); // Bright blue background
            gl.clear(gl.COLOR_BUFFER_BIT);

            gl.useProgram(prog.program);
            gl.uniform4f(uColor, 1.0, 0.0, 0.0, 1.0); // Small red quad in center
            gl.bindVertexArray(quad.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            // Corner should still be clear color
            const corner = readPixel(gl, 0, 0);
            expectPixelNear(corner, 0, 128, 255, 255, 2, 'corner background');

            // Center should be red
            const center = readPixel(gl, W / 2, H / 2);
            expectPixelNear(center, 255, 0, 0, 255, 2, 'center quad');

            quad.cleanup();
            gl.deleteProgram(prog.program);
        });

        it('renders primitives with different sizes and verifies spatial extent', () => {
            const prog = compileProgram(gl, VERT_POSITION_ONLY, FRAG_UNIFORM_COLOR);
            const uColor = gl.getUniformLocation(prog.program, 'u_color')!;

            // Large quad covering most of the screen
            const largeQuad = createPositionedPosOnly(gl, prog.program, 0.0, 0.0, 0.9, 0.9);
            // Small quad in top-right corner only
            const smallQuad = createPositionedPosOnly(gl, prog.program, 0.7, 0.7, 0.15, 0.15);

            gl.viewport(0, 0, W, H);
            gl.clearColor(0.0, 0.0, 0.0, 0.0);
            gl.clear(gl.COLOR_BUFFER_BIT);

            gl.useProgram(prog.program);

            // Draw large green quad
            gl.uniform4f(uColor, 0.0, 1.0, 0.0, 1.0);
            gl.bindVertexArray(largeQuad.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            // Draw small red quad on top
            gl.uniform4f(uColor, 1.0, 0.0, 0.0, 1.0);
            gl.bindVertexArray(smallQuad.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            // Center should be green (large quad)
            expectPixelNear(readPixel(gl, W / 2, H / 2), 0, 255, 0, 255, 2, 'large quad center');

            // Top-right should be red (small quad overrides)
            const trPixel = readPixel(gl, W - 20, H - 20);
            expectPixelNear(trPixel, 255, 0, 0, 255, 2, 'small quad top-right');

            // Bottom-left corner should be green (inside large quad)
            expectPixelNear(readPixel(gl, 15, 15), 0, 255, 0, 255, 2, 'large quad corner');

            largeQuad.cleanup();
            smallQuad.cleanup();
            gl.deleteProgram(prog.program);
        });
    });

    // =========================================================================
    // 2. Overdraw Detection
    // =========================================================================
    describe('Overdraw Detection', () => {
        it('verifies alpha compositing with two overlapping transparent quads', () => {
            const prog = compileProgram(gl, VERT_POSITION_ONLY, FRAG_UNIFORM_COLOR);
            const uColor = gl.getUniformLocation(prog.program, 'u_color')!;

            const quad = createFullscreenQuad(gl, prog.program);

            gl.viewport(0, 0, W, H);
            gl.clearColor(0.0, 0.0, 0.0, 1.0); // Opaque black background
            gl.clear(gl.COLOR_BUFFER_BIT);

            gl.useProgram(prog.program);
            gl.enable(gl.BLEND);
            gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);

            // First quad: 50% red
            gl.uniform4f(uColor, 1.0, 0.0, 0.0, 0.5);
            gl.bindVertexArray(quad.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

            // Second quad: 50% blue, overlapping the same area
            gl.uniform4f(uColor, 0.0, 0.0, 1.0, 0.5);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);
            gl.disable(gl.BLEND);

            // With opaque black background and SRC_ALPHA/ONE_MINUS_SRC_ALPHA:
            // After pass 1 (50% red): R = 255*0.5 + 0*0.5 = 128
            // After pass 2 (50% blue): R = 0*0.5 + 128*0.5 = 64, B = 255*0.5 + 0*0.5 = 128
            const center = readPixel(gl, W / 2, H / 2);
            expect(center[0]).toBeGreaterThanOrEqual(60);
            expect(center[0]).toBeLessThanOrEqual(70);
            expect(center[1]).toBeLessThanOrEqual(4);
            expect(center[2]).toBeGreaterThanOrEqual(122);
            expect(center[2]).toBeLessThanOrEqual(134);

            quad.cleanup();
            gl.deleteProgram(prog.program);
        });

        it('verifies overdraw with three overlapping quads accumulates correctly', () => {
            const prog = compileProgram(gl, VERT_POSITION_ONLY, FRAG_UNIFORM_COLOR);
            const uColor = gl.getUniformLocation(prog.program, 'u_color')!;

            const quad = createFullscreenQuad(gl, prog.program);

            gl.viewport(0, 0, W, H);
            gl.clearColor(0.0, 0.0, 0.0, 1.0); // Opaque black background
            gl.clear(gl.COLOR_BUFFER_BIT);

            gl.useProgram(prog.program);
            gl.enable(gl.BLEND);
            gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);

            // Three 30% alpha quads: red, green, blue
            gl.uniform4f(uColor, 1.0, 0.0, 0.0, 0.3);
            gl.bindVertexArray(quad.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

            gl.uniform4f(uColor, 0.0, 1.0, 0.0, 0.3);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

            gl.uniform4f(uColor, 0.0, 0.0, 1.0, 0.3);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);
            gl.disable(gl.BLEND);

            // All three channels should have significant values
            // With opaque black bg and 3x 30% alpha passes:
            // First channel: 255*0.3 = 76.5, then 76.5*0.7 = 53.6, then 53.6*0.7 ≈ 37.5
            const center = readPixel(gl, W / 2, H / 2);
            expect(center[0]).toBeGreaterThan(30);  // R from first quad
            expect(center[1]).toBeGreaterThan(30);  // G from second quad
            expect(center[2]).toBeGreaterThan(30);  // B from third quad
            // Alpha accumulates but doesn't reach 255 with 30% source alpha:
            // A = 255*0.3 + (255*0.3)*0.7 + ... ≈ 138 after 3 passes
            expect(center[3]).toBeGreaterThan(130);
            expect(center[3]).toBeLessThanOrEqual(255);

            quad.cleanup();
            gl.deleteProgram(prog.program);
        });
    });

    // =========================================================================
    // 3. Depth Ordering
    // =========================================================================
    describe('Depth Ordering', () => {
        it('renders 3 quads at different depths and verifies front-to-back occlusion', () => {
            const prog = compileProgram(gl, VERT_DEPTH, FRAG_UNIFORM_COLOR);
            const uColor = gl.getUniformLocation(prog.program, 'u_color')!;
            const uDepth = gl.getUniformLocation(prog.program, 'u_depth')!;

            const quad = createPositionOnlyQuad(gl, prog.program);

            gl.viewport(0, 0, W, H);
            gl.enable(gl.DEPTH_TEST);
            gl.depthFunc(gl.LESS);
            gl.clearColor(0.0, 0.0, 0.0, 0.0);
            gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

            gl.useProgram(prog.program);

            // Back quad (blue) at z=0.8
            gl.uniform4f(uColor, 0.0, 0.0, 1.0, 1.0);
            gl.uniform1f(uDepth, 0.8);
            gl.bindVertexArray(quad.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

            // Middle quad (green) at z=0.0
            gl.uniform4f(uColor, 0.0, 1.0, 0.0, 1.0);
            gl.uniform1f(uDepth, 0.0);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

            // Front quad (red) at z=-0.8
            gl.uniform4f(uColor, 1.0, 0.0, 0.0, 1.0);
            gl.uniform1f(uDepth, -0.8);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            // Center should be red (front quad at z=-0.8 is nearest)
            const center = readPixel(gl, W / 2, H / 2);
            expectPixelNear(center, 255, 0, 0, 255, 2, 'front quad visible');

            gl.disable(gl.DEPTH_TEST);
            quad.cleanup();
            gl.deleteProgram(prog.program);
        });

        it('with depth test OFF, last-drawn quad wins regardless of depth', () => {
            const prog = compileProgram(gl, VERT_DEPTH, FRAG_UNIFORM_COLOR);
            const uColor = gl.getUniformLocation(prog.program, 'u_color')!;
            const uDepth = gl.getUniformLocation(prog.program, 'u_depth')!;

            const quad = createPositionOnlyQuad(gl, prog.program);

            gl.viewport(0, 0, W, H);
            // Depth test DISABLED
            gl.clearColor(0.0, 0.0, 0.0, 0.0);
            gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

            gl.useProgram(prog.program);

            // Draw front quad first (red, z=-0.8)
            gl.uniform4f(uColor, 1.0, 0.0, 0.0, 1.0);
            gl.uniform1f(uDepth, -0.8);
            gl.bindVertexArray(quad.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

            // Draw back quad second (blue, z=0.8) — should overwrite
            gl.uniform4f(uColor, 0.0, 0.0, 1.0, 1.0);
            gl.uniform1f(uDepth, 0.8);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            // Center should be blue (last drawn wins without depth test)
            const center = readPixel(gl, W / 2, H / 2);
            expectPixelNear(center, 0, 0, 255, 255, 2, 'last-drawn wins');

            quad.cleanup();
            gl.deleteProgram(prog.program);
        });

        it('verifies depthFunc GREATER inverts occlusion order', () => {
            const prog = compileProgram(gl, VERT_DEPTH, FRAG_UNIFORM_COLOR);
            const uColor = gl.getUniformLocation(prog.program, 'u_color')!;
            const uDepth = gl.getUniformLocation(prog.program, 'u_depth')!;

            const quad = createPositionOnlyQuad(gl, prog.program);

            gl.viewport(0, 0, W, H);
            gl.enable(gl.DEPTH_TEST);
            gl.depthFunc(gl.GREATER); // Accept fragment if its depth is GREATER
            gl.clearDepth(0.0);       // Start with depth = 0.0 (nearest)
            gl.clearColor(0.0, 0.0, 0.0, 0.0);
            gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

            gl.useProgram(prog.program);

            // First quad (red) at z=0.5 — depth 0.5 > clearDepth 0.0, passes GREATER
            gl.uniform4f(uColor, 1.0, 0.0, 0.0, 1.0);
            gl.uniform1f(uDepth, 0.5);
            gl.bindVertexArray(quad.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

            // Second quad (green) at z=0.2 — depth 0.2 < buffer 0.5, fails GREATER
            gl.uniform4f(uColor, 0.0, 1.0, 0.0, 1.0);
            gl.uniform1f(uDepth, 0.2);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            // Center should be red (first quad passed, second was rejected)
            const center = readPixel(gl, W / 2, H / 2);
            expectPixelNear(center, 255, 0, 0, 255, 2, 'GREATER depth func');

            gl.disable(gl.DEPTH_TEST);
            gl.clearDepth(1.0); // Reset to default
            quad.cleanup();
            gl.deleteProgram(prog.program);
        });
    });

    // =========================================================================
    // 4. Texture Sampling Accuracy
    // =========================================================================
    describe('Texture Sampling Accuracy', () => {
        it('samples a 4x4 checkerboard texture at specific UV coordinates', () => {
            // 4x4 checkerboard: alternating black (0,0,0,255) and white (255,255,255,255)
            // Layout (row-major, bottom-to-top in GL):
            //   Row 0 (y=0): W B W B
            //   Row 1 (y=1): B W B W
            //   Row 2 (y=2): W B W B
            //   Row 3 (y=3): B W B W
            const W_ = 255, B_ = 0;
            const texData = new Uint8Array([
                // Row 0
                W_, W_, W_, 255,   B_, B_, B_, 255,   W_, W_, W_, 255,   B_, B_, B_, 255,
                // Row 1
                B_, B_, B_, 255,   W_, W_, W_, 255,   B_, B_, B_, 255,   W_, W_, W_, 255,
                // Row 2
                W_, W_, W_, 255,   B_, B_, B_, 255,   W_, W_, W_, 255,   B_, B_, B_, 255,
                // Row 3
                B_, B_, B_, 255,   W_, W_, W_, 255,   B_, B_, B_, 255,   W_, W_, W_, 255,
            ]);

            const tex = createTextureFromData(gl, 4, texData);
            const prog = compileProgram(gl, VERT_TEXTURED, FRAG_TEXTURED);
            const uTexture = gl.getUniformLocation(prog.program, 'u_texture')!;

            // Use a fullscreen quad to map UV [0..1] across the entire texture
            const quad = createFullscreenQuad(gl, prog.program);

            gl.viewport(0, 0, W, H);
            gl.clearColor(0, 0, 0, 0);
            gl.clear(gl.COLOR_BUFFER_BIT);

            gl.useProgram(prog.program);
            gl.activeTexture(gl.TEXTURE0);
            gl.bindTexture(gl.TEXTURE_2D, tex);
            gl.uniform1i(uTexture, 0);

            gl.bindVertexArray(quad.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            // With NEAREST filtering on a 4x4 texture over 128x128 pixels:
            // Each texel covers 32x32 pixels.
            // UV(0.125, 0.125) → texel (0,0) = White (bottom-left)
            // UV(0.375, 0.125) → texel (1,0) = Black
            // UV(0.125, 0.375) → texel (0,1) = Black
            // UV(0.375, 0.375) → texel (1,1) = White

            // Bottom-left texel region center (pixel ~16, 16)
            const blPixel = readPixel(gl, 16, 16);
            expectPixelNear(blPixel, 255, 255, 255, 255, 2, 'texel (0,0) white');

            // Next texel right (pixel ~48, 16)
            const brPixel = readPixel(gl, 48, 16);
            expectPixelNear(brPixel, 0, 0, 0, 255, 2, 'texel (1,0) black');

            // Next texel up (pixel ~16, 48)
            const tlPixel = readPixel(gl, 16, 48);
            expectPixelNear(tlPixel, 0, 0, 0, 255, 2, 'texel (0,1) black');

            // Diagonal texel (pixel ~48, 48)
            const centerPixel = readPixel(gl, 48, 48);
            expectPixelNear(centerPixel, 255, 255, 255, 255, 2, 'texel (1,1) white');

            quad.cleanup();
            gl.deleteTexture(tex);
            gl.deleteProgram(prog.program);
        });

        it('samples correct texel at UV boundaries with NEAREST filtering', () => {
            // 2x2 texture: red, green, blue, white
            const texData = new Uint8Array([
                255, 0, 0, 255,     0, 255, 0, 255,
                0, 0, 255, 255,     255, 255, 255, 255,
            ]);
            const tex = createTextureFromData(gl, 2, texData);
            const prog = compileProgram(gl, VERT_TEXTURED, FRAG_TEXTURED);
            const uTexture = gl.getUniformLocation(prog.program, 'u_texture')!;

            const quad = createFullscreenQuad(gl, prog.program);

            gl.viewport(0, 0, W, H);
            gl.clearColor(0, 0, 0, 0);
            gl.clear(gl.COLOR_BUFFER_BIT);

            gl.useProgram(prog.program);
            gl.activeTexture(gl.TEXTURE0);
            gl.bindTexture(gl.TEXTURE_2D, tex);
            gl.uniform1i(uTexture, 0);

            gl.bindVertexArray(quad.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            // 2x2 texture over 128x128 pixels → each texel = 64x64 pixels
            // Bottom-left (UV 0,0) = red → pixel (10, 10)
            expectPixelNear(readPixel(gl, 10, 10), 255, 0, 0, 255, 2, 'red texel');

            // Bottom-right (UV 1,0) = green → pixel (W-10, 10)
            expectPixelNear(readPixel(gl, W - 10, 10), 0, 255, 0, 255, 2, 'green texel');

            // Top-left (UV 0,1) = blue → pixel (10, H-10)
            expectPixelNear(readPixel(gl, 10, H - 10), 0, 0, 255, 255, 2, 'blue texel');

            // Top-right (UV 1,1) = white → pixel (W-10, H-10)
            expectPixelNear(readPixel(gl, W - 10, H - 10), 255, 255, 255, 255, 2, 'white texel');

            quad.cleanup();
            gl.deleteTexture(tex);
            gl.deleteProgram(prog.program);
        });
    });

    // =========================================================================
    // 5. Viewport Clipping
    // =========================================================================
    describe('Viewport Clipping', () => {
        it('pixels outside viewport remain untouched after rendering', () => {
            const prog = compileProgram(gl, VERT_POSITION_ONLY, FRAG_UNIFORM_COLOR);
            const uColor = gl.getUniformLocation(prog.program, 'u_color')!;

            const quad = createFullscreenQuad(gl, prog.program);

            // Clear entire canvas to dark grey
            gl.viewport(0, 0, W, H);
            gl.clearColor(0.2, 0.2, 0.2, 1.0);
            gl.clear(gl.COLOR_BUFFER_BIT);

            // Set viewport to bottom-left quadrant only
            const halfW = Math.floor(W / 2);
            const halfH = Math.floor(H / 2);
            gl.viewport(0, 0, halfW, halfH);

            gl.useProgram(prog.program);
            gl.uniform4f(uColor, 1.0, 0.0, 0.0, 1.0); // Red

            gl.bindVertexArray(quad.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            // Inside viewport (bottom-left) should be red
            const inside = readPixel(gl, 5, 5);
            expectPixelNear(inside, 255, 0, 0, 255, 2, 'inside viewport');

            // Outside viewport (top-right) should remain dark grey
            const outside = readPixel(gl, W - 5, H - 5);
            expectPixelNear(outside, 51, 51, 51, 255, 2, 'outside viewport');

            // Outside viewport (top-left) should also remain dark grey
            const outsideTL = readPixel(gl, 5, H - 5);
            expectPixelNear(outsideTL, 51, 51, 51, 255, 2, 'outside viewport TL');

            // Reset viewport
            gl.viewport(0, 0, W, H);

            quad.cleanup();
            gl.deleteProgram(prog.program);
        });

        it('viewport sub-region renders correctly with scissor test', () => {
            const prog = compileProgram(gl, VERT_POSITION_ONLY, FRAG_UNIFORM_COLOR);
            const uColor = gl.getUniformLocation(prog.program, 'u_color')!;

            const quad = createFullscreenQuad(gl, prog.program);

            // Clear to black
            gl.viewport(0, 0, W, H);
            gl.clearColor(0.0, 0.0, 0.0, 1.0);
            gl.clear(gl.COLOR_BUFFER_BIT);

            // Set viewport + scissor to center region
            const margin = 32;
            gl.viewport(margin, margin, W - 2 * margin, H - 2 * margin);
            gl.enable(gl.SCISSOR_TEST);
            gl.scissor(margin, margin, W - 2 * margin, H - 2 * margin);

            gl.useProgram(prog.program);
            gl.uniform4f(uColor, 0.0, 1.0, 0.0, 1.0); // Green

            gl.bindVertexArray(quad.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            gl.disable(gl.SCISSOR_TEST);
            gl.viewport(0, 0, W, H);

            // Center should be green
            expectPixelNear(readPixel(gl, W / 2, H / 2), 0, 255, 0, 255, 2, 'center green');

            // Corner should remain black
            expectPixelNear(readPixel(gl, 2, 2), 0, 0, 0, 255, 2, 'corner black');

            quad.cleanup();
            gl.deleteProgram(prog.program);
        });
    });

    // =========================================================================
    // 6. Alpha Blending Modes
    // =========================================================================
    describe('Alpha Blending Modes', () => {
        it('SRC_ALPHA / ONE_MINUS_SRC_ALPHA (standard alpha blending)', () => {
            const prog = compileProgram(gl, VERT_POSITION_ONLY, FRAG_UNIFORM_COLOR);
            const uColor = gl.getUniformLocation(prog.program, 'u_color')!;

            const backdrop = createFullscreenQuad(gl, prog.program);
            const overlay = createFullscreenQuad(gl, prog.program);

            gl.viewport(0, 0, W, H);
            gl.clearColor(0.0, 0.0, 0.0, 0.0);
            gl.clear(gl.COLOR_BUFFER_BIT);

            gl.useProgram(prog.program);

            // Draw opaque green backdrop
            gl.uniform4f(uColor, 0.0, 1.0, 0.0, 1.0);
            gl.bindVertexArray(backdrop.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            // Blend 50% red over green
            gl.enable(gl.BLEND);
            gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
            gl.uniform4f(uColor, 1.0, 0.0, 0.0, 0.5);
            gl.bindVertexArray(overlay.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);
            gl.disable(gl.BLEND);

            // Expected: 0.5 * (255,0,0) + 0.5 * (0,255,0) = (128, 128, 0)
            const center = readPixel(gl, W / 2, H / 2);
            expect(center[0]).toBeGreaterThanOrEqual(124);
            expect(center[0]).toBeLessThanOrEqual(130);
            expect(center[1]).toBeGreaterThanOrEqual(124);
            expect(center[1]).toBeLessThanOrEqual(130);
            expect(center[2]).toBeLessThanOrEqual(4);

            backdrop.cleanup();
            overlay.cleanup();
            gl.deleteProgram(prog.program);
        });

        it('ONE / ONE (additive blending)', () => {
            const prog = compileProgram(gl, VERT_POSITION_ONLY, FRAG_UNIFORM_COLOR);
            const uColor = gl.getUniformLocation(prog.program, 'u_color')!;

            const backdrop = createFullscreenQuad(gl, prog.program);
            const overlay = createFullscreenQuad(gl, prog.program);

            gl.viewport(0, 0, W, H);
            gl.clearColor(0.0, 0.0, 0.0, 0.0);
            gl.clear(gl.COLOR_BUFFER_BIT);

            gl.useProgram(prog.program);

            // Draw dim green backdrop (100/255 ≈ 0.39)
            gl.uniform4f(uColor, 0.0, 0.4, 0.0, 1.0);
            gl.bindVertexArray(backdrop.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            // Additive blend: red (0.3, 0, 0, 1)
            gl.enable(gl.BLEND);
            gl.blendFunc(gl.ONE, gl.ONE);
            gl.uniform4f(uColor, 0.3, 0.0, 0.0, 1.0);
            gl.bindVertexArray(overlay.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);
            gl.disable(gl.BLEND);

            // Expected: (0.3*255 + 0, 0.4*255 + 0, 0) = (77, 102, 0) approximately
            const center = readPixel(gl, W / 2, H / 2);
            expect(center[0]).toBeGreaterThanOrEqual(72);
            expect(center[0]).toBeLessThanOrEqual(82);
            expect(center[1]).toBeGreaterThanOrEqual(98);
            expect(center[1]).toBeLessThanOrEqual(108);
            expect(center[2]).toBeLessThanOrEqual(4);

            backdrop.cleanup();
            overlay.cleanup();
            gl.deleteProgram(prog.program);
        });

        it('ONE / ZERO (overwrite blending, source replaces destination)', () => {
            const prog = compileProgram(gl, VERT_POSITION_ONLY, FRAG_UNIFORM_COLOR);
            const uColor = gl.getUniformLocation(prog.program, 'u_color')!;

            const backdrop = createFullscreenQuad(gl, prog.program);
            const overlay = createFullscreenQuad(gl, prog.program);

            gl.viewport(0, 0, W, H);
            gl.clearColor(0.0, 0.0, 0.0, 0.0);
            gl.clear(gl.COLOR_BUFFER_BIT);

            gl.useProgram(prog.program);

            // Draw green backdrop
            gl.uniform4f(uColor, 0.0, 1.0, 0.0, 1.0);
            gl.bindVertexArray(backdrop.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            // ONE/ZERO: result = src * 1 + dst * 0 = src (pure overwrite)
            gl.enable(gl.BLEND);
            gl.blendFunc(gl.ONE, gl.ZERO);
            gl.uniform4f(uColor, 1.0, 0.0, 0.0, 1.0);
            gl.bindVertexArray(overlay.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);
            gl.disable(gl.BLEND);

            // Should be pure red (source overwrites destination)
            const center = readPixel(gl, W / 2, H / 2);
            expectPixelNear(center, 255, 0, 0, 255, 2, 'ONE/ZERO overwrite');

            backdrop.cleanup();
            overlay.cleanup();
            gl.deleteProgram(prog.program);
        });
    });

    // =========================================================================
    // 7. Multi-Pass Rendering
    // =========================================================================
    describe('Multi-Pass Rendering', () => {
        it('pass 1 sets background, pass 2 renders foreground quad', () => {
            const prog = compileProgram(gl, VERT_POSITION_ONLY, FRAG_UNIFORM_COLOR);
            const uColor = gl.getUniformLocation(prog.program, 'u_color')!;

            const fullscreen = createFullscreenQuad(gl, prog.program);
            const smallQuad = createPositionedPosOnly(gl, prog.program, 0.0, 0.0, 0.3, 0.3);

            gl.viewport(0, 0, W, H);

            // --- Pass 1: clear + fill with background color ---
            gl.clearColor(0.0, 0.0, 0.0, 0.0);
            gl.clear(gl.COLOR_BUFFER_BIT);

            gl.useProgram(prog.program);
            gl.uniform4f(uColor, 0.2, 0.0, 0.4, 1.0); // Purple background
            gl.bindVertexArray(fullscreen.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            // Verify background pass
            const bgPixel = readPixel(gl, 5, 5);
            expect(bgPixel[0]).toBeGreaterThanOrEqual(48);
            expect(bgPixel[0]).toBeLessThanOrEqual(56);
            expect(bgPixel[2]).toBeGreaterThanOrEqual(100);
            expect(bgPixel[2]).toBeLessThanOrEqual(108);

            // --- Pass 2: render foreground quad ---
            gl.useProgram(prog.program);
            gl.uniform4f(uColor, 1.0, 1.0, 0.0, 1.0); // Yellow foreground
            gl.bindVertexArray(smallQuad.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            // Foreground center should be yellow
            expectPixelNear(readPixel(gl, W / 2, H / 2), 255, 255, 0, 255, 2, 'foreground');

            // Background corner should still be purple
            const cornerAfter = readPixel(gl, 3, 3);
            expect(cornerAfter[0]).toBeGreaterThanOrEqual(48);
            expect(cornerAfter[0]).toBeLessThanOrEqual(56);
            expect(cornerAfter[2]).toBeGreaterThanOrEqual(100);
            expect(cornerAfter[2]).toBeLessThanOrEqual(108);

            fullscreen.cleanup();
            smallQuad.cleanup();
            gl.deleteProgram(prog.program);
        });

        it('multi-pass with depth clear between passes', () => {
            const prog = compileProgram(gl, VERT_DEPTH, FRAG_UNIFORM_COLOR);
            const uColor = gl.getUniformLocation(prog.program, 'u_color')!;
            const uDepth = gl.getUniformLocation(prog.program, 'u_depth')!;

            const quad = createPositionOnlyQuad(gl, prog.program);

            gl.viewport(0, 0, W, H);
            gl.enable(gl.DEPTH_TEST);
            gl.depthFunc(gl.LESS);

            // --- Pass 1: render a quad at z=0.0 ---
            gl.clearColor(0.0, 0.0, 1.0, 1.0); // Blue background
            gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

            gl.useProgram(prog.program);
            gl.uniform4f(uColor, 1.0, 0.0, 0.0, 1.0); // Red
            gl.uniform1f(uDepth, 0.0);
            gl.bindVertexArray(quad.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            // Center should be red
            expectPixelNear(readPixel(gl, W / 2, H / 2), 255, 0, 0, 255, 2, 'pass 1 red');

            // --- Pass 2: clear depth only, render at z=-0.5 (nearer) ---
            gl.clear(gl.DEPTH_BUFFER_BIT);
            // Color buffer should still have red

            gl.uniform4f(uColor, 0.0, 1.0, 0.0, 1.0); // Green
            gl.uniform1f(uDepth, -0.5);
            gl.bindVertexArray(quad.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            // Center should be green (new quad passes depth test after clear)
            expectPixelNear(readPixel(gl, W / 2, H / 2), 0, 255, 0, 255, 2, 'pass 2 green');

            gl.disable(gl.DEPTH_TEST);
            quad.cleanup();
            gl.deleteProgram(prog.program);
        });

        it('multi-pass with alpha accumulation across passes', () => {
            const prog = compileProgram(gl, VERT_POSITION_ONLY, FRAG_UNIFORM_COLOR);
            const uColor = gl.getUniformLocation(prog.program, 'u_color')!;

            const quad = createFullscreenQuad(gl, prog.program);

            gl.viewport(0, 0, W, H);
            gl.clearColor(0.0, 0.0, 0.0, 1.0);
            gl.clear(gl.COLOR_BUFFER_BIT);

            gl.enable(gl.BLEND);
            gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
            gl.useProgram(prog.program);

            // Pass 1: 25% white
            gl.uniform4f(uColor, 1.0, 1.0, 1.0, 0.25);
            gl.bindVertexArray(quad.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

            // Pass 2: another 25% white
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

            // Pass 3: another 25% white
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

            // Pass 4: another 25% white
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);
            gl.disable(gl.BLEND);

            // After 4 passes of 25% white over black:
            // Pass 1: 0.25 * 255 + 0 * 0.75 = 64
            // Pass 2: 0.25 * 255 + 64 * 0.75 = 64 + 48 = 112
            // Pass 3: 0.25 * 255 + 112 * 0.75 = 64 + 84 = 148
            // Pass 4: 0.25 * 255 + 148 * 0.75 = 64 + 111 = 175
            const center = readPixel(gl, W / 2, H / 2);
            expect(center[0]).toBeGreaterThanOrEqual(168);
            expect(center[0]).toBeLessThanOrEqual(182);
            expect(center[1]).toBeGreaterThanOrEqual(168);
            expect(center[1]).toBeLessThanOrEqual(182);
            expect(center[2]).toBeGreaterThanOrEqual(168);
            expect(center[2]).toBeLessThanOrEqual(182);

            quad.cleanup();
            gl.deleteProgram(prog.program);
        });
    });

    // =========================================================================
    // 8. Stencil Testing
    // =========================================================================
    describe('Stencil Testing', () => {
        it('stencil buffer masks regions: stencil-pass pixels rendered, stencil-fail pixels blocked', () => {
            // Shader for writing stencil values (uniform color, position only)
            const progColor = compileProgram(gl, VERT_POSITION_ONLY, FRAG_UNIFORM_COLOR);
            const uColor = gl.getUniformLocation(progColor.program, 'u_color')!;

            const fullscreen = createFullscreenQuad(gl, progColor.program);
            const smallQuad = createPositionedPosOnly(gl, progColor.program, 0.0, 0.0, 0.3, 0.3);

            gl.viewport(0, 0, W, H);
            gl.clearColor(0.0, 0.0, 0.0, 0.0);
            gl.clear(gl.COLOR_BUFFER_BIT | gl.STENCIL_BUFFER_BIT);

            // --- Step 1: Write stencil value 1 in the center region ---
            gl.enable(gl.STENCIL_TEST);
            gl.stencilFunc(gl.ALWAYS, 1, 0xFF);
            gl.stencilOp(gl.REPLACE, gl.REPLACE, gl.REPLACE);
            gl.colorMask(false, false, false, false); // Don't write color

            gl.useProgram(progColor.program);
            gl.bindVertexArray(smallQuad.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            // --- Step 2: Render fullscreen quad only where stencil == 1 ---
            gl.colorMask(true, true, true, true);
            gl.stencilFunc(gl.EQUAL, 1, 0xFF);
            gl.stencilOp(gl.KEEP, gl.KEEP, gl.KEEP);

            gl.uniform4f(uColor, 0.0, 1.0, 0.0, 1.0); // Green
            gl.bindVertexArray(fullscreen.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            gl.disable(gl.STENCIL_TEST);

            // Center (inside stencil mask) should be green
            expectPixelNear(readPixel(gl, W / 2, H / 2), 0, 255, 0, 255, 2, 'stencil pass');

            // Corner (outside stencil mask) should remain clear color (black/transparent)
            const corner = readPixel(gl, 3, 3);
            expectPixelNear(corner, 0, 0, 0, 0, 2, 'stencil fail');

            fullscreen.cleanup();
            smallQuad.cleanup();
            gl.deleteProgram(progColor.program);
        });

        it('stencil test with inverted mask (render where stencil == 0)', () => {
            const prog = compileProgram(gl, VERT_POSITION_ONLY, FRAG_UNIFORM_COLOR);
            const uColor = gl.getUniformLocation(prog.program, 'u_color')!;

            const fullscreen = createFullscreenQuad(gl, prog.program);
            const smallQuad = createPositionedPosOnly(gl, prog.program, 0.0, 0.0, 0.3, 0.3);

            gl.viewport(0, 0, W, H);
            gl.clearColor(0.0, 0.0, 0.0, 0.0);
            gl.clear(gl.COLOR_BUFFER_BIT | gl.STENCIL_BUFFER_BIT);

            // Write stencil=1 in center region
            gl.enable(gl.STENCIL_TEST);
            gl.stencilFunc(gl.ALWAYS, 1, 0xFF);
            gl.stencilOp(gl.REPLACE, gl.REPLACE, gl.REPLACE);
            gl.colorMask(false, false, false, false);

            gl.useProgram(prog.program);
            gl.bindVertexArray(smallQuad.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            // Render fullscreen where stencil == 0 (inverted mask)
            gl.colorMask(true, true, true, true);
            gl.stencilFunc(gl.EQUAL, 0, 0xFF);
            gl.stencilOp(gl.KEEP, gl.KEEP, gl.KEEP);

            gl.uniform4f(uColor, 1.0, 0.0, 0.0, 1.0); // Red
            gl.bindVertexArray(fullscreen.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            gl.disable(gl.STENCIL_TEST);

            // Center (stencil == 1, fails EQUAL 0) should remain clear
            const center = readPixel(gl, W / 2, H / 2);
            expectPixelNear(center, 0, 0, 0, 0, 2, 'inverted mask center');

            // Corner (stencil == 0, passes EQUAL 0) should be red
            expectPixelNear(readPixel(gl, 3, 3), 255, 0, 0, 255, 2, 'inverted mask corner');

            fullscreen.cleanup();
            smallQuad.cleanup();
            gl.deleteProgram(prog.program);
        });

        it('stencil multi-value masking with two-region EQUAL comparison', () => {
            const prog = compileProgram(gl, VERT_POSITION_ONLY, FRAG_UNIFORM_COLOR);
            const uColor = gl.getUniformLocation(prog.program, 'u_color')!;

            const fullscreen = createFullscreenQuad(gl, prog.program);
            // Quad covering left half
            const leftHalf = createPositionedPosOnly(gl, prog.program, -0.5, 0.0, 0.5, 1.0);

            gl.viewport(0, 0, W, H);
            gl.clearColor(0.0, 0.0, 0.0, 0.0);
            gl.clear(gl.COLOR_BUFFER_BIT | gl.STENCIL_BUFFER_BIT);

            gl.enable(gl.STENCIL_TEST);
            gl.stencilMask(0xFF);

            gl.useProgram(prog.program);

            // Pass 1: REPLACE stencil=1 across full screen
            gl.stencilFunc(gl.ALWAYS, 1, 0xFF);
            gl.stencilOp(gl.KEEP, gl.REPLACE, gl.KEEP);
            gl.colorMask(false, false, false, false);

            gl.bindVertexArray(fullscreen.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            // Pass 2: REPLACE stencil=2 in left half (overwrites 1 → 2)
            gl.stencilFunc(gl.ALWAYS, 2, 0xFF);
            gl.stencilOp(gl.KEEP, gl.REPLACE, gl.KEEP);

            gl.bindVertexArray(leftHalf.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            // Pass 3: Render fullscreen red where stencil == 2
            // Left half has stencil=2 (== 2, passes), right half has stencil=1 (!= 2, fails)
            gl.colorMask(true, true, true, true);
            gl.stencilFunc(gl.EQUAL, 2, 0xFF);
            gl.stencilOp(gl.KEEP, gl.KEEP, gl.KEEP);

            gl.uniform4f(uColor, 1.0, 0.0, 0.0, 1.0);
            gl.bindVertexArray(fullscreen.vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);

            gl.disable(gl.STENCIL_TEST);

            // Left side (stencil=2, passes EQUAL 2) should be red
            expectPixelNear(readPixel(gl, 10, H / 2), 255, 0, 0, 255, 2, 'stencil EQUAL(2) left');

            // Right side (stencil=1, fails EQUAL 2) should remain clear
            const rightSide = readPixel(gl, W - 10, H / 2);
            expectPixelNear(rightSide, 0, 0, 0, 0, 2, 'stencil EQUAL(2) right');

            fullscreen.cleanup();
            leftHalf.cleanup();
            gl.deleteProgram(prog.program);
        });
    });
});
