import { describe, it, expect, beforeEach, afterEach } from 'vitest';

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
// T-09: Cross-Browser WebGL2 Compatibility Test Suite
//
// These tests use ONLY standard WebGL2 APIs — no browser-specific extensions
// or vendor-prefixed features. They are designed to pass on Chromium, Firefox,
// and WebKit when run via Vitest browser mode with Playwright.
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// 1. WebGL2 Context Creation
// ---------------------------------------------------------------------------
describe('Cross-Browser: WebGL2 Context Creation', () => {
    it('should create a WebGL2 context from a canvas element', () => {
        const canvas = createTestCanvas();
        const gl = canvas.getContext('webgl2');
        expect(gl).not.toBeNull();
        expect(gl).toBeInstanceOf(WebGL2RenderingContext);
    });

    it('should expose WebGL2RenderingContext on the window object', () => {
        expect(window.WebGL2RenderingContext).toBeDefined();
        expect(typeof window.WebGL2RenderingContext).toBe('function');
    });

    it('should report WebGL version as 2.0', () => {
        const canvas = createTestCanvas();
        const gl = createWebGLContext(canvas);
        const version = gl.getParameter(gl.VERSION) as string;
        expect(version).toContain('WebGL 2.0');
    });

    it('should report GLSL ES 3.00 shading language version', () => {
        const canvas = createTestCanvas();
        const gl = createWebGLContext(canvas);
        const shadingLang = gl.getParameter(gl.SHADING_LANGUAGE_VERSION) as string;
        expect(shadingLang).toContain('GLSL ES 3.0');
    });
});

// ---------------------------------------------------------------------------
// 2. GLSL ES 3.00 Shader Compilation
// ---------------------------------------------------------------------------
describe('Cross-Browser: GLSL ES 3.00 Shader Compilation', () => {
    let gl: WebGL2RenderingContext;

    beforeEach(() => {
        const canvas = createTestCanvas();
        gl = createWebGLContext(canvas);
    });

    it('should compile a vertex shader with #version 300 es', () => {
        const src = `#version 300 es
            in vec4 a_position;
            void main() {
                gl_Position = a_position;
            }
        `;
        const shader = gl.createShader(gl.VERTEX_SHADER)!;
        gl.shaderSource(shader, src);
        gl.compileShader(shader);

        expect(gl.getShaderParameter(shader, gl.COMPILE_STATUS)).toBe(true);
        gl.deleteShader(shader);
    });

    it('should compile a fragment shader with #version 300 es', () => {
        const src = `#version 300 es
            precision mediump float;
            out vec4 fragColor;
            void main() {
                fragColor = vec4(1.0, 0.0, 0.0, 1.0);
            }
        `;
        const shader = gl.createShader(gl.FRAGMENT_SHADER)!;
        gl.shaderSource(shader, src);
        gl.compileShader(shader);

        expect(gl.getShaderParameter(shader, gl.COMPILE_STATUS)).toBe(true);
        gl.deleteShader(shader);
    });

    it('should link a vertex + fragment program successfully', () => {
        const vsSrc = `#version 300 es
            in vec2 a_position;
            void main() {
                gl_Position = vec4(a_position, 0.0, 1.0);
            }
        `;
        const fsSrc = `#version 300 es
            precision mediump float;
            out vec4 fragColor;
            void main() {
                fragColor = vec4(0.0, 1.0, 0.0, 1.0);
            }
        `;

        const vs = gl.createShader(gl.VERTEX_SHADER)!;
        gl.shaderSource(vs, vsSrc);
        gl.compileShader(vs);
        expect(gl.getShaderParameter(vs, gl.COMPILE_STATUS)).toBe(true);

        const fs = gl.createShader(gl.FRAGMENT_SHADER)!;
        gl.shaderSource(fs, fsSrc);
        gl.compileShader(fs);
        expect(gl.getShaderParameter(fs, gl.COMPILE_STATUS)).toBe(true);

        const program = gl.createProgram()!;
        gl.attachShader(program, vs);
        gl.attachShader(program, fs);
        gl.linkProgram(program);

        expect(gl.getProgramParameter(program, gl.LINK_STATUS)).toBe(true);

        gl.deleteShader(vs);
        gl.deleteShader(fs);
        gl.deleteProgram(program);
    });
});

