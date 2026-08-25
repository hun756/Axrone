import { describe, expect, it } from 'vitest';

/**
 * T-05: Rendering Correctness (Pixel-Level) Browser Tests
 *
 * These tests verify actual pixel output from WebGL2 rendering operations
 * using gl.readPixels(). They run in a real browser via Vitest browser mode
 * with Playwright, using a real WebGL2 context (not mocks).
 *
 * Each test group targets a specific aspect of the rendering pipeline:
 * 1. Clear color — verifies gl.clearColor + gl.clear produce correct pixels
 * 2. Colored quad — verifies a shader-driven full-screen quad renders expected color
 * 3. Texture mapping — verifies texture upload + sampling produces correct pixels
 * 4. Depth testing — verifies depth occlusion between overlapping geometry
 * 5. Viewport/scissor — verifies viewport transforms and scissor clipping
 */

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Tolerance for 8-bit channel comparisons (floating-point rounding). */
const CHANNEL_TOLERANCE = 2;

/**
 * Assert that a single channel value matches the expected value within tolerance.
 */
const expectChannelNear = (
    actual: number,
    expected: number,
    label: string,
): void => {
    expect(
        actual,
        `${label}: expected ${expected} +/- ${CHANNEL_TOLERANCE}, got ${actual}`,
    ).toBeGreaterThanOrEqual(expected - CHANNEL_TOLERANCE);
    expect(
        actual,
        `${label}: expected ${expected} +/- ${CHANNEL_TOLERANCE}, got ${actual}`,
    ).toBeLessThanOrEqual(expected + CHANNEL_TOLERANCE);
};

/**
 * Compile a shader, assert success, and return it.
 */
