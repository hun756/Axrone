import { describe, expect, it, vi, beforeEach } from 'vitest';
import {
    FramebufferError,
    Texture,
    Renderbuffer,
    Framebuffer,
    FramebufferFactory,
    createFramebufferFactory,
    createRenderTarget,
    createShadowMap,
    createMultisampledRenderTarget,
    type TextureOptions,
    type RenderbufferOptions,
    type FramebufferOptions,
    type GLTextureFormat,
} from '../framebuffer';

function createMockGL() {
    const mockTexture = {} as WebGLTexture;
    const mockRenderbuffer = {} as WebGLRenderbuffer;
    const mockFramebuffer = {} as WebGLFramebuffer;
    return {
        TEXTURE_2D: 0x0de1,
        TEXTURE_CUBE_MAP: 0x8513,
        TEXTURE_2D_ARRAY: 0x8c1a,
        TEXTURE_3D: 0x806f,
        RGB: 0x1907,
        RGBA: 0x1908,
        RED: 0x1903,
        RG: 0x8227,
        RGBA8: 0x8058,
        RGB8: 0x8051,
        RGBA16F: 0x881a,
        RGB16F: 0x881b,
        RGBA32F: 0x8814,
        RGB32F: 0x8815,
        R8: 0x8229,
        R16F: 0x822d,
        R32F: 0x822e,
        RG8: 0x822b,
        RG16F: 0x822f,
        RG32F: 0x8230,
        DEPTH_COMPONENT16: 0x81a5,
        DEPTH_COMPONENT24: 0x81a6,
        DEPTH_COMPONENT32F: 0x8cac,
        DEPTH24_STENCIL8: 0x88f0,
        DEPTH32F_STENCIL8: 0x8cad,
        COLOR_ATTACHMENT0: 0x8ce0,
        COLOR_ATTACHMENT1: 0x8ce1,
        COLOR_ATTACHMENT15: 0x8cef,
        DEPTH_ATTACHMENT: 0x8d00,
        STENCIL_ATTACHMENT: 0x8d20,
        DEPTH_STENCIL_ATTACHMENT: 0x821a,
        NEAREST: 0x2600,
        LINEAR: 0x2601,
        CLAMP_TO_EDGE: 0x812f,
        REPEAT: 0x2901,
        MIRRORED_REPEAT: 0x8370,
        UNSIGNED_BYTE: 0x1401,
        FLOAT: 0x1406,
        HALF_FLOAT: 0x140b,
        UNSIGNED_SHORT: 0x1403,
        UNSIGNED_INT: 0x1405,
        UNSIGNED_INT_24_8: 0x84fa,
        FLOAT_32_UNSIGNED_INT_24_8_REV: 0x8dad,
        FRAMEBUFFER_COMPLETE: 0x8cd5,
        FRAMEBUFFER_INCOMPLETE_ATTACHMENT: 0x8cd6,
        FRAMEBUFFER: 0x8d40,
        READ_FRAMEBUFFER: 0x8ca8,
        DRAW_FRAMEBUFFER: 0x8ca9,
        RENDERBUFFER: 0x8d41,
        TEXTURE_MIN_FILTER: 0x2801,
        TEXTURE_MAG_FILTER: 0x2800,
        TEXTURE_WRAP_S: 0x2802,
        TEXTURE_WRAP_T: 0x2803,
        COLOR_BUFFER_BIT: 0x4000,
        DEPTH_BUFFER_BIT: 0x100,
        STENCIL_BUFFER_BIT: 0x400,
        DEPTH_COMPONENT: 0x1902,
        DEPTH_STENCIL: 0x84f9,
        STENCIL_INDEX8: 0x8d4e,
        createTexture: vi.fn(() => mockTexture),
        deleteTexture: vi.fn(),
        bindTexture: vi.fn(),
        texStorage2D: vi.fn(),
        texSubImage2D: vi.fn(),
        texParameteri: vi.fn(),
        generateMipmap: vi.fn(),
        createRenderbuffer: vi.fn(() => mockRenderbuffer),
        deleteRenderbuffer: vi.fn(),
        bindRenderbuffer: vi.fn(),
        renderbufferStorage: vi.fn(),
        renderbufferStorageMultisample: vi.fn(),
        createFramebuffer: vi.fn(() => mockFramebuffer),
        deleteFramebuffer: vi.fn(),
        bindFramebuffer: vi.fn(),
        checkFramebufferStatus: vi.fn(() => 0x8cd5), // FRAMEBUFFER_COMPLETE
        framebufferTexture2D: vi.fn(),
        framebufferTextureLayer: vi.fn(),
        framebufferRenderbuffer: vi.fn(),
        readBuffer: vi.fn(),
        readPixels: vi.fn(),
        blitFramebuffer: vi.fn(),
        viewport: vi.fn(),
        clearColor: vi.fn(),
        clearDepth: vi.fn(),
        clearStencil: vi.fn(),
        clear: vi.fn(),
        getExtension: vi.fn(() => null),
    } as unknown as WebGL2RenderingContext;
}

// ---------------------------------------------------------------------------
// FramebufferError
// ---------------------------------------------------------------------------
describe('FramebufferError', () => {
    it('should store the raw message and code', () => {
        const err = new FramebufferError('something went wrong', 'INVALID_OPERATION');
        // The `public readonly message` parameter property overwrites the super() formatted string
        expect(err.message).toBe('something went wrong');
        expect(err.code).toBe('INVALID_OPERATION');
    });

    it('should store code and original message separately', () => {
        const err = new FramebufferError('out of memory', 'OUT_OF_MEMORY');
        expect(err.code).toBe('OUT_OF_MEMORY');
        expect(err.message).toBe('out of memory');
    });

    it('should pass formatted string to Error super constructor', () => {
        const err = new FramebufferError('test message', 'INVALID_VALUE');
        // The formatted string is passed to super() but overwritten by the parameter property.
        // We can verify the error is a proper Error instance with the raw message.
        expect(err).toBeInstanceOf(Error);
        expect(err.message).toBe('test message');
    });

    it('should store optional cause', () => {
        const cause = new Error('root cause');
        const err = new FramebufferError('wrapped', 'CONTEXT_LOST', cause);
        expect(err.cause).toBe(cause);
    });

    it('should be an instance of Error and FramebufferError', () => {
        const err = new FramebufferError('test', 'INVALID_VALUE');
        expect(err).toBeInstanceOf(Error);
        expect(err).toBeInstanceOf(FramebufferError);
    });

    it('should have correct prototype chain', () => {
        const err = new FramebufferError('test', 'INVALID_VALUE');
        expect(Object.getPrototypeOf(err)).toBe(FramebufferError.prototype);
        expect(err.name).toBe('Error'); // default Error name
    });
});

