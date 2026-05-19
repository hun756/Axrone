import { Mat4 } from '@axrone/numeric';
import {
    createRenderPassName,
    createRenderResourceName,
    type ReadonlyRenderList,
    type ReadonlyRenderResourceRegistry,
    type RenderExecutionContext,
    type RenderFrameResult,
    type RenderTextureDescriptor,
    type ResolvedRenderPass,
} from '@axrone/render-core/types';
import { describe, expect, it, vi } from 'vitest';
import {
    createManagedWebGL2RenderPipelineBackend,
    createWebGL2RenderResourceAllocator,
    type WebGL2RenderResourceHandle,
} from '../pipeline';

const createMockGL = (): WebGL2RenderingContext => {
    let nextTextureId = 1;
    let nextFramebufferId = 1;

    return {
        FRAMEBUFFER: 0x8d40,
        READ_FRAMEBUFFER: 0x8ca8,
        DRAW_FRAMEBUFFER: 0x8ca9,
        TEXTURE_2D: 0x0de1,
        TEXTURE_2D_ARRAY: 0x8c1a,
        TEXTURE_3D: 0x806f,
        TEXTURE_CUBE_MAP: 0x8513,
        TEXTURE_MIN_FILTER: 0x2801,
        TEXTURE_MAG_FILTER: 0x2800,
        TEXTURE_WRAP_S: 0x2802,
        TEXTURE_WRAP_T: 0x2803,
        TEXTURE_WRAP_R: 0x8072,
        CLAMP_TO_EDGE: 0x812f,
        NEAREST: 0x2600,
        COLOR_ATTACHMENT0: 0x8ce0,
        DEPTH_ATTACHMENT: 0x8d00,
        DEPTH_STENCIL_ATTACHMENT: 0x821a,
        COLOR_BUFFER_BIT: 0x4000,
        DEPTH_BUFFER_BIT: 0x0100,
        STENCIL_BUFFER_BIT: 0x0400,
        RGBA8: 0x8058,
        RGBA: 0x1908,
        RGB: 0x1907,
        RG: 0x8227,
        RED: 0x1903,
        R11F_G11F_B10F: 0x8c3a,
        UNSIGNED_INT_10F_11F_11F_REV: 0x8c3b,
        UNSIGNED_BYTE: 0x1401,
        RGBA16F: 0x881a,
        RGBA32F: 0x8814,
        RG16F: 0x822f,
        RG32F: 0x8230,
        R16F: 0x822d,
        R32F: 0x822e,
        HALF_FLOAT: 0x140b,
        FLOAT: 0x1406,
        DEPTH_COMPONENT24: 0x81a6,
        DEPTH_COMPONENT32F: 0x8cac,
        DEPTH_COMPONENT: 0x1902,
        UNSIGNED_INT: 0x1405,
        DEPTH24_STENCIL8: 0x88f0,
        DEPTH_STENCIL: 0x84f9,
        UNSIGNED_INT_24_8: 0x84fa,
        createTexture: vi.fn(() => ({ id: nextTextureId++ }) as unknown as WebGLTexture),
        deleteTexture: vi.fn(),
        bindTexture: vi.fn(),
        texParameteri: vi.fn(),
        texStorage2D: vi.fn(),
        texStorage3D: vi.fn(),
        createFramebuffer: vi.fn(() => ({ id: nextFramebufferId++ }) as unknown as WebGLFramebuffer),
        deleteFramebuffer: vi.fn(),
        bindFramebuffer: vi.fn(),
        framebufferTexture2D: vi.fn(),
        drawBuffers: vi.fn(),
        viewport: vi.fn(),
        clearColor: vi.fn(),
        clearDepth: vi.fn(),
        clearStencil: vi.fn(),
        clear: vi.fn(),
        blitFramebuffer: vi.fn(),
    } as unknown as WebGL2RenderingContext;
};

const createRenderList = <T>(items: readonly T[]): ReadonlyRenderList<T> => ({
    length: items.length,
    at(index) {
        return items[index]!;
    },
    toArray() {
        return items;
    },
    [Symbol.iterator]() {
        return items[Symbol.iterator]();
    },
});

const createSnapshot = (
    namespace: string,
    name: string,
    descriptor: Readonly<RenderTextureDescriptor>,
    native: WebGL2RenderResourceHandle
) => ({
    id: createRenderResourceName(namespace, name),
    descriptor,
    lifetime: 'transient' as const,
    native,
    version: 1,
    reused: false,
    lastFrameUsed: 7,
});

const createGraph = (
    snapshots: readonly ReturnType<typeof createSnapshot>[]
): ReadonlyRenderResourceRegistry<WebGL2RenderResourceHandle> => {
    const snapshotMap = new Map(snapshots.map((snapshot) => [snapshot.id, snapshot] as const));

    return {
        hasTexture(id) {
            return snapshotMap.has(id);
        },
        getTexture(id) {
            return snapshotMap.get(id) ?? null;
        },
        listTextures() {
            return snapshots;
        },
    };
};

