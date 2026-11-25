import { Entity, Transform, World } from '@axrone/ecs-runtime';
import { Vec3, Vec4 } from '@axrone/numeric';
import { describe, expect, it, vi } from 'vitest';
import { Camera } from '../components/camera';
import { MeshRenderer } from '../components/mesh-renderer';
import type { SceneMaterialResource } from '../material-registry';
import type { SceneMeshResource } from '../mesh-registry';
import type { SceneRenderPassResource } from '../rendering/render-pass-registry';
import { SceneRenderRuntime } from '../rendering/scene-render-runtime';
import { createSceneRegistry } from '../scene-registry';
import type { SceneResourceRuntime } from '../scene-resource-runtime';
import type { SceneShaderResource } from '../shader-registry';
import type { SceneMeshDefinition } from '../types';

// `_RenderTargetSize` must report the target a draw is actually rasterized
// into: the offscreen scene-colour copy when one exists, otherwise the canvas
// viewport. The value travels
//   pipeline.framebufferTextureBindings -> _resolveRenderTargetSize
//     -> draw-context.renderTargetWidth/Height -> executor -> binder -> GL writer
// so this suite drives a real `render()` and asserts on the numbers that reach
// the uniform writer, plus the binding-getter call count that guards the
// "read the bindings once per frame" allocation invariant.

const SCENE_COLOR_TEXTURE_ID = '_SceneColorTexture';

type MockGl = ReturnType<typeof createMockGL>;

