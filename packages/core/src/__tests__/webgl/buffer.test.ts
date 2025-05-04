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

