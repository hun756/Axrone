import { RenderPipeline } from '@axrone/render-core';
import type {
    RenderCameraState,
    RenderClearState,
    RenderLight,
    RenderMaterialSnapshot,
    RenderPrimitiveInstance,
    ResolvedRenderPass,
} from '@axrone/render-core/types';
import {
    createManagedWebGL2RenderPipelineBackend,
    createWebGL2RenderResourceAllocator,
    type ManagedWebGL2RenderPipelineBackend,
    type WebGL2RenderResourceHandle,
} from '@axrone/render-webgl2/pipeline';
import type { BoundingSphere } from '@axrone/geometry';
import type { SceneCameraFrameState } from './camera-frame-state';
import type { MeshRenderer } from './components/mesh-renderer';
import type { SceneDrawExecutor, SceneDrawExecutorContext } from './draw-executor';
import type { SceneLightingState } from './lighting-collector';
import type { SceneRenderFrameState } from './render-frame-state';
import type { SceneRenderItem } from './render-item-collector';
import type { SceneRenderPassResource } from './render-pass-registry';
import type {
    SceneMaterialAlphaMode,
    SceneRenderPlanningOptions,
    SceneRenderPlanningStats,
} from './types';

export interface SceneRenderPipelineParams {
    readonly frame: number;
    readonly deltaSeconds: number;
    readonly viewportWidth: number;
    readonly viewportHeight: number;
    readonly cameraFrame: SceneCameraFrameState;
    readonly lighting: SceneLightingState;
    readonly renderPass: SceneRenderPassResource;
    readonly drawContext: SceneDrawExecutorContext;
    readonly frameState: SceneRenderFrameState;
    readonly renderItems: readonly SceneRenderItem[];
    readonly resolveBounds: (renderer: MeshRenderer) => Readonly<BoundingSphere> | null | undefined;
    readonly resolveMaterial: (renderer: MeshRenderer) => {
        readonly materialId: string | null;
        readonly shadingModel: 'unlit' | 'pbr' | 'custom';
        readonly alphaMode: SceneMaterialAlphaMode;
        readonly transparent: boolean;
    };
}

const DEFAULT_SCENE_RENDER_PLANNING_STATS: SceneRenderPlanningStats = Object.freeze({
    passCount: 0,
    opaqueCount: 0,
    transparentCount: 0,
    meshTransparentCount: 0,
    spriteTransparentCount: 0,
    spriteBatchCount: 0,
    skippedSpriteCount: 0,
    warnings: Object.freeze([]) as readonly string[],
});

const clampCosine = (value: number): number => Math.min(1, Math.max(-1, value));

const toVector3Tuple = (
    value:
        | { readonly x: number; readonly y: number; readonly z: number }
        | readonly [number, number, number]
): readonly [number, number, number] =>
    'x' in value ? [value.x, value.y, value.z] : [value[0], value[1], value[2]];

const toVector4Tuple = (
    value:
        | { readonly x: number; readonly y: number; readonly z: number; readonly w: number }
        | readonly [number, number, number, number]
): readonly [number, number, number, number] =>
    'x' in value
        ? [value.x, value.y, value.z, value.w]
        : [value[0], value[1], value[2], value[3]];

const createDirectionalLightId = (index: number): string => `scene-light:directional:${index}`;
const createPointLightId = (index: number): string => `scene-light:point:${index}`;
const createSpotLightId = (index: number): string => `scene-light:spot:${index}`;

const toRenderBounds = (
    bounds: Readonly<BoundingSphere> | null | undefined
): RenderPrimitiveInstance['bounds'] | undefined => {
    if (!bounds) {
        return undefined;
    }

    return {
        center: toVector3Tuple(bounds.center),
        extents: [bounds.radius, bounds.radius, bounds.radius],
    };
};

const resolveRenderClearState = (
    cameraFrame: SceneCameraFrameState,
    renderPass: SceneRenderPassResource
): RenderClearState => {
    const clearFlags = cameraFrame.camera.clearFlags
        ? renderPass.clearFlags.filter((flag) => cameraFrame.camera.clearFlags.includes(flag))
        : renderPass.clearFlags;

    return {
        ...(clearFlags.includes('color')
            ? {
                  color: toVector4Tuple(
                      renderPass.clearColor ?? cameraFrame.camera.clearColor
                  ),
              }
            : {}),
        ...(clearFlags.includes('depth')
            ? {
                  depth: renderPass.clearDepth ?? cameraFrame.camera.clearDepth,
              }
            : {}),
    };
};

