import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { createInputSystem } from '@axrone/input';
import type { InputSystem, InputActionSchema } from '@axrone/input';

// ---------------------------------------------------------------------------
// Helpers from vitest.browser.setup.ts
// ---------------------------------------------------------------------------
const createTestCanvas = (width = 800, height = 600): HTMLCanvasElement =>
    (window as any).createTestCanvas(width, height);

const createWebGLContext = (
    canvas: HTMLCanvasElement,
    attrs: Partial<WebGLContextAttributes> = {},
): WebGL2RenderingContext => (window as any).createWebGLContext(canvas, attrs);

// ---------------------------------------------------------------------------
// Mobile device descriptors (viewport width, height, DPR)
// ---------------------------------------------------------------------------
interface MobileDeviceDescriptor {
    name: string;
    width: number;
    height: number;
    dpr: number;
}

const MOBILE_DEVICES: MobileDeviceDescriptor[] = [
    { name: 'iPhone SE', width: 375, height: 667, dpr: 2 },
    { name: 'iPhone 14 Pro Max', width: 414, height: 896, dpr: 3 },
    { name: 'Android Small', width: 360, height: 640, dpr: 2 },
];

// ---------------------------------------------------------------------------
// Shader helpers
// ---------------------------------------------------------------------------
const VERT_QUAD = `#version 300 es
in vec2 a_position;
void main() {
    gl_Position = vec4(a_position, 0.0, 1.0);
}
`;

const FRAG_SOLID = `#version 300 es
precision mediump float;
uniform vec4 u_color;
out vec4 fragColor;
void main() {
    fragColor = u_color;
}
`;

function compileProgram(
    gl: WebGL2RenderingContext,
    vertSrc: string,
    fragSrc: string,
): WebGLProgram {
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
    return program;
}

