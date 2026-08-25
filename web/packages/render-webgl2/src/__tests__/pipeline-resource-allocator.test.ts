import { beforeAll, describe, expect, it, vi } from 'vitest';
import { createWebGL2RenderResourceAllocator } from '../pipeline-resource-allocator';
import type { RenderTextureDescriptor } from '@axrone/render-core/types';

// WebGL2 mock with format constants
const GL_CONSTANTS: Record<string, number> = {
    R11F_G11F_B10F: 0x8c3a,
    RGB: 0x1907,
    UNSIGNED_INT_10F_11F_11F_REV: 0x8c3b,
    RGBA8: 0x8058,
    RGBA: 0x1908,
    UNSIGNED_BYTE: 0x1401,
    RGBA16F: 0x881a,
    HALF_FLOAT: 0x140b,
    RGBA32F: 0x8814,
    FLOAT: 0x1406,
    RG16F: 0x822f,
    RG: 0x8227,
    RG32F: 0x8230,
    R16F: 0x822d,
    RED: 0x1903,
    R32F: 0x822e,
    DEPTH_COMPONENT24: 0x81a6,
    DEPTH_COMPONENT: 0x1902,
    UNSIGNED_INT: 0x1405,
    DEPTH_COMPONENT32F: 0x8cac,
    DEPTH24_STENCIL8: 0x88f0,
    DEPTH_STENCIL: 0x84f9,
    UNSIGNED_INT_24_8: 0x84fa,
    TEXTURE_2D: 0x0de1,
    TEXTURE_CUBE_MAP: 0x8513,
    TEXTURE_2D_ARRAY: 0x8c1a,
    TEXTURE_3D: 0x806f,
    TEXTURE_MIN_FILTER: 0x2801,
    TEXTURE_MAG_FILTER: 0x2800,
    TEXTURE_WRAP_S: 0x2802,
    TEXTURE_WRAP_T: 0x2803,
    TEXTURE_WRAP_R: 0x8072,
    NEAREST: 0x2600,
    CLAMP_TO_EDGE: 0x812f,
};

function createGLMock() {
    const deletedTextures: WebGLTexture[] = [];
    const createdTextures: WebGLTexture[] = [];
    const gl = new Proxy(
        {
            ...GL_CONSTANTS,
            _deletedTextures: deletedTextures,
            _createdTextures: createdTextures,
            createTexture() {
                const tex = {} as WebGLTexture;
                createdTextures.push(tex);
                return tex;
            },
            deleteTexture(tex: WebGLTexture) {
                deletedTextures.push(tex);
            },
            bindTexture() {},
            texParameteri() {},
            texStorage2D() {},
            texStorage3D() {},
        },
        {
            get: (target, property) => {
                if (property in target) return (target as any)[property];
                if (typeof property === 'string' && property in GL_CONSTANTS) {
                    return GL_CONSTANTS[property];
                }
                return undefined;
            },
        }
    );
    return gl as unknown as WebGL2RenderingContext & {
        _deletedTextures: WebGLTexture[];
        _createdTextures: WebGLTexture[];
    };
}

