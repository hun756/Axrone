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

const checkWebGLSupport = (): boolean => (window as any).checkWebGLSupport();

// ---------------------------------------------------------------------------
// 1. WebGL2 Support Detection
// ---------------------------------------------------------------------------
describe('WebGL2 Support Detection', () => {
    it('should expose WebGL2RenderingContext on window', () => {
        expect(window.WebGL2RenderingContext).toBeDefined();
        expect(typeof window.WebGL2RenderingContext).toBe('function');
    });

    it('should return a context from canvas.getContext("webgl2")', () => {
        const canvas = createTestCanvas();
        const gl = canvas.getContext('webgl2');
        expect(gl).not.toBeNull();
        expect(gl).toBeInstanceOf(WebGL2RenderingContext);
    });

    it('should report WebGL2 support via checkWebGLSupport()', () => {
        expect(checkWebGLSupport()).toBe(true);
    });
});

// ---------------------------------------------------------------------------
// 2. Context Attributes Verification
// ---------------------------------------------------------------------------
describe('Context Attributes Verification', () => {
    it('should return default attributes after context creation', () => {
        const canvas = createTestCanvas();
        const gl = createWebGLContext(canvas);

        const attrs = gl.getContextAttributes();
        expect(attrs).not.toBeNull();
        expect(attrs!.alpha).toBe(true);
        expect(attrs!.depth).toBe(true);
        expect(attrs!.stencil).toBe(true);
        expect(attrs!.antialias).toBe(false);
        expect(attrs!.premultipliedAlpha).toBe(false);
        expect(attrs!.preserveDrawingBuffer).toBe(true);
    });

    it('should respect antialias: false', () => {
        const canvas = createTestCanvas();
        const gl = createWebGLContext(canvas, { antialias: false });
        expect(gl.getContextAttributes()!.antialias).toBe(false);
    });

    it('should respect alpha: true', () => {
        const canvas = createTestCanvas();
        const gl = createWebGLContext(canvas, { alpha: true });
        expect(gl.getContextAttributes()!.alpha).toBe(true);
    });

    it('should respect alpha: false', () => {
        const canvas = createTestCanvas();
        const gl = createWebGLContext(canvas, { alpha: false });
        expect(gl.getContextAttributes()!.alpha).toBe(false);
    });

    it('should support preserveDrawingBuffer: true and allow readPixels after draw', () => {
        const canvas = createTestCanvas(4, 4);
        const gl = createWebGLContext(canvas, { preserveDrawingBuffer: true });

        gl.clearColor(0.0, 1.0, 0.0, 1.0);
        gl.clear(gl.COLOR_BUFFER_BIT);

        const pixel = new Uint8Array(4);
        gl.readPixels(0, 0, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, pixel);

        // Green channel should be 255
        expect(pixel[1]).toBe(255);
        // Red and blue should be 0
        expect(pixel[0]).toBe(0);
        expect(pixel[2]).toBe(0);
    });
});