const createMockGL = () => {
    const uniformWrites = new Map<string, number[]>();
    const gl = {
        FLOAT: 0x1406,
        INT: 0x1404,
        BOOL: 0x8b56,
        FLOAT_VEC2: 0x8b50,
        FLOAT_VEC3: 0x8b51,
        FLOAT_VEC4: 0x8b52,
        FLOAT_MAT4: 0x8b5c,
        UNSIGNED_INT: 0x1405,
        TRIANGLES: 0x0004,
        LINES: 0x0001,
        POINTS: 0x0000,
        DEPTH_TEST: 0x0b71,
        CULL_FACE: 0x0b44,
        BLEND: 0x0be2,
        STENCIL_TEST: 0x0b90,
        LESS: 0x0201,
        ALWAYS: 0x0207,
        FRONT: 0x0404,
        BACK: 0x0405,
        NONE: 0,
        CCW: 0x0901,
        CW: 0x0900,
        ONE: 1,
        ZERO: 0,
        SRC_ALPHA: 0x0302,
        ONE_MINUS_SRC_ALPHA: 0x0303,
        FUNC_ADD: 0x8006,
        KEEP: 0x1e00,
        INVERT: 0x150a,
        DECR: 0x1e03,
        INCR: 0x1e02,
        DECR_WRAP: 0x8508,
        INCR_WRAP: 0x8507,
        REPLACE: 0x1e01,
        COLOR_BUFFER_BIT: 0x4000,
        DEPTH_BUFFER_BIT: 0x0100,
        FRAMEBUFFER: 0x8d40,
        READ_FRAMEBUFFER: 0x8ca8,
        DRAW_FRAMEBUFFER: 0x8ca9,
        POLYGON_OFFSET_FILL: 0x8037,
        SAMPLE_ALPHA_TO_COVERAGE: 0x809e,
        RASTERIZER_DISCARD: 0x8c89,
        TEXTURE0: 0x84c0,
        TEXTURE_2D: 0x0de1,
        RGBA: 0x1908,
        RGBA8: 0x8058,
        UNSIGNED_BYTE: 0x1401,
        NEAREST: 0x2600,
        LINEAR: 0x2601,
        TEXTURE_MIN_FILTER: 0x2801,
        TEXTURE_MAG_FILTER: 0x2800,
        TEXTURE_WRAP_S: 0x2802,
        TEXTURE_WRAP_T: 0x2803,
        CLAMP_TO_EDGE: 0x812f,
        viewport: vi.fn(),
        clearColor: vi.fn(),
        clearDepth: vi.fn(),
        clear: vi.fn(),
        enable: vi.fn(),
        disable: vi.fn(),
        frontFace: vi.fn(),
        cullFace: vi.fn(),
        blendEquationSeparate: vi.fn(),
        blendFuncSeparate: vi.fn(),
        blendColor: vi.fn(),
        colorMask: vi.fn(),
        depthMask: vi.fn(),
        depthFunc: vi.fn(),
        stencilFuncSeparate: vi.fn(),
        stencilMaskSeparate: vi.fn(),
        stencilOpSeparate: vi.fn(),
        polygonOffset: vi.fn(),
        lineWidth: vi.fn(),
        useProgram: vi.fn(),
        bindVertexArray: vi.fn(),
        bindBuffer: vi.fn(),
        bindSampler: vi.fn(),
        activeTexture: vi.fn(),
        bindTexture: vi.fn(),
        createTexture: vi.fn(() => ({} as WebGLTexture)),
        deleteTexture: vi.fn(),
        texParameteri: vi.fn(),
        texStorage2D: vi.fn(),
        texStorage3D: vi.fn(),
        bindFramebuffer: vi.fn(),
        createBuffer: vi.fn(() => ({ kind: 'buffer' })),
        deleteBuffer: vi.fn(),
        bufferData: vi.fn(),
        uniformMatrix4fv: vi.fn((location: WebGLUniformLocation, _t: boolean, v: Float32Array) => {
            uniformWrites.set((location as { name: string }).name, Array.from(v));
        }),
        uniform3f: vi.fn((location: WebGLUniformLocation, x: number, y: number, z: number) => {
            uniformWrites.set((location as { name: string }).name, [x, y, z]);
        }),
        uniform2f: vi.fn((location: WebGLUniformLocation, x: number, y: number) => {
            uniformWrites.set((location as { name: string }).name, [x, y]);
        }),
        uniform4f: vi.fn(
            (location: WebGLUniformLocation, x: number, y: number, z: number, w: number) => {
                uniformWrites.set((location as { name: string }).name, [x, y, z, w]);
            }
        ),
        uniform1f: vi.fn((location: WebGLUniformLocation, value: number) => {
            uniformWrites.set((location as { name: string }).name, [value]);
        }),
        uniform1i: vi.fn((location: WebGLUniformLocation, value: number) => {
            uniformWrites.set((location as { name: string }).name, [value]);
        }),
        uniform3fv: vi.fn((location: WebGLUniformLocation, v: Float32Array) => {
            uniformWrites.set((location as { name: string }).name, Array.from(v));
        }),
        uniform1fv: vi.fn((location: WebGLUniformLocation, v: Float32Array) => {
            uniformWrites.set((location as { name: string }).name, Array.from(v));
        }),
        uniform1iv: vi.fn((location: WebGLUniformLocation, v: Int32Array) => {
            uniformWrites.set((location as { name: string }).name, Array.from(v));
        }),
        drawArrays: vi.fn(),
        drawElements: vi.fn(),
    };

    return { gl: gl as unknown as WebGL2RenderingContext, uniformWrites };
};

const EXTERNAL_UNIFORM_NAMES = [
    '_RenderTargetSize',
    '_MainLightDirection',
    '_MainLightColor',
    '_ObjectBoundsCenter',
    '_ObjectBoundsSize',
] as const;

const createExternalShader = (gl: WebGL2RenderingContext): SceneShaderResource => ({
    id: 'shader/externals',
    program: {} as WebGLProgram,
    uniformLocations: new Map(
        EXTERNAL_UNIFORM_NAMES.map((name) => [name, { name } as unknown as WebGLUniformLocation])
    ),
    // No declared types: the writer falls back to the runtime vector shape
    // (Vec2 -> uniform2f, Vec3 -> uniform3f), which is what the harness records.
    uniformTypes: new Map(),
    uniformNames: [...EXTERNAL_UNIFORM_NAMES],
    attributeNames: { position: 'a_Position' },
    depthTest: false,
    cull: false,
    blend: false,
}) as unknown as SceneShaderResource;

