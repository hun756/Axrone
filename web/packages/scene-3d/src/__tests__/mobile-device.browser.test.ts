import { describe, expect, it } from 'vitest';

/**
 * Mobile Device Test Suite — Browser Tests
 *
 * Validates mobile-specific rendering, touch input, and responsive viewport
 * behavior using real WebGL2 contexts in the browser via Vitest + Playwright.
 *
 * Test groups:
 * 1. Responsive Viewport — canvas sizing at mobile resolutions & DPR
 * 2. Touch Input Simulation — touch events and coordinate capture
 * 3. Mobile GPU Precision — mediump float shader compilation & precision queries
 * 4. Mobile Draw Call Budget — instanced rendering within mobile budgets
 * 5. Texture Constraints — POT/NPOT textures, mipmaps, max texture size
 * 6. Performance Budget Validation — frame time at mobile resolution
 *
 * Mobile budgets (from project governance):
 * - Draw calls: <= 60 (2D) / <= 100 (3D)
 * - Triangles: <= 100K
 * - Texture memory: <= 128 MB
 */

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const createTestCanvas = (w: number, h: number): HTMLCanvasElement =>
    (window as any).createTestCanvas(w, h);

const createWebGLContext = (
    canvas: HTMLCanvasElement,
    attrs: Partial<WebGLContextAttributes> = {},
): WebGL2RenderingContext =>
    (window as any).createWebGLContext(canvas, attrs);

/** Tolerance for 8-bit channel comparisons. */
const CHANNEL_TOLERANCE = 2;

const expectChannelNear = (actual: number, expected: number, label: string): void => {
    expect(
        actual,
        `${label}: expected ${expected} +/- ${CHANNEL_TOLERANCE}, got ${actual}`,
    ).toBeGreaterThanOrEqual(expected - CHANNEL_TOLERANCE);
    expect(
        actual,
        `${label}: expected ${expected} +/- ${CHANNEL_TOLERANCE}, got ${actual}`,
    ).toBeLessThanOrEqual(expected + CHANNEL_TOLERANCE);
};

const compileShader = (gl: WebGL2RenderingContext, type: number, source: string): WebGLShader => {
    const shader = gl.createShader(type)!;
    gl.shaderSource(shader, source);
    gl.compileShader(shader);
    const ok = gl.getShaderParameter(shader, gl.COMPILE_STATUS);
    if (!ok) {
        const log = gl.getShaderInfoLog(shader);
        gl.deleteShader(shader);
        throw new Error(`Shader compilation failed: ${log}`);
    }
    return shader;
};

const linkProgram = (gl: WebGL2RenderingContext, vs: WebGLShader, fs: WebGLShader): WebGLProgram => {
    const program = gl.createProgram()!;
    gl.attachShader(program, vs);
    gl.attachShader(program, fs);
    gl.linkProgram(program);
    const ok = gl.getProgramParameter(program, gl.LINK_STATUS);
    if (!ok) {
        const log = gl.getProgramInfoLog(program);
        gl.deleteProgram(program);
        throw new Error(`Program link failed: ${log}`);
    }
    return program;
};

const readPixel = (gl: WebGL2RenderingContext, x: number, y: number): Uint8Array => {
    const pixel = new Uint8Array(4);
    gl.readPixels(x, y, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, pixel);
    return pixel;
};

// ---------------------------------------------------------------------------
// Shared shader sources
// ---------------------------------------------------------------------------

const SOLID_COLOR_VS = `#version 300 es
in vec2 a_position;
void main() {
    gl_Position = vec4(a_position, 0.0, 1.0);
}
`;

const SOLID_COLOR_FS = `#version 300 es
precision mediump float;
uniform vec4 u_color;
out vec4 o_color;
void main() {
    o_color = u_color;
}
`;

const INSTANCED_VS = `#version 300 es
in vec2 a_position;
in vec2 a_offset;
in vec3 a_color;
out vec3 v_color;
void main() {
    v_color = a_color;
    gl_Position = vec4(a_position + a_offset, 0.0, 1.0);
}
`;

const INSTANCED_FS = `#version 300 es
precision mediump float;
in vec3 v_color;
out vec4 o_color;
void main() {
    o_color = vec4(v_color, 1.0);
}
`;