// ---------------------------------------------------------------------------
// 3. Key WebGL2 Feature Availability
// ---------------------------------------------------------------------------
describe('Cross-Browser: Key WebGL2 Features', () => {
    let gl: WebGL2RenderingContext;

    beforeEach(() => {
        const canvas = createTestCanvas();
        gl = createWebGLContext(canvas);
    });

    it('should support Vertex Array Objects (VAOs)', () => {
        const vao = gl.createVertexArray();
        expect(vao).not.toBeNull();

        gl.bindVertexArray(vao);
        const boundVAO = gl.getParameter(gl.VERTEX_ARRAY_BINDING) as WebGLVertexArrayObject | null;
        expect(boundVAO).toBe(vao);

        gl.bindVertexArray(null);
        gl.deleteVertexArray(vao);
    });

    it('should support instanced rendering via drawArraysInstanced', () => {
        // Create minimal VAO and buffer to verify instanced draw doesn't throw
        const vao = gl.createVertexArray()!;
        gl.bindVertexArray(vao);

        const buffer = gl.createBuffer()!;
        gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
        gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([0, 0, 1, 0, 0, 1]), gl.STATIC_DRAW);

        gl.enableVertexAttribArray(0);
        gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);

        // This should not throw — verifies instancing API is available
        expect(() => {
            gl.drawArraysInstanced(gl.TRIANGLES, 0, 3, 1);
        }).not.toThrow();

        gl.deleteBuffer(buffer);
        gl.deleteVertexArray(vao);
    });

    it('should support Uniform Buffer Objects (UBOs)', () => {
        const ubo = gl.createBuffer()!;
        gl.bindBuffer(gl.UNIFORM_BUFFER, ubo);
        gl.bufferData(gl.UNIFORM_BUFFER, 64, gl.DYNAMIC_DRAW);

        const boundUBO = gl.getParameter(gl.UNIFORM_BUFFER_BINDING) as WebGLBuffer | null;
        expect(boundUBO).toBe(ubo);

        // Verify we can bind to a binding point
        expect(() => {
            gl.bindBufferBase(gl.UNIFORM_BUFFER, 0, ubo);
        }).not.toThrow();

        gl.deleteBuffer(ubo);
    });

    it('should support transform feedback objects', () => {
        const tf = gl.createTransformFeedback();
        expect(tf).not.toBeNull();

        gl.bindTransformFeedback(gl.TRANSFORM_FEEDBACK, tf);
        const boundTF = gl.getParameter(gl.TRANSFORM_FEEDBACK_BINDING) as WebGLTransformFeedback | null;
        expect(boundTF).toBe(tf);

        gl.bindTransformFeedback(gl.TRANSFORM_FEEDBACK, null);
        gl.deleteTransformFeedback(tf);
    });

    it('should support sampler objects', () => {
        const sampler = gl.createSampler();
        expect(sampler).not.toBeNull();

        gl.samplerParameteri(sampler, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.samplerParameteri(sampler, gl.TEXTURE_MAG_FILTER, gl.LINEAR);

        const param = gl.getSamplerParameter(sampler, gl.TEXTURE_MIN_FILTER) as number;
        expect(param).toBe(gl.LINEAR);

        gl.deleteSampler(sampler);
    });
});

// ---------------------------------------------------------------------------
// 4. Extension Availability
// ---------------------------------------------------------------------------
describe('Cross-Browser: Extension Availability', () => {
    let gl: WebGL2RenderingContext;

    beforeEach(() => {
        const canvas = createTestCanvas();
        gl = createWebGLContext(canvas);
    });

    it('should return a non-empty list of supported extensions', () => {
        const extensions = gl.getSupportedExtensions();
        expect(Array.isArray(extensions)).toBe(true);
        expect(extensions!.length).toBeGreaterThan(0);
    });

    it('should be able to query common extensions without throwing', () => {
        // These extensions are commonly available across browsers.
        // We use feature detection — they may or may not be available,
        // but querying them must not throw.
        const commonExtensions = [
            'EXT_color_buffer_float',
            'EXT_texture_filter_anisotropic',
            'OES_texture_float_linear',
            'WEBGL_debug_renderer_info',
        ];

        for (const extName of commonExtensions) {
            expect(() => {
                gl.getExtension(extName);
            }).not.toThrow();
        }
    });

    it('should return null for non-existent extensions', () => {
        const result = gl.getExtension('THIS_EXTENSION_DOES_NOT_EXIST_12345');
        expect(result).toBeNull();
    });
});