const toRenderCameraState = (
    cameraFrame: SceneCameraFrameState,
    clearState: RenderClearState
): RenderCameraState => ({
    id: `scene-camera:${cameraFrame.camera.id}`,
    viewMatrix: cameraFrame.viewMatrix,
    projectionMatrix: cameraFrame.projectionMatrix,
    viewProjectionMatrix: cameraFrame.viewProjectionMatrix,
    camera3D: cameraFrame.camera3D,
    frustum: cameraFrame.camera3D.frustum,
    position: toVector3Tuple(cameraFrame.position),
    near: cameraFrame.camera.near,
    far: cameraFrame.camera.far,
    clearState,
});

const toRenderLights = (lighting: SceneLightingState): readonly RenderLight[] => {
    const lights: RenderLight[] = [];

    for (let index = 0; index < lighting.stats.selectedDirectionalCount; index += 1) {
        const offset = index * 3;
        lights.push({
            type: 'directional',
            id: createDirectionalLightId(index),
            direction: [
                lighting.directionalDirections[offset] ?? 0,
                lighting.directionalDirections[offset + 1] ?? -1,
                lighting.directionalDirections[offset + 2] ?? 0,
            ],
            color: [
                lighting.directionalColors[offset] ?? 1,
                lighting.directionalColors[offset + 1] ?? 1,
                lighting.directionalColors[offset + 2] ?? 1,
            ],
            intensity: lighting.directionalIntensities[index] ?? 1,
        });
    }

    for (let index = 0; index < lighting.stats.selectedPointCount; index += 1) {
        const offset = index * 3;
        lights.push({
            type: 'point',
            id: createPointLightId(index),
            position: [
                lighting.pointPositions[offset] ?? 0,
                lighting.pointPositions[offset + 1] ?? 0,
                lighting.pointPositions[offset + 2] ?? 0,
            ],
            color: [
                lighting.pointColors[offset] ?? 1,
                lighting.pointColors[offset + 1] ?? 1,
                lighting.pointColors[offset + 2] ?? 1,
            ],
            intensity: lighting.pointIntensities[index] ?? 1,
            range: lighting.pointRanges[index] ?? 1,
        });
    }

    for (let index = 0; index < lighting.stats.selectedSpotCount; index += 1) {
        const offset = index * 3;
        lights.push({
            type: 'spot',
            id: createSpotLightId(index),
            position: [
                lighting.spotPositions[offset] ?? 0,
                lighting.spotPositions[offset + 1] ?? 0,
                lighting.spotPositions[offset + 2] ?? 0,
            ],
            direction: [
                lighting.spotDirections[offset] ?? 0,
                lighting.spotDirections[offset + 1] ?? -1,
                lighting.spotDirections[offset + 2] ?? 0,
            ],
            color: [
                lighting.spotColors[offset] ?? 1,
                lighting.spotColors[offset + 1] ?? 1,
                lighting.spotColors[offset + 2] ?? 1,
            ],
            intensity: lighting.spotIntensities[index] ?? 1,
            range: lighting.spotRanges[index] ?? 1,
            innerConeRadians: Math.acos(
                clampCosine(lighting.spotInnerConeCosines[index] ?? 0)
            ),
            outerConeRadians: Math.acos(
                clampCosine(lighting.spotOuterConeCosines[index] ?? 0)
            ),
        });
    }

    return lights;
};

const createMaterialSnapshot = (
    item: SceneRenderItem,
    resolvedMaterial: SceneRenderPipelineParams['resolveMaterial'] extends (
        renderer: MeshRenderer
    ) => infer TResult
        ? TResult
        : never
): RenderMaterialSnapshot => {
    const materialId = resolvedMaterial.materialId ?? `scene-material:${item.renderer.id}`;
    const baseQueue =
        resolvedMaterial.alphaMode === 'blend'
            ? 3000
            : resolvedMaterial.alphaMode === 'mask'
              ? 2450
              : 2000;

    return {
        id: materialId,
        model: resolvedMaterial.shadingModel,
        renderQueue: baseQueue + item.renderer.renderOrder,
        transparent: resolvedMaterial.transparent,
        alphaClipped: resolvedMaterial.alphaMode === 'mask',
        castsShadows: resolvedMaterial.transparent === false,
    };
};

interface SceneRenderPipelineOptions {
    readonly gl: WebGL2RenderingContext;
    readonly drawExecutor: Pick<SceneDrawExecutor, 'execute'>;
    readonly planning?: SceneRenderPlanningOptions;
}

interface ActiveExecutionState {
    readonly drawContext: SceneDrawExecutorContext;
    readonly frameState: SceneRenderFrameState;
}

