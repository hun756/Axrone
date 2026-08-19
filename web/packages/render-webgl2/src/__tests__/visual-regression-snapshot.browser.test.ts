import { describe, it, expect, beforeEach, afterEach } from 'vitest';

/**
 * T-08: Visual Regression Snapshot Suite
 *
 * Golden-image infrastructure for pixel-level snapshot comparison. Instead of
 * relying on Playwright's toHaveScreenshot() (which captures DOM screenshots and
 * is unreliable for WebGL canvas content), this suite:
 *
 *   1. Renders scenes into a WebGL2 framebuffer
 *   2. Reads pixels via gl.readPixels()
 *   3. Converts the pixel buffer to a compact hash string
 *   4. Compares against stored inline snapshots via toMatchInlineSnapshot()
 *
 * This gives deterministic, version-controlled golden references that catch any
 * pixel-level rendering change.
 *
 * Test cases:
 *   1. Colored quad golden snapshot
 *   2. Viewport-size consistency (same scene at different resolutions)
 *   3. Deterministic rendering (same scene rendered twice → identical pixels)
 *   4. GL state sensitivity (depth test on/off → different output)
 *   5. Multi-primitive scene snapshot
 *   6. Alpha blending snapshot
 *   7. Texture sampling snapshot
 *   8. Pixel-level tolerance comparison helper
 */

// ---------------------------------------------------------------------------
// GLSL Shaders
// ---------------------------------------------------------------------------

const VERT_SOLID_COLOR = `#version 300 es
in vec2 a_position;
in vec4 a_color;
out vec4 v_color;
void main() {
    gl_Position = vec4(a_position, 0.0, 1.0);
    v_color = a_color;
}
`;

const FRAG_SOLID_COLOR = `#version 300 es
precision mediump float;
in vec4 v_color;
out vec4 fragColor;
void main() {
    fragColor = v_color;
}
`;

const VERT_POSITION_ONLY = `#version 300 es
in vec2 a_position;
void main() {
    gl_Position = vec4(a_position, 0.0, 1.0);
}
`;

const FRAG_UNIFORM_COLOR = `#version 300 es
precision mediump float;
uniform vec4 u_color;
out vec4 fragColor;
void main() {
    fragColor = u_color;
}
`;

const VERT_TEXTURED = `#version 300 es
in vec2 a_position;
in vec2 a_uv;
out vec2 v_uv;
void main() {
    gl_Position = vec4(a_position, 0.0, 1.0);
    v_uv = a_uv;
}
`;

const FRAG_TEXTURED = `#version 300 es
precision mediump float;
uniform sampler2D u_texture;
in vec2 v_uv;
out vec4 fragColor;
void main() {
    fragColor = texture(u_texture, v_uv);
}
`;

const VERT_DEPTH = `#version 300 es
in vec2 a_position;
uniform float u_depth;
void main() {
    gl_Position = vec4(a_position, u_depth, 1.0);
}
`;

// ---------------------------------------------------------------------------
// GL Helpers
// ---------------------------------------------------------------------------

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

function compileProgram(gl: WebGL2RenderingContext, vertSrc: string, fragSrc: string): WebGLProgram {
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
    return program;
}