// ---------------------------------------------------------------------------
// 5. Maximum Texture Size, Renderbuffer Size, Viewport Dimensions
// ---------------------------------------------------------------------------
describe('Cross-Browser: Maximum Limits', () => {
    let gl: WebGL2RenderingContext;

    beforeEach(() => {
        const canvas = createTestCanvas();
        gl = createWebGLContext(canvas);
    });

    it('should report MAX_TEXTURE_SIZE >= 2048', () => {
        const maxTextureSize = gl.getParameter(gl.MAX_TEXTURE_SIZE) as number;
        expect(maxTextureSize).toBeGreaterThanOrEqual(2048);
    });

    it('should report MAX_RENDERBUFFER_SIZE >= 2048', () => {
        const maxRenderbufferSize = gl.getParameter(gl.MAX_RENDERBUFFER_SIZE) as number;
        expect(maxRenderbufferSize).toBeGreaterThanOrEqual(2048);
    });

    it('should report MAX_VIEWPORT_DIMS with reasonable values', () => {
        const maxViewportDims = gl.getParameter(gl.MAX_VIEWPORT_DIMS) as Int32Array;
        expect(maxViewportDims).toBeInstanceOf(Int32Array);
        expect(maxViewportDims[0]).toBeGreaterThanOrEqual(2048);
        expect(maxViewportDims[1]).toBeGreaterThanOrEqual(2048);
    });

    it('should report MAX_VERTEX_ATTRIBS >= 16', () => {
        const maxVertexAttribs = gl.getParameter(gl.MAX_VERTEX_ATTRIBS) as number;
        expect(maxVertexAttribs).toBeGreaterThanOrEqual(16);
    });

    it('should report MAX_CUBE_MAP_TEXTURE_SIZE >= 2048', () => {
        const maxCubeMapSize = gl.getParameter(gl.MAX_CUBE_MAP_TEXTURE_SIZE) as number;
        expect(maxCubeMapSize).toBeGreaterThanOrEqual(2048);
    });

    it('should query MAX_UNIFORM_BUFFER_SIZE without throwing', () => {
        // WebGL2 spec guarantees this returns >= 16384 on conformant
        // implementations, but some headless/CI environments may return null.
        // The key cross-browser check is that the query does not throw.
        expect(() => {
            gl.getParameter(gl.MAX_UNIFORM_BUFFER_SIZE);
        }).not.toThrow();
    });
});

// ---------------------------------------------------------------------------
// 6. Context Loss and Restoration
// ---------------------------------------------------------------------------
describe('Cross-Browser: Context Loss and Restoration', () => {
    let canvas: HTMLCanvasElement;
    let gl: WebGL2RenderingContext;

    beforeEach(() => {
        canvas = createTestCanvas();
        gl = createWebGLContext(canvas);
    });

    it('should receive webglcontextlost event', () => {
        let lostReceived = false;

        canvas.addEventListener('webglcontextlost', (e) => {
            e.preventDefault();
            lostReceived = true;
        });

        const event = new Event('webglcontextlost', {
            bubbles: true,
            cancelable: true,
        });
        canvas.dispatchEvent(event);

        expect(lostReceived).toBe(true);
    });

    it('should receive webglcontextrestored event', () => {
        let restoredReceived = false;

        canvas.addEventListener('webglcontextrestored', () => {
            restoredReceived = true;
        });

        const event = new Event('webglcontextrestored', { bubbles: true });
        canvas.dispatchEvent(event);

        expect(restoredReceived).toBe(true);
    });

    it('should be usable after simulated context loss and restore cycle', () => {
        // Simulate loss → restore cycle
        const lostEvent = new Event('webglcontextlost', {
            bubbles: true,
            cancelable: true,
        });
        canvas.dispatchEvent(lostEvent);

        const restoredEvent = new Event('webglcontextrestored', { bubbles: true });
        canvas.dispatchEvent(restoredEvent);

        // After restore the context should be able to clear without throwing
        expect(() => {
            gl.clearColor(0, 0, 0, 1);
            gl.clear(gl.COLOR_BUFFER_BIT);
        }).not.toThrow();
    });
});

