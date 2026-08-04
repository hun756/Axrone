import { describe, it, expect, vi, beforeEach } from 'vitest';
import {
    Buffer,
    GLError,
    alignTo,
    calculatePadding,
    createTypedArrayFromBuffer,
    createBufferFactory,
} from '../buffer';
import type { IBuffer, IBufferFactory, GLBufferTarget, GLBufferUsage, BufferOptions } from '../buffer';

// ---------------------------------------------------------------------------
// Mock GL context
// ---------------------------------------------------------------------------

function createMockGL() {
    const mockBuffer = {} as WebGLBuffer;
    return {
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
        STREAM_READ: 0x88ed,
        STATIC_COPY: 0x88e6,
        DYNAMIC_COPY: 0x88ea,
        STREAM_COPY: 0x88ee,
        createBuffer: vi.fn(() => mockBuffer),
        deleteBuffer: vi.fn(),
        bindBuffer: vi.fn(),
        bufferData: vi.fn(),
        bufferSubData: vi.fn(),
        getBufferSubData: vi.fn(),
        copyBufferSubData: vi.fn(),
        getParameter: vi.fn(() => null),
        getExtension: vi.fn(() => null),
        canvas: {
            addEventListener: vi.fn(),
            removeEventListener: vi.fn(),
        },
    } as unknown as WebGL2RenderingContext;
}

function createMockGLThatFailsCreate() {
    return {
        ...createMockGL(),
        createBuffer: vi.fn(() => null),
    } as unknown as WebGL2RenderingContext;
}

// ---------------------------------------------------------------------------
// GLError
// ---------------------------------------------------------------------------

describe('GLError', () => {
    it('should set message, code, and cause properties', () => {
        const cause = new Error('root cause');
        const error = new GLError('something broke', 'INVALID_OPERATION', cause);

        // The `public readonly message` parameter property shadows the Error base
        // class message, so error.message holds the raw message string.
        expect(error.message).toBe('something broke');
        expect(error.code).toBe('INVALID_OPERATION');
        expect(error.cause).toBe(cause);
    });

    it('should pass formatted [WebGL2] message to Error base class via super()', () => {
        const error = new GLError('test message', 'OUT_OF_MEMORY');
        // The formatted message is passed to super() but the parameter property
        // `message` shadows it. Verify via Error.prototype methods.
        expect(error).toBeInstanceOf(Error);
        // The raw message is accessible via the own property
        expect(error.message).toBe('test message');
        expect(error.code).toBe('OUT_OF_MEMORY');
    });

    it('should be an instance of Error', () => {
        const error = new GLError('msg', 'INVALID_VALUE');
        expect(error).toBeInstanceOf(Error);
    });

    it('should be an instance of GLError (prototype chain)', () => {
        const error = new GLError('msg', 'INVALID_VALUE');
        expect(error).toBeInstanceOf(GLError);
    });

    it('should have a name property from Error', () => {
        const error = new GLError('msg', 'BUFFER_ALREADY_DISPOSED');
        expect(error.name).toBe('Error');
    });

    it('should work without a cause', () => {
        const error = new GLError('no cause', 'CONTEXT_LOST');
        expect(error.cause).toBeUndefined();
    });

    it('should support all ErrorCode values', () => {
        const codes = [
            'INVALID_OPERATION',
            'BUFFER_ALREADY_DISPOSED',
            'OUT_OF_MEMORY',
            'INVALID_VALUE',
            'CONTEXT_LOST',
            'UNSUPPORTED_OPERATION',
        ] as const;

        for (const code of codes) {
            const error = new GLError('test', code);
            expect(error.code).toBe(code);
            expect(error.message).toBe('test');
            expect(error).toBeInstanceOf(Error);
        }
    });
});

// ---------------------------------------------------------------------------
// Utility functions
// ---------------------------------------------------------------------------

describe('alignTo', () => {
    it('should return value unchanged when already aligned', () => {
        expect(alignTo(16, 4)).toBe(16);
        expect(alignTo(0, 8)).toBe(0);
    });

    it('should round up to the next alignment boundary', () => {
        expect(alignTo(5, 4)).toBe(8);
        expect(alignTo(1, 16)).toBe(16);
        expect(alignTo(17, 8)).toBe(24);
    });

    it('should handle alignment of 1', () => {
        expect(alignTo(7, 1)).toBe(7);
    });

    it('should handle value of 0', () => {
        expect(alignTo(0, 4)).toBe(0);
    });
});

describe('calculatePadding', () => {
    it('should return 0 when offset is already aligned', () => {
        expect(calculatePadding(0, 4)).toBe(0);
        expect(calculatePadding(16, 8)).toBe(0);
    });

    it('should return the padding needed to reach alignment', () => {
        expect(calculatePadding(1, 4)).toBe(3);
        expect(calculatePadding(5, 8)).toBe(3);
        expect(calculatePadding(7, 4)).toBe(1);
    });

    it('should handle alignment of 1', () => {
        expect(calculatePadding(5, 1)).toBe(0);
    });
});

