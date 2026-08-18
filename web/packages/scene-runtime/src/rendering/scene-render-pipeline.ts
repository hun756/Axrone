import type { Actor } from '@axrone/ecs-runtime';
import { RenderPipeline } from '@axrone/render-core';
import type {
    AnyPostProcessEffect,
    RenderCameraState,
    RenderClearState,
    RenderHdrSettings,
    RenderLight,
    RenderMaterialSnapshot,
    RenderPrimitiveInstance,
    ResolvedRenderPass,
    RenderTonemappingSettings,
} from '@axrone/render-core/types';
import {
    createWebGL2RenderPassLibrary,
    createWebGL2RenderResourceAllocator,
    defineWebGL2RenderPassExecutor,
    type ManagedWebGL2RenderPassLibrary,
    type WebGL2RenderResourceHandle,
} from '@axrone/render-webgl2/pipeline';
import type { BoundingSphere } from '@axrone/geometry';
import type { SceneCameraFrameState } from '../camera-frame-state';
import type { MeshRenderer } from '../components/mesh-renderer';
import type { SceneDrawExecutor, SceneDrawExecutorContext } from './draw-executor';
import type { SceneLightingState } from '../lighting-collector';
import type { SceneRenderFrameState } from './render-frame-state';
import type { SceneRenderItem } from './render-item-collector';
import type { SceneRenderPassResource } from './render-pass-registry';
import type { SceneSpriteBatchRuntime } from './sprite-batch-runtime';
import type { SceneParticleBatchRuntime } from './particle-batch-runtime';
import type { SceneLineBatchRuntime } from './line-batch-runtime';
import type {
    SceneMaterialAlphaMode,
    SceneRenderPlanningOptions,
    SceneRenderPipelineSettings,
    SceneRenderPlanningStats,
} from '../types';

export interface SceneRenderPipelineParams {
    readonly actors?: readonly Actor[];
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

type SceneSpriteBatchRenderer = Pick<SceneSpriteBatchRuntime, 'render' | 'clear'>;
type SceneParticleBatchRenderer = Pick<SceneParticleBatchRuntime, 'render' | 'clear'>;
type SceneLineBatchRenderer = Pick<SceneLineBatchRuntime, 'render' | 'clear'>;

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

const resolveSceneHdrOption = (
    settings: SceneRenderPipelineSettings | undefined
): false | true | Partial<RenderHdrSettings> => {
    const hdr = settings?.hdr;

    if (hdr === undefined || hdr === false) {
        return false;
    }

    if (hdr === true) {
        return true;
    }

    return {
        ...hdr,
        enabled: hdr.enabled ?? true,
    };
};

const isHdrEnabled = (value: false | true | Partial<RenderHdrSettings>): boolean =>
    value === true || (typeof value === 'object' && (value.enabled ?? true));

const resolveSceneTonemappingOption = (
    settings: SceneRenderPipelineSettings | undefined,
    hdrEnabled: boolean
): Partial<RenderTonemappingSettings> | undefined => {
    if (settings?.tonemapping) {
        return settings.tonemapping;
    }

    if (hdrEnabled) {
        return undefined;
    }

    return {
        mode: 'none',
    };
};

const resolveScenePostProcessOptions = (
    settings: SceneRenderPipelineSettings | undefined
): readonly AnyPostProcessEffect[] => settings?.postProcess ?? Object.freeze([]);

const hasEnabledScenePostProcessEffects = (
    settings: SceneRenderPipelineSettings | undefined
): boolean => settings?.postProcess?.some((effect) => effect.enabled !== false) ?? false;

const pushUniqueWarnings = (target: string[], warnings: readonly string[]): void => {
    for (let index = 0; index < warnings.length; index += 1) {
        const warning = warnings[index]!;
        if (!target.includes(warning)) {
            target.push(warning);
        }
    }
};

interface SceneRenderPipelineOptions {
    readonly gl: WebGL2RenderingContext;
    readonly drawExecutor: Pick<SceneDrawExecutor, 'execute'>;
    readonly spriteBatchRuntime?: SceneSpriteBatchRenderer;
    readonly particleBatchRuntime?: SceneParticleBatchRenderer;
    readonly lineBatchRuntime?: SceneLineBatchRenderer;
    readonly planning?: SceneRenderPlanningOptions;
    readonly pipeline?: SceneRenderPipelineSettings;
}

interface ActiveExecutionState {
    readonly drawContext: SceneDrawExecutorContext;
    readonly frameState: SceneRenderFrameState;
}

export class SceneRenderPipeline {
    private readonly _primitiveLookup = new Map<string, SceneRenderItem>();
    private readonly _options: SceneRenderPipelineOptions;
    private _activeExecution: ActiveExecutionState | null = null;
    private _backend: ManagedWebGL2RenderPassLibrary;
    private _pipeline: RenderPipeline<WebGL2RenderResourceHandle>;

    constructor(options: SceneRenderPipelineOptions) {
        this._options = options;
        this._backend = this._createBackend();
        this._pipeline = this._createPipeline();
    }

