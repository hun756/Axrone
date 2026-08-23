import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { createBufferFactory } from '@axrone/render-webgl2/buffer';
import type { IBufferFactory } from '@axrone/render-webgl2/buffer';

/**
 * T-05: Rendering Correctness Test (Pixel-Level)
 *
 * Layer 3 (Browser) tests that use a real WebGL2 context to render geometry
 * and verify pixel output via gl.readPixels(). Covers clear color, colored
 * quads, textured quads, depth occlusion, alpha blending, scissor/viewport,
 * and spatial correctness of multiple quads.
 */

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const VERT_QUAD = `#version 300 es
in vec2 a_position;
in vec2 a_uv;
out vec2 v_uv;
void main() {
    gl_Position = vec4(a_position, 0.0, 1.0);
    v_uv = a_uv;
}
`;

const FRAG_SOLID = `#version 300 es
precision highp float;
uniform vec4 u_color;
out vec4 fragColor;
void main() {
    fragColor = u_color;
}
`;

const FRAG_TEXTURED = `#version 300 es
precision highp float;
uniform sampler2D u_texture;
in vec2 v_uv;
out vec4 fragColor;
void main() {
    fragColor = texture(u_texture, v_uv);
}
`;

interface CompiledProgram {
    program: WebGLProgram;
    attribs: Record<string, number>;
    uniforms: Record<string, WebGLUniformLocation>;
}

function compileProgram(
    gl: WebGL2RenderingContext,
    vertSrc: string,
    fragSrc: string
): CompiledProgram {
    const compile = (type: number, src: string): WebGLShader => {
        const s = gl.createShader(type)!;
        gl.shaderSource(s, src);
        gl.compileShader(s);
        if (!gl.getShaderParameter(s, gl.COMPILE_STATUS)) {
            throw new Error(gl.getShaderInfoLog(s) || 'shader compile failed');
        }
        return s;
    };

    const vs = compile(gl.VERTEX_SHADER, vertSrc);
    const fs = compile(gl.FRAGMENT_SHADER, fragSrc);
    const program = gl.createProgram()!;
    gl.attachShader(program, vs);
    gl.attachShader(program, fs);
    gl.linkProgram(program);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
        throw new Error(gl.getProgramInfoLog(program) || 'program link failed');
    }
    gl.deleteShader(vs);
    gl.deleteShader(fs);

    const attribs: Record<string, number> = {};
    const uniforms: Record<string, WebGLUniformLocation> = {};
    return { program, attribs, uniforms };
}

function getAttribLocation(prog: CompiledProgram, name: string): number {
    if (!(name in prog.attribs)) {
        // We need the gl context — store it on first call via closure workaround
        // Instead, just call gl directly through a helper.
    }
    return prog.attribs[name];
}