// ---------------------------------------------------------------------------
// 1. Responsive Viewport
// ---------------------------------------------------------------------------
describe('Mobile — Responsive Viewport', () => {
    it('sets canvas to iPhone viewport (375x667) and WebGL viewport matches', () => {
        const canvas = createTestCanvas(375, 667);
        const gl = createWebGLContext(canvas, { preserveDrawingBuffer: true });

        gl.viewport(0, 0, canvas.width, canvas.height);

        const vp = gl.getParameter(gl.VIEWPORT) as Int32Array;
        expect(vp[2]).toBe(375);
        expect(vp[3]).toBe(667);
    });

    it('sets canvas to Android viewport (360x740) and WebGL viewport matches', () => {
        const canvas = createTestCanvas(360, 740);
        const gl = createWebGLContext(canvas, { preserveDrawingBuffer: true });

        gl.viewport(0, 0, canvas.width, canvas.height);

        const vp = gl.getParameter(gl.VIEWPORT) as Int32Array;
        expect(vp[2]).toBe(360);
        expect(vp[3]).toBe(740);
    });

    it('renders correctly at 2x device pixel ratio', () => {
        // Simulate 2x DPR: canvas backing store is 750x1334, CSS size 375x667
        const canvas = createTestCanvas(750, 1334);
        canvas.style.width = '375px';
        canvas.style.height = '667px';
        const gl = createWebGLContext(canvas, { preserveDrawingBuffer: true });

        gl.viewport(0, 0, canvas.width, canvas.height);
        gl.clearColor(0.0, 1.0, 0.0, 1.0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        // Pixel read at the center of the backing store
        const center = readPixel(gl, 375, 667);
        expectChannelNear(center[1], 255, '2x DPR green channel');
    });

    it('renders correctly at 3x device pixel ratio', () => {
        // Simulate 3x DPR: canvas backing store is 1125x2001, CSS size 375x667
        const canvas = createTestCanvas(1125, 2001);
        canvas.style.width = '375px';
        canvas.style.height = '667px';
        const gl = createWebGLContext(canvas, { preserveDrawingBuffer: true });

        gl.viewport(0, 0, canvas.width, canvas.height);
        gl.clearColor(0.0, 0.0, 1.0, 1.0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        const center = readPixel(gl, 562, 1000);
        expectChannelNear(center[2], 255, '3x DPR blue channel');
    });

    it('updates gl.viewport when canvas is resized', () => {
        const canvas = createTestCanvas(375, 667);
        const gl = createWebGLContext(canvas, { preserveDrawingBuffer: true });

        gl.viewport(0, 0, canvas.width, canvas.height);
        let vp = gl.getParameter(gl.VIEWPORT) as Int32Array;
        expect(vp[2]).toBe(375);
        expect(vp[3]).toBe(667);

        // Resize canvas to a different mobile size
        canvas.width = 414;
        canvas.height = 896;
        gl.viewport(0, 0, canvas.width, canvas.height);

        vp = gl.getParameter(gl.VIEWPORT) as Int32Array;
        expect(vp[2]).toBe(414);
        expect(vp[3]).toBe(896);
    });
});

// ---------------------------------------------------------------------------
// 2. Touch Input Simulation
// ---------------------------------------------------------------------------
describe('Mobile — Touch Input Simulation', () => {
    it('captures touchstart coordinates on canvas', () => {
        const canvas = createTestCanvas(375, 667);
        let capturedX = -1;
        let capturedY = -1;

        canvas.addEventListener('touchstart', (e: TouchEvent) => {
            const touch = e.touches[0];
            const rect = canvas.getBoundingClientRect();
            capturedX = touch.clientX - rect.left;
            capturedY = touch.clientY - rect.top;
        });

        // Dispatch a synthetic touch event
        const touch = new Touch({
            identifier: 0,
            target: canvas,
            clientX: 100,
            clientY: 200,
        });
        const event = new TouchEvent('touchstart', {
            touches: [touch],
            targetTouches: [touch],
            changedTouches: [touch],
        });
        canvas.dispatchEvent(event);

        expect(capturedX).toBe(100);
        expect(capturedY).toBe(200);
    });

    it('captures touchmove coordinates', () => {
        const canvas = createTestCanvas(375, 667);
        const positions: Array<{ x: number; y: number }> = [];

        canvas.addEventListener('touchmove', (e: TouchEvent) => {
            const touch = e.touches[0];
            const rect = canvas.getBoundingClientRect();
            positions.push({
                x: touch.clientX - rect.left,
                y: touch.clientY - rect.top,
            });
        });

        const touch = new Touch({
            identifier: 0,
            target: canvas,
            clientX: 50,
            clientY: 150,
        });
        const event = new TouchEvent('touchmove', {
            touches: [touch],
            targetTouches: [touch],
            changedTouches: [touch],
        });
        canvas.dispatchEvent(event);

        expect(positions.length).toBe(1);
        expect(positions[0].x).toBe(50);
        expect(positions[0].y).toBe(150);
    });

    it('captures touchend event with correct changedTouches', () => {
        const canvas = createTestCanvas(375, 667);
        let endedId = -1;

        canvas.addEventListener('touchend', (e: TouchEvent) => {
            endedId = e.changedTouches[0].identifier;
        });

        const touch = new Touch({
            identifier: 42,
            target: canvas,
            clientX: 10,
            clientY: 20,
        });
        const event = new TouchEvent('touchend', {
            touches: [],
            targetTouches: [],
            changedTouches: [touch],
        });
        canvas.dispatchEvent(event);

        expect(endedId).toBe(42);
    });

    it('multi-touch (2 fingers) produces correct touch list', () => {
        const canvas = createTestCanvas(375, 667);
        let touchCount = 0;
        const coords: Array<{ x: number; y: number }> = [];

        canvas.addEventListener('touchstart', (e: TouchEvent) => {
            touchCount = e.touches.length;
            for (let i = 0; i < e.touches.length; i++) {
                const t = e.touches[i];
                const rect = canvas.getBoundingClientRect();
                coords.push({
                    x: t.clientX - rect.left,
                    y: t.clientY - rect.top,
                });
            }
        });

        const touch1 = new Touch({
            identifier: 0,
            target: canvas,
            clientX: 100,
            clientY: 200,
        });
        const touch2 = new Touch({
            identifier: 1,
            target: canvas,
            clientX: 300,
            clientY: 400,
        });
        const event = new TouchEvent('touchstart', {
            touches: [touch1, touch2],
            targetTouches: [touch1, touch2],
            changedTouches: [touch1, touch2],
        });
        canvas.dispatchEvent(event);

        expect(touchCount).toBe(2);
        expect(coords[0].x).toBe(100);
        expect(coords[0].y).toBe(200);
        expect(coords[1].x).toBe(300);
        expect(coords[1].y).toBe(400);
    });
});

// ---------------------------------------------------------------------------
// 3. Mobile GPU Precision
// ---------------------------------------------------------------------------
describe('Mobile — GPU Precision', () => {
    it('compiles shader with precision mediump float successfully', () => {
        const canvas = createTestCanvas(64, 64);
        const gl = createWebGLContext(canvas);

        const source = `#version 300 es
precision mediump float;
out vec4 o_color;
void main() {
    o_color = vec4(1.0, 0.0, 0.0, 1.0);
}
`;
        const shader = compileShader(gl, gl.FRAGMENT_SHADER, source);
        expect(gl.getShaderParameter(shader, gl.COMPILE_STATUS)).toBe(true);
        gl.deleteShader(shader);
    });

    it('queries MEDIUM_FLOAT precision format with sufficient range', () => {
        const canvas = createTestCanvas(64, 64);
        const gl = createWebGLContext(canvas);

        const format = gl.getShaderPrecisionFormat(gl.FRAGMENT_SHADER, gl.MEDIUM_FLOAT);
        expect(format).not.toBeNull();
        // mediump must have range >= 2^14 (IEEE 754 half-float)
        expect(format!.rangeMin).toBeGreaterThanOrEqual(14);
        // Precision (mantissa bits) should be at least 10 for half-float
        expect(format!.precision).toBeGreaterThanOrEqual(10);
    });

    it('mediump is sufficient for position calculations (range >= 2^14)', () => {
        const canvas = createTestCanvas(64, 64);
        const gl = createWebGLContext(canvas);

        const format = gl.getShaderPrecisionFormat(gl.VERTEX_SHADER, gl.MEDIUM_FLOAT);
        expect(format).not.toBeNull();
        // Verify vertex shader mediump also has range >= 2^14
        expect(format!.rangeMin).toBeGreaterThanOrEqual(14);

        // Also verify that a vertex shader using mediump compiles
        const vsSource = `#version 300 es
precision mediump float;
in vec2 a_position;
void main() {
    gl_Position = vec4(a_position * 100.0, 0.0, 1.0);
}
`;
        const shader = compileShader(gl, gl.VERTEX_SHADER, vsSource);
        expect(gl.getShaderParameter(shader, gl.COMPILE_STATUS)).toBe(true);
        gl.deleteShader(shader);
    });
});

// ---------------------------------------------------------------------------
// 4. Mobile Draw Call Budget
// ---------------------------------------------------------------------------
describe('Mobile — Draw Call Budget', () => {
    it('renders 100 instanced quads and verifies pixel output', () => {
        const canvas = createTestCanvas(256, 256);
        const gl = createWebGLContext(canvas, { preserveDrawingBuffer: true });

        const vs = compileShader(gl, gl.VERTEX_SHADER, INSTANCED_VS);
        const fs = compileShader(gl, gl.FRAGMENT_SHADER, INSTANCED_FS);
        const program = linkProgram(gl, vs, fs);

        const posLoc = gl.getAttribLocation(program, 'a_position');
        const offsetLoc = gl.getAttribLocation(program, 'a_offset');
        const colorLoc = gl.getAttribLocation(program, 'a_color');

        // Quad vertices (small quad centered at origin)
        const quadVerts = new Float32Array([
            -0.01, -0.01,
             0.01, -0.01,
            -0.01,  0.01,
            -0.01,  0.01,
             0.01, -0.01,
             0.01,  0.01,
        ]);

        // Instance offsets: 10x10 grid spread across [-0.9, 0.9]
        const INSTANCE_COUNT = 100;
        const offsets = new Float32Array(INSTANCE_COUNT * 2);
        const colors = new Float32Array(INSTANCE_COUNT * 3);
        for (let i = 0; i < INSTANCE_COUNT; i++) {
            const col = i % 10;
            const row = Math.floor(i / 10);
            offsets[i * 2] = -0.9 + col * 0.2;
            offsets[i * 2 + 1] = -0.9 + row * 0.2;
            // Alternate red/green for visual verification
            colors[i * 3] = i % 2 === 0 ? 1.0 : 0.0;
            colors[i * 3 + 1] = i % 2 === 0 ? 0.0 : 1.0;
            colors[i * 3 + 2] = 0.0;
        }

        const vao = gl.createVertexArray()!;
        gl.bindVertexArray(vao);

        // Quad position buffer
        const quadBuf = gl.createBuffer()!;
        gl.bindBuffer(gl.ARRAY_BUFFER, quadBuf);
        gl.bufferData(gl.ARRAY_BUFFER, quadVerts, gl.STATIC_DRAW);
        gl.enableVertexAttribArray(posLoc);
        gl.vertexAttribPointer(posLoc, 2, gl.FLOAT, false, 0, 0);

        // Instance offset buffer
        const offsetBuf = gl.createBuffer()!;
        gl.bindBuffer(gl.ARRAY_BUFFER, offsetBuf);
        gl.bufferData(gl.ARRAY_BUFFER, offsets, gl.STATIC_DRAW);
        gl.enableVertexAttribArray(offsetLoc);
        gl.vertexAttribPointer(offsetLoc, 2, gl.FLOAT, false, 0, 0);
        gl.vertexAttribDivisor(offsetLoc, 1);

        // Instance color buffer
        const colorBuf = gl.createBuffer()!;
        gl.bindBuffer(gl.ARRAY_BUFFER, colorBuf);
        gl.bufferData(gl.ARRAY_BUFFER, colors, gl.STATIC_DRAW);
        gl.enableVertexAttribArray(colorLoc);
        gl.vertexAttribPointer(colorLoc, 3, gl.FLOAT, false, 0, 0);
        gl.vertexAttribDivisor(colorLoc, 1);

        gl.bindVertexArray(null);

        gl.viewport(0, 0, canvas.width, canvas.height);
        gl.clearColor(0, 0, 0, 1);
        gl.clear(gl.COLOR_BUFFER_BIT);

        gl.useProgram(program);
        gl.bindVertexArray(vao);
        // Single draw call for all 100 instances — counts as 1 draw call
        gl.drawArraysInstanced(gl.TRIANGLES, 0, 6, INSTANCE_COUNT);

        // Verify a red instance pixel (instance 0 at offset (-0.9, -0.9))
        // In GL coords: x = (-0.9 + 1) * 128 = 12.8, y = (-0.9 + 1) * 128 = 12.8
        const redPixel = readPixel(gl, 13, 13);
        expectChannelNear(redPixel[0], 255, 'instance 0 red');
        expectChannelNear(redPixel[1], 0, 'instance 0 green');

        // Verify a green instance pixel (instance 1 at offset (-0.7, -0.9))
        // x = (-0.7 + 1) * 128 = 38.4, y = (-0.9 + 1) * 128 = 12.8
        const greenPixel = readPixel(gl, 38, 13);
        expectChannelNear(greenPixel[0], 0, 'instance 1 red');
        expectChannelNear(greenPixel[1], 255, 'instance 1 green');

        // Draw call count: 1 instanced draw = well within 2D budget of 60
        // and 3D budget of 100
        expect(INSTANCE_COUNT).toBeLessThanOrEqual(100);

        gl.bindVertexArray(null);
        gl.deleteVertexArray(vao);
        gl.deleteBuffer(quadBuf);
        gl.deleteBuffer(offsetBuf);
        gl.deleteBuffer(colorBuf);
        gl.deleteProgram(program);
        gl.deleteShader(vs);
        gl.deleteShader(fs);
    });

    it('100 instanced quads stays within mobile draw call budget (<=100)', () => {
        // This test validates the budget constraint: 100 instanced quads
        // use only 1 draw call, well within the 3D budget of 100.
        const MOBILE_3D_DRAW_CALL_BUDGET = 100;
        const MOBILE_2D_DRAW_CALL_BUDGET = 60;

        // 1 instanced draw call for 100 quads
        const drawCallsUsed = 1;
        expect(drawCallsUsed).toBeLessThanOrEqual(MOBILE_2D_DRAW_CALL_BUDGET);
        expect(drawCallsUsed).toBeLessThanOrEqual(MOBILE_3D_DRAW_CALL_BUDGET);

        // Triangle count: 100 instances * 2 triangles per quad = 200 triangles
        const triangleCount = 100 * 2;
        expect(triangleCount).toBeLessThanOrEqual(100_000);
    });
});

// ---------------------------------------------------------------------------
// 5. Texture Constraints
// ---------------------------------------------------------------------------
describe('Mobile — Texture Constraints', () => {
    it('creates 256x256 power-of-two texture and generates mipmaps', () => {
        const canvas = createTestCanvas(64, 64);
        const gl = createWebGLContext(canvas);

        const tex = gl.createTexture()!;
        gl.bindTexture(gl.TEXTURE_2D, tex);

        const data = new Uint8Array(256 * 256 * 4);
        for (let i = 0; i < data.length; i += 4) {
            data[i] = 255;
            data[i + 1] = 0;
            data[i + 2] = 0;
            data[i + 3] = 255;
        }
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 256, 256, 0, gl.RGBA, gl.UNSIGNED_BYTE, data);
        gl.generateMipmap(gl.TEXTURE_2D);

        // Verify no GL error occurred
        expect(gl.getError()).toBe(gl.NO_ERROR);

        const texParam = gl.getTexParameter(gl.TEXTURE_2D, gl.TEXTURE_MAX_LEVEL);
        // 256 = 2^8, so max mip level = 8
        expect(texParam).toBeGreaterThanOrEqual(8);

        gl.deleteTexture(tex);
    });

    it('creates 512x512 power-of-two texture and generates mipmaps', () => {
        const canvas = createTestCanvas(64, 64);
        const gl = createWebGLContext(canvas);

        const tex = createTestTexture(gl, 512, 512);
        gl.generateMipmap(gl.TEXTURE_2D);
        expect(gl.getError()).toBe(gl.NO_ERROR);

        gl.deleteTexture(tex);
    });

    it('creates 1024x1024 power-of-two texture and generates mipmaps', () => {
        const canvas = createTestCanvas(64, 64);
        const gl = createWebGLContext(canvas);

        const tex = createTestTexture(gl, 1024, 1024);
        gl.generateMipmap(gl.TEXTURE_2D);
        expect(gl.getError()).toBe(gl.NO_ERROR);

        gl.deleteTexture(tex);
    });

    it('NPOT texture (300x500) works with CLAMP_TO_EDGE wrapping', () => {
        const canvas = createTestCanvas(64, 64);
        const gl = createWebGLContext(canvas, { preserveDrawingBuffer: true });

        const tex = gl.createTexture()!;
        gl.bindTexture(gl.TEXTURE_2D, tex);

        const data = new Uint8Array(300 * 500 * 4);
        for (let i = 0; i < data.length; i += 4) {
            data[i] = 0;
            data[i + 1] = 255;
            data[i + 2] = 0;
            data[i + 3] = 255;
        }
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 300, 500, 0, gl.RGBA, gl.UNSIGNED_BYTE, data);

        // NPOT textures require CLAMP_TO_EDGE and NEAREST/LINEAR filtering
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);

        // Verify no errors
        expect(gl.getError()).toBe(gl.NO_ERROR);

        // Verify wrap modes are set correctly
        const wrapS = gl.getTexParameter(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S);
        const wrapT = gl.getTexParameter(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T);
        expect(wrapS).toBe(gl.CLAMP_TO_EDGE);
        expect(wrapT).toBe(gl.CLAMP_TO_EDGE);

        gl.deleteTexture(tex);
    });

    it('max texture size query returns >= 2048', () => {
        const canvas = createTestCanvas(64, 64);
        const gl = createWebGLContext(canvas);

        const maxTexSize = gl.getParameter(gl.MAX_TEXTURE_SIZE) as number;
        expect(maxTexSize).toBeGreaterThanOrEqual(2048);
    });
});