describe('createWebGL2RenderResourceAllocator', () => {
    describe('createTexture — present usage', () => {
        it('returns default-framebuffer handle for present usage', () => {
            const gl = createGLMock();
            const allocator = createWebGL2RenderResourceAllocator(gl);
            const descriptor: RenderTextureDescriptor = {
                width: 1920,
                height: 1080,
                format: 'rgba8',
                usage: ['present'],
            };
            const handle = allocator.createTexture(descriptor);
            expect(handle.kind).toBe('default-framebuffer');
        });

        it('cleans up previous texture on present re-create', () => {
            const gl = createGLMock();
            const allocator = createWebGL2RenderResourceAllocator(gl);
            const descriptor: RenderTextureDescriptor = {
                width: 1920,
                height: 1080,
                format: 'rgba8',
                usage: ['present'],
            };
            const prevTex = { kind: 'texture' as const, texture: {} as WebGLTexture, target: 0x0de1 };
            allocator.createTexture(descriptor, prevTex);
            expect(gl._deletedTextures).toHaveLength(1);
        });
    });

    describe('createTexture — MSAA', () => {
        it('creates multisampled texture via texStorage2DMultisample', () => {
            const gl = createGLMock() as unknown as WebGL2RenderingContext & {
                texStorage2DMultisample: ReturnType<typeof vi.fn>;
                _createdTextures: WebGLTexture[];
            };
            (gl as unknown as Record<string, unknown>).texStorage2DMultisample = vi.fn();
            const allocator = createWebGL2RenderResourceAllocator(gl);
            const descriptor: RenderTextureDescriptor = {
                width: 256,
                height: 256,
                format: 'rgba8',
                usage: ['color-attachment'],
                samples: 4,
            };
            const handle = allocator.createTexture(descriptor);
            expect(handle.kind).toBe('texture');
            expect(gl._createdTextures).toHaveLength(1);
        });
    });

    describe('createTexture — normal textures', () => {
        it('creates a 2D texture and returns native handle', () => {
            const gl = createGLMock();
            const allocator = createWebGL2RenderResourceAllocator(gl);
            const descriptor: RenderTextureDescriptor = {
                width: 512,
                height: 512,
                format: 'rgba8',
                usage: ['color-attachment', 'sampled'],
            };
            const handle = allocator.createTexture(descriptor);
            expect(handle.kind).toBe('texture');
            if (handle.kind === 'texture') {
                expect(handle.target).toBe(GL_CONSTANTS.TEXTURE_2D);
            }
            expect(gl._createdTextures).toHaveLength(1);
        });

        it('creates a cube texture when cube is true', () => {
            const gl = createGLMock();
            const allocator = createWebGL2RenderResourceAllocator(gl);
            const descriptor: RenderTextureDescriptor = {
                width: 256,
                height: 256,
                format: 'rgba16f',
                usage: ['color-attachment', 'sampled'],
                cube: true,
            };
            const handle = allocator.createTexture(descriptor);
            if (handle.kind === 'texture') {
                expect(handle.target).toBe(GL_CONSTANTS.TEXTURE_CUBE_MAP);
            }
        });

        it('creates a 3D texture when depth > 1', () => {
            const gl = createGLMock();
            const allocator = createWebGL2RenderResourceAllocator(gl);
            const descriptor: RenderTextureDescriptor = {
                width: 64,
                height: 64,
                depth: 32,
                format: 'rgba16f',
                usage: ['storage', 'sampled'],
            };
            const handle = allocator.createTexture(descriptor);
            if (handle.kind === 'texture') {
                expect(handle.target).toBe(GL_CONSTANTS.TEXTURE_3D);
            }
        });

        it('creates a 2D array texture when arrayLayers > 1', () => {
            const gl = createGLMock();
            const allocator = createWebGL2RenderResourceAllocator(gl);
            const descriptor: RenderTextureDescriptor = {
                width: 256,
                height: 256,
                format: 'rgba8',
                usage: ['color-attachment', 'sampled'],
                arrayLayers: 4,
            };
            const handle = allocator.createTexture(descriptor);
            if (handle.kind === 'texture') {
                expect(handle.target).toBe(GL_CONSTANTS.TEXTURE_2D_ARRAY);
            }
        });

        it('cleans up previous texture on re-create', () => {
            const gl = createGLMock();
            const allocator = createWebGL2RenderResourceAllocator(gl);
            const descriptor: RenderTextureDescriptor = {
                width: 256,
                height: 256,
                format: 'rgba8',
                usage: ['color-attachment'],
            };
            const prevTex = { kind: 'texture' as const, texture: {} as WebGLTexture, target: 0x0de1 };
            allocator.createTexture(descriptor, prevTex);
            expect(gl._deletedTextures).toHaveLength(1);
        });
    });

    describe('destroyTexture', () => {
        it('deletes native texture', () => {
            const gl = createGLMock();
            const allocator = createWebGL2RenderResourceAllocator(gl);
            const tex = {} as WebGLTexture;
            allocator.destroyTexture!({ kind: 'texture', texture: tex, target: 0x0de1 } as any);
            expect(gl._deletedTextures).toContain(tex);
        });

        it('ignores default-framebuffer', () => {
            const gl = createGLMock();
            const allocator = createWebGL2RenderResourceAllocator(gl);
            allocator.destroyTexture!({ kind: 'default-framebuffer' } as any);
            expect(gl._deletedTextures).toHaveLength(0);
        });
    });

    describe('format resolution', () => {
        const formats: Array<RenderTextureDescriptor['format']> = [
            'r11g11b10f', 'rgba8', 'rgba16f', 'rgba32f',
            'rg16f', 'rg32f', 'r16f', 'r32f',
            'depth24', 'depth32f', 'depth24-stencil8',
        ];

        for (const format of formats) {
            it(`creates texture with format ${format}`, () => {
                const gl = createGLMock();
                const allocator = createWebGL2RenderResourceAllocator(gl);
                const descriptor: RenderTextureDescriptor = {
                    width: 64,
                    height: 64,
                    format,
                    usage: format.startsWith('depth') ? ['depth-attachment', 'sampled'] : ['color-attachment', 'sampled'],
                };
                const handle = allocator.createTexture(descriptor);
                expect(handle.kind).toBe('texture');
            });
        }
    });
});