// ---------------------------------------------------------------------------
// Texture
// ---------------------------------------------------------------------------
describe('Texture', () => {
    let gl: WebGL2RenderingContext;

    beforeEach(() => {
        gl = createMockGL();
    });

    function createTexture(opts: Partial<TextureOptions> = {}): Texture {
        return new Texture(gl, gl.TEXTURE_2D, {
            width: 256,
            height: 256,
            ...opts,
        });
    }

    describe('constructor', () => {
        it('should create a GL texture via gl.createTexture', () => {
            createTexture();
            expect(gl.createTexture).toHaveBeenCalled();
        });

        it('should call texStorage2D with correct parameters', () => {
            createTexture({ width: 128, height: 64, internalFormat: gl.RGBA8 });
            expect(gl.texStorage2D).toHaveBeenCalledWith(
                gl.TEXTURE_2D,
                1,
                gl.RGBA8,
                128,
                64
            );
        });

        it('should set filter and wrap parameters', () => {
            createTexture({
                minFilter: gl.NEAREST,
                magFilter: gl.NEAREST,
                wrapS: gl.REPEAT,
                wrapT: gl.MIRRORED_REPEAT,
            });
            expect(gl.texParameteri).toHaveBeenCalledWith(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
            expect(gl.texParameteri).toHaveBeenCalledWith(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
            expect(gl.texParameteri).toHaveBeenCalledWith(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.REPEAT);
            expect(gl.texParameteri).toHaveBeenCalledWith(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.MIRRORED_REPEAT);
        });

        it('should default to LINEAR filter and CLAMP_TO_EDGE wrap', () => {
            createTexture();
            expect(gl.texParameteri).toHaveBeenCalledWith(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
            expect(gl.texParameteri).toHaveBeenCalledWith(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
            expect(gl.texParameteri).toHaveBeenCalledWith(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
            expect(gl.texParameteri).toHaveBeenCalledWith(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        });

        it('should throw OUT_OF_MEMORY if createTexture returns null', () => {
            (gl.createTexture as ReturnType<typeof vi.fn>).mockReturnValueOnce(null);
            try {
                createTexture();
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('OUT_OF_MEMORY');
                expect((e as FramebufferError).message).toContain('Failed to create WebGLTexture');
            }
        });

        it('should throw UNSUPPORTED_OPERATION if samples > 0', () => {
            try {
                createTexture({ samples: 4 });
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('UNSUPPORTED_OPERATION');
            }
        });

        it('should call generateMipmap when option is true', () => {
            createTexture({ generateMipmap: true });
            expect(gl.generateMipmap).toHaveBeenCalledWith(gl.TEXTURE_2D);
        });

        it('should not call generateMipmap when option is false', () => {
            createTexture({ generateMipmap: false });
            expect(gl.generateMipmap).not.toHaveBeenCalled();
        });

        it('should derive format from internalFormat when format not specified', () => {
            const tex = createTexture({ internalFormat: gl.RGB8 });
            expect(tex.format).toBe(gl.RGB);
        });

        it('should derive type from internalFormat when type not specified', () => {
            const tex = createTexture({ internalFormat: gl.RGBA16F });
            expect(tex.type).toBe(gl.HALF_FLOAT);
        });

        it('should use explicit format and type when provided', () => {
            const tex = createTexture({
                internalFormat: gl.RGBA8,
                format: gl.RGBA,
                type: gl.UNSIGNED_BYTE,
            });
            expect(tex.format).toBe(gl.RGBA);
            expect(tex.type).toBe(gl.UNSIGNED_BYTE);
        });
    });

    describe('properties', () => {
        it('should expose id, target, width, height, format, internalFormat, type, samples, label, isDisposed', () => {
            const tex = createTexture({
                width: 512,
                height: 128,
                internalFormat: gl.RGBA8,
                label: 'myTexture',
            });
            expect(tex.id).toBeDefined();
            expect(tex.target).toBe(gl.TEXTURE_2D);
            expect(tex.width).toBe(512);
            expect(tex.height).toBe(128);
            expect(tex.format).toBe(gl.RGBA);
            expect(tex.internalFormat).toBe(gl.RGBA8);
            expect(tex.type).toBe(gl.UNSIGNED_BYTE);
            expect(tex.samples).toBe(0);
            expect(tex.label).toBe('myTexture');
            expect(tex.isDisposed).toBe(false);
        });

        it('should have null label by default', () => {
            const tex = createTexture();
            expect(tex.label).toBeNull();
        });
    });

    describe('bind / unbind', () => {
        it('bind should call gl.bindTexture with target and texture id', () => {
            const tex = createTexture();
            (gl.bindTexture as ReturnType<typeof vi.fn>).mockClear();
            tex.bind();
            expect(gl.bindTexture).toHaveBeenCalledWith(gl.TEXTURE_2D, expect.anything());
        });

        it('unbind should call gl.bindTexture with target and null', () => {
            const tex = createTexture();
            (gl.bindTexture as ReturnType<typeof vi.fn>).mockClear();
            tex.unbind();
            expect(gl.bindTexture).toHaveBeenCalledWith(gl.TEXTURE_2D, null);
        });

        it('bind should return the texture for chaining', () => {
            const tex = createTexture();
            expect(tex.bind()).toBe(tex);
        });

        it('unbind should return the texture for chaining', () => {
            const tex = createTexture();
            expect(tex.unbind()).toBe(tex);
        });
    });

    describe('resize', () => {
        it('should update width and height and call texStorage2D', () => {
            const tex = createTexture({ width: 64, height: 64 });
            (gl.texStorage2D as ReturnType<typeof vi.fn>).mockClear();
            tex.resize(128, 256);
            expect(tex.width).toBe(128);
            expect(tex.height).toBe(256);
            expect(gl.texStorage2D).toHaveBeenCalled();
        });

        it('should throw INVALID_VALUE for zero or negative dimensions', () => {
            const tex = createTexture();
            try {
                tex.resize(0, 100);
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('INVALID_VALUE');
            }
            try {
                tex.resize(100, -1);
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('INVALID_VALUE');
            }
        });

        it('should return the texture for chaining', () => {
            const tex = createTexture();
            expect(tex.resize(100, 100)).toBe(tex);
        });
    });

    describe('generateMipmap', () => {
        it('should call gl.generateMipmap with the correct target', () => {
            const tex = createTexture();
            (gl.generateMipmap as ReturnType<typeof vi.fn>).mockClear();
            tex.generateMipmap();
            expect(gl.generateMipmap).toHaveBeenCalledWith(gl.TEXTURE_2D);
        });

        it('should return the texture for chaining', () => {
            const tex = createTexture();
            expect(tex.generateMipmap()).toBe(tex);
        });
    });

    describe('setData', () => {
        it('should handle ArrayBufferView data', () => {
            const tex = createTexture();
            const data = new Uint8Array(256 * 256 * 4);
            tex.setData(data);
            expect(gl.texSubImage2D).toHaveBeenCalled();
        });

        it('should handle null data', () => {
            const tex = createTexture();
            tex.setData(null);
            expect(gl.texSubImage2D).toHaveBeenCalled();
            // null data passes null as the last argument
            const call = (gl.texSubImage2D as ReturnType<typeof vi.fn>).mock.calls[0];
            expect(call[call.length - 1]).toBeNull();
        });

        it('should return the texture for chaining', () => {
            const tex = createTexture();
            expect(tex.setData(null)).toBe(tex);
        });
    });

    describe('getPixels', () => {
        it('should throw UNSUPPORTED_OPERATION', () => {
            const tex = createTexture();
            const output = new Uint8Array(100);
            try {
                tex.getPixels(output);
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('UNSUPPORTED_OPERATION');
            }
        });
    });

    describe('dispose', () => {
        it('should call gl.deleteTexture', () => {
            const tex = createTexture();
            tex.dispose();
            expect(gl.deleteTexture).toHaveBeenCalled();
        });

        it('should set isDisposed to true', () => {
            const tex = createTexture();
            expect(tex.isDisposed).toBe(false);
            tex.dispose();
            expect(tex.isDisposed).toBe(true);
        });

        it('should be idempotent — calling dispose twice only deletes once', () => {
            const tex = createTexture();
            tex.dispose();
            tex.dispose();
            expect(gl.deleteTexture).toHaveBeenCalledTimes(1);
        });

        it('should throw TEXTURE_ALREADY_DISPOSED when accessing id after dispose', () => {
            const tex = createTexture();
            tex.dispose();
            try {
                void tex.id;
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('TEXTURE_ALREADY_DISPOSED');
            }
        });

        it('should throw TEXTURE_ALREADY_DISPOSED when calling bind after dispose', () => {
            const tex = createTexture();
            tex.dispose();
            try {
                tex.bind();
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('TEXTURE_ALREADY_DISPOSED');
            }
        });

        it('should throw TEXTURE_ALREADY_DISPOSED when calling unbind after dispose', () => {
            const tex = createTexture();
            tex.dispose();
            try {
                tex.unbind();
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('TEXTURE_ALREADY_DISPOSED');
            }
        });

        it('should throw TEXTURE_ALREADY_DISPOSED when calling resize after dispose', () => {
            const tex = createTexture();
            tex.dispose();
            try {
                tex.resize(10, 10);
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('TEXTURE_ALREADY_DISPOSED');
            }
        });

        it('should throw TEXTURE_ALREADY_DISPOSED when calling generateMipmap after dispose', () => {
            const tex = createTexture();
            tex.dispose();
            try {
                tex.generateMipmap();
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('TEXTURE_ALREADY_DISPOSED');
            }
        });

        it('should throw TEXTURE_ALREADY_DISPOSED when calling setData after dispose', () => {
            const tex = createTexture();
            tex.dispose();
            try {
                tex.setData(null);
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('TEXTURE_ALREADY_DISPOSED');
            }
        });

        it('should throw TEXTURE_ALREADY_DISPOSED when calling getPixels after dispose', () => {
            const tex = createTexture();
            tex.dispose();
            try {
                tex.getPixels(new Uint8Array(10));
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('TEXTURE_ALREADY_DISPOSED');
            }
        });
    });

    describe('texture targets', () => {
        it('should support TEXTURE_CUBE_MAP target', () => {
            const tex = new Texture(gl, gl.TEXTURE_CUBE_MAP, { width: 64, height: 64 });
            expect(tex.target).toBe(gl.TEXTURE_CUBE_MAP);
            expect(gl.texStorage2D).toHaveBeenCalledWith(gl.TEXTURE_CUBE_MAP, 1, gl.RGBA8, 64, 64);
        });
    });

    describe('format type inference', () => {
        it('should infer UNSIGNED_BYTE for RGBA8', () => {
            const tex = createTexture({ internalFormat: gl.RGBA8 });
            expect(tex.type).toBe(gl.UNSIGNED_BYTE);
        });

        it('should infer HALF_FLOAT for RGBA16F', () => {
            const tex = createTexture({ internalFormat: gl.RGBA16F });
            expect(tex.type).toBe(gl.HALF_FLOAT);
        });

        it('should infer FLOAT for RGBA32F', () => {
            const tex = createTexture({ internalFormat: gl.RGBA32F });
            expect(tex.type).toBe(gl.FLOAT);
        });

        it('should infer UNSIGNED_SHORT for DEPTH_COMPONENT16', () => {
            const tex = createTexture({ internalFormat: gl.DEPTH_COMPONENT16 });
            expect(tex.type).toBe(gl.UNSIGNED_SHORT);
        });

        it('should infer UNSIGNED_INT for DEPTH_COMPONENT24', () => {
            const tex = createTexture({ internalFormat: gl.DEPTH_COMPONENT24 });
            expect(tex.type).toBe(gl.UNSIGNED_INT);
        });

        it('should infer FLOAT for DEPTH_COMPONENT32F', () => {
            const tex = createTexture({ internalFormat: gl.DEPTH_COMPONENT32F });
            expect(tex.type).toBe(gl.FLOAT);
        });

        it('should infer UNSIGNED_INT_24_8 for DEPTH24_STENCIL8', () => {
            const tex = createTexture({ internalFormat: gl.DEPTH24_STENCIL8 });
            expect(tex.type).toBe(gl.UNSIGNED_INT_24_8);
        });

        it('should infer FLOAT_32_UNSIGNED_INT_24_8_REV for DEPTH32F_STENCIL8', () => {
            const tex = createTexture({ internalFormat: gl.DEPTH32F_STENCIL8 });
            expect(tex.type).toBe(gl.FLOAT_32_UNSIGNED_INT_24_8_REV);
        });
    });

    describe('format pixel format inference', () => {
        it('should infer RGBA for RGBA8', () => {
            const tex = createTexture({ internalFormat: gl.RGBA8 });
            expect(tex.format).toBe(gl.RGBA);
        });

        it('should infer RGB for RGB8', () => {
            const tex = createTexture({ internalFormat: gl.RGB8 });
            expect(tex.format).toBe(gl.RGB);
        });

        it('should infer RED for R8', () => {
            const tex = createTexture({ internalFormat: gl.R8 });
            expect(tex.format).toBe(gl.RED);
        });

        it('should infer RG for RG8', () => {
            const tex = createTexture({ internalFormat: gl.RG8 });
            expect(tex.format).toBe(gl.RG);
        });

        it('should infer DEPTH_COMPONENT for DEPTH_COMPONENT24', () => {
            const tex = createTexture({ internalFormat: gl.DEPTH_COMPONENT24 });
            expect(tex.format).toBe(gl.DEPTH_COMPONENT);
        });

        it('should infer DEPTH_STENCIL for DEPTH24_STENCIL8', () => {
            const tex = createTexture({ internalFormat: gl.DEPTH24_STENCIL8 });
            expect(tex.format).toBe(gl.DEPTH_STENCIL);
        });
    });
});

// ---------------------------------------------------------------------------
// Renderbuffer
// ---------------------------------------------------------------------------
describe('Renderbuffer', () => {
    let gl: WebGL2RenderingContext;

    beforeEach(() => {
        gl = createMockGL();
    });

    function createRenderbuffer(opts: Partial<RenderbufferOptions> = {}): Renderbuffer {
        return new Renderbuffer(gl, {
            width: 256,
            height: 256,
            internalFormat: gl.DEPTH_COMPONENT24,
            ...opts,
        });
    }

    describe('constructor', () => {
        it('should create a GL renderbuffer', () => {
            createRenderbuffer();
            expect(gl.createRenderbuffer).toHaveBeenCalled();
        });

        it('should call renderbufferStorage for non-multisampled', () => {
            createRenderbuffer({ width: 128, height: 64, internalFormat: gl.DEPTH_COMPONENT24 });
            expect(gl.renderbufferStorage).toHaveBeenCalledWith(
                gl.RENDERBUFFER,
                gl.DEPTH_COMPONENT24,
                128,
                64
            );
        });

        it('should call renderbufferStorageMultisample for multisampled', () => {
            createRenderbuffer({ width: 128, height: 64, internalFormat: gl.DEPTH24_STENCIL8, samples: 4 });
            expect(gl.renderbufferStorageMultisample).toHaveBeenCalledWith(
                gl.RENDERBUFFER,
                4,
                gl.DEPTH24_STENCIL8,
                128,
                64
            );
        });

        it('should throw OUT_OF_MEMORY if createRenderbuffer returns null', () => {
            (gl.createRenderbuffer as ReturnType<typeof vi.fn>).mockReturnValueOnce(null);
            try {
                createRenderbuffer();
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('OUT_OF_MEMORY');
            }
        });
    });

    describe('properties', () => {
        it('should expose width, height, internalFormat, samples, label, isDisposed', () => {
            const rb = createRenderbuffer({
                width: 512,
                height: 128,
                internalFormat: gl.DEPTH24_STENCIL8,
                samples: 4,
                label: 'myRB',
            });
            expect(rb.width).toBe(512);
            expect(rb.height).toBe(128);
            expect(rb.internalFormat).toBe(gl.DEPTH24_STENCIL8);
            expect(rb.samples).toBe(4);
            expect(rb.label).toBe('myRB');
            expect(rb.isDisposed).toBe(false);
        });

        it('should have null label by default', () => {
            const rb = createRenderbuffer();
            expect(rb.label).toBeNull();
        });

        it('should expose id', () => {
            const rb = createRenderbuffer();
            expect(rb.id).toBeDefined();
        });
    });

    describe('bind / unbind', () => {
        it('bind should call gl.bindRenderbuffer with RENDERBUFFER target and id', () => {
            const rb = createRenderbuffer();
            (gl.bindRenderbuffer as ReturnType<typeof vi.fn>).mockClear();
            rb.bind();
            expect(gl.bindRenderbuffer).toHaveBeenCalledWith(gl.RENDERBUFFER, expect.anything());
        });

        it('unbind should call gl.bindRenderbuffer with RENDERBUFFER target and null', () => {
            const rb = createRenderbuffer();
            (gl.bindRenderbuffer as ReturnType<typeof vi.fn>).mockClear();
            rb.unbind();
            expect(gl.bindRenderbuffer).toHaveBeenCalledWith(gl.RENDERBUFFER, null);
        });

        it('bind should return the renderbuffer for chaining', () => {
            const rb = createRenderbuffer();
            expect(rb.bind()).toBe(rb);
        });

        it('unbind should return the renderbuffer for chaining', () => {
            const rb = createRenderbuffer();
            expect(rb.unbind()).toBe(rb);
        });
    });

    describe('resize', () => {
        it('should update dimensions and call renderbufferStorage', () => {
            const rb = createRenderbuffer({ width: 64, height: 64 });
            (gl.renderbufferStorage as ReturnType<typeof vi.fn>).mockClear();
            rb.resize(128, 256);
            expect(rb.width).toBe(128);
            expect(rb.height).toBe(256);
            expect(gl.renderbufferStorage).toHaveBeenCalled();
        });

        it('should update samples if provided', () => {
            const rb = createRenderbuffer({ width: 64, height: 64 });
            rb.resize(128, 128, 4);
            expect(rb.samples).toBe(4);
            expect(gl.renderbufferStorageMultisample).toHaveBeenCalled();
        });

        it('should throw INVALID_VALUE for zero or negative dimensions', () => {
            const rb = createRenderbuffer();
            try {
                rb.resize(0, 100);
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('INVALID_VALUE');
            }
            try {
                rb.resize(100, -1);
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('INVALID_VALUE');
            }
        });

        it('should return the renderbuffer for chaining', () => {
            const rb = createRenderbuffer();
            expect(rb.resize(100, 100)).toBe(rb);
        });
    });

    describe('dispose', () => {
        it('should call gl.deleteRenderbuffer', () => {
            const rb = createRenderbuffer();
            rb.dispose();
            expect(gl.deleteRenderbuffer).toHaveBeenCalled();
        });

        it('should set isDisposed to true', () => {
            const rb = createRenderbuffer();
            rb.dispose();
            expect(rb.isDisposed).toBe(true);
        });

        it('should be idempotent', () => {
            const rb = createRenderbuffer();
            rb.dispose();
            rb.dispose();
            expect(gl.deleteRenderbuffer).toHaveBeenCalledTimes(1);
        });

        it('should throw RENDERBUFFER_ALREADY_DISPOSED when accessing id after dispose', () => {
            const rb = createRenderbuffer();
            rb.dispose();
            try {
                void rb.id;
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('RENDERBUFFER_ALREADY_DISPOSED');
            }
        });

        it('should throw RENDERBUFFER_ALREADY_DISPOSED when calling bind after dispose', () => {
            const rb = createRenderbuffer();
            rb.dispose();
            try {
                rb.bind();
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('RENDERBUFFER_ALREADY_DISPOSED');
            }
        });

        it('should throw RENDERBUFFER_ALREADY_DISPOSED when calling resize after dispose', () => {
            const rb = createRenderbuffer();
            rb.dispose();
            try {
                rb.resize(10, 10);
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('RENDERBUFFER_ALREADY_DISPOSED');
            }
        });
    });
});

// ---------------------------------------------------------------------------
// Framebuffer
// ---------------------------------------------------------------------------
describe('Framebuffer', () => {
    let gl: WebGL2RenderingContext;

    beforeEach(() => {
        gl = createMockGL();
    });

    function createTextureForFB(opts: Partial<TextureOptions> = {}): Texture {
        return new Texture(gl, gl.TEXTURE_2D, {
            width: 256,
            height: 256,
            ...opts,
        });
    }

    function createRenderbufferForFB(opts: Partial<RenderbufferOptions> = {}): Renderbuffer {
        return new Renderbuffer(gl, {
            width: 256,
            height: 256,
            internalFormat: gl.DEPTH_COMPONENT24,
            ...opts,
        });
    }

    function createFramebuffer(opts: Partial<FramebufferOptions> = {}): Framebuffer {
        const colorTex = createTextureForFB();
        return new Framebuffer(gl, {
            width: 256,
            height: 256,
            colorAttachments: [
                { attachment: gl.COLOR_ATTACHMENT0, texture: colorTex },
            ],
            ...opts,
        });
    }

    describe('constructor', () => {
        it('should create a GL framebuffer', () => {
            createFramebuffer();
            expect(gl.createFramebuffer).toHaveBeenCalled();
        });

        it('should attach color attachments via framebufferTexture2D', () => {
            createFramebuffer();
            expect(gl.framebufferTexture2D).toHaveBeenCalledWith(
                gl.FRAMEBUFFER,
                gl.COLOR_ATTACHMENT0,
                gl.TEXTURE_2D,
                expect.anything(),
                0
            );
        });

        it('should attach depth attachment', () => {
            const depthTex = createTextureForFB({ internalFormat: gl.DEPTH_COMPONENT24 });
            createFramebuffer({
                depthAttachment: { attachment: gl.DEPTH_ATTACHMENT, texture: depthTex },
            });
            expect(gl.framebufferTexture2D).toHaveBeenCalledWith(
                gl.FRAMEBUFFER,
                gl.DEPTH_ATTACHMENT,
                gl.TEXTURE_2D,
                expect.anything(),
                0
            );
        });

        it('should attach depth-stencil attachment', () => {
            const dsTex = createTextureForFB({ internalFormat: gl.DEPTH24_STENCIL8 });
            createFramebuffer({
                depthStencilAttachment: { attachment: gl.DEPTH_STENCIL_ATTACHMENT, texture: dsTex },
            });
            expect(gl.framebufferTexture2D).toHaveBeenCalledWith(
                gl.FRAMEBUFFER,
                gl.DEPTH_STENCIL_ATTACHMENT,
                gl.TEXTURE_2D,
                expect.anything(),
                0
            );
        });

        it('should attach renderbuffer attachments', () => {
            const depthRB = createRenderbufferForFB();
            createFramebuffer({
                depthAttachment: { attachment: gl.DEPTH_ATTACHMENT, renderbuffer: depthRB },
            });
            expect(gl.framebufferRenderbuffer).toHaveBeenCalledWith(
                gl.FRAMEBUFFER,
                gl.DEPTH_ATTACHMENT,
                gl.RENDERBUFFER,
                expect.anything()
            );
        });

        it('should check framebuffer completeness', () => {
            createFramebuffer();
            expect(gl.checkFramebufferStatus).toHaveBeenCalledWith(gl.FRAMEBUFFER);
        });

        it('should throw INCOMPLETE_FRAMEBUFFER if status is not FRAMEBUFFER_COMPLETE', () => {
            (gl.checkFramebufferStatus as ReturnType<typeof vi.fn>).mockReturnValue(0x8cd6); // INCOMPLETE_ATTACHMENT
            try {
                createFramebuffer();
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('INCOMPLETE_FRAMEBUFFER');
            }
        });

        it('should throw OUT_OF_MEMORY if createFramebuffer returns null', () => {
            (gl.createFramebuffer as ReturnType<typeof vi.fn>).mockReturnValueOnce(null);
            try {
                createFramebuffer();
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('OUT_OF_MEMORY');
            }
        });

        it('should throw for attachment config with neither texture nor renderbuffer', () => {
            try {
                new Framebuffer(gl, {
                    width: 64,
                    height: 64,
                    colorAttachments: [{ attachment: gl.COLOR_ATTACHMENT0 }],
                });
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('INVALID_ATTACHMENT');
            }
        });

        it('should throw for attachment config with both texture and renderbuffer', () => {
            const tex = createTextureForFB();
            const rb = createRenderbufferForFB();
            try {
                new Framebuffer(gl, {
                    width: 64,
                    height: 64,
                    colorAttachments: [
                        { attachment: gl.COLOR_ATTACHMENT0, texture: tex, renderbuffer: rb },
                    ],
                });
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('INVALID_ATTACHMENT');
            }
        });

        it('should throw for attachment config with disposed texture', () => {
            const tex = createTextureForFB();
            tex.dispose();
            try {
                new Framebuffer(gl, {
                    width: 64,
                    height: 64,
                    colorAttachments: [{ attachment: gl.COLOR_ATTACHMENT0, texture: tex }],
                });
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('TEXTURE_ALREADY_DISPOSED');
            }
        });

        it('should throw for attachment config with disposed renderbuffer', () => {
            const rb = createRenderbufferForFB();
            rb.dispose();
            try {
                new Framebuffer(gl, {
                    width: 64,
                    height: 64,
                    depthAttachment: { attachment: gl.DEPTH_ATTACHMENT, renderbuffer: rb },
                });
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('RENDERBUFFER_ALREADY_DISPOSED');
            }
        });
    });

    describe('properties', () => {
        it('should expose width, height, label, isDisposed', () => {
            const fb = createFramebuffer({ width: 512, height: 128, label: 'myFB' });
            expect(fb.width).toBe(512);
            expect(fb.height).toBe(128);
            expect(fb.label).toBe('myFB');
            expect(fb.isDisposed).toBe(false);
        });

        it('should have null label by default', () => {
            const fb = createFramebuffer();
            expect(fb.label).toBeNull();
        });

        it('should expose id', () => {
            const fb = createFramebuffer();
            expect(fb.id).toBeDefined();
        });

        it('should expose colorAttachments', () => {
            const fb = createFramebuffer();
            expect(fb.colorAttachments).toHaveLength(1);
        });

        it('should expose depthAttachment as null when not set', () => {
            const fb = createFramebuffer();
            expect(fb.depthAttachment).toBeNull();
        });

        it('should expose stencilAttachment as null when not set', () => {
            const fb = createFramebuffer();
            expect(fb.stencilAttachment).toBeNull();
        });

        it('should expose depthStencilAttachment as null when not set', () => {
            const fb = createFramebuffer();
            expect(fb.depthStencilAttachment).toBeNull();
        });
    });

    describe('isComplete / status', () => {
        it('isComplete should return true when framebuffer is complete', () => {
            const fb = createFramebuffer();
            expect(fb.isComplete).toBe(true);
        });

        it('isComplete should return false when framebuffer is incomplete', () => {
            const fb = createFramebuffer();
            (gl.checkFramebufferStatus as ReturnType<typeof vi.fn>).mockReturnValue(0x8cd6);
            expect(fb.isComplete).toBe(false);
        });

        it('status should return the framebuffer status value', () => {
            const fb = createFramebuffer();
            expect(fb.status).toBe(gl.FRAMEBUFFER_COMPLETE);
        });
    });

    describe('bind / unbind', () => {
        it('bind should call bindFramebuffer and viewport', () => {
            const fb = createFramebuffer({ width: 256, height: 128 });
            (gl.bindFramebuffer as ReturnType<typeof vi.fn>).mockClear();
            (gl.viewport as ReturnType<typeof vi.fn>).mockClear();
            fb.bind();
            expect(gl.bindFramebuffer).toHaveBeenCalledWith(gl.FRAMEBUFFER, expect.anything());
            expect(gl.viewport).toHaveBeenCalledWith(0, 0, 256, 128);
        });

        it('unbind should call bindFramebuffer with null', () => {
            const fb = createFramebuffer();
            (gl.bindFramebuffer as ReturnType<typeof vi.fn>).mockClear();
            fb.unbind();
            expect(gl.bindFramebuffer).toHaveBeenCalledWith(gl.FRAMEBUFFER, null);
        });

        it('bind should return the framebuffer for chaining', () => {
            const fb = createFramebuffer();
            expect(fb.bind()).toBe(fb);
        });

        it('unbind should return the framebuffer for chaining', () => {
            const fb = createFramebuffer();
            expect(fb.unbind()).toBe(fb);
        });
    });

    describe('attachTexture', () => {
        it('should call framebufferTexture2D', () => {
            const fb = createFramebuffer();
            const tex = createTextureForFB();
            fb.attachTexture(gl.COLOR_ATTACHMENT1, tex);
            expect(gl.framebufferTexture2D).toHaveBeenCalledWith(
                gl.FRAMEBUFFER,
                gl.COLOR_ATTACHMENT1,
                gl.TEXTURE_2D,
                expect.anything(),
                0
            );
        });

        it('should throw TEXTURE_ALREADY_DISPOSED for disposed texture', () => {
            const fb = createFramebuffer();
            const tex = createTextureForFB();
            tex.dispose();
            try {
                fb.attachTexture(gl.COLOR_ATTACHMENT1, tex);
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('TEXTURE_ALREADY_DISPOSED');
            }
        });

        it('should return the framebuffer for chaining', () => {
            const fb = createFramebuffer();
            const tex = createTextureForFB();
            expect(fb.attachTexture(gl.COLOR_ATTACHMENT1, tex)).toBe(fb);
        });
    });

    describe('attachRenderbuffer', () => {
        it('should call framebufferRenderbuffer', () => {
            const fb = createFramebuffer();
            const rb = createRenderbufferForFB();
            fb.attachRenderbuffer(gl.DEPTH_ATTACHMENT, rb);
            expect(gl.framebufferRenderbuffer).toHaveBeenCalledWith(
                gl.FRAMEBUFFER,
                gl.DEPTH_ATTACHMENT,
                gl.RENDERBUFFER,
                expect.anything()
            );
        });

        it('should throw RENDERBUFFER_ALREADY_DISPOSED for disposed renderbuffer', () => {
            const fb = createFramebuffer();
            const rb = createRenderbufferForFB();
            rb.dispose();
            try {
                fb.attachRenderbuffer(gl.DEPTH_ATTACHMENT, rb);
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('RENDERBUFFER_ALREADY_DISPOSED');
            }
        });

        it('should return the framebuffer for chaining', () => {
            const fb = createFramebuffer();
            const rb = createRenderbufferForFB();
            expect(fb.attachRenderbuffer(gl.DEPTH_ATTACHMENT, rb)).toBe(fb);
        });
    });

    describe('detach', () => {
        it('should call framebufferTexture2D with null', () => {
            const fb = createFramebuffer();
            (gl.framebufferTexture2D as ReturnType<typeof vi.fn>).mockClear();
            fb.detach(gl.COLOR_ATTACHMENT0);
            expect(gl.framebufferTexture2D).toHaveBeenCalledWith(
                gl.FRAMEBUFFER,
                gl.COLOR_ATTACHMENT0,
                gl.TEXTURE_2D,
                null,
                0
            );
        });

        it('should return the framebuffer for chaining', () => {
            const fb = createFramebuffer();
            expect(fb.detach(gl.COLOR_ATTACHMENT0)).toBe(fb);
        });
    });

    describe('resize', () => {
        it('should update width and height', () => {
            const fb = createFramebuffer({ width: 64, height: 64 });
            fb.resize(128, 256);
            expect(fb.width).toBe(128);
            expect(fb.height).toBe(256);
        });

        it('should resize color attachment textures', () => {
            const tex = createTextureForFB({ width: 64, height: 64 });
            const fb = new Framebuffer(gl, {
                width: 64,
                height: 64,
                colorAttachments: [{ attachment: gl.COLOR_ATTACHMENT0, texture: tex }],
            });
            fb.resize(128, 128);
            expect(tex.width).toBe(128);
            expect(tex.height).toBe(128);
        });

        it('should throw INVALID_VALUE for zero or negative dimensions', () => {
            const fb = createFramebuffer();
            try {
                fb.resize(0, 100);
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('INVALID_VALUE');
            }
            try {
                fb.resize(100, -1);
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('INVALID_VALUE');
            }
        });

        it('should return the framebuffer for chaining', () => {
            const fb = createFramebuffer();
            expect(fb.resize(100, 100)).toBe(fb);
        });
    });

    describe('clear', () => {
        it('should call clearColor and clear with COLOR_BUFFER_BIT', () => {
            const fb = createFramebuffer();
            fb.clear([1, 0, 0, 1]);
            expect(gl.clearColor).toHaveBeenCalledWith(1, 0, 0, 1);
            expect(gl.clear).toHaveBeenCalledWith(gl.COLOR_BUFFER_BIT);
        });

        it('should call clearDepth and clear with DEPTH_BUFFER_BIT when depth attachment exists', () => {
            const depthTex = createTextureForFB({ internalFormat: gl.DEPTH_COMPONENT24 });
            const fb = new Framebuffer(gl, {
                width: 64,
                height: 64,
                colorAttachments: [{ attachment: gl.COLOR_ATTACHMENT0, texture: createTextureForFB() }],
                depthAttachment: { attachment: gl.DEPTH_ATTACHMENT, texture: depthTex },
            });
            (gl.clearColor as ReturnType<typeof vi.fn>).mockClear();
            (gl.clear as ReturnType<typeof vi.fn>).mockClear();
            fb.clear([0, 0, 0, 1], 1.0);
            expect(gl.clearDepth).toHaveBeenCalledWith(1.0);
            expect(gl.clear).toHaveBeenCalledWith(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
        });

        it('should call clearStencil with STENCIL_BUFFER_BIT when depth-stencil attachment exists', () => {
            const dsTex = createTextureForFB({ internalFormat: gl.DEPTH24_STENCIL8 });
            const fb = new Framebuffer(gl, {
                width: 64,
                height: 64,
                colorAttachments: [{ attachment: gl.COLOR_ATTACHMENT0, texture: createTextureForFB() }],
                depthStencilAttachment: { attachment: gl.DEPTH_STENCIL_ATTACHMENT, texture: dsTex },
            });
            (gl.clear as ReturnType<typeof vi.fn>).mockClear();
            fb.clear(undefined, undefined, 0);
            expect(gl.clearStencil).toHaveBeenCalledWith(0);
            expect(gl.clear).toHaveBeenCalledWith(gl.STENCIL_BUFFER_BIT);
        });

        it('should not call clear when no buffers specified', () => {
            const fb = createFramebuffer();
            (gl.clear as ReturnType<typeof vi.fn>).mockClear();
            fb.clear();
            expect(gl.clear).not.toHaveBeenCalled();
        });

        it('should return the framebuffer for chaining', () => {
            const fb = createFramebuffer();
            expect(fb.clear([0, 0, 0, 1])).toBe(fb);
        });
    });

    describe('dispose', () => {
        it('should call gl.deleteFramebuffer', () => {
            const fb = createFramebuffer();
            fb.dispose();
            expect(gl.deleteFramebuffer).toHaveBeenCalled();
        });

        it('should set isDisposed to true', () => {
            const fb = createFramebuffer();
            fb.dispose();
            expect(fb.isDisposed).toBe(true);
        });

        it('should clear attachment references', () => {
            const fb = createFramebuffer();
            fb.dispose();
            expect(fb.colorAttachments).toHaveLength(0);
            expect(fb.depthAttachment).toBeNull();
            expect(fb.stencilAttachment).toBeNull();
            expect(fb.depthStencilAttachment).toBeNull();
        });

        it('should be idempotent', () => {
            const fb = createFramebuffer();
            fb.dispose();
            fb.dispose();
            expect(gl.deleteFramebuffer).toHaveBeenCalledTimes(1);
        });

        it('should throw FRAMEBUFFER_ALREADY_DISPOSED when accessing id after dispose', () => {
            const fb = createFramebuffer();
            fb.dispose();
            try {
                void fb.id;
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('FRAMEBUFFER_ALREADY_DISPOSED');
            }
        });

        it('should throw FRAMEBUFFER_ALREADY_DISPOSED when calling bind after dispose', () => {
            const fb = createFramebuffer();
            fb.dispose();
            try {
                fb.bind();
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('FRAMEBUFFER_ALREADY_DISPOSED');
            }
        });

        it('should throw FRAMEBUFFER_ALREADY_DISPOSED when calling unbind after dispose', () => {
            const fb = createFramebuffer();
            fb.dispose();
            try {
                fb.unbind();
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('FRAMEBUFFER_ALREADY_DISPOSED');
            }
        });

        it('should throw FRAMEBUFFER_ALREADY_DISPOSED when calling attachTexture after dispose', () => {
            const fb = createFramebuffer();
            fb.dispose();
            const tex = createTextureForFB();
            try {
                fb.attachTexture(gl.COLOR_ATTACHMENT1, tex);
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('FRAMEBUFFER_ALREADY_DISPOSED');
            }
        });

        it('should throw FRAMEBUFFER_ALREADY_DISPOSED when calling attachRenderbuffer after dispose', () => {
            const fb = createFramebuffer();
            fb.dispose();
            const rb = createRenderbufferForFB();
            try {
                fb.attachRenderbuffer(gl.DEPTH_ATTACHMENT, rb);
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('FRAMEBUFFER_ALREADY_DISPOSED');
            }
        });

        it('should throw FRAMEBUFFER_ALREADY_DISPOSED when calling detach after dispose', () => {
            const fb = createFramebuffer();
            fb.dispose();
            try {
                fb.detach(gl.COLOR_ATTACHMENT0);
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('FRAMEBUFFER_ALREADY_DISPOSED');
            }
        });

        it('should throw FRAMEBUFFER_ALREADY_DISPOSED when calling resize after dispose', () => {
            const fb = createFramebuffer();
            fb.dispose();
            try {
                fb.resize(10, 10);
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('FRAMEBUFFER_ALREADY_DISPOSED');
            }
        });

        it('should throw FRAMEBUFFER_ALREADY_DISPOSED when calling clear after dispose', () => {
            const fb = createFramebuffer();
            fb.dispose();
            try {
                fb.clear([0, 0, 0, 1]);
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('FRAMEBUFFER_ALREADY_DISPOSED');
            }
        });
    });
});

// ---------------------------------------------------------------------------
// FramebufferFactory
// ---------------------------------------------------------------------------
describe('FramebufferFactory', () => {
    let gl: WebGL2RenderingContext;
    let factory: FramebufferFactory;

    beforeEach(() => {
        gl = createMockGL();
        factory = new FramebufferFactory(gl);
    });

    describe('createTexture2D', () => {
        it('should create a Texture with TEXTURE_2D target', () => {
            const tex = factory.createTexture2D({ width: 128, height: 128 });
            expect(tex.target).toBe(gl.TEXTURE_2D);
            expect(tex.width).toBe(128);
            expect(tex.height).toBe(128);
        });
    });

    describe('createRenderbuffer', () => {
        it('should create a Renderbuffer', () => {
            const rb = factory.createRenderbuffer({
                width: 128,
                height: 128,
                internalFormat: gl.DEPTH_COMPONENT24,
            });
            expect(rb.width).toBe(128);
            expect(rb.height).toBe(128);
            expect(rb.internalFormat).toBe(gl.DEPTH_COMPONENT24);
        });
    });

    describe('createFramebuffer', () => {
        it('should create a Framebuffer from options', () => {
            const tex = factory.createTexture2D({ width: 64, height: 64 });
            const fb = factory.createFramebuffer({
                width: 64,
                height: 64,
                colorAttachments: [{ attachment: gl.COLOR_ATTACHMENT0, texture: tex }],
            });
            expect(fb.width).toBe(64);
            expect(fb.height).toBe(64);
        });
    });

    describe('createColorFramebuffer', () => {
        it('should create a framebuffer with a color texture attachment', () => {
            const fb = factory.createColorFramebuffer(256, 256);
            expect(fb.width).toBe(256);
            expect(fb.height).toBe(256);
            expect(fb.colorAttachments.length).toBeGreaterThanOrEqual(1);
        });

        it('should use RGBA8 format by default', () => {
            const fb = factory.createColorFramebuffer(64, 64);
            // The color attachment texture should use RGBA8
            const colorTex = fb.colorAttachments[0];
            expect(colorTex.internalFormat).toBe(gl.RGBA8);
        });

        it('should accept custom format', () => {
            const fb = factory.createColorFramebuffer(64, 64, gl.RGBA16F);
            const colorTex = fb.colorAttachments[0];
            expect(colorTex.internalFormat).toBe(gl.RGBA16F);
        });
    });

    describe('createDepthFramebuffer', () => {
        it('should create a framebuffer with depth texture (no samples)', () => {
            const fb = factory.createDepthFramebuffer(256, 256);
            expect(fb.width).toBe(256);
            expect(fb.height).toBe(256);
            expect(fb.depthAttachment).not.toBeNull();
        });

        it('should use DEPTH_COMPONENT24 by default', () => {
            const fb = factory.createDepthFramebuffer(64, 64);
            const depthTex = fb.depthAttachment as Texture;
            expect(depthTex.internalFormat).toBe(gl.DEPTH_COMPONENT24);
        });

        it('should create a renderbuffer for multisampled depth', () => {
            const fb = factory.createDepthFramebuffer(64, 64, gl.DEPTH_COMPONENT24, 4);
            expect(fb.depthAttachment).not.toBeNull();
            // For multisampled, a renderbuffer is used
            expect(gl.renderbufferStorageMultisample).toHaveBeenCalled();
        });
    });

    describe('createFramebufferWithDepth', () => {
        it('should create a framebuffer with both color and depth attachments', () => {
            const fb = factory.createFramebufferWithDepth(256, 256);
            expect(fb.width).toBe(256);
            expect(fb.height).toBe(256);
            expect(fb.colorAttachments.length).toBeGreaterThanOrEqual(1);
            expect(fb.depthAttachment).not.toBeNull();
        });

        it('should use RGBA8 and DEPTH_COMPONENT24 by default', () => {
            const fb = factory.createFramebufferWithDepth(64, 64);
            const colorTex = fb.colorAttachments[0];
            expect(colorTex.internalFormat).toBe(gl.RGBA8);
            const depthTex = fb.depthAttachment as Texture;
            expect(depthTex.internalFormat).toBe(gl.DEPTH_COMPONENT24);
        });

        it('should accept custom color and depth formats', () => {
            const fb = factory.createFramebufferWithDepth(64, 64, gl.RGBA16F, gl.DEPTH_COMPONENT32F);
            const colorTex = fb.colorAttachments[0];
            expect(colorTex.internalFormat).toBe(gl.RGBA16F);
            const depthTex = fb.depthAttachment as Texture;
            expect(depthTex.internalFormat).toBe(gl.DEPTH_COMPONENT32F);
        });

        it('should throw when samples > 0 because color texture cannot be multisampled', () => {
            // The implementation passes samples to the color texture constructor,
            // which throws UNSUPPORTED_OPERATION for multisampled textures.
            try {
                factory.createFramebufferWithDepth(64, 64, gl.RGBA8, gl.DEPTH24_STENCIL8, 4);
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(FramebufferError);
                expect((e as FramebufferError).code).toBe('UNSUPPORTED_OPERATION');
            }
        });
    });
});

// ---------------------------------------------------------------------------
// Utility functions
// ---------------------------------------------------------------------------
describe('createFramebufferFactory', () => {
    it('should return a FramebufferFactory instance', () => {
        const gl = createMockGL();
        const factory = createFramebufferFactory(gl);
        expect(factory).toBeDefined();
        expect(typeof factory.createTexture2D).toBe('function');
        expect(typeof factory.createRenderbuffer).toBe('function');
        expect(typeof factory.createFramebuffer).toBe('function');
        expect(typeof factory.createColorFramebuffer).toBe('function');
        expect(typeof factory.createDepthFramebuffer).toBe('function');
        expect(typeof factory.createFramebufferWithDepth).toBe('function');
    });
});

describe('createRenderTarget', () => {
    let gl: WebGL2RenderingContext;

    beforeEach(() => {
        gl = createMockGL();
    });

    it('should create a color-only framebuffer when useDepth and useStencil are false', () => {
        const fb = createRenderTarget(gl, 256, 256, { useDepth: false, useStencil: false });
        expect(fb.width).toBe(256);
        expect(fb.height).toBe(256);
        expect(fb.colorAttachments.length).toBeGreaterThanOrEqual(1);
    });

    it('should create a framebuffer with depth when useDepth is true', () => {
        const fb = createRenderTarget(gl, 256, 256, { useDepth: true });
        expect(fb.depthAttachment).not.toBeNull();
    });

    it('should use default options', () => {
        const fb = createRenderTarget(gl, 128, 128);
        expect(fb.width).toBe(128);
        expect(fb.height).toBe(128);
    });
});

describe('createShadowMap', () => {
    it('should create a depth framebuffer of the given size', () => {
        const gl = createMockGL();
        const fb = createShadowMap(gl, 1024);
        expect(fb.width).toBe(1024);
        expect(fb.height).toBe(1024);
        expect(fb.depthAttachment).not.toBeNull();
    });

    it('should accept a custom depth format', () => {
        const gl = createMockGL();
        const fb = createShadowMap(gl, 512, gl.DEPTH_COMPONENT32F);
        expect(fb.width).toBe(512);
        expect(fb.height).toBe(512);
    });
});

describe('createMultisampledRenderTarget', () => {
    it('should throw because the underlying color texture cannot be multisampled', () => {
        const gl = createMockGL();
        // createMultisampledRenderTarget calls createFramebufferWithDepth with samples > 0,
        // which tries to create a multisampled color texture, which throws UNSUPPORTED_OPERATION.
        try {
            createMultisampledRenderTarget(gl, 256, 256, 4);
            expect.unreachable('should have thrown');
        } catch (e) {
            expect(e).toBeInstanceOf(FramebufferError);
            expect((e as FramebufferError).code).toBe('UNSUPPORTED_OPERATION');
        }
    });
});