describe('createTypedArrayFromBuffer', () => {
    it('should create a Float32Array view from an ArrayBuffer', () => {
        const ab = new ArrayBuffer(16);
        const view = createTypedArrayFromBuffer(ab, Float32Array);
        expect(view).toBeInstanceOf(Float32Array);
        expect(view.byteLength).toBe(16);
        expect(view.length).toBe(4);
    });

    it('should create a Uint8Array view', () => {
        const ab = new ArrayBuffer(8);
        const view = createTypedArrayFromBuffer(ab, Uint8Array);
        expect(view).toBeInstanceOf(Uint8Array);
        expect(view.length).toBe(8);
    });

    it('should respect byteOffset', () => {
        const ab = new ArrayBuffer(16);
        const view = createTypedArrayFromBuffer(ab, Float32Array, 8);
        expect(view.byteOffset).toBe(8);
        expect(view.length).toBe(2);
    });

    it('should respect length parameter', () => {
        const ab = new ArrayBuffer(16);
        const view = createTypedArrayFromBuffer(ab, Float32Array, 0, 2);
        expect(view.length).toBe(2);
    });

    it('should clamp length to available data', () => {
        const ab = new ArrayBuffer(8);
        const view = createTypedArrayFromBuffer(ab, Float32Array, 0, 100);
        expect(view.length).toBe(2); // 8 bytes / 4 bytes per element = 2
    });

    it('should throw GLError when byteOffset is not aligned to element size', () => {
        const ab = new ArrayBuffer(16);
        expect(() => createTypedArrayFromBuffer(ab, Float32Array, 3)).toThrow(GLError);
        expect(() => createTypedArrayFromBuffer(ab, Float32Array, 3)).toThrow(
            'Byte offset (3) must be a multiple of element size (4)'
        );
    });

    it('should throw GLError with INVALID_VALUE code for misaligned offset', () => {
        const ab = new ArrayBuffer(16);
        try {
            createTypedArrayFromBuffer(ab, Float32Array, 5);
            expect.unreachable('Should have thrown');
        } catch (e) {
            expect(e).toBeInstanceOf(GLError);
            expect((e as GLError).code).toBe('INVALID_VALUE');
        }
    });

    it('should work with Uint16Array', () => {
        const ab = new ArrayBuffer(8);
        const view = createTypedArrayFromBuffer(ab, Uint16Array);
        expect(view).toBeInstanceOf(Uint16Array);
        expect(view.length).toBe(4);
    });

    it('should work with Int32Array', () => {
        const ab = new ArrayBuffer(16);
        const view = createTypedArrayFromBuffer(ab, Int32Array, 0, 2);
        expect(view).toBeInstanceOf(Int32Array);
        expect(view.length).toBe(2);
    });
});

// ---------------------------------------------------------------------------
// Buffer class
// ---------------------------------------------------------------------------