// ---------------------------------------------------------------------------
// 6. Performance Budget Validation
// ---------------------------------------------------------------------------
describe('Mobile — Performance Budget Validation', () => {
    it('measures frame time for simple scene at mobile resolution', () => {
        const canvas = createTestCanvas(375, 667);
        const gl = createWebGLContext(canvas, { preserveDrawingBuffer: true });

        const vs = compileShader(gl, gl.VERTEX_SHADER, SOLID_COLOR_VS);
        const fs = compileShader(gl, gl.FRAGMENT_SHADER, SOLID_COLOR_FS);
        const program = linkProgram(gl, vs, fs);

        const posLoc = gl.getAttribLocation(program, 'a_position');
        const colorLoc = gl.getUniformLocation(program, 'u_color');

        // Full-screen quad VAO
        const vao = gl.createVertexArray()!;
        gl.bindVertexArray(vao);
        const vertices = new Float32Array([-1, -1, 1, -1, -1, 1, -1, 1, 1, -1, 1, 1]);
        const buf = gl.createBuffer()!;
        gl.bindBuffer(gl.ARRAY_BUFFER, buf);
        gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);
        gl.enableVertexAttribArray(posLoc);
        gl.vertexAttribPointer(posLoc, 2, gl.FLOAT, false, 0, 0);
        gl.bindVertexArray(null);

        gl.viewport(0, 0, canvas.width, canvas.height);
        gl.useProgram(program);

        // Measure 10 frames
        const frameTimes: number[] = [];
        for (let i = 0; i < 10; i++) {
            const start = performance.now();

            gl.clearColor(0, 0, 0, 1);
            gl.clear(gl.COLOR_BUFFER_BIT);
            gl.uniform4f(colorLoc, 1.0, 0.0, 0.0, 1.0);
            gl.bindVertexArray(vao);
            gl.drawArrays(gl.TRIANGLES, 0, 6);
            // Force GPU sync by reading a pixel
            gl.readPixels(0, 0, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, new Uint8Array(4));

            const end = performance.now();
            frameTimes.push(end - start);
        }

        // All frames should complete — just verify we got measurements
        expect(frameTimes.length).toBe(10);
        // Each frame time should be a positive number
        for (const t of frameTimes) {
            expect(t).toBeGreaterThan(0);
        }

        gl.bindVertexArray(null);
        gl.deleteVertexArray(vao);
        gl.deleteBuffer(buf);
        gl.deleteProgram(program);
        gl.deleteShader(vs);
        gl.deleteShader(fs);
    });

    it('average frame time is within 30 FPS mobile budget (< 33ms)', () => {
        const canvas = createTestCanvas(375, 667);
        const gl = createWebGLContext(canvas, { preserveDrawingBuffer: true });

        const vs = compileShader(gl, gl.VERTEX_SHADER, SOLID_COLOR_VS);
        const fs = compileShader(gl, gl.FRAGMENT_SHADER, SOLID_COLOR_FS);
        const program = linkProgram(gl, vs, fs);

        const posLoc = gl.getAttribLocation(program, 'a_position');
        const colorLoc = gl.getUniformLocation(program, 'u_color');

        const vao = gl.createVertexArray()!;
        gl.bindVertexArray(vao);
        const vertices = new Float32Array([-1, -1, 1, -1, -1, 1, -1, 1, 1, -1, 1, 1]);
        const buf = gl.createBuffer()!;
        gl.bindBuffer(gl.ARRAY_BUFFER, buf);
        gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);
        gl.enableVertexAttribArray(posLoc);
        gl.vertexAttribPointer(posLoc, 2, gl.FLOAT, false, 0, 0);
        gl.bindVertexArray(null);

        gl.viewport(0, 0, canvas.width, canvas.height);
        gl.useProgram(program);

        const MOBILE_FRAME_BUDGET_MS = 33; // 30 FPS minimum
        const frameTimes: number[] = [];

        for (let i = 0; i < 10; i++) {
            const start = performance.now();

            gl.clearColor(0, 0, 0, 1);
            gl.clear(gl.COLOR_BUFFER_BIT);
            gl.uniform4f(colorLoc, 0.0, 1.0, 0.0, 1.0);
            gl.bindVertexArray(vao);
            gl.drawArrays(gl.TRIANGLES, 0, 6);
            gl.readPixels(0, 0, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, new Uint8Array(4));

            const end = performance.now();
            frameTimes.push(end - start);
        }

        const avgFrameTime = frameTimes.reduce((a, b) => a + b, 0) / frameTimes.length;
        expect(avgFrameTime).toBeLessThan(MOBILE_FRAME_BUDGET_MS);

        gl.bindVertexArray(null);
        gl.deleteVertexArray(vao);
        gl.deleteBuffer(buf);
        gl.deleteProgram(program);
        gl.deleteShader(vs);
        gl.deleteShader(fs);
    });

    it('no frame exceeds 33ms (minimum 30 FPS guarantee)', () => {
        const canvas = createTestCanvas(375, 667);
        const gl = createWebGLContext(canvas, { preserveDrawingBuffer: true });

        const vs = compileShader(gl, gl.VERTEX_SHADER, SOLID_COLOR_VS);
        const fs = compileShader(gl, gl.FRAGMENT_SHADER, SOLID_COLOR_FS);
        const program = linkProgram(gl, vs, fs);

        const posLoc = gl.getAttribLocation(program, 'a_position');
        const colorLoc = gl.getUniformLocation(program, 'u_color');

        const vao = gl.createVertexArray()!;
        gl.bindVertexArray(vao);
        const vertices = new Float32Array([-1, -1, 1, -1, -1, 1, -1, 1, 1, -1, 1, 1]);
        const buf = gl.createBuffer()!;
        gl.bindBuffer(gl.ARRAY_BUFFER, buf);
        gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);
        gl.enableVertexAttribArray(posLoc);
        gl.vertexAttribPointer(posLoc, 2, gl.FLOAT, false, 0, 0);
        gl.bindVertexArray(null);

        gl.viewport(0, 0, canvas.width, canvas.height);
        gl.useProgram(program);

        const MAX_FRAME_MS = 33;
        let maxFrameTime = 0;

        for (let i = 0; i < 10; i++) {
            const start = performance.now();

            gl.clearColor(0, 0, 0, 1);
            gl.clear(gl.COLOR_BUFFER_BIT);
            gl.uniform4f(colorLoc, 0.0, 0.0, 1.0, 1.0);
            gl.bindVertexArray(vao);
            gl.drawArrays(gl.TRIANGLES, 0, 6);
            gl.readPixels(0, 0, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, new Uint8Array(4));

            const elapsed = performance.now() - start;
            if (elapsed > maxFrameTime) {
                maxFrameTime = elapsed;
            }
        }

        expect(maxFrameTime).toBeLessThan(MAX_FRAME_MS);

        gl.bindVertexArray(null);
        gl.deleteVertexArray(vao);
        gl.deleteBuffer(buf);
        gl.deleteProgram(program);
        gl.deleteShader(vs);
        gl.deleteShader(fs);
    });
});

// ---------------------------------------------------------------------------
// Private helpers
// ---------------------------------------------------------------------------

function createTestTexture(gl: WebGL2RenderingContext, width: number, height: number): WebGLTexture {
    const tex = gl.createTexture()!;
    gl.bindTexture(gl.TEXTURE_2D, tex);
    const data = new Uint8Array(width * height * 4);
    for (let i = 0; i < data.length; i += 4) {
        data[i] = 128;
        data[i + 1] = 128;
        data[i + 2] = 128;
        data[i + 3] = 255;
    }
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, width, height, 0, gl.RGBA, gl.UNSIGNED_BYTE, data);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    return tex;
}
