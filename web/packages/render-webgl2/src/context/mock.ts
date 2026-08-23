import { GLContext } from './gl-context';
import type { IGLContext } from './types';

export interface MockGLOptions {
    readonly canvas?: HTMLCanvasElement;
    readonly attributes?: WebGLContextAttributes;
    readonly extensions?: Record<string, unknown | null>;
    readonly parameters?: Record<number, unknown>;
}

const DEFAULT_PARAMETERS: Record<number, unknown> = {
    0x1f02: 'WebGL 2.0 (Mock)',
    0x8b8c: 'WebGL GLSL ES 3.00 (Mock)',
    0x1f00: 'MockVendor',
    0x1f01: 'MockRenderer',
    0x0d3a: new Int32Array([16384, 16384]),
    0x846d: new Float32Array([1, 1]),
    0x846e: new Float32Array([1, 1]),
    0x0d33: 16384,
    0x851c: 16384,
    0x88ff: 2048,
    0x8073: 2048,
    0x8869: 16,
    0x8dfc: 15,
    0x8dfd: 16,
    0x8dfb: 16,
    0x8b4d: 32,
    0x8872: 16,
    0x84e8: 16384,
};

export const createMockCanvas = (): HTMLCanvasElement => {
    const canvas = {
        width: 800,
        height: 600,
        addEventListener: () => {},
        removeEventListener: () => {},
        dispatchEvent: () => true,
        getContext: () => null,
    } as unknown as HTMLCanvasElement;
    return canvas;
};