    private _createBackend(): ManagedWebGL2RenderPassLibrary {
        const hdr = resolveSceneHdrOption(this._options.pipeline);
        const hdrEnabled = isHdrEnabled(hdr);
        const tonemapping = resolveSceneTonemappingOption(this._options.pipeline, hdrEnabled);
        const hasPostProcess = hasEnabledScenePostProcessEffects(this._options.pipeline);

        return createWebGL2RenderPassLibrary({
            gl: this._options.gl,
            directFrameOutput:
                !hdrEnabled &&
                (tonemapping?.mode ?? 'none') === 'none' &&
                !hasPostProcess,
            executors: [
                defineWebGL2RenderPassExecutor({
                    kind: 'opaque',
                    name: 'scene-draw-opaque',
                    execute: (pass) => this._executeDrawPass(pass),
                }),
                defineWebGL2RenderPassExecutor({
                    kind: 'transparent',
                    name: 'scene-draw-transparent',
                    execute: (pass) => this._executeDrawPass(pass),
                }),
            ],
        });
    }

    private _createPipeline(): RenderPipeline<WebGL2RenderResourceHandle> {
        const hdr = resolveSceneHdrOption(this._options.pipeline);
        const hdrEnabled = isHdrEnabled(hdr);
        const tonemapping = resolveSceneTonemappingOption(this._options.pipeline, hdrEnabled);
        const postProcess = resolveScenePostProcessOptions(this._options.pipeline);

        return new RenderPipeline<WebGL2RenderResourceHandle>({
            name: 'SceneRenderPipeline',
            hdr,
            ...(tonemapping ? { tonemapping } : {}),
            shadows: false,
            gi: {
                mode: 'disabled',
            },
            volumetrics: {
                enabled: false,
            },
            lightBaking: false,
            postProcess,
            enableDepthPrepass: false,
            maxActiveReflectionProbes: 0,
            maxActiveLocalLights: 4,
            maxTransparentPrimitives: this._options.planning?.maxTransparentPrimitives,
            ...(this._options.pipeline?.maxPostProcessPasses !== undefined
                ? {
                      maxPostProcessPasses: this._options.pipeline.maxPostProcessPasses,
                  }
                : {}),
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

            const meshTransparentCount = result.statistics.transparentCount;
            const warnings = [...result.warnings];
            const spriteStats =
                this._options.spriteBatchRuntime && params.actors
                    ? this._options.spriteBatchRuntime.render({
                          actors: params.actors,
                          cameraFrame: params.cameraFrame,
                          renderPass: params.renderPass,
                          frameState: params.frameState,
                          viewportWidth: params.viewportWidth,
                          viewportHeight: params.viewportHeight,
                          ...(this._options.planning?.maxTransparentPrimitives !== undefined
                              ? {
                                    transparentBudget: {
                                        total: this._options.planning.maxTransparentPrimitives,
                                        remaining: Math.max(
                                            0,
                                            this._options.planning.maxTransparentPrimitives -
                                                meshTransparentCount
                                        ),
                                    },
                                }
                              : {}),
                      })
                    : null;

            if (spriteStats) {
                pushUniqueWarnings(warnings, spriteStats.warnings);
            }

            if (this._options.particleBatchRuntime && params.actors) {
                this._options.particleBatchRuntime.render({
                    actors: params.actors,
                    cameraFrame: params.cameraFrame,
                    frameState: params.frameState,
                    viewportWidth: params.viewportWidth,
                    viewportHeight: params.viewportHeight,
                });
            }

            if (this._options.lineBatchRuntime && params.actors) {
                this._options.lineBatchRuntime.render({
                    actors: params.actors,
                    cameraFrame: params.cameraFrame,
                    frameState: params.frameState,
                });
            }

            return Object.freeze({
                passCount: result.statistics.passCount,
                opaqueCount: result.statistics.opaqueCount,
                transparentCount: meshTransparentCount + (spriteStats?.drawnSpriteCount ?? 0),
                meshTransparentCount,
                spriteTransparentCount: spriteStats?.drawnSpriteCount ?? 0,
                spriteBatchCount: spriteStats?.spriteBatchCount ?? 0,
                skippedSpriteCount: spriteStats?.skippedSpriteCount ?? 0,
                warnings: Object.freeze(warnings),
            });
        } finally {
            this._activeExecution = null;
            this._primitiveLookup.clear();
        }
    }

    reset(): void {
        this._activeExecution = null;
        this._primitiveLookup.clear();
        this._options.spriteBatchRuntime?.clear();
        this._options.particleBatchRuntime?.clear();
        this._options.lineBatchRuntime?.clear();
        this._pipeline.dispose();
        this._backend.dispose();
        this._backend = this._createBackend();
        this._pipeline = this._createPipeline();
    }
                                    
    /**
     * Recovers from a lost-and-restored WebGL context: forgets every cached
     * GPU handle without issuing GL delete calls (the objects are already
     * invalid) and rebuilds the backend so resources are recreated lazily on
     * the next frame.
     */
    invalidateContextResources(): void {
        this._activeExecution = null;
        this._primitiveLookup.clear();
        // Batch runtimes drop their GL objects; delete calls are no-ops on a
        // lost context, so clear() is safe and resets their lazy creation.
        this._options.spriteBatchRuntime?.clear();
        this._options.particleBatchRuntime?.clear();
        this._options.lineBatchRuntime?.clear();
        this._backend.invalidateContextResources();
        this._pipeline.dispose();
        this._pipeline = this._createPipeline();
    }

    dispose(): void {
        this._pipeline.dispose();
        this._backend.dispose();
        this._options.spriteBatchRuntime?.clear();
        this._options.particleBatchRuntime?.clear();
        this._options.lineBatchRuntime?.clear();
        this._activeExecution = null;
        this._primitiveLookup.clear();
    }
}

export { DEFAULT_SCENE_RENDER_PLANNING_STATS };