export class SceneRenderPipeline {
    private readonly _primitiveLookup = new Map<string, SceneRenderItem>();
    private readonly _options: SceneRenderPipelineOptions;
    private _activeExecution: ActiveExecutionState | null = null;
    private _backend: ManagedWebGL2RenderPipelineBackend;
    private _pipeline: RenderPipeline<WebGL2RenderResourceHandle>;

    constructor(options: SceneRenderPipelineOptions) {
        this._options = options;
        this._backend = this._createBackend();
        this._pipeline = this._createPipeline();
    }

    private _createBackend(): ManagedWebGL2RenderPipelineBackend {
        return createManagedWebGL2RenderPipelineBackend({
            gl: this._options.gl,
            directFrameOutput: true,
            handlers: {
                opaque: (pass) => this._executeDrawPass(pass),
                transparent: (pass) => this._executeDrawPass(pass),
            },
        });
    }

    private _createPipeline(): RenderPipeline<WebGL2RenderResourceHandle> {
        return new RenderPipeline<WebGL2RenderResourceHandle>({
            name: 'SceneRenderPipeline',
            hdr: false,
            tonemapping: {
                mode: 'none',
            },
            shadows: false,
            gi: {
                mode: 'disabled',
            },
            volumetrics: {
                enabled: false,
            },
            lightBaking: false,
            postProcess: Object.freeze([]),
            enableDepthPrepass: false,
            maxActiveReflectionProbes: 0,
            maxActiveLocalLights: 4,
            maxTransparentPrimitives: this._options.planning?.maxTransparentPrimitives,
            backend: this._backend,
            resourceAllocator: createWebGL2RenderResourceAllocator(this._options.gl),
        });
    }

    private _executeDrawPass(pass: ResolvedRenderPass): { readonly drawCalls: number } {
        if (!this._activeExecution) {
            return { drawCalls: 0 };
        }

        const drawCallsBefore = this._activeExecution.frameState.drawCalls;
        for (const primitive of pass.items?.toArray() ?? []) {
            const item = this._primitiveLookup.get(primitive.id);
            if (item) {
                this._options.drawExecutor.execute(
                    item,
                    this._activeExecution.drawContext,
                    this._activeExecution.frameState
                );
            }
        }

        return {
            drawCalls: this._activeExecution.frameState.drawCalls - drawCallsBefore,
        };
    }

    render(params: SceneRenderPipelineParams): SceneRenderPlanningStats {
        const clearState = resolveRenderClearState(params.cameraFrame, params.renderPass);
        const camera = toRenderCameraState(params.cameraFrame, clearState);
        const lights = toRenderLights(params.lighting);
        const primitives: RenderPrimitiveInstance[] = [];

        this._primitiveLookup.clear();

        for (let index = 0; index < params.renderItems.length; index += 1) {
            const item = params.renderItems[index]!;
            const primitiveId = `scene-primitive:${item.renderer.id}`;
            const material = createMaterialSnapshot(item, params.resolveMaterial(item.renderer));

            primitives.push({
                id: primitiveId,
                meshId: item.renderer.meshId ?? `scene-mesh:${item.renderer.id}`,
                material,
                worldMatrix: item.transform.worldMatrix,
                bounds: toRenderBounds(params.resolveBounds(item.renderer)),
                sortBias: item.renderer.renderOrder,
                visible: item.renderer.visible,
                receivesLighting: item.renderer.receiveLighting,
            });
            this._primitiveLookup.set(primitiveId, item);
        }

        this._activeExecution = {
            drawContext: params.drawContext,
            frameState: params.frameState,
        };

        try {
            const result = this._pipeline.renderImmediate({
                frame: params.frame,
                deltaTime: params.deltaSeconds,
                viewport: {
                    width: params.viewportWidth,
                    height: params.viewportHeight,
                },
                camera,
                primitives,
                lights,
            });

            return Object.freeze({
                passCount: result.statistics.passCount,
                opaqueCount: result.statistics.opaqueCount,
                transparentCount: result.statistics.transparentCount,
                meshTransparentCount: result.statistics.transparentCount,
                spriteTransparentCount: 0,
                spriteBatchCount: 0,
                skippedSpriteCount: 0,
                warnings: Object.freeze([...result.warnings]),
            });
        } finally {
            this._activeExecution = null;
            this._primitiveLookup.clear();
        }
    }

    reset(): void {
        this._activeExecution = null;
        this._primitiveLookup.clear();
        this._pipeline.dispose();
        this._backend.dispose();
        this._backend = this._createBackend();
        this._pipeline = this._createPipeline();
    }

    dispose(): void {
        this._pipeline.dispose();
        this._backend.dispose();
        this._activeExecution = null;
        this._primitiveLookup.clear();
    }
}

export { DEFAULT_SCENE_RENDER_PLANNING_STATS };