describe('Buffer', () => {
    let gl: WebGL2RenderingContext;

    beforeEach(() => {
        gl = createMockGL();
    });

    describe('constructor', () => {
        it('should create a GL buffer via gl.createBuffer', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);
            expect(gl.createBuffer).toHaveBeenCalledTimes(1);
            expect(buffer).toBeDefined();
        });

        it('should throw GLError if createBuffer returns null', () => {
            const failGL = createMockGLThatFailsCreate();
            expect(() => new Buffer(failGL, failGL.ARRAY_BUFFER as GLBufferTarget)).toThrow(GLError);
            expect(() => new Buffer(failGL, failGL.ARRAY_BUFFER as GLBufferTarget)).toThrow(
                'Failed to create WebGLBuffer'
            );
        });

        it('should throw GLError with OUT_OF_MEMORY code when createBuffer fails', () => {
            const failGL = createMockGLThatFailsCreate();
            try {
                new Buffer(failGL, failGL.ARRAY_BUFFER as GLBufferTarget);
                expect.unreachable('Should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(GLError);
                expect((e as GLError).code).toBe('OUT_OF_MEMORY');
            }
        });

        it('should apply initial data via resize', () => {
            const data = new Float32Array([1, 2, 3, 4]);
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                initialData: data,
            });

            expect(gl.bufferData).toHaveBeenCalled();
            expect(buffer.byteLength).toBe(data.byteLength);
        });

        it('should apply byteSize when no initial data is provided', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                byteSize: 1024,
            });

            expect(gl.bufferData).toHaveBeenCalledWith(
                gl.ARRAY_BUFFER,
                1024,
                gl.STATIC_DRAW
            );
            expect(buffer.byteLength).toBe(1024);
        });

        it('should use provided usage for initial data', () => {
            const data = new Float32Array([1, 2]);
            new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                initialData: data,
                usage: gl.DYNAMIC_DRAW as GLBufferUsage,
            });

            expect(gl.bufferData).toHaveBeenCalledWith(
                gl.ARRAY_BUFFER,
                data,
                gl.DYNAMIC_DRAW
            );
        });

        it('should default usage to STATIC_DRAW', () => {
            new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);
            // No bufferData call when no initial data or byteSize
            expect(gl.bufferData).not.toHaveBeenCalled();
        });

        it('should not call bufferData when byteSize is 0 and no initialData', () => {
            new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 0 });
            expect(gl.bufferData).not.toHaveBeenCalled();
        });

        it('should set label to null by default', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);
            expect(buffer.label).toBeNull();
        });

        it('should store the label when provided', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                label: 'my-buffer',
            });
            expect(buffer.label).toBe('my-buffer');
        });
    });

    describe('properties', () => {
        it('should expose id (the WebGLBuffer)', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);
            expect(buffer.id).toBeDefined();
        });

        it('should expose target', () => {
            const buffer = new Buffer(gl, gl.ELEMENT_ARRAY_BUFFER as GLBufferTarget);
            expect(buffer.target).toBe(gl.ELEMENT_ARRAY_BUFFER);
        });

        it('should expose byteLength', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                byteSize: 256,
            });
            expect(buffer.byteLength).toBe(256);
        });

        it('should expose usage', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                usage: gl.DYNAMIC_DRAW as GLBufferUsage,
            });
            expect(buffer.usage).toBe(gl.DYNAMIC_DRAW);
        });

        it('should expose isDisposed as false initially', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);
            expect(buffer.isDisposed).toBe(false);
        });

        it('should expose label', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                label: 'test-label',
            });
            expect(buffer.label).toBe('test-label');
        });
    });

    describe('bind / unbind', () => {
        it('bind should call gl.bindBuffer with correct target and id', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);
            (gl.bindBuffer as ReturnType<typeof vi.fn>).mockClear();

            buffer.bind();

            expect(gl.bindBuffer).toHaveBeenCalledWith(gl.ARRAY_BUFFER, buffer.id);
        });

        it('bind should return the buffer for chaining', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);
            const result = buffer.bind();
            expect(result).toBe(buffer);
        });

        it('unbind should call gl.bindBuffer with null', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);
            (gl.bindBuffer as ReturnType<typeof vi.fn>).mockClear();

            buffer.unbind();

            expect(gl.bindBuffer).toHaveBeenCalledWith(gl.ARRAY_BUFFER, null);
        });

        it('unbind should return the buffer for chaining', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);
            const result = buffer.unbind();
            expect(result).toBe(buffer);
        });

        it('bind should throw GLError after dispose', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);
            buffer.dispose();
            expect(() => buffer.bind()).toThrow(GLError);
            expect(() => buffer.bind()).toThrow('Buffer has been disposed');
        });

        it('unbind should throw GLError after dispose', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);
            buffer.dispose();
            expect(() => buffer.unbind()).toThrow(GLError);
        });
    });

    describe('update', () => {
        it('should call bufferSubData with correct arguments', () => {
            const data = new Float32Array([1, 2, 3]);
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                byteSize: data.byteLength,
            });

            buffer.update(data);

            expect(gl.bindBuffer).toHaveBeenCalledWith(gl.ARRAY_BUFFER, buffer.id);
            expect(gl.bufferSubData).toHaveBeenCalledWith(gl.ARRAY_BUFFER, 0, data);
        });

        it('should respect offset parameter', () => {
            const data = new Float32Array([1, 2]);
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                byteSize: 100,
            });

            buffer.update(data, 16);

            expect(gl.bufferSubData).toHaveBeenCalledWith(gl.ARRAY_BUFFER, 16, data);
        });

        it('should return the buffer for chaining', () => {
            const data = new Float32Array([1]);
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                byteSize: data.byteLength,
            });
            const result = buffer.update(data);
            expect(result).toBe(buffer);
        });

        it('should throw GLError when offset + data exceeds buffer bounds', () => {
            const data = new Float32Array([1, 2, 3]);
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                byteSize: 8,
            });

            expect(() => buffer.update(data, 4)).toThrow(GLError);
            expect(() => buffer.update(data, 4)).toThrow('exceed buffer bounds');
        });

        it('should throw GLError with INVALID_VALUE for out-of-bounds update', () => {
            const data = new Float32Array([1, 2, 3]);
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                byteSize: 8,
            });

            try {
                buffer.update(data, 4);
                expect.unreachable('Should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(GLError);
                expect((e as GLError).code).toBe('INVALID_VALUE');
            }
        });

        it('should throw GLError when offset is negative', () => {
            const data = new Float32Array([1]);
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                byteSize: 100,
            });

            expect(() => buffer.update(data, -1)).toThrow(GLError);
            expect(() => buffer.update(data, -1)).toThrow('Offset cannot be negative');
        });

        it('should throw GLError after dispose', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                byteSize: 100,
            });
            buffer.dispose();

            expect(() => buffer.update(new Float32Array([1]))).toThrow(GLError);
            try {
                buffer.update(new Float32Array([1]));
                expect.unreachable('Should have thrown');
            } catch (e) {
                expect((e as GLError).code).toBe('BUFFER_ALREADY_DISPOSED');
            }
        });

        it('should accept ArrayBuffer as data', () => {
            const ab = new ArrayBuffer(8);
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                byteSize: 8,
            });

            buffer.update(ab);
            expect(gl.bufferSubData).toHaveBeenCalledWith(gl.ARRAY_BUFFER, 0, ab);
        });

        it('should accept Uint8Array as data', () => {
            const data = new Uint8Array([1, 2, 3, 4]);
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                byteSize: data.byteLength,
            });

            buffer.update(data);
            expect(gl.bufferSubData).toHaveBeenCalledWith(gl.ARRAY_BUFFER, 0, data);
        });
    });

    describe('updateRange', () => {
        it('should call bufferSubData with range parameters', () => {
            const data = new Float32Array([1, 2, 3, 4]);
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                byteSize: data.byteLength,
            });

            buffer.updateRange(data, 0);

            expect(gl.bindBuffer).toHaveBeenCalledWith(gl.ARRAY_BUFFER, buffer.id);
            expect(gl.bufferSubData).toHaveBeenCalled();
        });

        it('should handle srcByteOffset for typed arrays', () => {
            const data = new Float32Array([1, 2, 3, 4]);
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                byteSize: 16,
            });

            // srcByteOffset = 4 (1 float element), length = 8 (2 float elements)
            buffer.updateRange(data, 0, 4, 8);

            expect(gl.bufferSubData).toHaveBeenCalled();
            const callArgs = (gl.bufferSubData as ReturnType<typeof vi.fn>).mock.calls[0];
            expect(callArgs[0]).toBe(gl.ARRAY_BUFFER);
            expect(callArgs[1]).toBe(0); // dstByteOffset
        });

        it('should throw GLError when offsets are negative', () => {
            const data = new Float32Array([1, 2]);
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                byteSize: 100,
            });

            expect(() => buffer.updateRange(data, -1, 0)).toThrow(GLError);
            expect(() => buffer.updateRange(data, -1, 0)).toThrow('Offsets cannot be negative');
        });

        it('should throw GLError when srcByteOffset is negative', () => {
            const data = new Float32Array([1, 2]);
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                byteSize: 100,
            });

            expect(() => buffer.updateRange(data, 0, -1)).toThrow(GLError);
        });

        it('should throw GLError when source range exceeds data bounds', () => {
            const data = new Float32Array([1, 2]); // 8 bytes
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                byteSize: 100,
            });

            expect(() => buffer.updateRange(data, 0, 0, 100)).toThrow(GLError);
            expect(() => buffer.updateRange(data, 0, 0, 100)).toThrow(
                'Source range exceeds data bounds'
            );
        });

        it('should throw GLError when destination range exceeds buffer bounds', () => {
            const data = new Float32Array([1, 2]); // 8 bytes
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                byteSize: 4,
            });

            expect(() => buffer.updateRange(data, 0, 0, 8)).toThrow(GLError);
            expect(() => buffer.updateRange(data, 0, 0, 8)).toThrow(
                'Destination range exceeds buffer bounds'
            );
        });

        it('should throw GLError after dispose', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                byteSize: 100,
            });
            buffer.dispose();

            expect(() =>
                buffer.updateRange(new Float32Array([1]), 0)
            ).toThrow(GLError);
        });

        it('should handle raw ArrayBuffer data', () => {
            const ab = new ArrayBuffer(8);
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                byteSize: 8,
            });

            buffer.updateRange(ab, 0, 0, 8);

            expect(gl.bufferSubData).toHaveBeenCalled();
            const callArgs = (gl.bufferSubData as ReturnType<typeof vi.fn>).mock.calls[0];
            // For raw ArrayBuffer, it creates a Uint8Array view
            expect(callArgs[2]).toBeInstanceOf(Uint8Array);
        });

        it('should handle misaligned srcByteOffset by falling back to Uint8Array', () => {
            const data = new Float32Array([1, 2, 3, 4]); // 16 bytes
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                byteSize: 16,
            });

            // srcByteOffset = 1 is not aligned to Float32 (4 bytes)
            buffer.updateRange(data, 0, 1, 4);

            expect(gl.bufferSubData).toHaveBeenCalled();
            const callArgs = (gl.bufferSubData as ReturnType<typeof vi.fn>).mock.calls[0];
            expect(callArgs[2]).toBeInstanceOf(Uint8Array);
        });

        it('should return the buffer for chaining', () => {
            const data = new Float32Array([1, 2]);
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                byteSize: data.byteLength,
            });
            const result = buffer.updateRange(data, 0);
            expect(result).toBe(buffer);
        });
    });

    describe('resize', () => {
        it('should call bufferData with number size', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);

            buffer.resize(512);

            expect(gl.bindBuffer).toHaveBeenCalledWith(gl.ARRAY_BUFFER, buffer.id);
            expect(gl.bufferData).toHaveBeenCalledWith(gl.ARRAY_BUFFER, 512, gl.STATIC_DRAW);
            expect(buffer.byteLength).toBe(512);
        });

        it('should call bufferData with data', () => {
            const data = new Float32Array([1, 2, 3]);
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);

            buffer.resize(data);

            expect(gl.bufferData).toHaveBeenCalledWith(gl.ARRAY_BUFFER, data, gl.STATIC_DRAW);
            expect(buffer.byteLength).toBe(data.byteLength);
        });

        it('should update usage when provided', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);

            buffer.resize(256, gl.DYNAMIC_DRAW as GLBufferUsage);

            expect(gl.bufferData).toHaveBeenCalledWith(
                gl.ARRAY_BUFFER,
                256,
                gl.DYNAMIC_DRAW
            );
            expect(buffer.usage).toBe(gl.DYNAMIC_DRAW);
        });

        it('should preserve existing usage when not provided', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                usage: gl.DYNAMIC_DRAW as GLBufferUsage,
            });

            buffer.resize(256);

            expect(gl.bufferData).toHaveBeenCalledWith(
                gl.ARRAY_BUFFER,
                256,
                gl.DYNAMIC_DRAW
            );
        });

        it('should throw GLError for negative size', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);

            expect(() => buffer.resize(-1)).toThrow(GLError);
            expect(() => buffer.resize(-1)).toThrow('Buffer size cannot be negative');
        });

        it('should throw GLError with INVALID_VALUE for negative size', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);

            try {
                buffer.resize(-1);
                expect.unreachable('Should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(GLError);
                expect((e as GLError).code).toBe('INVALID_VALUE');
            }
        });

        it('should throw GLError after dispose', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);
            buffer.dispose();

            expect(() => buffer.resize(100)).toThrow(GLError);
        });

        it('should return the buffer for chaining', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);
            const result = buffer.resize(100);
            expect(result).toBe(buffer);
        });

        it('should handle zero size', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);
            buffer.resize(0);
            expect(buffer.byteLength).toBe(0);
            expect(gl.bufferData).toHaveBeenCalledWith(gl.ARRAY_BUFFER, 0, gl.STATIC_DRAW);
        });
    });

    describe('copyTo', () => {
        it('should use COPY_READ_BUFFER and COPY_WRITE_BUFFER targets', () => {
            const src = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 64 });
            const dst = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 64 });

            (gl.bindBuffer as ReturnType<typeof vi.fn>).mockClear();
            src.copyTo(dst);

            expect(gl.bindBuffer).toHaveBeenCalledWith(gl.COPY_READ_BUFFER, src.id);
            expect(gl.bindBuffer).toHaveBeenCalledWith(gl.COPY_WRITE_BUFFER, dst.id);
        });

        it('should call copyBufferSubData with correct arguments', () => {
            const src = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 64 });
            const dst = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 64 });

            src.copyTo(dst, 0, 0, 32);

            expect(gl.copyBufferSubData).toHaveBeenCalledWith(
                gl.COPY_READ_BUFFER,
                gl.COPY_WRITE_BUFFER,
                0,
                0,
                32
            );
        });

        it('should compute copy size from min of available space when size is not given', () => {
            const src = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 64 });
            const dst = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 32 });

            src.copyTo(dst);

            // min(64 - 0, 32 - 0) = 32
            expect(gl.copyBufferSubData).toHaveBeenCalledWith(
                gl.COPY_READ_BUFFER,
                gl.COPY_WRITE_BUFFER,
                0,
                0,
                32
            );
        });

        it('should unbind COPY_READ/WRITE_BUFFER after copy', () => {
            const src = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 64 });
            const dst = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 64 });

            src.copyTo(dst);

            const bindCalls = (gl.bindBuffer as ReturnType<typeof vi.fn>).mock.calls;
            // Last two calls should unbind (null)
            const lastTwo = bindCalls.slice(-2);
            expect(lastTwo[0]).toEqual([gl.COPY_READ_BUFFER, null]);
            expect(lastTwo[1]).toEqual([gl.COPY_WRITE_BUFFER, null]);
        });

        it('should throw GLError when destination buffer is disposed', () => {
            const src = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 64 });
            const dst = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 64 });
            dst.dispose();

            expect(() => src.copyTo(dst)).toThrow(GLError);
            expect(() => src.copyTo(dst)).toThrow('Cannot copy to a disposed buffer');
        });

        it('should throw GLError with BUFFER_ALREADY_DISPOSED for disposed dest', () => {
            const src = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 64 });
            const dst = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 64 });
            dst.dispose();

            try {
                src.copyTo(dst);
                expect.unreachable('Should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(GLError);
                expect((e as GLError).code).toBe('BUFFER_ALREADY_DISPOSED');
            }
        });

        it('should throw GLError when offsets are negative', () => {
            const src = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 64 });
            const dst = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 64 });

            expect(() => src.copyTo(dst, -1, 0)).toThrow(GLError);
            expect(() => src.copyTo(dst, -1, 0)).toThrow('Offsets cannot be negative');
        });

        it('should throw GLError when dstOffset is negative', () => {
            const src = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 64 });
            const dst = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 64 });

            expect(() => src.copyTo(dst, 0, -1)).toThrow(GLError);
        });

        it('should throw GLError when source range exceeds buffer bounds', () => {
            const src = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 32 });
            const dst = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 64 });

            expect(() => src.copyTo(dst, 0, 0, 64)).toThrow(GLError);
            expect(() => src.copyTo(dst, 0, 0, 64)).toThrow(
                'Source range exceeds buffer bounds'
            );
        });

        it('should throw GLError when destination range exceeds buffer bounds', () => {
            const src = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 64 });
            const dst = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 32 });

            expect(() => src.copyTo(dst, 0, 0, 64)).toThrow(GLError);
            expect(() => src.copyTo(dst, 0, 0, 64)).toThrow(
                'Destination range exceeds buffer bounds'
            );
        });

        it('should throw GLError after dispose', () => {
            const src = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 64 });
            const dst = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 64 });
            src.dispose();

            expect(() => src.copyTo(dst)).toThrow(GLError);
        });

        it('should return early (no copy) when copySize is 0 or negative', () => {
            const src = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 0 });
            const dst = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 0 });

            src.copyTo(dst);

            expect(gl.copyBufferSubData).not.toHaveBeenCalled();
        });

        it('should return the buffer for chaining', () => {
            const src = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 64 });
            const dst = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 64 });
            const result = src.copyTo(dst);
            expect(result).toBe(src);
        });
    });

    describe('getData', () => {
        it('should use PIXEL_PACK_BUFFER target', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 16 });
            const output = new Float32Array(4);

            buffer.getData(output);

            expect(gl.bindBuffer).toHaveBeenCalledWith(gl.PIXEL_PACK_BUFFER, buffer.id);
        });

        it('should call getBufferSubData', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 16 });
            const output = new Float32Array(4);

            buffer.getData(output);

            expect(gl.getBufferSubData).toHaveBeenCalledWith(
                gl.PIXEL_PACK_BUFFER,
                0,
                output,
                0,
                4 // 16 bytes / 4 bytes per element
            );
        });

        it('should respect byteOffset parameter', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 32 });
            const output = new Float32Array(4);

            buffer.getData(output, 8);

            expect(gl.getBufferSubData).toHaveBeenCalledWith(
                gl.PIXEL_PACK_BUFFER,
                8,
                output,
                0,
                4 // min(32 - 8, 16) = 16 bytes / 4 = 4 elements
            );
        });

        it('should respect length parameter', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 32 });
            const output = new Float32Array(4);

            buffer.getData(output, 0, 8);

            expect(gl.getBufferSubData).toHaveBeenCalledWith(
                gl.PIXEL_PACK_BUFFER,
                0,
                output,
                0,
                2 // 8 bytes / 4 bytes per element
            );
        });

        it('should unbind PIXEL_PACK_BUFFER after read (in finally)', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 16 });
            const output = new Float32Array(4);

            buffer.getData(output);

            const bindCalls = (gl.bindBuffer as ReturnType<typeof vi.fn>).mock.calls;
            const lastCall = bindCalls[bindCalls.length - 1];
            expect(lastCall).toEqual([gl.PIXEL_PACK_BUFFER, null]);
        });

        it('should return the output view', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 16 });
            const output = new Float32Array(4);

            const result = buffer.getData(output);
            expect(result).toBe(output);
        });

        it('should throw GLError when byteOffset is negative', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 16 });
            const output = new Float32Array(4);

            expect(() => buffer.getData(output, -1)).toThrow(GLError);
            expect(() => buffer.getData(output, -1)).toThrow('Offset cannot be negative');
        });

        it('should throw GLError after dispose', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 16 });
            buffer.dispose();

            expect(() => buffer.getData(new Float32Array(4))).toThrow(GLError);
        });

        it('should return early when readLength is 0', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 0 });
            const output = new Float32Array(4);

            const result = buffer.getData(output);
            expect(result).toBe(output);
            expect(gl.getBufferSubData).not.toHaveBeenCalled();
        });

        it('should throw GLError when getBufferSubData is not a function', () => {
            const noGetBufferGL = createMockGL();
            (noGetBufferGL as any).getBufferSubData = undefined;

            const buffer = new Buffer(noGetBufferGL, noGetBufferGL.ARRAY_BUFFER as GLBufferTarget, {
                byteSize: 16,
            });
            const output = new Float32Array(4);

            expect(() => buffer.getData(output)).toThrow(GLError);
            expect(() => buffer.getData(output)).toThrow('getBufferSubData is not supported');
        });
    });

    describe('getSubData', () => {
        it('should use PIXEL_PACK_BUFFER target', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 32 });
            const output = new Float32Array(4);

            buffer.getSubData(output, 0);

            expect(gl.bindBuffer).toHaveBeenCalledWith(gl.PIXEL_PACK_BUFFER, buffer.id);
        });

        it('should call getBufferSubData with src/dst offsets', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 32 });
            const output = new Float32Array(4);

            buffer.getSubData(output, 8, 4);

            // maxLength = min(32-8, 16-4) = 12; alignedLength = floor(12/4)*4 = 12
            // dstElementOffset = floor(4/4) = 1; elements = 12/4 = 3
            expect(gl.getBufferSubData).toHaveBeenCalledWith(
                gl.PIXEL_PACK_BUFFER,
                8,
                output,
                1, // dstElementOffset = floor(4 / 4)
                3 // alignedLength / bytesPerElement = 12 / 4
            );
        });

        it('should handle misaligned dstByteOffset via temp buffer', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 32 });
            const output = new Float32Array(4);

            // dstByteOffset = 1 is not aligned to Float32 (4 bytes)
            buffer.getSubData(output, 0, 1, 4);

            // Should use temp buffer approach
            expect(gl.getBufferSubData).toHaveBeenCalled();
            const callArgs = (gl.getBufferSubData as ReturnType<typeof vi.fn>).mock.calls[0];
            // The third arg should be a Uint8Array temp buffer
            expect(callArgs[2]).toBeInstanceOf(Uint8Array);
        });

        it('should throw GLError when srcByteOffset is negative', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 32 });
            const output = new Float32Array(4);

            expect(() => buffer.getSubData(output, -1)).toThrow(GLError);
            expect(() => buffer.getSubData(output, -1)).toThrow('Offsets cannot be negative');
        });

        it('should throw GLError when dstByteOffset is negative', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 32 });
            const output = new Float32Array(4);

            expect(() => buffer.getSubData(output, 0, -1)).toThrow(GLError);
        });

        it('should throw GLError when dstByteOffset exceeds output size', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 32 });
            const output = new Float32Array(4); // 16 bytes

            expect(() => buffer.getSubData(output, 0, 16)).toThrow(GLError);
            expect(() => buffer.getSubData(output, 0, 16)).toThrow(
                'Destination offset exceeds output buffer size'
            );
        });

        it('should throw GLError after dispose', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 32 });
            buffer.dispose();

            expect(() => buffer.getSubData(new Float32Array(4), 0)).toThrow(GLError);
        });

        it('should return the output view', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 32 });
            const output = new Float32Array(4);

            const result = buffer.getSubData(output, 0);
            expect(result).toBe(output);
        });

        it('should return early when readLength is 0', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 0 });
            const output = new Float32Array(4);

            const result = buffer.getSubData(output, 0);
            expect(result).toBe(output);
            expect(gl.getBufferSubData).not.toHaveBeenCalled();
        });

        it('should unbind PIXEL_PACK_BUFFER in finally', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 32 });
            const output = new Float32Array(4);

            buffer.getSubData(output, 0);

            const bindCalls = (gl.bindBuffer as ReturnType<typeof vi.fn>).mock.calls;
            const lastCall = bindCalls[bindCalls.length - 1];
            expect(lastCall).toEqual([gl.PIXEL_PACK_BUFFER, null]);
        });
    });

    describe('dispose', () => {
        it('should call gl.deleteBuffer', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);
            buffer.dispose();
            expect(gl.deleteBuffer).toHaveBeenCalledTimes(1);
        });

        it('should set isDisposed to true', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);
            buffer.dispose();
            expect(buffer.isDisposed).toBe(true);
        });

        it('should be idempotent (calling dispose twice does not error)', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);
            buffer.dispose();
            expect(() => buffer.dispose()).not.toThrow();
            // deleteBuffer should only be called once
            expect(gl.deleteBuffer).toHaveBeenCalledTimes(1);
        });

        it('should throw GLError on property access after dispose (id)', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);
            buffer.dispose();

            expect(() => buffer.id).toThrow(GLError);
            try {
                void buffer.id;
                expect.unreachable('Should have thrown');
            } catch (e) {
                expect((e as GLError).code).toBe('BUFFER_ALREADY_DISPOSED');
            }
        });

        it('should still allow reading target after dispose', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);
            buffer.dispose();
            // target does not throw
            expect(buffer.target).toBe(gl.ARRAY_BUFFER);
        });

        it('should still allow reading byteLength after dispose', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, { byteSize: 64 });
            buffer.dispose();
            expect(buffer.byteLength).toBe(64);
        });

        it('should still allow reading usage after dispose', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget);
            buffer.dispose();
            expect(buffer.usage).toBe(gl.STATIC_DRAW);
        });

        it('should still allow reading label after dispose', () => {
            const buffer = new Buffer(gl, gl.ARRAY_BUFFER as GLBufferTarget, {
                label: 'test',
            });
            buffer.dispose();
            expect(buffer.label).toBe('test');
        });

        it('should check bindings and unbind if buffer is currently bound', () => {
            let createdBuffer: WebGLBuffer | null = null;
            const glWithBinding = createMockGL();
            (glWithBinding.createBuffer as ReturnType<typeof vi.fn>).mockImplementation(() => {
                createdBuffer = {} as WebGLBuffer;
                return createdBuffer;
            });
            // getParameter returns our buffer for ARRAY_BUFFER + 0x20
            (glWithBinding.getParameter as ReturnType<typeof vi.fn>).mockImplementation(
                (target: number) => {
                    if (target === glWithBinding.ARRAY_BUFFER + 0x20) {
                        return createdBuffer;
                    }
                    return null;
                }
            );

            const buffer = new Buffer(glWithBinding, glWithBinding.ARRAY_BUFFER as GLBufferTarget);
            buffer.dispose();

            // Should have called bindBuffer(target, null) to unbind
            expect(glWithBinding.bindBuffer).toHaveBeenCalledWith(
                glWithBinding.ARRAY_BUFFER,
                null
            );
        });
    });
});