export const createMockGL = (options: MockGLOptions = {}): WebGL2RenderingContext => {
    const canvas = options.canvas ?? createMockCanvas();
    const attributes: WebGLContextAttributes = {
        alpha: true,
        depth: true,
        stencil: false,
        antialias: true,
        premultipliedAlpha: true,
        preserveDrawingBuffer: false,
        ...options.attributes,
    };

    const extensions = options.extensions ?? {};
    const parameters = { ...DEFAULT_PARAMETERS, ...options.parameters };

    const gl = new Proxy(
        {
            canvas,
            drawingBufferWidth: canvas.width,
            drawingBufferHeight: canvas.height,

            // Constants
            ARRAY_BUFFER: 0x8892,
            ELEMENT_ARRAY_BUFFER: 0x8893,
            COPY_READ_BUFFER: 0x8f36,
            COPY_WRITE_BUFFER: 0x8f37,
            TRANSFORM_FEEDBACK_BUFFER: 0x8c8e,
            UNIFORM_BUFFER: 0x8a11,
            PIXEL_PACK_BUFFER: 0x88eb,
            PIXEL_UNPACK_BUFFER: 0x88ec,
            STATIC_DRAW: 0x88e4,
            DYNAMIC_DRAW: 0x88e8,
            STREAM_DRAW: 0x88e0,
            STATIC_READ: 0x88e5,
            DYNAMIC_READ: 0x88e9,
            STREAM_READ: 0x88e1,
            STATIC_COPY: 0x88e6,
            DYNAMIC_COPY: 0x88ea,
            STREAM_COPY: 0x88e2,
            TEXTURE_2D: 0x0de1,
            TEXTURE_CUBE_MAP: 0x8513,
            TEXTURE_2D_ARRAY: 0x8c1a,
            TEXTURE_3D: 0x806f,
            TEXTURE0: 0x84c0,
            RGB: 0x1907,
            RGBA: 0x1908,
            RED: 0x1903,
            RG: 0x8227,
            RGBA8: 0x8058,
            RGB8: 0x8051,
            RG8: 0x822b,
            R8: 0x8229,
            RGBA16F: 0x881a,
            RGB16F: 0x881b,
            RG16F: 0x822f,
            R16F: 0x822d,
            RGBA32F: 0x8814,
            RGB32F: 0x8815,
            RG32F: 0x8230,
            R32F: 0x822e,
            DEPTH_COMPONENT16: 0x81a5,
            DEPTH_COMPONENT24: 0x81a6,
            DEPTH_COMPONENT32F: 0x8cac,
            DEPTH24_STENCIL8: 0x88f0,
            DEPTH32F_STENCIL8: 0x8cad,
            COLOR_ATTACHMENT0: 0x8ce0,
            DEPTH_ATTACHMENT: 0x8d00,
            STENCIL_ATTACHMENT: 0x8d20,
            DEPTH_STENCIL_ATTACHMENT: 0x821a,
            FRAMEBUFFER: 0x8d40,
            READ_FRAMEBUFFER: 0x8ca8,
            DRAW_FRAMEBUFFER: 0x8ca9,
            RENDERBUFFER: 0x8d41,
            COLOR_BUFFER_BIT: 0x4000,
            DEPTH_BUFFER_BIT: 0x0100,
            STENCIL_BUFFER_BIT: 0x0400,
            NEAREST: 0x2600,
            LINEAR: 0x2601,
            CLAMP_TO_EDGE: 0x812f,
            REPEAT: 0x2901,
            MIRRORED_REPEAT: 0x8370,
            UNSIGNED_BYTE: 0x1401,
            UNSIGNED_SHORT: 0x1403,
            UNSIGNED_INT: 0x1405,
            FLOAT: 0x1406,
            HALF_FLOAT: 0x140b,
            BYTE: 0x1400,
            SHORT: 0x1402,
            INT: 0x1404,
            FRAMEBUFFER_COMPLETE: 0x8cd5,
            TRIANGLES: 0x0004,
            LINES: 0x0001,
            POINTS: 0x0000,
            LINE_LOOP: 0x0002,
            LINE_STRIP: 0x0003,
            TRIANGLE_STRIP: 0x0005,
            TRIANGLE_FAN: 0x0006,
            VERTEX_SHADER: 0x8b31,
            FRAGMENT_SHADER: 0x8b30,
            COMPILE_STATUS: 0x8b81,
            LINK_STATUS: 0x8b82,
            VERSION: 0x1f02,
            SHADING_LANGUAGE_VERSION: 0x8b8c,
            VENDOR: 0x1f00,
            RENDERER: 0x1f01,
            MAX_TEXTURE_SIZE: 0x0d33,
            MAX_CUBE_MAP_TEXTURE_SIZE: 0x851c,
            MAX_ARRAY_TEXTURE_LAYERS: 0x88ff,
            MAX_3D_TEXTURE_SIZE: 0x8073,
            MAX_VERTEX_ATTRIBS: 0x8869,
            MAX_VARYING_VECTORS: 0x8dfc,
            MAX_FRAGMENT_UNIFORM_VECTORS: 0x8dfd,
            MAX_VERTEX_UNIFORM_VECTORS: 0x8dfb,
            MAX_COMBINED_TEXTURE_IMAGE_UNITS: 0x8b4d,
            MAX_TEXTURE_IMAGE_UNITS: 0x8872,
            MAX_RENDERBUFFER_SIZE: 0x84e8,
            MAX_VIEWPORT_DIMS: 0x0d3a,
            ALIASED_LINE_WIDTH_RANGE: 0x846e,
            ALIASED_POINT_SIZE_RANGE: 0x846d,
            BLEND: 0x0be2,
            CULL_FACE: 0x0b44,
            DEPTH_TEST: 0x0b71,
            SCISSOR_TEST: 0x0c11,
            STENCIL_TEST: 0x0b90,
            INVALID_INDEX: 0xffffffff,
            BOOL: 0x8b56,
            SAMPLER_2D: 0x8b5e,
            SAMPLER_CUBE: 0x8b60,
            SAMPLER_2D_ARRAY: 0x8dc1,
            TEXTURE_MIN_FILTER: 0x2801,
            TEXTURE_MAG_FILTER: 0x2800,
            TEXTURE_WRAP_S: 0x2802,
            TEXTURE_WRAP_T: 0x2803,
            TEXTURE_WRAP_R: 0x8072,
            TEXTURE_BASE_LEVEL: 0x813c,
            TEXTURE_MAX_LEVEL: 0x813d,

            // Methods
            getParameter(pname: number) {
                if (pname in parameters) return parameters[pname];
                if (pname === 0x0d33) return 16384;
                if (pname === 0x851c) return 16384;
                if (pname === 0x88ff) return 2048;
                if (pname === 0x8073) return 2048;
                if (pname === 0x8869) return 16;
                if (pname === 0x8dfc) return 15;
                if (pname === 0x8dfd) return 16;
                if (pname === 0x8dfb) return 16;
                if (pname === 0x8b4d) return 32;
                if (pname === 0x8872) return 16;
                if (pname === 0x84e8) return 16384;
                return 0;
            },
            getExtension(name: string) {
                if (name in extensions) return extensions[name];
                return null;
            },
            getContextAttributes() {
                return { ...attributes };
            },
            isContextLost() {
                return false;
            },
            getError() {
                return 0;
            },
            flush() {},
            finish() {},
            createBuffer() {
                return {} as WebGLBuffer;
            },
            deleteBuffer() {},
            bindBuffer() {},
            bufferData() {},
            bufferSubData() {},
            copyBufferSubData() {},
            getBufferSubData() {},
            createTexture() {
                return {} as WebGLTexture;
            },
            deleteTexture() {},
            bindTexture() {},
            texStorage2D() {},
            texStorage3D() {},
            texParameteri() {},
            texImage2D() {},
            generateMipmap() {},
            createFramebuffer() {
                return {} as WebGLFramebuffer;
            },
            deleteFramebuffer() {},
            bindFramebuffer() {},
            framebufferTexture2D() {},
            checkFramebufferStatus() {
                return 0x8cd5;
            },
            drawBuffers() {},
            createRenderbuffer() {
                return {} as WebGLRenderbuffer;
            },
            deleteRenderbuffer() {},
            bindRenderbuffer() {},
            renderbufferStorageMultisample() {},
            createVertexArray() {
                return {} as WebGLVertexArrayObject;
            },
            deleteVertexArray() {},
            bindVertexArray() {},
            enableVertexAttribArray() {},
            vertexAttribPointer() {},
            vertexAttribDivisor() {},
            createShader() {
                return {} as WebGLShader;
            },
            deleteShader() {},
            shaderSource() {},
            compileShader() {},
            getShaderParameter() {
                return true;
            },
            getShaderInfoLog() {
                return '';
            },
            getShaderSource() {
                return '';
            },
            createProgram() {
                return {} as WebGLProgram;
            },
            deleteProgram() {},
            attachShader() {},
            linkProgram() {},
            getProgramParameter() {
                return true;
            },
            getProgramInfoLog() {
                return '';
            },
            getAttachedShaders() {
                return [];
            },
            getUniformLocation() {
                return {} as WebGLUniformLocation;
            },
            getAttribLocation() {
                return 0;
            },
            getUniformBlockIndex() {
                return 0xffffffff;
            },
            useProgram() {},
            activeTexture() {},
            bindSampler() {},
            enable() {},
            disable() {},
            viewport() {},
            scissor() {},
            clear() {},
            clearColor() {},
            drawArrays() {},
            drawArraysInstanced() {},
            drawElements() {},
            drawElementsInstanced() {},
            blitFramebuffer() {},
            readPixels() {},
            samplerParameterf() {},
            samplerParameteri() {},
            bindSampler() {},
            createSampler() {
                return {} as WebGLSampler;
            },
            deleteSampler() {},
            isSampler() { return false; },
            clearBufferfv() {},
            clearBufferfi() {},
            clearBufferiv() {},
            clearBufferuiv() {},
            fenceSync() { return {} as WebGLSync; },
            deleteSync() {},
            isSync() { return false; },
            clientWaitSync() { return 0x911f; },
            waitSync() {},
            getSyncParameter() { return 0; },
            getActiveUniform() { return null; },
            getActiveAttrib() { return null; },
        },
        {
            get(target, prop) {
                if (prop in target) return (target as Record<string | symbol, unknown>)[prop];
                if (typeof prop === 'string') {
                    if (prop.startsWith('MAX_')) return 4096;
                    if (/^[A-Z_]+$/.test(prop)) return 0;
                    if (/^[a-z]/.test(prop)) return () => {};
                }
                return 0;
            },
        }
    ) as unknown as WebGL2RenderingContext;

    return gl;
};

export const createMockGLContext = (options: MockGLOptions = {}): IGLContext => {
    const gl = createMockGL(options);
    const canvas = (gl.canvas as HTMLCanvasElement) ?? createMockCanvas();
    return new GLContext(gl, canvas, 'en', options.attributes ?? {}, true, true);
};

export const createMockGLContextWithGL = (
    gl: WebGL2RenderingContext,
    canvas?: HTMLCanvasElement
): IGLContext => {
    const resolvedCanvas = canvas ?? (gl.canvas as HTMLCanvasElement) ?? createMockCanvas();
    return new GLContext(gl, resolvedCanvas, 'en', {}, true, true);
};