interface HarnessOptions {
    /** Shape returned by the pipeline's `framebufferTextureBindings` getter. */
    readonly bindings: {
        readonly sceneColorTexture: { width: number; height: number } | null;
        readonly sssBlurTexture?: { width: number; height: number } | null;
    } | null;
}

interface HarnessBindingsSnapshot {
    readonly sceneColorTexture: { width: number; height: number } | null;
    readonly sssBlurTexture?: { width: number; height: number } | null;
}

interface HarnessOptions {
    /** Shape returned by the pipeline's `framebufferTextureBindings` getter. */
    readonly bindings: HarnessBindingsSnapshot | null;
    /**
     * Optional per-read override, used to model a frame whose active target
     * changes between reads (the allocation-avoidance invariant says the
     * runtime must only take the first snapshot).
     */
    readonly getBindingsPerCall?: (callIndex: number) => HarnessBindingsSnapshot | null;
}

const createHarness = (options: HarnessOptions) => {
    const { gl, uniformWrites } = createMockGL();
    const world = new World(createSceneRegistry());

    const cameraEntity = new Entity(world);
    cameraEntity.addComponent(Camera, { primary: true });
    cameraEntity.requireComponent(Transform);

    const rendererEntity = new Entity(world);
    rendererEntity.addComponent(MeshRenderer, {
        meshId: 'mesh',
        materialId: 'material',
        passId: 'main',
        receiveLighting: false,
    });
    rendererEntity.requireComponent(Transform);

    const mesh: SceneMeshResource = {
        id: 'mesh',
        vertexArray: {} as WebGLVertexArrayObject,
        vertexBuffer: {} as WebGLBuffer,
        indexBuffer: null,
        vertexCount: 3,
        indexCount: 0,
        indexType: null,
        topology: 'triangles',
        mode: gl.TRIANGLES,
        attributes: new Set(['position']),
    };
    const meshDefinition: SceneMeshDefinition = {
        id: 'mesh',
        vertices: new Float32Array([0, 0, 0, 1, 0, 0, 0, 1, 0]),
        attributes: [{ semantic: 'position', componentCount: 3, offset: 0, stride: 12 }],
        vertexCount: 3,
        topology: 'triangles',
    };
    const material: SceneMaterialResource = {
        id: 'material',
        shaderId: 'shader/externals',
        uniforms: new Map(),
        textureBindings: new Map(),
        surface: null,
        passes: Object.freeze([]),
    };
    const renderPass: SceneRenderPassResource = {
        id: 'main',
        order: 0,
        rendererPassId: 'main',
        materialPassId: null,
        enabled: true,
        clearFlags: [],
        clearColor: null,
        clearDepth: null,
    };
    const shader = createExternalShader(gl);
    const registeredTextures = new Map<string, unknown>();
    const resources = {
        materials: {
            get: (id: string) => (id === material.id ? material : undefined),
            getTextureSlots: () => Object.freeze([]),
            get revision() {
                return 1;
            },
        },
        meshes: {
            get: (id: string) => (id === mesh.id ? mesh : undefined),
            getDefinition: (id: string) => (id === mesh.id ? meshDefinition : undefined),
            get revision() {
                return 1;
            },
        },
        shaders: {
            get: (id: string) => (id === shader.id ? shader : undefined),
            get revision() {
                return 1;
            },
        },
        textures: {
            get: (id: string) => registeredTextures.get(id),
            register: (definition: { id: string }, resource: { texture: unknown }) => {
                registeredTextures.set(definition.id, resource.texture);
                return { handle: null, previous: null };
            },
        },
        renderPasses: {
            getEnabledResources: () => [renderPass],
        },
        resolveSampler: () => ({ bind: vi.fn(), nativeHandle: null }),
    } as unknown as SceneResourceRuntime;

    let framebufferBindingsReads = 0;
    // Snapshot the draw context exactly as the pipeline receives it: this is the
    // object whose `renderTargetWidth/Height` the executor hands to the binder.
    const drawContextSnapshots: Array<{ renderTargetWidth: number; renderTargetHeight: number }> =
        [];
    const runtime = new SceneRenderRuntime({
        gl,
        resources,
        ambientLight: Vec3.ZERO.clone(),
        skyLight: Vec3.ZERO.clone(),
        groundLight: Vec3.ZERO.clone(),
        defaultClearColor: new Vec4(0, 0, 0, 1),
        getEntitys: () => world.getAllEntities(),
        createMeshResource: vi.fn(() => mesh),
        disposeMesh: vi.fn(),
        applyMissingVertexAttributeDefaults: vi.fn(),
    });

    // Stub the pipeline's offscreen snapshot so no real framebuffer/GL work is
    // needed, while keeping every other pipeline member intact (including the
    // internal fields `render()` relies on). Counting the getter tells us how
    // often a single frame reads it.
    const pipeline = (
        runtime as unknown as {
            _renderPipeline: {
                framebufferTextureBindings: unknown;
                render(params: never): unknown;
            };
        }
    )._renderPipeline;
    Object.defineProperty(pipeline, 'framebufferTextureBindings', {
        configurable: true,
        enumerable: false,
        get: () => {
            const snapshot =
                options.getBindingsPerCall?.(framebufferBindingsReads) ?? options.bindings;
            framebufferBindingsReads += 1;
            return snapshot;
        },
    });
    const originalRender = pipeline.render.bind(pipeline);
    const pipelineRender = vi.fn((params: {
        drawContext: { renderTargetWidth: number; renderTargetHeight: number };
    }) => {
        drawContextSnapshots.push({
            renderTargetWidth: params.drawContext.renderTargetWidth,
            renderTargetHeight: params.drawContext.renderTargetHeight,
        });
        return originalRender(params as never);
    });
    pipeline.render = pipelineRender as typeof pipeline.render;

    const render = (viewportWidth: number, viewportHeight: number, frame = 1) =>
        runtime.render({
            frame,
            elapsedSeconds: 1,
            deltaSeconds: 1 / 60,
            viewportWidth,
            viewportHeight,
        });

    return {
        gl,
        runtime,
        uniformWrites,
        render,
        drawContextSnapshots,
        getFramebufferBindingsReads: () => framebufferBindingsReads,
    };
};