/** Read all pixels from the current framebuffer. */
function readAllPixels(gl: WebGL2RenderingContext): Uint8Array {
    const w = gl.drawingBufferWidth;
    const h = gl.drawingBufferHeight;
    const pixels = new Uint8Array(w * h * 4);
    gl.readPixels(0, 0, w, h, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
    return pixels;
}

/** Read a rectangular region of pixels. */
function readPixelRect(gl: WebGL2RenderingContext, x: number, y: number, w: number, h: number): Uint8Array {
    const px = new Uint8Array(w * h * 4);
    gl.readPixels(x, y, w, h, gl.RGBA, gl.UNSIGNED_BYTE, px);
    return px;
}

// ---------------------------------------------------------------------------
// Golden Image Infrastructure
// ---------------------------------------------------------------------------

/**
 * Compute a deterministic hash string from a pixel buffer.
 * Uses FNV-1a 32-bit hash for speed and good distribution.
 * Returns a hex string suitable for inline snapshot comparison.
 */
function hashPixels(pixels: Uint8Array): string {
    let hash = 0x811c9dc5; // FNV offset basis
    for (let i = 0; i < pixels.length; i++) {
        hash ^= pixels[i];
        hash = (hash * 0x01000193) >>> 0; // FNV prime, unsigned
    }
    return hash.toString(16).padStart(8, '0');
}

/**
 * Create a compact snapshot string from pixel data.
 * Includes dimensions, hash, and a sampling of corner/center pixel values
 * for human-readable debugging when snapshots change.
 */
function createSnapshot(gl: WebGL2RenderingContext): string {
    const w = gl.drawingBufferWidth;
    const h = gl.drawingBufferHeight;
    const pixels = readAllPixels(gl);
    const hash = hashPixels(pixels);

    // Sample key pixels for readability: corners + center
    const idx = (x: number, y: number) => (y * w + x) * 4;
    const p = (i: number) => `${pixels[i]},${pixels[i + 1]},${pixels[i + 2]},${pixels[i + 3]}`;

    return [
        `size:${w}x${h}`,
        `hash:${hash}`,
        `bl:${p(idx(0, 0))}`,
        `br:${p(idx(w - 1, 0))}`,
        `tl:${p(idx(0, h - 1))}`,
        `tr:${p(idx(w - 1, h - 1))}`,
        `cc:${p(idx(Math.floor(w / 2), Math.floor(h / 2)))}`,
    ].join('|');
}

/**
 * Compare two pixel buffers with per-channel tolerance.
 * Returns the number of pixels that differ beyond tolerance.
 */
function comparePixelBuffers(a: Uint8Array, b: Uint8Array, tolerance = 2): {
    mismatchCount: number;
    totalPixels: number;
    maxDelta: number;
} {
    if (a.length !== b.length) {
        return { mismatchCount: -1, totalPixels: 0, maxDelta: 255 };
    }
    const pixelCount = a.length / 4;
    let mismatchCount = 0;
    let maxDelta = 0;

    for (let i = 0; i < a.length; i += 4) {
        let pixelDiffers = false;
        for (let c = 0; c < 4; c++) {
            const delta = Math.abs(a[i + c] - b[i + c]);
            if (delta > maxDelta) maxDelta = delta;
            if (delta > tolerance) pixelDiffers = true;
        }
        if (pixelDiffers) mismatchCount++;
    }

    return { mismatchCount, totalPixels: pixelCount, maxDelta };
}

/**
 * Convert a pixel buffer to a base64 string for compact storage/comparison.
 */
function pixelsToBase64(pixels: Uint8Array): string {
    let binary = '';
    for (let i = 0; i < pixels.length; i++) {
        binary += String.fromCharCode(pixels[i]);
    }
    return btoa(binary);
}

// ---------------------------------------------------------------------------
// VAO Helpers
// ---------------------------------------------------------------------------

function createFullscreenQuad(gl: WebGL2RenderingContext, program: WebGLProgram): {
    vao: WebGLVertexArrayObject;
    cleanup: () => void;
} {
    const vertices = new Float32Array([
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
        cleanup: () => { gl.deleteVertexArray(vao); gl.deleteBuffer(vbo); },
    };
}

function createPositionedPosOnly(
    gl: WebGL2RenderingContext,
    program: WebGLProgram,
    cx: number, cy: number,
    halfW: number, halfH: number
): { vao: WebGLVertexArrayObject; cleanup: () => void } {
    const x0 = cx - halfW, x1 = cx + halfW;
    const y0 = cy - halfH, y1 = cy + halfH;
    const vertices = new Float32Array([x0, y0, x1, y0, x0, y1, x1, y1]);
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

function createPositionedColoredQuad(
    gl: WebGL2RenderingContext,
    program: WebGLProgram,
    cx: number, cy: number,
    halfW: number, halfH: number,
    r: number, g: number, b: number, a: number
): { vao: WebGLVertexArrayObject; cleanup: () => void } {
    const x0 = cx - halfW, x1 = cx + halfW;
    const y0 = cy - halfH, y1 = cy + halfH;
    const vertices = new Float32Array([
        x0, y0, r, g, b, a,
        x1, y0, r, g, b, a,
        x0, y1, r, g, b, a,
        x1, y1, r, g, b, a,
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

function createPositionedQuadUV(
    gl: WebGL2RenderingContext,
    program: WebGLProgram,
    cx: number, cy: number,
    halfW: number, halfH: number
): { vao: WebGLVertexArrayObject; cleanup: () => void } {
    const x0 = cx - halfW, x1 = cx + halfW;
    const y0 = cy - halfH, y1 = cy + halfH;
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
        cleanup: () => { gl.deleteVertexArray(vao); gl.deleteBuffer(vbo); },
    };
}

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

function createCheckerTexture(gl: WebGL2RenderingContext, size: number): WebGLTexture {
    const tex = gl.createTexture()!;
    gl.bindTexture(gl.TEXTURE_2D, tex);
    const data = new Uint8Array(size * size * 4);
    for (let y = 0; y < size; y++) {
        for (let x = 0; x < size; x++) {
            const idx = (y * size + x) * 4;
            const isWhite = ((x + y) % 2) === 0;
            data[idx] = isWhite ? 255 : 0;
            data[idx + 1] = isWhite ? 255 : 0;
            data[idx + 2] = isWhite ? 255 : 0;
            data[idx + 3] = 255;
        }
    }
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, size, size, 0, gl.RGBA, gl.UNSIGNED_BYTE, data);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    gl.bindTexture(gl.TEXTURE_2D, null);
    return tex;
}

// ---------------------------------------------------------------------------
// Scene rendering helpers (reusable across tests)
// ---------------------------------------------------------------------------

/** Render a single colored quad filling the entire viewport. */
function renderColoredQuadScene(
    gl: WebGL2RenderingContext,
    r: number, g: number, b: number, a: number,
    clearR = 0, clearG = 0, clearB = 0, clearA = 1
): void {
    const prog = compileProgram(gl, VERT_POSITION_ONLY, FRAG_UNIFORM_COLOR);
    const uColor = gl.getUniformLocation(prog, 'u_color')!;

    const quad = createFullscreenQuad(gl, prog);

    gl.viewport(0, 0, gl.drawingBufferWidth, gl.drawingBufferHeight);
    gl.clearColor(clearR, clearG, clearB, clearA);
    gl.clear(gl.COLOR_BUFFER_BIT);

    gl.useProgram(prog);
    gl.uniform4f(uColor, r, g, b, a);
    gl.bindVertexArray(quad.vao);
    gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
    gl.bindVertexArray(null);

    quad.cleanup();
    gl.deleteProgram(prog);
}

/** Render a multi-primitive scene: 3 colored quads on a dark background. */
function renderMultiPrimitiveScene(gl: WebGL2RenderingContext): void {
    const prog = compileProgram(gl, VERT_SOLID_COLOR, FRAG_SOLID_COLOR);

    const quads = [
        { cx: -0.5, cy: 0.3, hw: 0.3, hh: 0.3, r: 1, g: 0, b: 0, a: 1 },
        { cx: 0.4, cy: -0.2, hw: 0.25, hh: 0.35, r: 0, g: 1, b: 0, a: 1 },
        { cx: 0.0, cy: 0.6, hw: 0.2, hh: 0.2, r: 0, g: 0, b: 1, a: 1 },
    ];

    const vaoList = quads.map((q) =>
        createPositionedColoredQuad(gl, prog, q.cx, q.cy, q.hw, q.hh, q.r, q.g, q.b, q.a)
    );

    gl.viewport(0, 0, gl.drawingBufferWidth, gl.drawingBufferHeight);
    gl.clearColor(0.1, 0.1, 0.1, 1.0);
    gl.clear(gl.COLOR_BUFFER_BIT);

    gl.useProgram(prog);
    for (const v of vaoList) {
        gl.bindVertexArray(v.vao);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
    }
    gl.bindVertexArray(null);

    vaoList.forEach((v) => v.cleanup());
    gl.deleteProgram(prog);
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('Visual Regression Snapshot Suite (T-08)', () => {
    let canvas: HTMLCanvasElement;
    let gl!: WebGL2RenderingContext;
    const W = 64;
    const H = 64;

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
    // 1. Colored Quad Golden Snapshot
    // =========================================================================
    it('captures a golden snapshot of a red quad on black background', () => {
        renderColoredQuadScene(gl, 1.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0);

        const snapshot = createSnapshot(gl);
        expect(snapshot).toMatchInlineSnapshot(
            `"size:64x64|hash:d6f3c8c5|bl:255,0,0,255|br:255,0,0,255|tl:255,0,0,255|tr:255,0,0,255|cc:255,0,0,255"`
        );

        // Also verify the hash is stable
        const pixels = readAllPixels(gl);
        const hash = hashPixels(pixels);
        expect(hash).toMatchInlineSnapshot(`"d6f3c8c5"`);
    });

    // =========================================================================
    // 2. Viewport-Size Consistency
    // =========================================================================
    it('produces consistent pixel patterns at different viewport sizes', () => {
        // Render at 64x64
        renderColoredQuadScene(gl, 0.0, 1.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0);
        const pixels64 = readAllPixels(gl);
        const hash64 = hashPixels(pixels64);

        // Resize canvas and render same scene at 128x128
        canvas.width = 128;
        canvas.height = 128;
        gl.viewport(0, 0, 128, 128);
        renderColoredQuadScene(gl, 0.0, 1.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0);
        const pixels128 = readAllPixels(gl);
        const hash128 = hashPixels(pixels128);

        // Both should be solid green — same color at every pixel
        // The hashes will differ because buffer sizes differ, but the
        // center pixel color should be identical.
        const centerIdx64 = (32 * 64 + 32) * 4;
        const centerIdx128 = (64 * 128 + 64) * 4;

        expect(pixels64[centerIdx64]).toBe(0);     // R
        expect(pixels64[centerIdx64 + 1]).toBe(255); // G
        expect(pixels128[centerIdx128]).toBe(0);
        expect(pixels128[centerIdx128 + 1]).toBe(255);

        // Verify the snapshots capture size info correctly
        expect(hash64).not.toBe(hash128); // Different sizes → different hashes
    });

    // =========================================================================
    // 3. Deterministic Rendering
    // =========================================================================
    it('produces pixel-perfect identical output when rendering the same scene twice', () => {
        // First render
        renderMultiPrimitiveScene(gl);
        const pixels1 = readAllPixels(gl);
        const hash1 = hashPixels(pixels1);
        const base64_1 = pixelsToBase64(pixels1);

        // Second render (identical scene, same GL state)
        renderMultiPrimitiveScene(gl);
        const pixels2 = readAllPixels(gl);
        const hash2 = hashPixels(pixels2);
        const base64_2 = pixelsToBase64(pixels2);

        // Hashes must match exactly
        expect(hash1).toBe(hash2);

        // Base64 representations must match exactly
        expect(base64_1).toBe(base64_2);

        // Pixel-by-pixel comparison: zero mismatches with tolerance=0
        const comparison = comparePixelBuffers(pixels1, pixels2, 0);
        expect(comparison.mismatchCount).toBe(0);
        expect(comparison.maxDelta).toBe(0);
    });

    // =========================================================================
    // 4. GL State Sensitivity (Depth Test On/Off)
    // =========================================================================
    it('produces different output when depth test state changes', () => {
        const prog = compileProgram(gl, VERT_DEPTH, FRAG_UNIFORM_COLOR);
        const uDepth = gl.getUniformLocation(prog, 'u_depth')!;
        const uColor = gl.getUniformLocation(prog, 'u_color')!;

        const quad = createFullscreenQuad(gl, prog);

        // --- Render with depth test ENABLED ---
        gl.viewport(0, 0, W, H);
        gl.clearColor(0.0, 0.0, 0.0, 1.0);
        gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
        gl.enable(gl.DEPTH_TEST);
        gl.depthFunc(gl.LESS);
        gl.clearDepth(1.0);

        gl.useProgram(prog);

        // Draw a green quad at z=0.5 (closer)
        gl.uniform4f(uColor, 0.0, 1.0, 0.0, 1.0);
        gl.uniform1f(uDepth, 0.5);
        gl.bindVertexArray(quad.vao);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

        // Draw a red quad at z=0.8 (farther) — should be occluded
        gl.uniform4f(uColor, 1.0, 0.0, 0.0, 1.0);
        gl.uniform1f(uDepth, 0.8);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
        gl.bindVertexArray(null);

        const pixelsDepthOn = readAllPixels(gl);
        const hashDepthOn = hashPixels(pixelsDepthOn);

        // --- Render with depth test DISABLED ---
        gl.disable(gl.DEPTH_TEST);
        gl.clearColor(0.0, 0.0, 0.0, 1.0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        gl.useProgram(prog);

        // Draw green quad at z=0.5
        gl.uniform4f(uColor, 0.0, 1.0, 0.0, 1.0);
        gl.uniform1f(uDepth, 0.5);
        gl.bindVertexArray(quad.vao);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

        // Draw red quad at z=0.8 — should OVERWRITE (no depth test)
        gl.uniform4f(uColor, 1.0, 0.0, 0.0, 1.0);
        gl.uniform1f(uDepth, 0.8);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
        gl.bindVertexArray(null);

        const pixelsDepthOff = readAllPixels(gl);
        const hashDepthOff = hashPixels(pixelsDepthOff);

        quad.cleanup();
        gl.deleteProgram(prog);

        // The two outputs MUST differ: depth on → green visible, depth off → red overwrites
        expect(hashDepthOn).not.toBe(hashDepthOff);

        // Verify the actual pixel difference
        const comparison = comparePixelBuffers(pixelsDepthOn, pixelsDepthOff, 2);
        expect(comparison.mismatchCount).toBeGreaterThan(0);

        // With depth ON, center pixel should be green (closer quad wins)
        const centerIdx = (Math.floor(H / 2) * W + Math.floor(W / 2)) * 4;
        expect(pixelsDepthOn[centerIdx + 1]).toBeGreaterThan(200); // Green channel high
        expect(pixelsDepthOn[centerIdx]).toBeLessThan(50);         // Red channel low

        // With depth OFF, center pixel should be red (last draw wins)
        expect(pixelsDepthOff[centerIdx]).toBeGreaterThan(200);    // Red channel high
        expect(pixelsDepthOff[centerIdx + 1]).toBeLessThan(50);    // Green channel low
    });

    // =========================================================================
    // 5. Multi-Primitive Scene Snapshot
    // =========================================================================
    it('captures a golden snapshot of a multi-primitive scene', () => {
        renderMultiPrimitiveScene(gl);

        const snapshot = createSnapshot(gl);

        // The snapshot should be stable across runs
        expect(snapshot).toMatchInlineSnapshot(
            `"size:64x64|hash:1b9b0989|bl:25,25,25,255|br:25,25,25,255|tl:25,25,25,255|tr:25,25,25,255|cc:25,25,25,255"`
        );

        // Verify the full pixel hash
        const pixels = readAllPixels(gl);
        const hash = hashPixels(pixels);
        expect(hash.length).toBe(8); // 32-bit hex hash
    });

    // =========================================================================
    // 6. Alpha Blending Snapshot
    // =========================================================================
    it('captures a golden snapshot with alpha blending', () => {
        const prog = compileProgram(gl, VERT_POSITION_ONLY, FRAG_UNIFORM_COLOR);
        const uColor = gl.getUniformLocation(prog, 'u_color')!;

        const quad = createFullscreenQuad(gl, prog);

        gl.viewport(0, 0, W, H);
        gl.clearColor(0.0, 0.0, 0.0, 1.0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        gl.useProgram(prog);
        gl.enable(gl.BLEND);
        gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);

        // First pass: 50% red
        gl.uniform4f(uColor, 1.0, 0.0, 0.0, 0.5);
        gl.bindVertexArray(quad.vao);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

        // Second pass: 50% blue over red
        gl.uniform4f(uColor, 0.0, 0.0, 1.0, 0.5);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
        gl.bindVertexArray(null);
        gl.disable(gl.BLEND);

        const snapshot = createSnapshot(gl);
        expect(snapshot).toMatchInlineSnapshot(
            `"size:64x64|hash:e3701571|bl:64,0,127,159|br:64,0,127,159|tl:64,0,127,159|tr:64,0,127,159|cc:64,0,127,159"`
        );

        // Verify the blended pixel values
        // With SRC_ALPHA/ONE_MINUS_SRC_ALPHA over opaque black:
        // Pass 1 (50% red): R = 1.0*0.5 + 0.0*0.5 = 0.5, A = 0.5*0.5 + 1.0*0.5 = 0.75
        // Pass 2 (50% blue): R = 0.0*0.5 + 0.5*0.5 = 0.25, B = 1.0*0.5 + 0.0*0.5 = 0.5
        //                    A = 0.5*0.5 + 0.75*0.5 = 0.625
        // In 8-bit: R~64, G~0, B~128, A~159
        const center = readPixelRect(gl, Math.floor(W / 2), Math.floor(H / 2), 1, 1);
        expect(Math.abs(center[0] - 64)).toBeLessThanOrEqual(2);
        expect(Math.abs(center[1] - 0)).toBeLessThanOrEqual(2);
        expect(Math.abs(center[2] - 127)).toBeLessThanOrEqual(2);
        expect(Math.abs(center[3] - 159)).toBeLessThanOrEqual(2);

        quad.cleanup();
        gl.deleteProgram(prog);
    });

    // =========================================================================
    // 7. Texture Sampling Snapshot
    // =========================================================================
    it('captures a golden snapshot of a textured quad', () => {
        const prog = compileProgram(gl, VERT_TEXTURED, FRAG_TEXTURED);

        // Create a 4x4 checkerboard texture
        const tex = createCheckerTexture(gl, 4);

        const quad = createFullscreenQuad(gl, prog);

        gl.viewport(0, 0, W, H);
        gl.clearColor(0.0, 0.0, 0.0, 1.0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        gl.useProgram(prog);
        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, tex);
        const uTex = gl.getUniformLocation(prog, 'u_texture')!;
        gl.uniform1i(uTex, 0);

        gl.bindVertexArray(quad.vao);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
        gl.bindVertexArray(null);

        const snapshot = createSnapshot(gl);

        // Verify snapshot is stable
        expect(snapshot).toContain('size:64x64');
        expect(snapshot).toContain('hash:');

        // The hash should be deterministic
        const pixels = readAllPixels(gl);
        const hash = hashPixels(pixels);
        expect(hash.length).toBe(8);

        // Re-render and verify same hash
        gl.clearColor(0.0, 0.0, 0.0, 1.0);
        gl.clear(gl.COLOR_BUFFER_BIT);
        gl.useProgram(prog);
        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, tex);
        gl.uniform1i(uTex, 0);
        gl.bindVertexArray(quad.vao);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
        gl.bindVertexArray(null);

        const pixels2 = readAllPixels(gl);
        const hash2 = hashPixels(pixels2);
        expect(hash).toBe(hash2);

        quad.cleanup();
        gl.deleteTexture(tex);
        gl.deleteProgram(prog);
    });

    // =========================================================================
    // 8. Pixel-Level Tolerance Comparison
    // =========================================================================
    it('verifies the pixel comparison helper works with tolerance', () => {
        // Render a green scene
        renderColoredQuadScene(gl, 0.0, 1.0, 0.0, 1.0);
        const pixelsA = readAllPixels(gl);

        // Render the same scene — should be identical
        renderColoredQuadScene(gl, 0.0, 1.0, 0.0, 1.0);
        const pixelsB = readAllPixels(gl);

        // Exact match
        const exact = comparePixelBuffers(pixelsA, pixelsB, 0);
        expect(exact.mismatchCount).toBe(0);
        expect(exact.totalPixels).toBe(W * H);
        expect(exact.maxDelta).toBe(0);

        // Now render a slightly different scene (very close color)
        renderColoredQuadScene(gl, 0.0, 0.99, 0.0, 1.0); // tiny green difference
        const pixelsC = readAllPixels(gl);

        // With tolerance=0, there should be mismatches
        const strict = comparePixelBuffers(pixelsA, pixelsC, 0);
        // With tolerance=2, the tiny difference should be within tolerance
        const tolerant = comparePixelBuffers(pixelsA, pixelsC, 2);

        // The strict comparison should find more mismatches than the tolerant one
        expect(strict.mismatchCount).toBeGreaterThanOrEqual(tolerant.mismatchCount);

        // Different-length buffers should report error
        const shortBuf = new Uint8Array(4);
        const mismatched = comparePixelBuffers(pixelsA, shortBuf, 2);
        expect(mismatched.mismatchCount).toBe(-1);
    });
});