/** Read a single pixel at (x, y) from the currently bound framebuffer. */
function readPixel(gl: WebGL2RenderingContext, x: number, y: number): Uint8Array {
    const pixels = new Uint8Array(4);
    gl.readPixels(x, y, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
    return pixels;
}

/** Create a fullscreen quad VAO covering clip-space [-1..1]. */
function createFullscreenQuad(gl: WebGL2RenderingContext, program: WebGLProgram): {
    vao: WebGLVertexArrayObject;
    cleanup: () => void;
} {
    // Two triangles covering the full clip-space quad.
    // a_position (vec2), a_uv (vec2) interleaved.
    const vertices = new Float32Array([
        // pos       uv
        -1, -1, 0, 0,
         1, -1, 1, 0,
        -1,  1, 0, 1,
         1,  1, 1, 1,
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
        cleanup: () => {
            gl.deleteVertexArray(vao);
            gl.deleteBuffer(vbo);
        },
    };
}

/** Create a smaller quad VAO at a specific clip-space position and size. */
function createPositionedQuad(
    gl: WebGL2RenderingContext,
    program: WebGLProgram,
    cx: number,
    cy: number,
    halfW: number,
    halfH: number
): { vao: WebGLVertexArrayObject; cleanup: () => void } {
    const x0 = cx - halfW;
    const x1 = cx + halfW;
    const y0 = cy - halfH;
    const y1 = cy + halfH;

    const vertices = new Float32Array([
        x0, y0, 0, 0,
        x1, y0, 1, 0,
        x0, y1, 0, 1,
        x1, y1, 1, 1,
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
        cleanup: () => {
            gl.deleteVertexArray(vao);
            gl.deleteBuffer(vbo);
        },
    };
}

/** Create a 2x2 RGBA8 texture from raw pixel data. */
function createSolidTexture(gl: WebGL2RenderingContext, r: number, g: number, b: number, a: number): WebGLTexture {
    const tex = gl.createTexture()!;
    gl.bindTexture(gl.TEXTURE_2D, tex);
    const data = new Uint8Array([r, g, b, a, r, g, b, a, r, g, b, a, r, g, b, a]);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 2, 2, 0, gl.RGBA, gl.UNSIGNED_BYTE, data);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    gl.bindTexture(gl.TEXTURE_2D, null);
    return tex;
}

/** Create a 2x2 gradient texture (red top-left, green top-right, blue bottom-left, white bottom-right). */
function createGradientTexture(gl: WebGL2RenderingContext): WebGLTexture {
    const tex = gl.createTexture()!;
    gl.bindTexture(gl.TEXTURE_2D, tex);
    const data = new Uint8Array([
        // (0,0) = red
        255, 0, 0, 255,
        // (1,0) = green
        0, 255, 0, 255,
        // (0,1) = blue
        0, 0, 255, 255,
        // (1,1) = white
        255, 255, 255, 255,
    ]);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 2, 2, 0, gl.RGBA, gl.UNSIGNED_BYTE, data);
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

describe('Rendering Correctness (Pixel-Level)', () => {
    let canvas: HTMLCanvasElement;
    let gl!: WebGL2RenderingContext;
    let factory!: IBufferFactory;
    const WIDTH = 128;
    const HEIGHT = 128;

    beforeEach(() => {
        canvas = document.createElement('canvas');
        canvas.width = WIDTH;
        canvas.height = HEIGHT;
        document.body.appendChild(canvas);

        const _gl = canvas.getContext('webgl2', {
            antialias: false,
            depth: true,
            stencil: false,
            alpha: true,
            premultipliedAlpha: false,
            preserveDrawingBuffer: true,
        });
        if (!_gl) throw new Error('WebGL2 not supported in this browser');
        gl = _gl;
        factory = createBufferFactory(gl);
    });

    afterEach(() => {
        try {
            (factory as any)?.dispose?.();
        } catch { // best-effort}
        if (canvas.parentNode) {
            canvas.parentNode.removeChild(canvas);
        }
    });

    // -----------------------------------------------------------------------
    // 1. Clear color → readPixels
    // -----------------------------------------------------------------------
    it('verifies clear color via readPixels', () => {
        gl.viewport(0, 0, WIDTH, HEIGHT);
        gl.clearColor(0.2, 0.4, 0.6, 1.0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        const center = readPixel(gl, WIDTH / 2, HEIGHT / 2);
        // 0.2 * 255 ≈ 51, 0.4 * 255 ≈ 102, 0.6 * 255 ≈ 153
        expect(center[0]).toBeCloseTo(51, 0);
        expect(center[1]).toBeCloseTo(102, 0);
        expect(center[2]).toBeCloseTo(153, 0);
        expect(center[3]).toBe(255);

        // Also check a corner pixel
        const corner = readPixel(gl, 0, 0);
        expect(corner[0]).toBeCloseTo(51, 0);
        expect(corner[1]).toBeCloseTo(102, 0);
    });

    // -----------------------------------------------------------------------
    // 2. Colored quad → readPixels verifies quad color
    // -----------------------------------------------------------------------
    it('renders a solid-colored quad and verifies pixel output', () => {
        const prog = compileProgram(gl, VERT_QUAD, FRAG_SOLID);
        const aPos = gl.getAttribLocation(prog.program, 'a_position');
        const uColor = gl.getUniformLocation(prog.program, 'u_color')!;

        const quad = createFullscreenQuad(gl, prog.program);

        gl.viewport(0, 0, WIDTH, HEIGHT);
        gl.clearColor(0, 0, 0, 0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        gl.useProgram(prog.program);
        gl.uniform4f(uColor, 1.0, 0.0, 0.0, 1.0); // Red

        gl.bindVertexArray(quad.vao);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
        gl.bindVertexArray(null);

        // Center should be red
        const center = readPixel(gl, WIDTH / 2, HEIGHT / 2);
        expect(center[0]).toBeGreaterThan(250); // R
        expect(center[1]).toBeLessThan(5);      // G
        expect(center[2]).toBeLessThan(5);      // B
        expect(center[3]).toBe(255);             // A

        quad.cleanup();
        gl.deleteProgram(prog.program);
    });

    // -----------------------------------------------------------------------
    // 3. Textured quad → readPixels verifies texture sampling
    // -----------------------------------------------------------------------
    it('renders a textured quad and verifies texture sampling', () => {
        const prog = compileProgram(gl, VERT_QUAD, FRAG_TEXTURED);
        const uTexture = gl.getUniformLocation(prog.program, 'u_texture')!;

        const quad = createFullscreenQuad(gl, prog.program);
        const tex = createGradientTexture(gl);

        gl.viewport(0, 0, WIDTH, HEIGHT);
        gl.clearColor(0, 0, 0, 0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        gl.useProgram(prog.program);
        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, tex);
        gl.uniform1i(uTexture, 0);

        gl.bindVertexArray(quad.vao);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
        gl.bindVertexArray(null);

        // The gradient texture has:
        // UV(0,0)=red at bottom-left, UV(1,0)=green at bottom-right
        // UV(0,1)=blue at top-left, UV(1,1)=white at top-right
        // Center of the quad samples UV ~(0.5, 0.5) — between all four texels.
        // With NEAREST filtering, the exact center pixel depends on coordinate rounding.
        // Instead, sample near the corners where the color is unambiguous.

        // Bottom-left area should be red (UV ~0,0)
        const bottomLeft = readPixel(gl, 2, 2);
        expect(bottomLeft[0]).toBeGreaterThan(200); // R
        expect(bottomLeft[1]).toBeLessThan(60);     // G
        expect(bottomLeft[2]).toBeLessThan(60);     // B

        // Top-right area should be white (UV ~1,1)
        const topRight = readPixel(gl, WIDTH - 3, HEIGHT - 3);
        expect(topRight[0]).toBeGreaterThan(200); // R
        expect(topRight[1]).toBeGreaterThan(200); // G
        expect(topRight[2]).toBeGreaterThan(200); // B

        quad.cleanup();
        gl.deleteTexture(tex);
        gl.deleteProgram(prog.program);
    });

    // -----------------------------------------------------------------------
    // 4. Depth test — front quad occludes back quad
    // -----------------------------------------------------------------------
    it('verifies depth occlusion: front quad occludes back quad', () => {
        // Use a vertex shader that takes a z offset via a uniform.
        const vertDepth = `#version 300 es
in vec2 a_position;
uniform float u_z;
void main() {
    gl_Position = vec4(a_position, u_z, 1.0);
}
`;
        const prog = compileProgram(gl, vertDepth, FRAG_SOLID);
        const uColor = gl.getUniformLocation(prog.program, 'u_color')!;
        const uZ = gl.getUniformLocation(prog.program, 'u_z')!;

        const quad = createFullscreenQuad(gl, prog.program);

        gl.viewport(0, 0, WIDTH, HEIGHT);
        gl.enable(gl.DEPTH_TEST);
        gl.depthFunc(gl.LESS);
        gl.clearColor(0, 0, 0, 0);
        gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

        gl.useProgram(prog.program);

        // Draw back quad (green) at z = 0.5 (farther)
        gl.uniform4f(uColor, 0.0, 1.0, 0.0, 1.0);
        gl.uniform1f(uZ, 0.5);
        gl.bindVertexArray(quad.vao);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

        // Draw front quad (red) at z = -0.5 (nearer)
        gl.uniform4f(uColor, 1.0, 0.0, 0.0, 1.0);
        gl.uniform1f(uZ, -0.5);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
        gl.bindVertexArray(null);

        // Center should be red (front quad wins)
        const center = readPixel(gl, WIDTH / 2, HEIGHT / 2);
        expect(center[0]).toBeGreaterThan(250); // R — front quad
        expect(center[1]).toBeLessThan(5);      // G — back quad occluded

        gl.disable(gl.DEPTH_TEST);
        quad.cleanup();
        gl.deleteProgram(prog.program);
    });

    // -----------------------------------------------------------------------
    // 5. Alpha blending — verify alpha compositing
    // -----------------------------------------------------------------------
    it('verifies alpha blending composites correctly', () => {
        const prog = compileProgram(gl, VERT_QUAD, FRAG_SOLID);
        const uColor = gl.getUniformLocation(prog.program, 'u_color')!;

        // Full-screen backdrop quad
        const backdrop = createFullscreenQuad(gl, prog.program);
        // Half-screen overlay quad (right half)
        const overlay = createPositionedQuad(gl, prog.program, 0.5, 0.0, 0.5, 1.0);

        gl.viewport(0, 0, WIDTH, HEIGHT);
        gl.clearColor(0, 0, 0, 0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        gl.useProgram(prog.program);

        // Draw blue backdrop
        gl.uniform4f(uColor, 0.0, 0.0, 1.0, 1.0);
        gl.bindVertexArray(backdrop.vao);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
        gl.bindVertexArray(null);

        // Enable blending and draw semi-transparent red overlay on right half
        gl.enable(gl.BLEND);
        gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);

        gl.uniform4f(uColor, 1.0, 0.0, 0.0, 0.5); // 50% red
        gl.bindVertexArray(overlay.vao);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
        gl.bindVertexArray(null);

        gl.disable(gl.BLEND);

        // Left side should be pure blue (no overlay)
        const leftSide = readPixel(gl, 5, HEIGHT / 2);
        expect(leftSide[0]).toBeLessThan(5);      // R
        expect(leftSide[1]).toBeLessThan(5);      // G
        expect(leftSide[2]).toBeGreaterThan(250); // B

        // Right side should be blended: 0.5 * red + 0.5 * blue ≈ (128, 0, 128)
        const rightSide = readPixel(gl, WIDTH - 5, HEIGHT / 2);
        expect(rightSide[0]).toBeGreaterThan(100); // R — blended red
        expect(rightSide[1]).toBeLessThan(30);     // G
        expect(rightSide[2]).toBeGreaterThan(100); // B — blended blue

        backdrop.cleanup();
        overlay.cleanup();
        gl.deleteProgram(prog.program);
    });

    // -----------------------------------------------------------------------
    // 6. Viewport scissor — pixels outside viewport are not rendered
    // -----------------------------------------------------------------------
    it('verifies viewport scissor: pixels outside viewport are not rendered', () => {
        const prog = compileProgram(gl, VERT_QUAD, FRAG_SOLID);
        const uColor = gl.getUniformLocation(prog.program, 'u_color')!;

        const quad = createFullscreenQuad(gl, prog.program);

        // Clear entire canvas to black
        gl.viewport(0, 0, WIDTH, HEIGHT);
        gl.clearColor(0, 0, 0, 1);
        gl.clear(gl.COLOR_BUFFER_BIT);

        // Set viewport to only the bottom-left quadrant
        const halfW = WIDTH / 2;
        const halfH = HEIGHT / 2;
        gl.viewport(0, 0, halfW, halfH);

        // Enable scissor test to match viewport
        gl.enable(gl.SCISSOR_TEST);
        gl.scissor(0, 0, halfW, halfH);

        gl.useProgram(prog.program);
        gl.uniform4f(uColor, 0.0, 1.0, 0.0, 1.0); // Green

        gl.bindVertexArray(quad.vao);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
        gl.bindVertexArray(null);

        gl.disable(gl.SCISSOR_TEST);
        gl.viewport(0, 0, WIDTH, HEIGHT);

        // Inside the scissor/viewport region (bottom-left) should be green
        const inside = readPixel(gl, 5, 5);
        expect(inside[0]).toBeLessThan(5);      // R
        expect(inside[1]).toBeGreaterThan(250); // G
        expect(inside[2]).toBeLessThan(5);      // B

        // Outside the scissor region (top-right) should remain black
        const outside = readPixel(gl, WIDTH - 5, HEIGHT - 5);
        expect(outside[0]).toBeLessThan(5); // R
        expect(outside[1]).toBeLessThan(5); // G — not green
        expect(outside[2]).toBeLessThan(5); // B

        quad.cleanup();
        gl.deleteProgram(prog.program);
    });

    // -----------------------------------------------------------------------
    // 7. Multiple quads at different positions — spatial correctness
    // -----------------------------------------------------------------------
    it('renders multiple quads at different positions and verifies spatial correctness', () => {
        const prog = compileProgram(gl, VERT_QUAD, FRAG_SOLID);
        const uColor = gl.getUniformLocation(prog.program, 'u_color')!;

        // Create 4 small quads, each in a different quadrant
        const quadSize = 0.4; // half-size in clip space
        const offsets = [
            { cx: -0.5, cy: -0.5, color: [1, 0, 0, 1] },   // bottom-left: red
            { cx: 0.5, cy: -0.5, color: [0, 1, 0, 1] },    // bottom-right: green
            { cx: -0.5, cy: 0.5, color: [0, 0, 1, 1] },    // top-left: blue
            { cx: 0.5, cy: 0.5, color: [1, 1, 0, 1] },     // top-right: yellow
        ];

        const quads = offsets.map((o) => createPositionedQuad(gl, prog.program, o.cx, o.cy, quadSize, quadSize));

        gl.viewport(0, 0, WIDTH, HEIGHT);
        gl.clearColor(0, 0, 0, 0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        gl.useProgram(prog.program);

        for (let i = 0; i < offsets.length; i++) {
            const o = offsets[i];
            gl.uniform4f(uColor, o.color[0], o.color[1], o.color[2], o.color[3]);
            gl.bindVertexArray(quads[i].vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
        }
        gl.bindVertexArray(null);

        // Map clip-space centers to pixel coordinates.
        // clip x=-0.5 → pixel x = (1 + (-0.5))/2 * WIDTH = 0.25 * 128 = 32
        // clip y=-0.5 → pixel y = (1 + (-0.5))/2 * HEIGHT = 32 (WebGL origin is bottom-left)
        const clipToPixel = (cx: number, cy: number): [number, number] => [
            Math.floor(((cx + 1) / 2) * WIDTH),
            Math.floor(((cy + 1) / 2) * HEIGHT),
        ];

        // Bottom-left quad should be red
        const [blX, blY] = clipToPixel(-0.5, -0.5);
        const blPixel = readPixel(gl, blX, blY);
        expect(blPixel[0]).toBeGreaterThan(250); // R
        expect(blPixel[1]).toBeLessThan(5);      // G

        // Bottom-right quad should be green
        const [brX, brY] = clipToPixel(0.5, -0.5);
        const brPixel = readPixel(gl, brX, brY);
        expect(brPixel[0]).toBeLessThan(5);      // R
        expect(brPixel[1]).toBeGreaterThan(250); // G

        // Top-left quad should be blue
        const [tlX, tlY] = clipToPixel(-0.5, 0.5);
        const tlPixel = readPixel(gl, tlX, tlY);
        expect(tlPixel[0]).toBeLessThan(5);      // R
        expect(tlPixel[2]).toBeGreaterThan(250); // B

        // Top-right quad should be yellow (R+G)
        const [trX, trY] = clipToPixel(0.5, 0.5);
        const trPixel = readPixel(gl, trX, trY);
        expect(trPixel[0]).toBeGreaterThan(250); // R
        expect(trPixel[1]).toBeGreaterThan(250); // G
        expect(trPixel[2]).toBeLessThan(5);      // B

        quads.forEach((q) => q.cleanup());
        gl.deleteProgram(prog.program);
    });

    // -----------------------------------------------------------------------
    // 8. Buffer factory integration — verify engine buffer API works with rendering
    // -----------------------------------------------------------------------
    it('uses engine BufferFactory for vertex data and renders correctly', () => {
        // Create vertex data via the engine's buffer factory
        const vertexData = new Float32Array([
            -1, -1,
             1, -1,
            -1,  1,
             1,  1,
        ]);
        const vbo = factory.createArrayBufferFromData(vertexData, gl.STATIC_DRAW);

        const prog = compileProgram(gl, VERT_QUAD, FRAG_SOLID);
        const aPos = gl.getAttribLocation(prog.program, 'a_position');
        const uColor = gl.getUniformLocation(prog.program, 'u_color')!;

        const vao = gl.createVertexArray()!;
        gl.bindVertexArray(vao);
        vbo.bind();
        gl.enableVertexAttribArray(aPos);
        gl.vertexAttribPointer(aPos, 2, gl.FLOAT, false, 0, 0);
        gl.bindVertexArray(null);

        gl.viewport(0, 0, WIDTH, HEIGHT);
        gl.clearColor(0, 0, 0, 0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        gl.useProgram(prog.program);
        gl.uniform4f(uColor, 0.0, 1.0, 1.0, 1.0); // Cyan

        gl.bindVertexArray(vao);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
        gl.bindVertexArray(null);

        const center = readPixel(gl, WIDTH / 2, HEIGHT / 2);
        expect(center[0]).toBeLessThan(5);       // R
        expect(center[1]).toBeGreaterThan(250);  // G
        expect(center[2]).toBeGreaterThan(250);  // B
        expect(center[3]).toBe(255);

        vbo.dispose();
        gl.deleteVertexArray(vao);
        gl.deleteProgram(prog.program);
    });
});
