import { createBufferFactory, GLError } from '../../renderer/webgl2/buffer';

function createMockGL() {
    const bufferStore = new WeakSet<WebGLBuffer>();
    const nextBufferId = { count: 1 };
    const makeBuffer = () => ({ id: nextBufferId.count++ } as unknown as WebGLBuffer);
    const gl: any = {
        ARRAY_BUFFER: 0x8892,
        ELEMENT_ARRAY_BUFFER: 0x8893,
        COPY_READ_BUFFER: 0x8F36,
        COPY_WRITE_BUFFER: 0x8F37,
        PIXEL_PACK_BUFFER: 0x88EB,
        PIXEL_UNPACK_BUFFER: 0x88EC,
        STATIC_DRAW: 0x88E4,
        DYNAMIC_DRAW: 0x88E8,
        createBuffer: jest.fn(() => {
            const b = makeBuffer();
            bufferStore.add(b);
            return b;
        }),
        bindBuffer: jest.fn(),
        bufferData: jest.fn(),
        bufferSubData: jest.fn(),
        copyBufferSubData: jest.fn(),
        getBufferSubData: jest.fn((target, srcOff, dst, dstOff?, len?) => {
            const view = dst as Uint8Array;
            for (let i = 0; i < (len ?? view.length); i++) {
                view[(dstOff as number) + i] = 42 + i;
            }
        }),
        getParameter: jest.fn((p) => null),
        deleteBuffer: jest.fn(),
        getExtension: jest.fn(() => null),
        canvas: {
            addEventListener: jest.fn(),
            removeEventListener: jest.fn(),
        },
    };
    return gl as WebGL2RenderingContext;
}