// ---------------------------------------------------------------------------
// 3. Shader Compilation Compatibility
// ---------------------------------------------------------------------------
describe('Shader Compilation Compatibility', () => {
    let canvas: HTMLCanvasElement;
    let gl: WebGL2RenderingContext;

    beforeEach(() => {
        canvas = createTestCanvas();
        gl = createWebGLContext(canvas);
    });

    it('should compile a GLSL ES 3.00 vertex shader successfully', () => {
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

    it('should compile a GLSL ES 3.00 fragment shader successfully', () => {
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

    it('should compile shader with precision mediump float', () => {
        const src = `#version 300 es
            precision mediump float;
            out vec4 fragColor;
            void main() {
                fragColor = vec4(0.5);
            }
        `;
        const shader = gl.createShader(gl.FRAGMENT_SHADER)!;
        gl.shaderSource(shader, src);
        gl.compileShader(shader);

        expect(gl.getShaderParameter(shader, gl.COMPILE_STATUS)).toBe(true);
        gl.deleteShader(shader);
    });

    it('should compile shader with precision highp float', () => {
        const src = `#version 300 es
            precision highp float;
            out vec4 fragColor;
            void main() {
                fragColor = vec4(1.0);
            }
        `;
        const shader = gl.createShader(gl.FRAGMENT_SHADER)!;
        gl.shaderSource(shader, src);
        gl.compileShader(shader);

        expect(gl.getShaderParameter(shader, gl.COMPILE_STATUS)).toBe(true);
        gl.deleteShader(shader);
    });

    it('should fail compilation when using gl_FragColor (not available in GLSL ES 3.00)', () => {
        const src = `#version 300 es
            void main() {
                gl_FragColor = vec4(1.0, 0.0, 0.0, 1.0);
            }
        `;
        const shader = gl.createShader(gl.FRAGMENT_SHADER)!;
        gl.shaderSource(shader, src);
        gl.compileShader(shader);

        expect(gl.getShaderParameter(shader, gl.COMPILE_STATUS)).toBe(false);
        gl.deleteShader(shader);
    });
});

// ---------------------------------------------------------------------------
// 4. WebGL Constants Verification
// ---------------------------------------------------------------------------
describe('WebGL Constants Verification', () => {
    let gl: WebGL2RenderingContext;

    beforeEach(() => {
        const canvas = createTestCanvas();
        gl = createWebGLContext(canvas);
    });

    it('should expose standard WebGL constants', () => {
        expect(gl.ARRAY_BUFFER).toBe(0x8892);
        expect(gl.ELEMENT_ARRAY_BUFFER).toBe(0x8893);
        expect(gl.TRIANGLES).toBe(0x0004);
        expect(gl.FLOAT).toBe(0x1406);
        expect(gl.STATIC_DRAW).toBe(0x88e4);
        expect(gl.COLOR_BUFFER_BIT).toBe(0x00004000);
        expect(gl.DEPTH_BUFFER_BIT).toBe(0x00000100);
    });

    it('should expose WebGL2-specific constants', () => {
        expect(gl.VERTEX_ARRAY_BINDING).toBe(0x85b5);
        expect(gl.UNIFORM_BUFFER).toBe(0x8a11);
        expect(gl.TRANSFORM_FEEDBACK_BUFFER).toBe(0x8c8e);
        expect(gl.COPY_READ_BUFFER).toBe(0x8f36);
        expect(gl.COPY_WRITE_BUFFER).toBe(0x8f37);
    });
});

// ---------------------------------------------------------------------------
// 5. Extension Availability
// ---------------------------------------------------------------------------
describe('Extension Availability', () => {
    let gl: WebGL2RenderingContext;

    beforeEach(() => {
        const canvas = createTestCanvas();
        gl = createWebGLContext(canvas);
    });

    it('should report supported extensions as a non-empty array', () => {
        const extensions = gl.getSupportedExtensions();
        expect(Array.isArray(extensions)).toBe(true);
        expect(extensions!.length).toBeGreaterThan(0);
    });

    it('should attempt to load EXT_texture_filter_anisotropic (informational)', () => {
        const ext =
            gl.getExtension('EXT_texture_filter_anisotropic') ??
            gl.getExtension('MOZ_EXT_texture_filter_anisotropic') ??
            gl.getExtension('WEBKIT_EXT_texture_filter_anisotropic');

        if (ext) {
            expect(ext).toBeDefined();
        }
        // Not failing — extension may not be available in headless environments
    });

    it('should attempt to load OES_texture_float_linear (informational)', () => {
        const ext = gl.getExtension('OES_texture_float_linear');

        if (ext) {
            expect(ext).toBeDefined();
        }
        // Not failing — extension may not be available in headless environments
    });
});

// ---------------------------------------------------------------------------
// 6. Context Loss Handling
// ---------------------------------------------------------------------------
describe('Context Loss Handling', () => {
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

        const event = new Event('webglcontextlost', { bubbles: true });
        // The event must be cancelable for preventDefault to work
        const cancelableEvent = new Event('webglcontextlost', {
            bubbles: true,
            cancelable: true,
        });
        canvas.dispatchEvent(cancelableEvent);

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

    it('should be usable after context restore', () => {
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
// 7. Maximum Limits Query
// ---------------------------------------------------------------------------
describe('Maximum Limits Query', () => {
    let gl: WebGL2RenderingContext;

    beforeEach(() => {
        const canvas = createTestCanvas();
        gl = createWebGLContext(canvas);
    });

    it('should report MAX_TEXTURE_SIZE >= 2048', () => {
        const maxTextureSize = gl.getParameter(gl.MAX_TEXTURE_SIZE) as number;
        expect(maxTextureSize).toBeGreaterThanOrEqual(2048);
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
});