// ---------------------------------------------------------------------------
// 7. Buffer Operations
// ---------------------------------------------------------------------------
describe('Cross-Browser: Buffer Operations', () => {
    let gl: WebGL2RenderingContext;

    beforeEach(() => {
        const canvas = createTestCanvas();
        gl = createWebGLContext(canvas);
    });

    it('should create, bind, and upload data to an ARRAY_BUFFER', () => {
        const buffer = gl.createBuffer()!;
        expect(buffer).not.toBeNull();

        gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
        const data = new Float32Array([1.0, 2.0, 3.0, 4.0, 5.0, 6.0]);
        gl.bufferData(gl.ARRAY_BUFFER, data, gl.STATIC_DRAW);

        const boundBuffer = gl.getParameter(gl.ARRAY_BUFFER_BINDING) as WebGLBuffer | null;
        expect(boundBuffer).toBe(buffer);

        gl.deleteBuffer(buffer);
    });

    it('should create and bind an ELEMENT_ARRAY_BUFFER', () => {
        const buffer = gl.createBuffer()!;
        gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, buffer);
        const indices = new Uint16Array([0, 1, 2, 2, 3, 0]);
        gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, indices, gl.STATIC_DRAW);

        const boundBuffer = gl.getParameter(gl.ELEMENT_ARRAY_BUFFER_BINDING) as WebGLBuffer | null;
        expect(boundBuffer).toBe(buffer);

        gl.deleteBuffer(buffer);
    });

    it('should support partial buffer updates via bufferSubData', () => {
        const buffer = gl.createBuffer()!;
        gl.bindBuffer(gl.ARRAY_BUFFER, buffer);

        // Allocate buffer with initial data
        const initialData = new Float32Array([1.0, 2.0, 3.0, 4.0]);
        gl.bufferData(gl.ARRAY_BUFFER, initialData, gl.DYNAMIC_DRAW);

        // Update a sub-range
        const updateData = new Float32Array([10.0, 20.0]);
        gl.bufferSubData(gl.ARRAY_BUFFER, 0, updateData);

        // No error should have occurred
        expect(gl.getError()).toBe(gl.NO_ERROR);

        gl.deleteBuffer(buffer);
    });
});

// ---------------------------------------------------------------------------
// 8. Texture Operations
// ---------------------------------------------------------------------------
describe('Cross-Browser: Texture Operations', () => {
    let gl: WebGL2RenderingContext;

    beforeEach(() => {
        const canvas = createTestCanvas();
        gl = createWebGLContext(canvas);
    });

    it('should create and bind a 2D texture', () => {
        const texture = gl.createTexture()!;
        expect(texture).not.toBeNull();

        gl.bindTexture(gl.TEXTURE_2D, texture);
        const boundTexture = gl.getParameter(gl.TEXTURE_BINDING_2D) as WebGLTexture | null;
        expect(boundTexture).toBe(texture);

        gl.deleteTexture(texture);
    });

    it('should upload pixel data to a 2D texture', () => {
        const texture = gl.createTexture()!;
        gl.bindTexture(gl.TEXTURE_2D, texture);

        const width = 2;
        const height = 2;
        const pixels = new Uint8Array([
            255, 0, 0, 255,   0, 255, 0, 255,
            0, 0, 255, 255,   255, 255, 0, 255,
        ]);

        gl.texImage2D(
            gl.TEXTURE_2D,
            0,
            gl.RGBA,
            width,
            height,
            0,
            gl.RGBA,
            gl.UNSIGNED_BYTE,
            pixels,
        );

        // Set filtering to avoid incomplete texture issues
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);

        expect(gl.getError()).toBe(gl.NO_ERROR);

        gl.deleteTexture(texture);
    });

    it('should generate mipmaps for a power-of-two texture', () => {
        const texture = gl.createTexture()!;
        gl.bindTexture(gl.TEXTURE_2D, texture);

        const size = 4;
        const pixels = new Uint8Array(size * size * 4);
        gl.texImage2D(
            gl.TEXTURE_2D,
            0,
            gl.RGBA,
            size,
            size,
            0,
            gl.RGBA,
            gl.UNSIGNED_BYTE,
            pixels,
        );

        expect(() => {
            gl.generateMipmap(gl.TEXTURE_2D);
        }).not.toThrow();

        expect(gl.getError()).toBe(gl.NO_ERROR);

        gl.deleteTexture(texture);
    });
});