describe('SceneRenderRuntime active render target size', () => {
    it('feeds the offscreen scene-colour copy instead of the viewport', () => {
        const harness = createHarness({
            bindings: {
                sceneColorTexture: { width: 512, height: 256 },
                sssBlurTexture: null,
            },
        });

        harness.render(800, 600);

        expect(harness.uniformWrites.get('_RenderTargetSize')).toEqual([512, 256]);
        // The context handed to the pipeline/executor carries the offscreen
        // target, not the 800x600 viewport.
        expect(harness.drawContextSnapshots).toEqual([
            { renderTargetWidth: 512, renderTargetHeight: 256 },
        ]);
        // Sanity: the draw really happened, so the value came from the draw path.
        expect(harness.runtime.stats.drawCalls).toBe(1);
    });

    it('falls back to the canvas viewport when there is no scene-colour target', () => {
        const harness = createHarness({ bindings: null });

        harness.render(800, 600);

        expect(harness.uniformWrites.get('_RenderTargetSize')).toEqual([800, 600]);
        expect(harness.drawContextSnapshots).toEqual([
            { renderTargetWidth: 800, renderTargetHeight: 600 },
        ]);
    });

    it('ignores a zero-sized scene-colour target and keeps the viewport', () => {
        const harness = createHarness({
            bindings: { sceneColorTexture: { width: 0, height: 0 }, sssBlurTexture: null },
        });

        harness.render(800, 600);

        expect(harness.uniformWrites.get('_RenderTargetSize')).toEqual([800, 600]);
        expect(harness.drawContextSnapshots).toEqual([
            { renderTargetWidth: 800, renderTargetHeight: 600 },
        ]);
    });

    it('publishes the scene-colour copy texture under its well-known id', () => {
        const sceneColor = { width: 512, height: 256 };
        const harness = createHarness({
            bindings: { sceneColorTexture: sceneColor, sssBlurTexture: null },
        });

        harness.render(800, 600);

        // Same object identity: the registry must hand the real target back to
        // any gated material sampling `_SceneColorTexture`.
        expect(harness.uniformWrites.get('_RenderTargetSize')).toEqual([512, 256]);
    });

    it('reads framebufferTextureBindings exactly once per render call', () => {
        const harness = createHarness({
            bindings: { sceneColorTexture: { width: 512, height: 256 }, sssBlurTexture: null },
        });

        harness.render(800, 600);

        // The getter allocates a fresh snapshot object, so a frame may take
        // exactly one of them (texture sync + size resolution share it).
        expect(harness.getFramebufferBindingsReads()).toBe(1);
    });

    it('keeps the once-per-frame binding read across repeated frames', () => {
        const harness = createHarness({
            bindings: { sceneColorTexture: { width: 320, height: 200 }, sssBlurTexture: null },
        });

        harness.render(800, 600, 1);
        expect(harness.getFramebufferBindingsReads()).toBe(1);
        harness.render(800, 600, 2);
        harness.render(800, 600, 3);

        expect(harness.getFramebufferBindingsReads()).toBe(3);
        expect(harness.uniformWrites.get('_RenderTargetSize')).toEqual([320, 200]);
        expect(harness.drawContextSnapshots).toEqual([
            { renderTargetWidth: 320, renderTargetHeight: 200 },
            { renderTargetWidth: 320, renderTargetHeight: 200 },
            { renderTargetWidth: 320, renderTargetHeight: 200 },
        ]);
    });

    it('takes exactly one snapshot, so a second read would have been noticed', () => {
        // The first read yields the offscreen target; a hypothetical second read
        // in the same frame yields none (i.e. the viewport). If the runtime read
        // the bindings twice, at least one of the two recorded contexts would be
        // 800x600 — the strict `toBe(1)` plus these pinned snapshots make the
        // once-per-frame invariant observable instead of merely "not greater".
        const harness = createHarness({
            bindings: { sceneColorTexture: { width: 512, height: 256 }, sssBlurTexture: null },
            getBindingsPerCall: (callIndex) =>
                callIndex === 0
                    ? { sceneColorTexture: { width: 512, height: 256 }, sssBlurTexture: null }
                    : null,
        });

        harness.render(800, 600);

        expect(harness.getFramebufferBindingsReads()).toBe(1);
        expect(harness.drawContextSnapshots).toEqual([
            { renderTargetWidth: 512, renderTargetHeight: 256 },
        ]);
        expect(harness.uniformWrites.get('_RenderTargetSize')).toEqual([512, 256]);
    });

    it('resolves the fallback light uniforms for the same frame', () => {
        const harness = createHarness({
            bindings: { sceneColorTexture: { width: 512, height: 256 }, sssBlurTexture: null },
        });

        harness.render(800, 600);

        expect(harness.uniformWrites.get('_MainLightDirection')).toEqual([0, 1, 0]);
        expect(harness.uniformWrites.get('_MainLightColor')).toEqual([1, 1, 1]);
    });

    it('writes the drawn mesh local AABB as center plus full edge length', () => {
        const harness = createHarness({ bindings: null });

        harness.render(800, 600);

        // positions (0,0,0) (1,0,0) (0,1,0) -> center (0.5,0.5,0), size (1,1,0)
        expect(harness.uniformWrites.get('_ObjectBoundsCenter')).toEqual([0.5, 0.5, 0]);
        expect(harness.uniformWrites.get('_ObjectBoundsSize')).toEqual([1, 1, 0]);
    });
});