function readPixel(gl: WebGL2RenderingContext, x: number, y: number): Uint8Array {
    const pixels = new Uint8Array(4);
    gl.readPixels(x, y, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
    return pixels;
}

// ---------------------------------------------------------------------------
// T-12: Mobile Device Test Suite
//
// Tests WebGL2 rendering and touch input under mobile device emulation
// conditions: viewport sizes, DPR scaling, touch events, resize behavior.
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// 1. WebGL2 Context Creation at Mobile Viewport Sizes
// ---------------------------------------------------------------------------
describe('Mobile: WebGL2 Context at Mobile Viewports', () => {
    it.each(MOBILE_DEVICES)(
        'creates WebGL2 context at $name viewport ($width x $height)',
        (device) => {
            const canvas = createTestCanvas(device.width, device.height);
            const gl = createWebGLContext(canvas);

            expect(gl).not.toBeNull();
            expect(gl).toBeInstanceOf(WebGL2RenderingContext);
            expect(canvas.width).toBe(device.width);
            expect(canvas.height).toBe(device.height);
        },
    );
});

// ---------------------------------------------------------------------------
// 2. Shader Compilation at Mobile Viewport (no precision issues)
// ---------------------------------------------------------------------------
describe('Mobile: Shader Compilation at Mobile Viewport', () => {
    it('compiles vertex and fragment shaders at mobile viewport without precision errors', () => {
        const canvas = createTestCanvas(375, 667);
        const gl = createWebGLContext(canvas);

        // Use highp in fragment shader to test precision support
        const fragHighP = `#version 300 es
            precision highp float;
            out vec4 fragColor;
            void main() {
                fragColor = vec4(1.0, 0.0, 0.0, 1.0);
            }
        `;

        const program = compileProgram(gl, VERT_QUAD, fragHighP);
        expect(program).toBeTruthy();

        gl.deleteProgram(program);
    });

    it('compiles shaders with mediump precision at mobile viewport', () => {
        const canvas = createTestCanvas(360, 640);
        const gl = createWebGLContext(canvas);

        const fragMedP = `#version 300 es
            precision mediump float;
            out vec4 fragColor;
            void main() {
                fragColor = vec4(0.0, 1.0, 0.0, 1.0);
            }
        `;

        const program = compileProgram(gl, VERT_QUAD, fragMedP);
        expect(program).toBeTruthy();

        gl.deleteProgram(program);
    });
});

// ---------------------------------------------------------------------------
// 3. Rendering Correctness at Low Resolutions
// ---------------------------------------------------------------------------
describe('Mobile: Rendering at Low Resolutions', () => {
    it.each([
        { label: '64x64', w: 64, h: 64 },
        { label: '128x128', w: 128, h: 128 },
    ])('renders correctly at $label resolution', ({ w, h }) => {
        const canvas = createTestCanvas(w, h);
        const gl = createWebGLContext(canvas, { preserveDrawingBuffer: true });

        // Clear to a known color
        gl.viewport(0, 0, w, h);
        gl.clearColor(0.0, 0.0, 1.0, 1.0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        // Read back center pixel
        const center = readPixel(gl, Math.floor(w / 2), Math.floor(h / 2));
        expect(center[0]).toBe(0);   // R
        expect(center[1]).toBe(0);   // G
        expect(center[2]).toBe(255); // B
        expect(center[3]).toBe(255); // A
    });

    it('renders a colored quad at 64x64 and verifies pixel output', () => {
        const canvas = createTestCanvas(64, 64);
        const gl = createWebGLContext(canvas, { preserveDrawingBuffer: true });

        const program = compileProgram(gl, VERT_QUAD, FRAG_SOLID);
        const aPos = gl.getAttribLocation(program, 'a_position');
        const uColor = gl.getUniformLocation(program, 'u_color')!;

        // Fullscreen quad
        const vertices = new Float32Array([-1, -1, 1, -1, -1, 1, 1, 1]);
        const vbo = gl.createBuffer()!;
        gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
        gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);

        const vao = gl.createVertexArray()!;
        gl.bindVertexArray(vao);
        gl.enableVertexAttribArray(aPos);
        gl.vertexAttribPointer(aPos, 2, gl.FLOAT, false, 0, 0);
        gl.bindVertexArray(null);

        gl.viewport(0, 0, 64, 64);
        gl.clearColor(0, 0, 0, 0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        gl.useProgram(program);
        gl.uniform4f(uColor, 1.0, 0.5, 0.0, 1.0); // Orange

        gl.bindVertexArray(vao);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
        gl.bindVertexArray(null);

        const center = readPixel(gl, 32, 32);
        expect(center[0]).toBeGreaterThan(250); // R
        expect(center[1]).toBeGreaterThan(120); // G (~127)
        expect(center[1]).toBeLessThan(135);
        expect(center[2]).toBeLessThan(5);      // B
        expect(center[3]).toBe(255);             // A

        gl.deleteVertexArray(vao);
        gl.deleteBuffer(vbo);
        gl.deleteProgram(program);
    });
});

// ---------------------------------------------------------------------------
// 4. Device Pixel Ratio (DPR) Handling
// ---------------------------------------------------------------------------
describe('Mobile: Device Pixel Ratio (DPR) Handling', () => {
    it.each([
        { label: 'DPR 1', dpr: 1 },
        { label: 'DPR 2', dpr: 2 },
        { label: 'DPR 3', dpr: 3 },
    ])('canvas backing store scales correctly at $label', ({ dpr }) => {
        const cssWidth = 375;
        const cssHeight = 667;
        const canvas = createTestCanvas(cssWidth * dpr, cssHeight * dpr);

        // Simulate CSS size
        canvas.style.width = `${cssWidth}px`;
        canvas.style.height = `${cssHeight}px`;

        const gl = createWebGLContext(canvas, { preserveDrawingBuffer: true });

        // The backing store should be at physical resolution
        expect(canvas.width).toBe(cssWidth * dpr);
        expect(canvas.height).toBe(cssHeight * dpr);

        // Set viewport to match backing store
        gl.viewport(0, 0, canvas.width, canvas.height);
        gl.clearColor(1.0, 0.0, 0.0, 1.0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        // Read pixel at center of backing store
        const cx = Math.floor(canvas.width / 2);
        const cy = Math.floor(canvas.height / 2);
        const pixel = readPixel(gl, cx, cy);
        expect(pixel[0]).toBe(255); // R
        expect(pixel[1]).toBe(0);   // G
        expect(pixel[2]).toBe(0);   // B
    });

    it('renders a quad correctly when viewport matches DPR-scaled canvas', () => {
        const dpr = 2;
        const cssWidth = 375;
        const cssHeight = 667;
        const canvas = createTestCanvas(cssWidth * dpr, cssHeight * dpr);
        canvas.style.width = `${cssWidth}px`;
        canvas.style.height = `${cssHeight}px`;

        const gl = createWebGLContext(canvas, { preserveDrawingBuffer: true });

        const program = compileProgram(gl, VERT_QUAD, FRAG_SOLID);
        const aPos = gl.getAttribLocation(program, 'a_position');
        const uColor = gl.getUniformLocation(program, 'u_color')!;

        const vertices = new Float32Array([-1, -1, 1, -1, -1, 1, 1, 1]);
        const vbo = gl.createBuffer()!;
        gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
        gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);

        const vao = gl.createVertexArray()!;
        gl.bindVertexArray(vao);
        gl.enableVertexAttribArray(aPos);
        gl.vertexAttribPointer(aPos, 2, gl.FLOAT, false, 0, 0);
        gl.bindVertexArray(null);

        // Viewport must match the backing store (physical pixels)
        gl.viewport(0, 0, canvas.width, canvas.height);
        gl.clearColor(0, 0, 0, 0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        gl.useProgram(program);
        gl.uniform4f(uColor, 0.0, 1.0, 0.0, 1.0); // Green

        gl.bindVertexArray(vao);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
        gl.bindVertexArray(null);

        // Center of the DPR-scaled canvas should be green
        const cx = Math.floor(canvas.width / 2);
        const cy = Math.floor(canvas.height / 2);
        const pixel = readPixel(gl, cx, cy);
        expect(pixel[0]).toBeLessThan(5);      // R
        expect(pixel[1]).toBeGreaterThan(250); // G
        expect(pixel[2]).toBeLessThan(5);      // B

        gl.deleteVertexArray(vao);
        gl.deleteBuffer(vbo);
        gl.deleteProgram(program);
    });
});

// ---------------------------------------------------------------------------
// 5. Touch Event Dispatch — touchstart/touchmove/touchend
// ---------------------------------------------------------------------------
describe('Mobile: Touch Event Dispatch via InputSystem', () => {
    let input: InputSystem<InputActionSchema> | undefined;

    beforeEach(() => {
        input = createInputSystem({
            schema: {
                zoom: { kind: 'axis' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        zoom: [{ type: 'control', control: 'touch/pinch' }],
                    },
                },
            ],
        });
    });

    afterEach(() => {
        input?.dispose();
        input = undefined;
    });

    it('processes touchstart event', () => {
        input!.dispatch({
            type: 'touch',
            phase: 'start',
            touches: [
                { id: 1, x: 100, y: 200 },
                { id: 2, x: 110, y: 200 },
            ],
            changed: [
                { id: 1, x: 100, y: 200 },
                { id: 2, x: 110, y: 200 },
            ],
        });
        input!.update(1);

        // The touch was registered — system should not throw and should be processable
        expect(() => input!.update(2)).not.toThrow();
    });

    it('processes touchmove event with delta', () => {
        input!.dispatch({
            type: 'touch',
            phase: 'start',
            touches: [
                { id: 1, x: 100, y: 200 },
                { id: 2, x: 110, y: 200 },
            ],
            changed: [
                { id: 1, x: 100, y: 200 },
                { id: 2, x: 110, y: 200 },
            ],
        });
        input!.update(1);

        input!.dispatch({
            type: 'touch',
            phase: 'move',
            touches: [
                { id: 1, x: 100, y: 200 },
                { id: 2, x: 150, y: 250 },
            ],
            changed: [{ id: 2, x: 150, y: 250 }],
        });
        input!.update(2);

        // Touch move should be processed without error
        expect(() => input!.update(3)).not.toThrow();
    });

    it('processes touchend event', () => {
        input!.dispatch({
            type: 'touch',
            phase: 'start',
            touches: [
                { id: 1, x: 100, y: 200 },
                { id: 2, x: 110, y: 200 },
            ],
            changed: [
                { id: 1, x: 100, y: 200 },
                { id: 2, x: 110, y: 200 },
            ],
        });
        input!.update(1);

        input!.dispatch({
            type: 'touch',
            phase: 'end',
            touches: [],
            changed: [{ id: 1, x: 100, y: 200 }],
        });
        input!.update(2);

        // After touch end, system should be clean
        expect(() => input!.update(3)).not.toThrow();
    });
});

// ---------------------------------------------------------------------------
// 6. Multi-Touch Support — 2+ simultaneous touch points
// ---------------------------------------------------------------------------
describe('Mobile: Multi-Touch Support', () => {
    it('tracks 2 simultaneous touch points via pinch gesture', () => {
        const input = createInputSystem({
            schema: {
                zoom: { kind: 'axis' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        zoom: [{ type: 'control', control: 'touch/pinch' }],
                    },
                },
            ],
        });

        // Two fingers start 10 units apart
        input.dispatch({
            type: 'touch',
            phase: 'start',
            touches: [
                { id: 1, x: 100, y: 200 },
                { id: 2, x: 110, y: 200 },
            ],
            changed: [
                { id: 1, x: 100, y: 200 },
                { id: 2, x: 110, y: 200 },
            ],
        });
        input.update(1);

        // Move finger 2 to increase distance by 20 (pinch out)
        input.dispatch({
            type: 'touch',
            phase: 'move',
            touches: [
                { id: 1, x: 100, y: 200 },
                { id: 2, x: 130, y: 200 },
            ],
            changed: [{ id: 2, x: 130, y: 200 }],
        });
        input.update(2);

        // Pinch value should reflect the distance change
        const zoomValue = input.read('zoom');
        expect(Number.isFinite(zoomValue)).toBe(true);
        expect(zoomValue).toBeGreaterThan(0);

        input.dispose();
    });

    it('tracks 3 simultaneous touch points', () => {
        const input = createInputSystem({
            schema: {
                zoom: { kind: 'axis' },
            },
            contexts: [
                {
                    id: 'gameplay',
                    bindings: {
                        zoom: [{ type: 'control', control: 'touch/pinch' }],
                    },
                },
            ],
        });

        // Three fingers
        input.dispatch({
            type: 'touch',
            phase: 'start',
            touches: [
                { id: 1, x: 100, y: 200 },
                { id: 2, x: 200, y: 200 },
                { id: 3, x: 150, y: 100 },
            ],
            changed: [
                { id: 1, x: 100, y: 200 },
                { id: 2, x: 200, y: 200 },
                { id: 3, x: 150, y: 100 },
            ],
        });
        input.update(1);

        // Move one finger
        input.dispatch({
            type: 'touch',
            phase: 'move',
            touches: [
                { id: 1, x: 100, y: 200 },
                { id: 2, x: 250, y: 200 },
                { id: 3, x: 150, y: 100 },
            ],
            changed: [{ id: 2, x: 250, y: 200 }],
        });
        input.update(2);

        // Should still be tracking without error
        expect(Number.isFinite(input.read('zoom'))).toBe(true);

        input.dispose();
    });
});

// ---------------------------------------------------------------------------
// 7. Viewport Resize Behavior — Portrait to Landscape
// ---------------------------------------------------------------------------
describe('Mobile: Viewport Resize (Orientation Change)', () => {
    it('canvas adapts when resizing from portrait to landscape', () => {
        // Start in portrait: 375x667
        const canvas = createTestCanvas(375, 667);
        const gl = createWebGLContext(canvas, { preserveDrawingBuffer: true });

        // Clear portrait viewport
        gl.viewport(0, 0, 375, 667);
        gl.clearColor(1.0, 0.0, 0.0, 1.0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        // Simulate orientation change to landscape: 667x375
        canvas.width = 667;
        canvas.height = 375;

        // Update viewport to match new dimensions
        gl.viewport(0, 0, 667, 375);
        gl.clearColor(0.0, 1.0, 0.0, 1.0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        // Verify the new landscape viewport renders correctly
        const center = readPixel(gl, Math.floor(667 / 2), Math.floor(375 / 2));
        expect(center[0]).toBe(0);   // R
        expect(center[1]).toBe(255); // G
        expect(center[2]).toBe(0);   // B
    });

    it('renders correctly after multiple resize cycles', () => {
        const canvas = createTestCanvas(375, 667);
        const gl = createWebGLContext(canvas, { preserveDrawingBuffer: true });

        const sizes = [
            { w: 375, h: 667 },  // Portrait
            { w: 667, h: 375 },  // Landscape
            { w: 414, h: 896 },  // Larger portrait
            { w: 896, h: 414 },  // Larger landscape
        ];

        for (const { w, h } of sizes) {
            canvas.width = w;
            canvas.height = h;
            gl.viewport(0, 0, w, h);
            gl.clearColor(0.0, 0.0, 1.0, 1.0);
            gl.clear(gl.COLOR_BUFFER_BIT);

            const center = readPixel(gl, Math.floor(w / 2), Math.floor(h / 2));
            expect(center[2]).toBe(255); // B channel should be 255
            expect(gl.getError()).toBe(gl.NO_ERROR);
        }
    });
});

// ---------------------------------------------------------------------------
// 8. Mobile GPU Limits — MAX_TEXTURE_SIZE, MAX_VIEWPORT_DIMS
// ---------------------------------------------------------------------------
describe('Mobile: GPU Limits at Mobile Viewport', () => {
    it('reports MAX_TEXTURE_SIZE >= 2048 at mobile viewport', () => {
        const canvas = createTestCanvas(375, 667);
        const gl = createWebGLContext(canvas);

        const maxTextureSize = gl.getParameter(gl.MAX_TEXTURE_SIZE) as number;
        // Mobile GPUs typically support at least 2048, most support 4096+
        expect(maxTextureSize).toBeGreaterThanOrEqual(2048);
    });

    it('reports MAX_VIEWPORT_DIMS sufficient for mobile resolutions', () => {
        const canvas = createTestCanvas(414, 896);
        const gl = createWebGLContext(canvas);

        const maxViewportDims = gl.getParameter(gl.MAX_VIEWPORT_DIMS) as Int32Array;
        expect(maxViewportDims).toBeInstanceOf(Int32Array);
        // Should be able to handle at least the mobile viewport size
        expect(maxViewportDims[0]).toBeGreaterThanOrEqual(414);
        expect(maxViewportDims[1]).toBeGreaterThanOrEqual(896);
    });

    it('reports MAX_RENDERBUFFER_SIZE >= 2048 at mobile viewport', () => {
        const canvas = createTestCanvas(360, 640);
        const gl = createWebGLContext(canvas);

        const maxRenderbufferSize = gl.getParameter(gl.MAX_RENDERBUFFER_SIZE) as number;
        expect(maxRenderbufferSize).toBeGreaterThanOrEqual(2048);
    });
});

// ---------------------------------------------------------------------------
// 9. Performance at Mobile Resolution
// ---------------------------------------------------------------------------
describe('Mobile: Performance at Mobile Resolution', () => {
    it('renders a frame at mobile resolution within time budget', () => {
        const canvas = createTestCanvas(375, 667);
        const gl = createWebGLContext(canvas);

        const program = compileProgram(gl, VERT_QUAD, FRAG_SOLID);
        const aPos = gl.getAttribLocation(program, 'a_position');
        const uColor = gl.getUniformLocation(program, 'u_color')!;

        const vertices = new Float32Array([-1, -1, 1, -1, -1, 1, 1, 1]);
        const vbo = gl.createBuffer()!;
        gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
        gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);

        const vao = gl.createVertexArray()!;
        gl.bindVertexArray(vao);
        gl.enableVertexAttribArray(aPos);
        gl.vertexAttribPointer(aPos, 2, gl.FLOAT, false, 0, 0);
        gl.bindVertexArray(null);

        gl.viewport(0, 0, 375, 667);
        gl.useProgram(program);
        gl.uniform4f(uColor, 1.0, 0.0, 0.0, 1.0);

        // Measure frame time
        const start = performance.now();
        for (let i = 0; i < 10; i++) {
            gl.clearColor(0, 0, 0, 1);
            gl.clear(gl.COLOR_BUFFER_BIT);
            gl.bindVertexArray(vao);
            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
            gl.bindVertexArray(null);
        }
        // Force GPU to finish
        gl.finish();
        const elapsed = performance.now() - start;

        // 10 frames should complete well within 80ms (8ms per frame budget)
        expect(elapsed).toBeLessThan(80);

        gl.deleteVertexArray(vao);
        gl.deleteBuffer(vbo);
        gl.deleteProgram(program);
    });
});

// ---------------------------------------------------------------------------
// 10. Safe Area / Notch Handling
// ---------------------------------------------------------------------------
describe('Mobile: Safe Area / Notch Handling', () => {
    it('viewport respects safe area insets by adjusting scissor region', () => {
        const canvas = createTestCanvas(375, 812); // iPhone X-like with notch
        const gl = createWebGLContext(canvas, { preserveDrawingBuffer: true });

        // Simulate safe area insets (top: 44px for notch, bottom: 34px for home indicator)
        const safeAreaTop = 44;
        const safeAreaBottom = 34;
        const safeWidth = 375;
        const safeHeight = 812 - safeAreaTop - safeAreaBottom; // 734

        // Set viewport to safe area only
        gl.viewport(0, safeAreaBottom, safeWidth, safeHeight);
        gl.enable(gl.SCISSOR_TEST);
        gl.scissor(0, safeAreaBottom, safeWidth, safeHeight);

        // Clear to red (should only fill safe area)
        gl.clearColor(1.0, 0.0, 0.0, 1.0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        // Pixel inside safe area should be red
        const insideY = safeAreaBottom + Math.floor(safeHeight / 2);
        const insidePixel = readPixel(gl, Math.floor(safeWidth / 2), insideY);
        expect(insidePixel[0]).toBe(255); // R
        expect(insidePixel[1]).toBe(0);   // G

        // Pixel in the notch area (above safe area) should NOT be red
        // (it should remain at whatever was there before — black/cleared)
        const notchY = 5; // Well within the notch area
        const notchPixel = readPixel(gl, Math.floor(safeWidth / 2), notchY);
        // The notch area was not cleared by our scissor-limited clear
        // It should be black (0,0,0,0) from default clear or untouched
        expect(notchPixel[0]).toBeLessThan(5); // R — not red

        gl.disable(gl.SCISSOR_TEST);
    });

    it('renders a quad within safe area bounds at mobile viewport', () => {
        const canvas = createTestCanvas(375, 812);
        const gl = createWebGLContext(canvas, { preserveDrawingBuffer: true });

        // Safe area
        const safeAreaTop = 44;
        const safeAreaBottom = 34;
        const safeHeight = 812 - safeAreaTop - safeAreaBottom;

        const program = compileProgram(gl, VERT_QUAD, FRAG_SOLID);
        const aPos = gl.getAttribLocation(program, 'a_position');
        const uColor = gl.getUniformLocation(program, 'u_color')!;

        const vertices = new Float32Array([-1, -1, 1, -1, -1, 1, 1, 1]);
        const vbo = gl.createBuffer()!;
        gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
        gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);

        const vao = gl.createVertexArray()!;
        gl.bindVertexArray(vao);
        gl.enableVertexAttribArray(aPos);
        gl.vertexAttribPointer(aPos, 2, gl.FLOAT, false, 0, 0);
        gl.bindVertexArray(null);

        // Set viewport to safe area
        gl.viewport(0, safeAreaBottom, 375, safeHeight);
        gl.clearColor(0, 0, 0, 0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        gl.useProgram(program);
        gl.uniform4f(uColor, 0.0, 0.0, 1.0, 1.0); // Blue

        gl.bindVertexArray(vao);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
        gl.bindVertexArray(null);

        // Center of safe area should be blue
        const centerY = safeAreaBottom + Math.floor(safeHeight / 2);
        const pixel = readPixel(gl, Math.floor(375 / 2), centerY);
        expect(pixel[0]).toBeLessThan(5);      // R
        expect(pixel[1]).toBeLessThan(5);      // G
        expect(pixel[2]).toBeGreaterThan(250); // B

        gl.deleteVertexArray(vao);
        gl.deleteBuffer(vbo);
        gl.deleteProgram(program);
    });
});