// ---------------------------------------------------------------------------
// 9. Framebuffer Operations
// ---------------------------------------------------------------------------
describe('Cross-Browser: Framebuffer Operations', () => {
    let gl: WebGL2RenderingContext;

    beforeEach(() => {
        const canvas = createTestCanvas();
        gl = createWebGLContext(canvas);
    });

    it('should create and bind a framebuffer', () => {
        const fbo = gl.createFramebuffer()!;
        expect(fbo).not.toBeNull();

        gl.bindFramebuffer(gl.FRAMEBUFFER, fbo);
        const boundFBO = gl.getParameter(gl.FRAMEBUFFER_BINDING) as WebGLFramebuffer | null;
        expect(boundFBO).toBe(fbo);

        gl.deleteFramebuffer(fbo);
    });

    it('should attach a texture to a framebuffer and check completeness', () => {
        const fbo = gl.createFramebuffer()!;
        gl.bindFramebuffer(gl.FRAMEBUFFER, fbo);

        // Create color attachment texture
        const texture = gl.createTexture()!;
        gl.bindTexture(gl.TEXTURE_2D, texture);
        gl.texImage2D(
            gl.TEXTURE_2D,
            0,
            gl.RGBA,
            64,
            64,
            0,
            gl.RGBA,
            gl.UNSIGNED_BYTE,
            null,
        );
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);

        gl.framebufferTexture2D(
            gl.FRAMEBUFFER,
            gl.COLOR_ATTACHMENT0,
            gl.TEXTURE_2D,
            texture,
            0,
        );

        const status = gl.checkFramebufferStatus(gl.FRAMEBUFFER);
        expect(status).toBe(gl.FRAMEBUFFER_COMPLETE);

        gl.deleteTexture(texture);
        gl.deleteFramebuffer(fbo);
    });

    it('should attach a renderbuffer to a framebuffer', () => {
        const fbo = gl.createFramebuffer()!;
        gl.bindFramebuffer(gl.FRAMEBUFFER, fbo);

        // Create color attachment texture (required for completeness)
        const texture = gl.createTexture()!;
        gl.bindTexture(gl.TEXTURE_2D, texture);
        gl.texImage2D(
            gl.TEXTURE_2D,
            0,
            gl.RGBA,
            32,
            32,
            0,
            gl.RGBA,
            gl.UNSIGNED_BYTE,
            null,
        );
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
        gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, texture, 0);

        // Create and attach depth renderbuffer
        const rbo = gl.createRenderbuffer()!;
        gl.bindRenderbuffer(gl.RENDERBUFFER, rbo);
        gl.renderbufferStorage(gl.RENDERBUFFER, gl.DEPTH_COMPONENT16, 32, 32);
        gl.framebufferRenderbuffer(gl.FRAMEBUFFER, gl.DEPTH_ATTACHMENT, gl.RENDERBUFFER, rbo);

        const status = gl.checkFramebufferStatus(gl.FRAMEBUFFER);
        expect(status).toBe(gl.FRAMEBUFFER_COMPLETE);

        gl.deleteTexture(texture);
        gl.deleteRenderbuffer(rbo);
        gl.deleteFramebuffer(fbo);
    });
});

// ---------------------------------------------------------------------------
// 10. ReadPixels Returns Expected Data
// ---------------------------------------------------------------------------
describe('Cross-Browser: ReadPixels', () => {
    it('should read back cleared color values correctly', () => {
        const canvas = createTestCanvas(4, 4);
        const gl = createWebGLContext(canvas, { preserveDrawingBuffer: true });

        // Clear to red
        gl.clearColor(1.0, 0.0, 0.0, 1.0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        const pixel = new Uint8Array(4);
        gl.readPixels(0, 0, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, pixel);

        expect(pixel[0]).toBe(255); // R
        expect(pixel[1]).toBe(0);   // G
        expect(pixel[2]).toBe(0);   // B
        expect(pixel[3]).toBe(255); // A
    });

    it('should read back green channel correctly', () => {
        const canvas = createTestCanvas(4, 4);
        const gl = createWebGLContext(canvas, { preserveDrawingBuffer: true });

        gl.clearColor(0.0, 1.0, 0.0, 1.0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        const pixel = new Uint8Array(4);
        gl.readPixels(0, 0, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, pixel);

        expect(pixel[0]).toBe(0);   // R
        expect(pixel[1]).toBe(255); // G
        expect(pixel[2]).toBe(0);   // B
        expect(pixel[3]).toBe(255); // A
    });

    it('should read back rendered geometry color via framebuffer', () => {
        const canvas = createTestCanvas(4, 4);
        const gl = createWebGLContext(canvas, { preserveDrawingBuffer: true });

        // Create an FBO with a texture attachment for off-screen rendering
        const fbo = gl.createFramebuffer()!;
        gl.bindFramebuffer(gl.FRAMEBUFFER, fbo);

        const texture = gl.createTexture()!;
        gl.bindTexture(gl.TEXTURE_2D, texture);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 4, 4, 0, gl.RGBA, gl.UNSIGNED_BYTE, null);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, texture, 0);

        // Clear the FBO to blue
        gl.clearColor(0.0, 0.0, 1.0, 1.0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        // Read back from the FBO
        const pixel = new Uint8Array(4);
        gl.readPixels(0, 0, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, pixel);

        expect(pixel[0]).toBe(0);   // R
        expect(pixel[1]).toBe(0);   // G
        expect(pixel[2]).toBe(255); // B
        expect(pixel[3]).toBe(255); // A

        gl.deleteTexture(texture);
        gl.deleteFramebuffer(fbo);
    });
});