const createContext = (
    graph: ReadonlyRenderResourceRegistry<WebGL2RenderResourceHandle>
): RenderExecutionContext<WebGL2RenderResourceHandle> => ({
    frame: 7,
    viewport: {
        width: 320,
        height: 180,
    },
    camera: {
        id: 'camera:test',
        viewMatrix: new Mat4(),
        projectionMatrix: new Mat4(),
        viewProjectionMatrix: new Mat4(),
        position: [0, 0, 0],
        near: 0.1,
        far: 100,
    },
    graph,
    statistics: {
        frame: 7,
        deltaTime: 1 / 60,
        passCount: 2,
        postProcessPassCount: 0,
        opaqueCount: 1,
        transparentCount: 0,
        shadowCasterCount: 0,
        lightCount: 0,
        activeLocalLightCount: 0,
        activeReflectionProbeCount: 0,
        reflectionProbeUpdateCount: 0,
        bakeTaskCount: 0,
        transientResourceCount: 2,
        persistentResourceCount: 0,
        resourceReuseCount: 0,
        estimatedCost: 1,
    },
});

describe('render-webgl2 pipeline backend', () => {
    it('allocates render textures and executes an opaque pass with managed framebuffer binding', async () => {
        const gl = createMockGL();
        const allocator = createWebGL2RenderResourceAllocator(gl);
        const colorDescriptor: RenderTextureDescriptor = {
            width: 128,
            height: 64,
            format: 'rgba8',
            usage: ['color-attachment', 'sampled'],
        };
        const depthDescriptor: RenderTextureDescriptor = {
            width: 128,
            height: 64,
            format: 'depth24',
            usage: ['depth-attachment', 'sampled'],
        };

        const graph = createGraph([
            createSnapshot('frame', 'scene-color', colorDescriptor, allocator.createTexture(colorDescriptor)),
            createSnapshot('frame', 'scene-depth', depthDescriptor, allocator.createTexture(depthDescriptor)),
        ]);
        const context = createContext(graph);
        const opaqueHandler = vi.fn(async () => ({
            drawCalls: 3,
            notes: ['opaque-draw'],
        }));
        const backend = createManagedWebGL2RenderPipelineBackend({
            gl,
            handlers: {
                opaque: opaqueHandler,
            },
        });

        const pass: ResolvedRenderPass = {
            kind: 'opaque',
            name: createRenderPassName('opaque'),
            order: 0,
            queue: 'geometry',
            enabled: true,
            estimatedCost: 1,
            target: createRenderResourceName('frame', 'scene-color'),
            inputs: [
                createRenderResourceName('frame', 'scene-color'),
                createRenderResourceName('frame', 'scene-depth'),
            ],
            clearState: {
                color: [0, 0, 0, 1],
                depth: 1,
            },
            metadata: {
                color: createRenderResourceName('frame', 'scene-color'),
                depth: createRenderResourceName('frame', 'scene-depth'),
                hdr: false,
                giMode: 'disabled',
                ibl: false,
            },
            items: createRenderList([
                {
                    id: 'primitive-1',
                    meshId: 'mesh-1',
                    material: {
                        id: 'mat-1',
                        model: 'unlit',
                    },
                    worldMatrix: new Mat4(),
                },
            ]),
        };

        await backend.beginFrame(context);
        await backend.executePass(pass, context);
        await backend.endFrame(
            {
                frame: 7,
                viewport: context.viewport,
                passes: [],
                resources: graph.listTextures(),
                statistics: context.statistics,
                degraded: false,
                warnings: [],
            } as RenderFrameResult<WebGL2RenderResourceHandle>,
            context
        );

        expect(opaqueHandler).toHaveBeenCalledTimes(1);
        expect(gl.createFramebuffer).toHaveBeenCalledTimes(1);
        expect(gl.framebufferTexture2D).toHaveBeenCalledTimes(2);
        expect(gl.viewport).toHaveBeenCalledWith(0, 0, 128, 64);
        expect(gl.clear).toHaveBeenCalledWith(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
        expect(backend.getProfilerSnapshot().drawCalls).toBe(3);
        expect(backend.getLastFrameCapture()?.passes[0]?.notes).toEqual(['opaque-draw']);
    });

    it('blits present passes to the default framebuffer without requiring a custom handler', async () => {
        const gl = createMockGL();
        const allocator = createWebGL2RenderResourceAllocator(gl);
        const colorDescriptor: RenderTextureDescriptor = {
            width: 256,
            height: 128,
            format: 'rgba8',
            usage: ['color-attachment', 'sampled'],
        };
        const swapDescriptor: RenderTextureDescriptor = {
            width: 256,
            height: 128,
            format: 'rgba8',
            usage: ['present'],
        };

        const graph = createGraph([
            createSnapshot('frame', 'scene-color', colorDescriptor, allocator.createTexture(colorDescriptor)),
            createSnapshot('swap', 'back-buffer', swapDescriptor, allocator.createTexture(swapDescriptor)),
        ]);
        const context = createContext(graph);
        const backend = createManagedWebGL2RenderPipelineBackend({ gl });
        const pass: ResolvedRenderPass = {
            kind: 'present',
            name: createRenderPassName('present'),
            order: 1,
            queue: 'present',
            enabled: true,
            estimatedCost: 0.1,
            target: createRenderResourceName('swap', 'back-buffer'),
            inputs: [createRenderResourceName('frame', 'scene-color')],
            metadata: {
                source: createRenderResourceName('frame', 'scene-color'),
                destination: createRenderResourceName('swap', 'back-buffer'),
                colorSpace: 'srgb',
            },
        };

        await backend.beginFrame(context);
        await backend.executePass(pass, context);
        await backend.endFrame(
            {
                frame: 7,
                viewport: context.viewport,
                passes: [],
                resources: graph.listTextures(),
                statistics: context.statistics,
                degraded: false,
                warnings: [],
            } as RenderFrameResult<WebGL2RenderResourceHandle>,
            context
        );

        expect(gl.blitFramebuffer).toHaveBeenCalledTimes(1);
        expect(backend.getProfilerSnapshot().presents).toBe(1);
        expect(backend.getProfilerSnapshot().drawCalls).toBe(1);
    });

    it('renders frame outputs directly to the default framebuffer when configured', async () => {
        const gl = createMockGL();
        const allocator = createWebGL2RenderResourceAllocator(gl);
        const colorDescriptor: RenderTextureDescriptor = {
            width: 256,
            height: 128,
            format: 'rgba8',
            usage: ['color-attachment', 'sampled'],
        };
        const depthDescriptor: RenderTextureDescriptor = {
            width: 256,
            height: 128,
            format: 'depth24',
            usage: ['depth-attachment', 'sampled'],
        };
        const graph = createGraph([
            createSnapshot('frame', 'scene-color', colorDescriptor, allocator.createTexture(colorDescriptor)),
            createSnapshot('frame', 'scene-depth', depthDescriptor, allocator.createTexture(depthDescriptor)),
        ]);
        const context = createContext(graph);
        const opaqueHandler = vi.fn(() => ({
            drawCalls: 1,
        }));
        const backend = createManagedWebGL2RenderPipelineBackend({
            gl,
            directFrameOutput: true,
            handlers: {
                opaque: opaqueHandler,
            },
        });

        await backend.beginFrame(context);
        await backend.executePass(
            {
                kind: 'opaque',
                name: createRenderPassName('opaque'),
                order: 0,
                queue: 'geometry',
                enabled: true,
                estimatedCost: 1,
                target: createRenderResourceName('frame', 'scene-color'),
                inputs: [
                    createRenderResourceName('frame', 'scene-color'),
                    createRenderResourceName('frame', 'scene-depth'),
                ],
                metadata: {
                    color: createRenderResourceName('frame', 'scene-color'),
                    depth: createRenderResourceName('frame', 'scene-depth'),
                    hdr: false,
                    giMode: 'disabled',
                    ibl: false,
                },
                items: createRenderList([]),
            },
            context
        );
        await backend.executePass(
            {
                kind: 'present',
                name: createRenderPassName('present'),
                order: 1,
                queue: 'present',
                enabled: true,
                estimatedCost: 0.1,
                target: createRenderResourceName('swap', 'back-buffer'),
                inputs: [createRenderResourceName('frame', 'scene-color')],
                metadata: {
                    source: createRenderResourceName('frame', 'scene-color'),
                    destination: createRenderResourceName('swap', 'back-buffer'),
                    colorSpace: 'srgb',
                },
            },
            context
        );

        expect(opaqueHandler).toHaveBeenCalledTimes(1);
        expect(gl.createFramebuffer).not.toHaveBeenCalled();
        expect(gl.blitFramebuffer).not.toHaveBeenCalled();
        expect(backend.getProfilerSnapshot().presents).toBe(1);
    });

    it('fails loudly for unsupported passes when no handler is registered', async () => {
        const gl = createMockGL();
        const backend = createManagedWebGL2RenderPipelineBackend({ gl });
        const context = createContext(createGraph([]));

        await backend.beginFrame(context);

        expect(() =>
            backend.executePass(
                {
                    kind: 'post-process',
                    name: createRenderPassName('post-process'),
                    order: 1,
                    queue: 'post-process',
                    enabled: true,
                    estimatedCost: 0.2,
                    target: createRenderResourceName('post', 'fx'),
                    inputs: [createRenderResourceName('frame', 'scene-color')],
                    metadata: {
                        source: createRenderResourceName('frame', 'scene-color'),
                        target: createRenderResourceName('post', 'fx'),
                        phase: 'before-tonemap',
                        effect: {
                            category: 'builtin',
                            name: 'bloom',
                            phase: 'before-tonemap',
                            quality: 'medium',
                            order: 0,
                            settings: {
                                threshold: 1,
                                knee: 0.5,
                                intensity: 1,
                                radius: 0.5,
                            },
                        },
                    },
                },
                context
            )
        ).toThrow(/No WebGL2 render pass handler is registered/);
    });
});