const compileShader = (
    gl: WebGL2RenderingContext,
    type: number,
    source: string,
): WebGLShader => {
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

/**
 * Link a program from vertex + fragment shaders, assert success, and return it.
 */
const linkProgram = (
    gl: WebGL2RenderingContext,
    vs: WebGLShader,
    fs: WebGLShader,
): WebGLProgram => {
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

/**
 * Read a single pixel at (x, y) from the currently bound framebuffer.
 * WebGL origin is bottom-left.
 */
const readPixel = (
    gl: WebGL2RenderingContext,
    x: number,
    y: number,
): Uint8Array => {
    const pixel = new Uint8Array(4);
    gl.readPixels(x, y, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, pixel);
    return pixel;
};

/**
 * Create a full-screen quad VAO (two triangles in NDC covering -1..1 on both axes).
 * Returns the VAO and the vertex count.
 */
const createFullscreenQuadVAO = (
    gl: WebGL2RenderingContext,
    positionLocation: number,
): { vao: WebGLVertexArrayObject; vertexCount: number } => {
    const vao = gl.createVertexArray()!;
    gl.bindVertexArray(vao);

    // Two triangles covering the entire clip-space quad.
    const vertices = new Float32Array([
        -1, -1,
         1, -1,
        -1,  1,
        -1,  1,
         1, -1,
         1,  1,
    ]);

    const buffer = gl.createBuffer()!;
    gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
    gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);
    gl.enableVertexAttribArray(positionLocation);
    gl.vertexAttribPointer(positionLocation, 2, gl.FLOAT, false, 0, 0);

    gl.bindVertexArray(null);
    return { vao, vertexCount: 6 };
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

const TEXTURED_VS = `#version 300 es
in vec2 a_position;
in vec2 a_uv;
out vec2 v_uv;
void main() {
    gl_Position = vec4(a_position, 0.0, 1.0);
    v_uv = a_uv;
}
`;

const TEXTURED_FS = `#version 300 es
precision mediump float;
in vec2 v_uv;
uniform sampler2D u_texture;
out vec4 o_color;
void main() {
    o_color = texture(u_texture, v_uv);
}
`;

// ---------------------------------------------------------------------------
// Test Groups
// ---------------------------------------------------------------------------

describe('Rendering Correctness — Pixel-Level Verification', () => {
    // -----------------------------------------------------------------------
    // Group 1: Clear Color Verification
    // -----------------------------------------------------------------------
    describe('Clear Color', () => {
        it('produces red pixels after clearing with (1, 0, 0, 1)', () => {
            const canvas = (window as any).createTestCanvas(64, 64) as HTMLCanvasElement;
            const gl = (window as any).createWebGLContext(canvas, {
                preserveDrawingBuffer: true,
            }) as WebGL2RenderingContext;

            gl.viewport(0, 0, canvas.width, canvas.height);
            gl.clearColor(1.0, 0.0, 0.0, 1.0);
            gl.clear(gl.COLOR_BUFFER_BIT);

            const center = readPixel(gl, 32, 32);
            expectChannelNear(center[0], 255, 'center red');
            expectChannelNear(center[1], 0, 'center green');
            expectChannelNear(center[2], 0, 'center blue');
            expectChannelNear(center[3], 255, 'center alpha');

            const corner = readPixel(gl, 0, 0);
            expectChannelNear(corner[0], 255, 'corner red');
            expectChannelNear(corner[1], 0, 'corner green');
            expectChannelNear(corner[2], 0, 'corner blue');
        });

        it('produces green pixels after clearing with (0, 1, 0, 1)', () => {
            const canvas = (window as any).createTestCanvas(64, 64) as HTMLCanvasElement;
            const gl = (window as any).createWebGLContext(canvas, {
                preserveDrawingBuffer: true,
            }) as WebGL2RenderingContext;

            gl.viewport(0, 0, canvas.width, canvas.height);
            gl.clearColor(0.0, 1.0, 0.0, 1.0);
            gl.clear(gl.COLOR_BUFFER_BIT);

            const center = readPixel(gl, 32, 32);
            expectChannelNear(center[0], 0, 'center red');
            expectChannelNear(center[1], 255, 'center green');
            expectChannelNear(center[2], 0, 'center blue');
            expectChannelNear(center[3], 255, 'center alpha');
        });

        it('produces blue pixels after clearing with (0, 0, 1, 1)', () => {
            const canvas = (window as any).createTestCanvas(64, 64) as HTMLCanvasElement;
            const gl = (window as any).createWebGLContext(canvas, {
                preserveDrawingBuffer: true,
            }) as WebGL2RenderingContext;

            gl.viewport(0, 0, canvas.width, canvas.height);
            gl.clearColor(0.0, 0.0, 1.0, 1.0);
            gl.clear(gl.COLOR_BUFFER_BIT);

            const center = readPixel(gl, 32, 32);
            expectChannelNear(center[0], 0, 'center red');
            expectChannelNear(center[1], 0, 'center green');
            expectChannelNear(center[2], 255, 'center blue');
            expectChannelNear(center[3], 255, 'center alpha');
        });

        it('produces semi-transparent pixels after clearing with alpha = 0.5', () => {
            const canvas = (window as any).createTestCanvas(64, 64) as HTMLCanvasElement;
            const gl = (window as any).createWebGLContext(canvas, {
                alpha: true,
                premultipliedAlpha: false,
                preserveDrawingBuffer: true,
            }) as WebGL2RenderingContext;

            gl.viewport(0, 0, canvas.width, canvas.height);
            gl.clearColor(1.0, 1.0, 1.0, 0.5);
            gl.clear(gl.COLOR_BUFFER_BIT);

            const center = readPixel(gl, 32, 32);
            expectChannelNear(center[0], 255, 'center red');
            expectChannelNear(center[3], 128, 'center alpha');
        });
    });

    // -----------------------------------------------------------------------
    // Group 2: Colored Quad Rendering
    // -----------------------------------------------------------------------
    describe('Colored Quad Rendering', () => {
        it('renders a solid red quad covering the full viewport', () => {
            const canvas = (window as any).createTestCanvas(128, 128) as HTMLCanvasElement;
            const gl = (window as any).createWebGLContext(canvas, {
                preserveDrawingBuffer: true,
            }) as WebGL2RenderingContext;

            const vs = compileShader(gl, gl.VERTEX_SHADER, SOLID_COLOR_VS);
            const fs = compileShader(gl, gl.FRAGMENT_SHADER, SOLID_COLOR_FS);
            const program = linkProgram(gl, vs, fs);

            const posLoc = gl.getAttribLocation(program, 'a_position');
            const colorLoc = gl.getUniformLocation(program, 'u_color');
            const { vao, vertexCount } = createFullscreenQuadVAO(gl, posLoc);

            gl.viewport(0, 0, canvas.width, canvas.height);
            gl.clearColor(0, 0, 0, 1);
            gl.clear(gl.COLOR_BUFFER_BIT);

            gl.useProgram(program);
            gl.uniform4f(colorLoc, 1.0, 0.0, 0.0, 1.0);
            gl.bindVertexArray(vao);
            gl.drawArrays(gl.TRIANGLES, 0, vertexCount);

            // Sample multiple points to confirm uniform coverage.
            const points = [
                [64, 64],   // center
                [10, 10],   // near bottom-left
                [120, 120], // near top-right
                [64, 10],   // bottom-center
            ];

            for (const [x, y] of points) {
                const pixel = readPixel(gl, x, y);
                expectChannelNear(pixel[0], 255, `(${x},${y}) red`);
                expectChannelNear(pixel[1], 0, `(${x},${y}) green`);
                expectChannelNear(pixel[2], 0, `(${x},${y}) blue`);
                expectChannelNear(pixel[3], 255, `(${x},${y}) alpha`);
            }

            gl.deleteVertexArray(vao);
            gl.deleteProgram(program);
            gl.deleteShader(vs);
            gl.deleteShader(fs);
        });

        it('renders a solid green quad with a different color uniform', () => {
            const canvas = (window as any).createTestCanvas(128, 128) as HTMLCanvasElement;
            const gl = (window as any).createWebGLContext(canvas, {
                preserveDrawingBuffer: true,
            }) as WebGL2RenderingContext;

            const vs = compileShader(gl, gl.VERTEX_SHADER, SOLID_COLOR_VS);
            const fs = compileShader(gl, gl.FRAGMENT_SHADER, SOLID_COLOR_FS);
            const program = linkProgram(gl, vs, fs);

            const posLoc = gl.getAttribLocation(program, 'a_position');
            const colorLoc = gl.getUniformLocation(program, 'u_color');
            const { vao, vertexCount } = createFullscreenQuadVAO(gl, posLoc);

            gl.viewport(0, 0, canvas.width, canvas.height);
            gl.clearColor(0, 0, 0, 1);
            gl.clear(gl.COLOR_BUFFER_BIT);

            gl.useProgram(program);
            gl.uniform4f(colorLoc, 0.0, 1.0, 0.0, 1.0);
            gl.bindVertexArray(vao);
            gl.drawArrays(gl.TRIANGLES, 0, vertexCount);

            const center = readPixel(gl, 64, 64);
            expectChannelNear(center[0], 0, 'center red');
            expectChannelNear(center[1], 255, 'center green');
            expectChannelNear(center[2], 0, 'center blue');

            gl.deleteVertexArray(vao);
            gl.deleteProgram(program);
            gl.deleteShader(vs);
            gl.deleteShader(fs);
        });

        it('renders a half-brightness color (0.5, 0.5, 0.5, 1.0) correctly', () => {
            const canvas = (window as any).createTestCanvas(64, 64) as HTMLCanvasElement;
            const gl = (window as any).createWebGLContext(canvas, {
                preserveDrawingBuffer: true,
            }) as WebGL2RenderingContext;

            const vs = compileShader(gl, gl.VERTEX_SHADER, SOLID_COLOR_VS);
            const fs = compileShader(gl, gl.FRAGMENT_SHADER, SOLID_COLOR_FS);
            const program = linkProgram(gl, vs, fs);

            const posLoc = gl.getAttribLocation(program, 'a_position');
            const colorLoc = gl.getUniformLocation(program, 'u_color');
            const { vao, vertexCount } = createFullscreenQuadVAO(gl, posLoc);

            gl.viewport(0, 0, canvas.width, canvas.height);
            gl.clearColor(0, 0, 0, 1);
            gl.clear(gl.COLOR_BUFFER_BIT);

            gl.useProgram(program);
            gl.uniform4f(colorLoc, 0.5, 0.5, 0.5, 1.0);
            gl.bindVertexArray(vao);
            gl.drawArrays(gl.TRIANGLES, 0, vertexCount);

            // 0.5 * 255 = 127.5, rounded to 127 or 128 depending on implementation.
            const center = readPixel(gl, 32, 32);
            expectChannelNear(center[0], 128, 'center red (mid-gray)');
            expectChannelNear(center[1], 128, 'center green (mid-gray)');
            expectChannelNear(center[2], 128, 'center blue (mid-gray)');

            gl.deleteVertexArray(vao);
            gl.deleteProgram(program);
            gl.deleteShader(vs);
            gl.deleteShader(fs);
        });
    });

    // -----------------------------------------------------------------------
    // Group 3: Texture Mapping Verification
    // -----------------------------------------------------------------------
    describe('Texture Mapping', () => {
        it('samples a 2x2 checkerboard texture at the correct UV coordinates', () => {
            const canvas = (window as any).createTestCanvas(128, 128) as HTMLCanvasElement;
            const gl = (window as any).createWebGLContext(canvas, {
                preserveDrawingBuffer: true,
            }) as WebGL2RenderingContext;

            // Create a 2x2 texture:
            //   (0,0)=red  (1,0)=green
            //   (0,1)=blue (1,1)=white
            const texture = gl.createTexture()!;
            gl.bindTexture(gl.TEXTURE_2D, texture);
            const texels = new Uint8Array([
                // Row 0 (bottom in GL): red, green
                255, 0, 0, 255,    0, 255, 0, 255,
                // Row 1 (top in GL): blue, white
                0, 0, 255, 255,    255, 255, 255, 255,
            ]);
            gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 2, 2, 0, gl.RGBA, gl.UNSIGNED_BYTE, texels);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);

            const vs = compileShader(gl, gl.VERTEX_SHADER, TEXTURED_VS);
            const fs = compileShader(gl, gl.FRAGMENT_SHADER, TEXTURED_FS);
            const program = linkProgram(gl, vs, fs);

            const posLoc = gl.getAttribLocation(program, 'a_position');
            const uvLoc = gl.getAttribLocation(program, 'a_uv');
            const texLoc = gl.getUniformLocation(program, 'u_texture');

            // Build a VAO with position + UV for a full-screen quad.
            const vao = gl.createVertexArray()!;
            gl.bindVertexArray(vao);

            // Interleaved: x, y, u, v
            const vertices = new Float32Array([
                -1, -1,  0.0, 0.0,
                 1, -1,  1.0, 0.0,
                -1,  1,  0.0, 1.0,
                -1,  1,  0.0, 1.0,
                 1, -1,  1.0, 0.0,
                 1,  1,  1.0, 1.0,
            ]);

            const buffer = gl.createBuffer()!;
            gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
            gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);
            gl.enableVertexAttribArray(posLoc);
            gl.vertexAttribPointer(posLoc, 2, gl.FLOAT, false, 16, 0);
            gl.enableVertexAttribArray(uvLoc);
            gl.vertexAttribPointer(uvLoc, 2, gl.FLOAT, false, 16, 8);

            gl.bindVertexArray(null);

            gl.viewport(0, 0, canvas.width, canvas.height);
            gl.clearColor(0, 0, 0, 1);
            gl.clear(gl.COLOR_BUFFER_BIT);

            gl.useProgram(program);
            gl.activeTexture(gl.TEXTURE0);
            gl.bindTexture(gl.TEXTURE_2D, texture);
            gl.uniform1i(texLoc, 0);

            gl.bindVertexArray(vao);
            gl.drawArrays(gl.TRIANGLES, 0, 6);

            // With NEAREST filtering on a 2x2 texture mapped to a 128x128 quad,
            // sample at the center of each quadrant.
            // GL origin is bottom-left.
            // Bottom-left quadrant -> UV ~(0.25, 0.25) -> texel (0,0) = red
            const bottomLeft = readPixel(gl, 32, 32);
            expectChannelNear(bottomLeft[0], 255, 'bottom-left red');
            expectChannelNear(bottomLeft[1], 0, 'bottom-left green');
            expectChannelNear(bottomLeft[2], 0, 'bottom-left blue');

            // Bottom-right quadrant -> UV ~(0.75, 0.25) -> texel (1,0) = green
            const bottomRight = readPixel(gl, 96, 32);
            expectChannelNear(bottomRight[0], 0, 'bottom-right red');
            expectChannelNear(bottomRight[1], 255, 'bottom-right green');
            expectChannelNear(bottomRight[2], 0, 'bottom-right blue');

            // Top-left quadrant -> UV ~(0.25, 0.75) -> texel (0,1) = blue
            const topLeft = readPixel(gl, 32, 96);
            expectChannelNear(topLeft[0], 0, 'top-left red');
            expectChannelNear(topLeft[1], 0, 'top-left green');
            expectChannelNear(topLeft[2], 255, 'top-left blue');

            // Top-right quadrant -> UV ~(0.75, 0.75) -> texel (1,1) = white
            const topRight = readPixel(gl, 96, 96);
            expectChannelNear(topRight[0], 255, 'top-right red');
            expectChannelNear(topRight[1], 255, 'top-right green');
            expectChannelNear(topRight[2], 255, 'top-right blue');

            gl.deleteVertexArray(vao);
            gl.deleteBuffer(buffer);
            gl.deleteTexture(texture);
            gl.deleteProgram(program);
            gl.deleteShader(vs);
            gl.deleteShader(fs);
        });

        it('samples a uniform 4x4 single-color texture correctly', () => {
            const canvas = (window as any).createTestCanvas(64, 64) as HTMLCanvasElement;
            const gl = (window as any).createWebGLContext(canvas, {
                preserveDrawingBuffer: true,
            }) as WebGL2RenderingContext;

            // Solid cyan 4x4 texture.
            const texture = gl.createTexture()!;
            gl.bindTexture(gl.TEXTURE_2D, texture);
            const texels = new Uint8Array(4 * 4 * 4);
            for (let i = 0; i < texels.length; i += 4) {
                texels[i] = 0;
                texels[i + 1] = 255;
                texels[i + 2] = 255;
                texels[i + 3] = 255;
            }
            gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 4, 4, 0, gl.RGBA, gl.UNSIGNED_BYTE, texels);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
            gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);

            const vs = compileShader(gl, gl.VERTEX_SHADER, TEXTURED_VS);
            const fs = compileShader(gl, gl.FRAGMENT_SHADER, TEXTURED_FS);
            const program = linkProgram(gl, vs, fs);

            const posLoc = gl.getAttribLocation(program, 'a_position');
            const uvLoc = gl.getAttribLocation(program, 'a_uv');
            const texLoc = gl.getUniformLocation(program, 'u_texture');

            const vao = gl.createVertexArray()!;
            gl.bindVertexArray(vao);

            const vertices = new Float32Array([
                -1, -1,  0, 0,
                 1, -1,  1, 0,
                -1,  1,  0, 1,
                -1,  1,  0, 1,
                 1, -1,  1, 0,
                 1,  1,  1, 1,
            ]);

            const buffer = gl.createBuffer()!;
            gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
            gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);
            gl.enableVertexAttribArray(posLoc);
            gl.vertexAttribPointer(posLoc, 2, gl.FLOAT, false, 16, 0);
            gl.enableVertexAttribArray(uvLoc);
            gl.vertexAttribPointer(uvLoc, 2, gl.FLOAT, false, 16, 8);

            gl.bindVertexArray(null);

            gl.viewport(0, 0, canvas.width, canvas.height);
            gl.clearColor(0, 0, 0, 1);
            gl.clear(gl.COLOR_BUFFER_BIT);

            gl.useProgram(program);
            gl.activeTexture(gl.TEXTURE0);
            gl.bindTexture(gl.TEXTURE_2D, texture);
            gl.uniform1i(texLoc, 0);

            gl.bindVertexArray(vao);
            gl.drawArrays(gl.TRIANGLES, 0, 6);

            const center = readPixel(gl, 32, 32);
            expectChannelNear(center[0], 0, 'center red');
            expectChannelNear(center[1], 255, 'center green');
            expectChannelNear(center[2], 255, 'center blue');
            expectChannelNear(center[3], 255, 'center alpha');

            gl.deleteVertexArray(vao);
            gl.deleteBuffer(buffer);
            gl.deleteTexture(texture);
            gl.deleteProgram(program);
            gl.deleteShader(vs);
            gl.deleteShader(fs);
        });
    });

    // -----------------------------------------------------------------------
    // Group 4: Depth Testing
    // -----------------------------------------------------------------------
    describe('Depth Testing', () => {
        const DEPTH_VS = `#version 300 es
in vec2 a_position;
uniform float u_z;
void main() {
    gl_Position = vec4(a_position, u_z, 1.0);
}
`;

        it('nearer quad occludes farther quad when depth test is enabled', () => {
            const canvas = (window as any).createTestCanvas(128, 128) as HTMLCanvasElement;
            const gl = (window as any).createWebGLContext(canvas, {
                depth: true,
                preserveDrawingBuffer: true,
            }) as WebGL2RenderingContext;

            const vs = compileShader(gl, gl.VERTEX_SHADER, DEPTH_VS);
            const fs = compileShader(gl, gl.FRAGMENT_SHADER, SOLID_COLOR_FS);
            const program = linkProgram(gl, vs, fs);

            const posLoc = gl.getAttribLocation(program, 'a_position');
            const colorLoc = gl.getUniformLocation(program, 'u_color');
            const zLoc = gl.getUniformLocation(program, 'u_z');

            gl.viewport(0, 0, canvas.width, canvas.height);
            gl.enable(gl.DEPTH_TEST);
            gl.depthFunc(gl.LESS);
            gl.clearColor(0, 0, 0, 1);
            gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

            gl.useProgram(program);

            // Draw FAR quad (z = 0.5) in red.
            gl.uniform1f(zLoc, 0.5);
            gl.uniform4f(colorLoc, 1.0, 0.0, 0.0, 1.0);
            {
                const vao = gl.createVertexArray()!;
                gl.bindVertexArray(vao);
                const vertices = new Float32Array([-1, -1, 1, -1, -1, 1, -1, 1, 1, -1, 1, 1]);
                const buf = gl.createBuffer()!;
                gl.bindBuffer(gl.ARRAY_BUFFER, buf);
                gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);
                gl.enableVertexAttribArray(posLoc);
                gl.vertexAttribPointer(posLoc, 2, gl.FLOAT, false, 0, 0);
                gl.bindVertexArray(null);
                gl.bindVertexArray(vao);
                gl.drawArrays(gl.TRIANGLES, 0, 6);
                gl.bindVertexArray(null);
                gl.deleteVertexArray(vao);
                gl.deleteBuffer(buf);
            }

            // Draw NEAR quad (z = -0.5) in green — should occlude the red.
            gl.uniform1f(zLoc, -0.5);
            gl.uniform4f(colorLoc, 0.0, 1.0, 0.0, 1.0);
            {
                const vao = gl.createVertexArray()!;
                gl.bindVertexArray(vao);
                const vertices = new Float32Array([-1, -1, 1, -1, -1, 1, -1, 1, 1, -1, 1, 1]);
                const buf = gl.createBuffer()!;
                gl.bindBuffer(gl.ARRAY_BUFFER, buf);
                gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);
                gl.enableVertexAttribArray(posLoc);
                gl.vertexAttribPointer(posLoc, 2, gl.FLOAT, false, 0, 0);
                gl.bindVertexArray(null);
                gl.bindVertexArray(vao);
                gl.drawArrays(gl.TRIANGLES, 0, 6);
                gl.bindVertexArray(null);
                gl.deleteVertexArray(vao);
                gl.deleteBuffer(buf);
            }

            // Center pixel should be green (near quad won).
            const center = readPixel(gl, 64, 64);
            expectChannelNear(center[0], 0, 'center red (should be occluded)');
            expectChannelNear(center[1], 255, 'center green (near quad visible)');
            expectChannelNear(center[2], 0, 'center blue');

            gl.deleteProgram(program);
            gl.deleteShader(vs);
            gl.deleteShader(fs);
        });

        it('farther quad is visible when drawn first and depth test is disabled', () => {
            const canvas = (window as any).createTestCanvas(128, 128) as HTMLCanvasElement;
            const gl = (window as any).createWebGLContext(canvas, {
                depth: true,
                preserveDrawingBuffer: true,
            }) as WebGL2RenderingContext;

            const vs = compileShader(gl, gl.VERTEX_SHADER, DEPTH_VS);
            const fs = compileShader(gl, gl.FRAGMENT_SHADER, SOLID_COLOR_FS);
            const program = linkProgram(gl, vs, fs);

            const posLoc = gl.getAttribLocation(program, 'a_position');
            const colorLoc = gl.getUniformLocation(program, 'u_color');
            const zLoc = gl.getUniformLocation(program, 'u_z');

            gl.viewport(0, 0, canvas.width, canvas.height);
            // Depth test DISABLED — later draws overwrite earlier ones regardless of Z.
            gl.disable(gl.DEPTH_TEST);
            gl.clearColor(0, 0, 0, 1);
            gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

            gl.useProgram(program);

            // Draw NEAR quad (z = -0.5) in red first.
            gl.uniform1f(zLoc, -0.5);
            gl.uniform4f(colorLoc, 1.0, 0.0, 0.0, 1.0);
            {
                const vao = gl.createVertexArray()!;
                gl.bindVertexArray(vao);
                const vertices = new Float32Array([-1, -1, 1, -1, -1, 1, -1, 1, 1, -1, 1, 1]);
                const buf = gl.createBuffer()!;
                gl.bindBuffer(gl.ARRAY_BUFFER, buf);
                gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);
                gl.enableVertexAttribArray(posLoc);
                gl.vertexAttribPointer(posLoc, 2, gl.FLOAT, false, 0, 0);
                gl.bindVertexArray(null);
                gl.bindVertexArray(vao);
                gl.drawArrays(gl.TRIANGLES, 0, 6);
                gl.bindVertexArray(null);
                gl.deleteVertexArray(vao);
                gl.deleteBuffer(buf);
            }

            // Draw FAR quad (z = 0.5) in green second — should overwrite red.
            gl.uniform1f(zLoc, 0.5);
            gl.uniform4f(colorLoc, 0.0, 1.0, 0.0, 1.0);
            {
                const vao = gl.createVertexArray()!;
                gl.bindVertexArray(vao);
                const vertices = new Float32Array([-1, -1, 1, -1, -1, 1, -1, 1, 1, -1, 1, 1]);
                const buf = gl.createBuffer()!;
                gl.bindBuffer(gl.ARRAY_BUFFER, buf);
                gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);
                gl.enableVertexAttribArray(posLoc);
                gl.vertexAttribPointer(posLoc, 2, gl.FLOAT, false, 0, 0);
                gl.bindVertexArray(null);
                gl.bindVertexArray(vao);
                gl.drawArrays(gl.TRIANGLES, 0, 6);
                gl.bindVertexArray(null);
                gl.deleteVertexArray(vao);
                gl.deleteBuffer(buf);
            }

            // Center pixel should be green (second draw overwrote first).
            const center = readPixel(gl, 64, 64);
            expectChannelNear(center[0], 0, 'center red (overwritten)');
            expectChannelNear(center[1], 255, 'center green (second draw wins)');
            expectChannelNear(center[2], 0, 'center blue');

            gl.deleteProgram(program);
            gl.deleteShader(vs);
            gl.deleteShader(fs);
        });

        it('respects depthFunc(GREATER) — only farther fragments pass', () => {
            const canvas = (window as any).createTestCanvas(128, 128) as HTMLCanvasElement;
            const gl = (window as any).createWebGLContext(canvas, {
                depth: true,
                preserveDrawingBuffer: true,
            }) as WebGL2RenderingContext;

            const vs = compileShader(gl, gl.VERTEX_SHADER, DEPTH_VS);
            const fs = compileShader(gl, gl.FRAGMENT_SHADER, SOLID_COLOR_FS);
            const program = linkProgram(gl, vs, fs);

            const posLoc = gl.getAttribLocation(program, 'a_position');
            const colorLoc = gl.getUniformLocation(program, 'u_color');
            const zLoc = gl.getUniformLocation(program, 'u_z');

            gl.viewport(0, 0, canvas.width, canvas.height);
            gl.enable(gl.DEPTH_TEST);
            gl.depthFunc(gl.GREATER); // Only pass if fragment depth > buffer depth
            gl.clearDepth(0.0);       // Start with depth buffer at 0 (nearest)
            gl.clearColor(0, 0, 0, 1);
            gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

            gl.useProgram(program);

            // Draw near quad (z = -0.5, depth ~0.25) in red.
            // GREATER test: 0.25 > 0.0 = true, so this passes and writes depth.
            gl.uniform1f(zLoc, -0.5);
            gl.uniform4f(colorLoc, 1.0, 0.0, 0.0, 1.0);
            {
                const vao = gl.createVertexArray()!;
                gl.bindVertexArray(vao);
                const vertices = new Float32Array([-1, -1, 1, -1, -1, 1, -1, 1, 1, -1, 1, 1]);
                const buf = gl.createBuffer()!;
                gl.bindBuffer(gl.ARRAY_BUFFER, buf);
                gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);
                gl.enableVertexAttribArray(posLoc);
                gl.vertexAttribPointer(posLoc, 2, gl.FLOAT, false, 0, 0);
                gl.bindVertexArray(null);
                gl.bindVertexArray(vao);
                gl.drawArrays(gl.TRIANGLES, 0, 6);
                gl.bindVertexArray(null);
                gl.deleteVertexArray(vao);
                gl.deleteBuffer(buf);
            }

            // Draw far quad (z = 0.5, depth ~0.75) in green.
            // GREATER test: 0.75 > 0.25 = true, so this also passes.
            gl.uniform1f(zLoc, 0.5);
            gl.uniform4f(colorLoc, 0.0, 1.0, 0.0, 1.0);
            {
                const vao = gl.createVertexArray()!;
                gl.bindVertexArray(vao);
                const vertices = new Float32Array([-1, -1, 1, -1, -1, 1, -1, 1, 1, -1, 1, 1]);
                const buf = gl.createBuffer()!;
                gl.bindBuffer(gl.ARRAY_BUFFER, buf);
                gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);
                gl.enableVertexAttribArray(posLoc);
                gl.vertexAttribPointer(posLoc, 2, gl.FLOAT, false, 0, 0);
                gl.bindVertexArray(null);
                gl.bindVertexArray(vao);
                gl.drawArrays(gl.TRIANGLES, 0, 6);
                gl.bindVertexArray(null);
                gl.deleteVertexArray(vao);
                gl.deleteBuffer(buf);
            }

            // With GREATER, the farther quad overwrites the nearer one.
            const center = readPixel(gl, 64, 64);
            expectChannelNear(center[0], 0, 'center red (overwritten by farther)');
            expectChannelNear(center[1], 255, 'center green (farther quad passes GREATER)');

            gl.deleteProgram(program);
            gl.deleteShader(vs);
            gl.deleteShader(fs);
        });
    });

    // -----------------------------------------------------------------------
    // Group 5: Viewport / Scissor Rendering
    // -----------------------------------------------------------------------
    describe('Viewport and Scissor', () => {
        it('renders only within the scissor rectangle', () => {
            const canvas = (window as any).createTestCanvas(128, 128) as HTMLCanvasElement;
            const gl = (window as any).createWebGLContext(canvas, {
                preserveDrawingBuffer: true,
            }) as WebGL2RenderingContext;

            gl.viewport(0, 0, canvas.width, canvas.height);
            gl.clearColor(0, 0, 0, 1);
            gl.clear(gl.COLOR_BUFFER_BIT);

            // Enable scissor test — only allow writes inside a 32x32 box at bottom-left.
            gl.enable(gl.SCISSOR_TEST);
            gl.scissor(0, 0, 32, 32);

            gl.clearColor(0, 0, 1, 1);
            gl.clear(gl.COLOR_BUFFER_BIT);

            // Inside scissor region: should be blue.
            const inside = readPixel(gl, 16, 16);
            expectChannelNear(inside[0], 0, 'inside scissor red');
            expectChannelNear(inside[1], 0, 'inside scissor green');
            expectChannelNear(inside[2], 255, 'inside scissor blue');

            // Outside scissor region: should remain black (from the first clear).
            const outside = readPixel(gl, 64, 64);
            expectChannelNear(outside[0], 0, 'outside scissor red');
            expectChannelNear(outside[1], 0, 'outside scissor green');
            expectChannelNear(outside[2], 0, 'outside scissor blue');

            gl.disable(gl.SCISSOR_TEST);
        });

        it('viewport transform maps NDC to the correct sub-region', () => {
            const canvas = (window as any).createTestCanvas(256, 256) as HTMLCanvasElement;
            const gl = (window as any).createWebGLContext(canvas, {
                preserveDrawingBuffer: true,
            }) as WebGL2RenderingContext;

            const vs = compileShader(gl, gl.VERTEX_SHADER, SOLID_COLOR_VS);
            const fs = compileShader(gl, gl.FRAGMENT_SHADER, SOLID_COLOR_FS);
            const program = linkProgram(gl, vs, fs);

            const posLoc = gl.getAttribLocation(program, 'a_position');
            const colorLoc = gl.getUniformLocation(program, 'u_color');
            const { vao, vertexCount } = createFullscreenQuadVAO(gl, posLoc);

            gl.clearColor(0, 0, 0, 1);
            gl.clear(gl.COLOR_BUFFER_BIT);

            // Set viewport to the top-left quadrant: (0, 128) -> (128, 256) in window coords.
            gl.viewport(0, 128, 128, 128);

            gl.useProgram(program);
            gl.uniform4f(colorLoc, 1.0, 0.0, 0.0, 1.0);
            gl.bindVertexArray(vao);
            gl.drawArrays(gl.TRIANGLES, 0, vertexCount);

            // Inside the viewport region (top-left quadrant): should be red.
            // GL origin is bottom-left, so top-left is around (64, 192).
            const inside = readPixel(gl, 64, 192);
            expectChannelNear(inside[0], 255, 'inside viewport red');
            expectChannelNear(inside[1], 0, 'inside viewport green');
            expectChannelNear(inside[2], 0, 'inside viewport blue');

            // Outside the viewport region (bottom-right quadrant): should be black.
            const outside = readPixel(gl, 192, 64);
            expectChannelNear(outside[0], 0, 'outside viewport red');
            expectChannelNear(outside[1], 0, 'outside viewport green');
            expectChannelNear(outside[2], 0, 'outside viewport blue');

            gl.deleteVertexArray(vao);
            gl.deleteProgram(program);
            gl.deleteShader(vs);
            gl.deleteShader(fs);
        });

        it('colorMask(false,...) prevents color writes while keeping depth writes', () => {
            const canvas = (window as any).createTestCanvas(64, 64) as HTMLCanvasElement;
            const gl = (window as any).createWebGLContext(canvas, {
                depth: true,
                preserveDrawingBuffer: true,
            }) as WebGL2RenderingContext;

            gl.viewport(0, 0, canvas.width, canvas.height);
            gl.clearColor(1, 0, 0, 1); // Red background
            gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);

            // Disable all color writes.
            gl.colorMask(false, false, false, false);

            // Clear with green — should NOT affect the color buffer.
            gl.clearColor(0, 1, 0, 1);
            gl.clear(gl.COLOR_BUFFER_BIT);

            // Pixel should still be red.
            const center = readPixel(gl, 32, 32);
            expectChannelNear(center[0], 255, 'color masked: red preserved');
            expectChannelNear(center[1], 0, 'color masked: green blocked');
            expectChannelNear(center[2], 0, 'color masked: blue blocked');

            // Restore color mask and clear again — should work now.
            gl.colorMask(true, true, true, true);
            gl.clearColor(0, 0, 1, 1);
            gl.clear(gl.COLOR_BUFFER_BIT);

            const afterRestore = readPixel(gl, 32, 32);
            expectChannelNear(afterRestore[0], 0, 'restored: red cleared');
            expectChannelNear(afterRestore[1], 0, 'restored: green cleared');
            expectChannelNear(afterRestore[2], 255, 'restored: blue written');
        });
    });
});