// ---------------------------------------------------------------------------
// BufferFactory (via createBufferFactory)
// ---------------------------------------------------------------------------

describe('BufferFactory', () => {
    let gl: WebGL2RenderingContext;
    let factory: IBufferFactory & { dispose(): void; isDisposed: boolean };

    beforeEach(() => {
        gl = createMockGL();
        factory = createBufferFactory(gl) as IBufferFactory & {
            dispose(): void;
            isDisposed: boolean;
        };
    });

    describe('createBuffer', () => {
        it('should create a Buffer with the given target', () => {
            const buffer = factory.createBuffer(gl.ARRAY_BUFFER as GLBufferTarget);
            expect(buffer).toBeDefined();
            expect(buffer.target).toBe(gl.ARRAY_BUFFER);
        });

        it('should pass options to the Buffer constructor', () => {
            const data = new Float32Array([1, 2, 3]);
            const buffer = factory.createBuffer(gl.ARRAY_BUFFER as GLBufferTarget, {
                initialData: data,
                usage: gl.DYNAMIC_DRAW as GLBufferUsage,
                label: 'test',
            });

            expect(buffer.byteLength).toBe(data.byteLength);
            expect(buffer.usage).toBe(gl.DYNAMIC_DRAW);
            expect(buffer.label).toBe('test');
        });
    });

    describe('createArrayBuffer', () => {
        it('should create a buffer with ARRAY_BUFFER target', () => {
            const buffer = factory.createArrayBuffer();
            expect(buffer.target).toBe(gl.ARRAY_BUFFER);
        });

        it('should accept options', () => {
            const buffer = factory.createArrayBuffer({ byteSize: 128 });
            expect(buffer.byteLength).toBe(128);
        });
    });

    describe('createElementArrayBuffer', () => {
        it('should create a buffer with ELEMENT_ARRAY_BUFFER target', () => {
            const buffer = factory.createElementArrayBuffer();
            expect(buffer.target).toBe(gl.ELEMENT_ARRAY_BUFFER);
        });
    });

    describe('createUniformBuffer', () => {
        it('should create a buffer with UNIFORM_BUFFER target', () => {
            const buffer = factory.createUniformBuffer();
            expect(buffer.target).toBe(gl.UNIFORM_BUFFER);
        });
    });

    describe('createBufferFromData', () => {
        it('should create a buffer with initial data', () => {
            const data = new Float32Array([1, 2, 3, 4]);
            const buffer = factory.createBufferFromData(
                gl.ARRAY_BUFFER as GLBufferTarget,
                data
            );

            expect(buffer.byteLength).toBe(data.byteLength);
            expect(gl.bufferData).toHaveBeenCalled();
        });

        it('should accept custom usage', () => {
            const data = new Float32Array([1, 2]);
            factory.createBufferFromData(
                gl.ARRAY_BUFFER as GLBufferTarget,
                data,
                gl.DYNAMIC_DRAW as GLBufferUsage
            );

            expect(gl.bufferData).toHaveBeenCalledWith(
                gl.ARRAY_BUFFER,
                data,
                gl.DYNAMIC_DRAW
            );
        });

        it('should default usage to STATIC_DRAW', () => {
            const data = new Float32Array([1]);
            factory.createBufferFromData(gl.ARRAY_BUFFER as GLBufferTarget, data);

            expect(gl.bufferData).toHaveBeenCalledWith(
                gl.ARRAY_BUFFER,
                data,
                gl.STATIC_DRAW
            );
        });
    });

    describe('createArrayBufferFromData', () => {
        it('should create an ARRAY_BUFFER with initial data', () => {
            const data = new Uint16Array([1, 2, 3]);
            const buffer = factory.createArrayBufferFromData(data);

            expect(buffer.target).toBe(gl.ARRAY_BUFFER);
            expect(buffer.byteLength).toBe(data.byteLength);
        });

        it('should accept custom usage', () => {
            const data = new Uint16Array([1, 2]);
            factory.createArrayBufferFromData(data, gl.STREAM_DRAW as GLBufferUsage);

            expect(gl.bufferData).toHaveBeenCalledWith(
                gl.ARRAY_BUFFER,
                data,
                gl.STREAM_DRAW
            );
        });
    });

    describe('createElementArrayBufferFromData', () => {
        it('should create an ELEMENT_ARRAY_BUFFER with initial data', () => {
            const data = new Uint16Array([0, 1, 2]);
            const buffer = factory.createElementArrayBufferFromData(data);

            expect(buffer.target).toBe(gl.ELEMENT_ARRAY_BUFFER);
            expect(buffer.byteLength).toBe(data.byteLength);
        });
    });

    describe('createUniformBufferFromData', () => {
        it('should create a UNIFORM_BUFFER with initial data', () => {
            const data = new Float32Array(16);
            const buffer = factory.createUniformBufferFromData(data);

            expect(buffer.target).toBe(gl.UNIFORM_BUFFER);
            expect(buffer.byteLength).toBe(data.byteLength);
        });
    });

    describe('createPool', () => {
        it('should create a BufferPool', () => {
            const pool = factory.createPool<ArrayBuffer>();
            expect(pool).toBeDefined();
            expect(typeof pool.allocate).toBe('function');
            expect(typeof pool.release).toBe('function');
            expect(typeof pool.acquire).toBe('function');
            expect(typeof pool.dispose).toBe('function');
        });

        it('should allocate buffers from the pool', () => {
            const pool = factory.createPool<ArrayBuffer>();
            const buffer = pool.allocate(64);

            expect(buffer).toBeDefined();
            expect(buffer.byteLength).toBe(64);
        });

        it('should release buffers back to the pool', () => {
            const pool = factory.createPool<ArrayBuffer>();
            const buffer = pool.allocate(64);
            expect(() => pool.release(buffer)).not.toThrow();
        });

        it('should acquire buffers with data', () => {
            const pool = factory.createPool<Float32Array>();
            const data = new Float32Array([1, 2, 3, 4]);
            const buffer = pool.acquire(data);

            expect(buffer).toBeDefined();
            expect(buffer.byteLength).toBe(data.byteLength);
        });
    });

    describe('dispose', () => {
        it('should dispose the factory', () => {
            factory.dispose();
            expect(factory.isDisposed).toBe(true);
        });

        it('should be idempotent', () => {
            factory.dispose();
            expect(() => factory.dispose()).not.toThrow();
        });

        it('should dispose all tracked resources', () => {
            const buffer1 = factory.createArrayBuffer({ byteSize: 64 });
            const buffer2 = factory.createArrayBuffer({ byteSize: 128 });

            factory.dispose();

            expect(buffer1.isDisposed).toBe(true);
            expect(buffer2.isDisposed).toBe(true);
        });

        it('should remove the webglcontextlost event listener', () => {
            factory.dispose();
            expect(gl.canvas.removeEventListener).toHaveBeenCalledWith(
                'webglcontextlost',
                expect.any(Function)
            );
        });

        it('should throw GLError on createBuffer after dispose', () => {
            factory.dispose();

            expect(() => factory.createBuffer(gl.ARRAY_BUFFER as GLBufferTarget)).toThrow(
                GLError
            );
            expect(() => factory.createBuffer(gl.ARRAY_BUFFER as GLBufferTarget)).toThrow(
                'BufferFactory has been disposed'
            );
        });

        it('should throw GLError with INVALID_OPERATION code after dispose', () => {
            factory.dispose();

            try {
                factory.createArrayBuffer();
                expect.unreachable('Should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(GLError);
                expect((e as GLError).code).toBe('INVALID_OPERATION');
            }
        });

        it('should throw on createArrayBuffer after dispose', () => {
            factory.dispose();
            expect(() => factory.createArrayBuffer()).toThrow(GLError);
        });

        it('should throw on createElementArrayBuffer after dispose', () => {
            factory.dispose();
            expect(() => factory.createElementArrayBuffer()).toThrow(GLError);
        });

        it('should throw on createUniformBuffer after dispose', () => {
            factory.dispose();
            expect(() => factory.createUniformBuffer()).toThrow(GLError);
        });

        it('should throw on createBufferFromData after dispose', () => {
            factory.dispose();
            expect(() =>
                factory.createBufferFromData(gl.ARRAY_BUFFER as GLBufferTarget, new Float32Array(1))
            ).toThrow(GLError);
        });

        it('should throw on createArrayBufferFromData after dispose', () => {
            factory.dispose();
            expect(() =>
                factory.createArrayBufferFromData(new Float32Array(1))
            ).toThrow(GLError);
        });

        it('should throw on createElementArrayBufferFromData after dispose', () => {
            factory.dispose();
            expect(() =>
                factory.createElementArrayBufferFromData(new Uint16Array(1))
            ).toThrow(GLError);
        });

        it('should throw on createUniformBufferFromData after dispose', () => {
            factory.dispose();
            expect(() =>
                factory.createUniformBufferFromData(new Float32Array(1))
            ).toThrow(GLError);
        });

        it('should throw on createPool after dispose', () => {
            factory.dispose();
            expect(() => factory.createPool()).toThrow(GLError);
        });
    });

    describe('context lost handling', () => {
        it('should register webglcontextlost event listener on construction', () => {
            expect(gl.canvas.addEventListener).toHaveBeenCalledWith(
                'webglcontextlost',
                expect.any(Function)
            );
        });

        it('should dispose factory when context is lost', () => {
            // Get the handler that was registered
            const addEventCalls = (gl.canvas.addEventListener as ReturnType<typeof vi.fn>).mock.calls;
            const contextLostHandler = addEventCalls.find(
                (call: unknown[]) => call[0] === 'webglcontextlost'
            )?.[1] as ((event: Event) => void) | undefined;

            expect(contextLostHandler).toBeDefined();

            // Simulate context lost
            const mockEvent = { preventDefault: vi.fn() };
            contextLostHandler!(mockEvent as unknown as Event);

            expect(mockEvent.preventDefault).toHaveBeenCalled();
            expect(factory.isDisposed).toBe(true);
        });
    });
});