describe('Buffer (via createBufferFactory)', () => {
    let gl: WebGL2RenderingContext;
    let factory: ReturnType<typeof createBufferFactory>;

    beforeEach(() => {
        gl = createMockGL();
        factory = createBufferFactory(gl);
    });

    test('constructor with initialData calls bufferData and sets byteLength', () => {
        const data = new Uint8Array([1, 2, 3, 4]);
        const buf = factory.createArrayBufferFromData(data, gl.DYNAMIC_DRAW);
        expect(gl.bufferData).toHaveBeenCalledWith(gl.ARRAY_BUFFER, data, gl.DYNAMIC_DRAW);
        expect(buf.byteLength).toBe(data.byteLength);
        expect(buf.usage).toBe(gl.DYNAMIC_DRAW);
    });

    test('resize(number) updates byteLength and usage', () => {
        const buf = factory.createArrayBuffer();
        buf.resize(16, gl.DYNAMIC_DRAW);
        expect(gl.bufferData).toHaveBeenLastCalledWith(gl.ARRAY_BUFFER, 16, gl.DYNAMIC_DRAW);
        expect(buf.byteLength).toBe(16);
        expect(buf.usage).toBe(gl.DYNAMIC_DRAW);
    });

    test('resize(ArrayBuffer) updates byteLength', () => {
        const arr = new ArrayBuffer(10);
        const buf = factory.createArrayBuffer();
        buf.resize(arr);
        expect(gl.bufferData).toHaveBeenLastCalledWith(gl.ARRAY_BUFFER, arr, gl.STATIC_DRAW);
        expect(buf.byteLength).toBe(10);
    });

    describe('update()', () => {
        let buf: ReturnType<typeof factory.createArrayBuffer>;

        beforeEach(() => {
            buf = factory.createArrayBuffer();
            buf.resize(8);
            (gl.bufferSubData as jest.Mock).mockClear();
        });

        test('writes ArrayBuffer at offset', () => {
            const arr = new ArrayBuffer(4);
            buf.update(arr, 2);
            expect(gl.bufferSubData).toHaveBeenCalledWith(gl.ARRAY_BUFFER, 2, arr);
        });

        test('writes TypedArray at offset', () => {
            const ta = new Uint8Array([9, 8, 7]);
            buf.update(ta, 1);
            expect(gl.bufferSubData).toHaveBeenCalledWith(gl.ARRAY_BUFFER, 1, ta);
        });

        test('throws on invalid data type', () => {
            // @ts-expect-error
            expect(() => buf.update({})).toThrow(GLError);
        });

        test('throws on negative offset', () => {
            const arr = new ArrayBuffer(2);
            expect(() => buf.update(arr, -1)).toThrow(GLError);
        });

        test('throws when update exceeds bounds', () => {
            const arr = new Uint8Array(5);
            expect(() => buf.update(arr, 5)).toThrow(GLError);
        });
    });

    describe('updateRange()', () => {
        let buf: ReturnType<typeof factory.createArrayBuffer>;

        beforeEach(() => {
            buf = factory.createArrayBuffer();
            buf.resize(10);
            (gl.bufferSubData as jest.Mock).mockClear();
        });

        test('writes a sub-range of TypedArray', () => {
            const data = new Uint8Array([1, 2, 3, 4, 5]);
            buf.updateRange(data, 2, 1, 3);
            // Uint8Array view of length 3 starting at data.byteOffset+1
            const viewArg = (gl.bufferSubData as jest.Mock).mock.calls[0][2];
            expect(viewArg.byteLength).toBe(3);
            expect(gl.bufferSubData).toHaveBeenCalledWith(gl.ARRAY_BUFFER, 2, viewArg);
        });

        test('throws on negative dst offset', () => {
            const data = new Uint8Array(2);
            expect(() => buf.updateRange(data, -1)).toThrow(GLError);
        });

        test('throws on negative src offset', () => {
            const data = new Uint8Array(2);
            expect(() => buf.updateRange(data, 0, -5)).toThrow(GLError);
        });

        test('throws when src range exceeds data', () => {
            const data = new Uint8Array(2);
            expect(() => buf.updateRange(data, 0, 1, 5)).toThrow(GLError);
        });

        test('throws when dst range exceeds buffer', () => {
            const data = new Uint8Array(5);
            expect(() => buf.updateRange(data, 8, 0, 5)).toThrow(GLError);
        });
    });

    describe('copyTo()', () => {
        let src: ReturnType<typeof factory.createArrayBuffer>;
        let dst: ReturnType<typeof factory.createArrayBuffer>;

        beforeEach(() => {
            src = factory.createArrayBuffer();
            dst = factory.createArrayBuffer();
            src.resize(8);
            dst.resize(8);
            (gl.copyBufferSubData as jest.Mock).mockClear();
            (gl.bindBuffer as jest.Mock).mockClear();
        });

        test('copies correct size and offsets', () => {
            src.copyTo(dst, 1, 2, 4);
            expect(gl.bindBuffer).toHaveBeenCalledWith(gl.COPY_READ_BUFFER, src.id as any);
            expect(gl.bindBuffer).toHaveBeenCalledWith(gl.COPY_WRITE_BUFFER, dst.id as any);
            expect(gl.copyBufferSubData).toHaveBeenCalledWith(
                gl.COPY_READ_BUFFER,
                gl.COPY_WRITE_BUFFER,
                1,
                2,
                4
            );
        });

        test('defaults size to min available', () => {
            src.copyTo(dst, 2, 3);
            const sizeArg = (gl.copyBufferSubData as jest.Mock).mock.calls[0][4];
            expect(sizeArg).toBe(Math.min(8 - 2, 8 - 3));
        });

        test('throws on negative offsets', () => {
            expect(() => src.copyTo(dst, -1, 0)).toThrow(GLError);
            expect(() => src.copyTo(dst, 0, -1)).toThrow(GLError);
        });

        test('throws when dest is disposed', () => {
            dst.dispose();
            expect(() => src.copyTo(dst)).toThrow(GLError);
        });

        test('throws when ranges exceed bounds', () => {
            expect(() => src.copyTo(dst, 5, 5, 10)).toThrow(GLError);
        });
    });

    describe('getData()', () => {
        let buf: ReturnType<typeof factory.createArrayBuffer>;

        beforeEach(() => {
            buf = factory.createArrayBuffer();
            buf.resize(6);
            (gl.getBufferSubData as jest.Mock).mockClear();
            (gl.bindBuffer as jest.Mock).mockClear();
        });

        test('reads into TypedArray', () => {
            const out = new Uint8Array(4);
            const result = buf.getData(out, 1, 2);
            expect(result).toBe(out);
            expect(gl.bindBuffer).toHaveBeenCalledWith(gl.PIXEL_PACK_BUFFER, buf.id as any);
            expect(gl.getBufferSubData).toHaveBeenCalledWith(
                gl.PIXEL_PACK_BUFFER,
                1,
                out,
                0,
                2
            );
            // verify our mock filled data
            expect(out[0]).toBe(42);
            expect(out[1]).toBe(43);
        });

        test('throws on invalid output', () => {
            // @ts-expect-error
            expect(() => buf.getData({}, 0)).toThrow(GLError);
        });

        test('throws on negative offset', () => {
            const out = new Uint8Array(2);
            expect(() => buf.getData(out, -1)).toThrow(GLError);
        });

        test('returns early when length <= 0', () => {
            const out = new Uint8Array(2);
            const res = buf.getData(out, 10, 5);
            expect(res).toBe(out);
            expect(gl.getBufferSubData).not.toHaveBeenCalled();
        });
    });

    describe('getSubData()', () => {
        let buf: ReturnType<typeof factory.createArrayBuffer>;

        beforeEach(() => {
            buf = factory.createArrayBuffer();
            buf.resize(8);
            (gl.getBufferSubData as jest.Mock).mockClear();
            (gl.bindBuffer as jest.Mock).mockClear();
        });

        test('reads aligned subrange', () => {
            const out = new Uint16Array(4);
            buf.getSubData(out, 2, 2, 2);
            expect(gl.getBufferSubData).toHaveBeenCalledWith(
                gl.PIXEL_PACK_BUFFER,
                2,
                out,
                1,
                1 // 2 bytes => 1 element
            );
        });

        test('reads unaligned dstOffset with temp buffer', () => {
            const out = new Uint8Array(4);
            buf.getSubData(out, 0, 1, 3);
            // first call fills tempBuffer
            expect(gl.getBufferSubData).toHaveBeenCalled();
        });

        test('throws on invalid output', () => {
            // @ts-expect-error
            expect(() => buf.getSubData({}, 0)).toThrow(GLError);
        });

        test('throws on negative offsets', () => {
            const out = new Uint8Array(2);
            expect(() => buf.getSubData(out, -1, 0)).toThrow(GLError);
            expect(() => buf.getSubData(out, 0, -1)).toThrow(GLError);
        });

        test('throws when dstOffset >= output.byteLength', () => {
            const out = new Uint8Array(2);
            expect(() => buf.getSubData(out, 0, 2)).toThrow(GLError);
        });
    });

    describe('dispose()', () => {
        let buf: ReturnType<typeof factory.createArrayBuffer>;
        beforeEach(() => {
            buf = factory.createArrayBuffer();
            (gl.getParameter as jest.Mock).mockReturnValue(buf.id);
            (gl.bindBuffer as jest.Mock).mockClear();
            (gl.deleteBuffer as jest.Mock).mockClear();
        });

        test('unbinds from all targets and deletes buffer', () => {
            const id = buf.id as any;
            buf.dispose();
            expect(gl.deleteBuffer).toHaveBeenCalledWith(id);
            expect(buf.isDisposed).toBe(true);
        });

        test('id getter throws after dispose', () => {
            buf.dispose();
            expect(() => { const x = buf.id; }).toThrow(GLError);
        });

        test('operations throw after dispose', () => {
            buf.dispose();
            expect(() => buf.bind()).toThrow(GLError);
            expect(() => buf.resize(1)).toThrow(GLError);
            expect(() => buf.update(new ArrayBuffer(1))).toThrow(GLError);
        });
    });
});