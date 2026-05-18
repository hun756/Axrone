import { RenderTextureRegistry } from '@axrone/render-core/graph';
import { ReusableList } from '@axrone/render-core/memory';
import { RenderFrameClassifier, RenderPassPlanner } from '@axrone/render-core/planner';
import type {
    RenderCameraState,
    RenderLight,
    RenderMaterialSnapshot,
    RenderPrimitiveInstance,
} from '@axrone/render-core/types';
import type { BoundingSphere } from '@axrone/geometry';
import type { SceneCameraFrameState } from './camera-frame-state';
import type { SceneLightingState } from './lighting-collector';
import type { MeshRenderer } from './components/mesh-renderer';
import type { SceneRenderItem } from './render-item-collector';
import type {
    SceneMaterialAlphaMode,
    SceneRenderPlanningOptions,
    SceneRenderPlanningStats,
} from './types';

export interface SceneRenderPlannerParams {
    readonly frame: number;
    readonly deltaSeconds: number;
    readonly viewportWidth: number;
    readonly viewportHeight: number;
    readonly cameraFrame: SceneCameraFrameState;
    readonly lighting: SceneLightingState;
    readonly renderItems: readonly SceneRenderItem[];
    readonly resolveBounds: (renderer: MeshRenderer) => Readonly<BoundingSphere> | null | undefined;
    readonly resolveMaterial: (renderer: MeshRenderer) => {
        readonly materialId: string | null;
        readonly shadingModel: 'unlit' | 'pbr' | 'custom';
        readonly alphaMode: SceneMaterialAlphaMode;
        readonly transparent: boolean;
    };
}

export interface SceneRenderPlannerResult {
    readonly orderedItems: readonly SceneRenderItem[];
    readonly stats: SceneRenderPlanningStats;
}

const DEFAULT_SCENE_RENDER_PLANNING_STATS: SceneRenderPlanningStats = Object.freeze({
    passCount: 0,
    opaqueCount: 0,
    transparentCount: 0,
    warnings: Object.freeze([]) as readonly string[],
});

const clampCosine = (value: number): number => Math.min(1, Math.max(-1, value));

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
        center: bounds.center,
        extents: [bounds.radius, bounds.radius, bounds.radius],
    };
};

const toRenderCameraState = (cameraFrame: SceneCameraFrameState): RenderCameraState => ({
    id: `scene-camera:${cameraFrame.camera.id}`,
    viewMatrix: cameraFrame.viewMatrix,
    projectionMatrix: cameraFrame.projectionMatrix,
    viewProjectionMatrix: cameraFrame.viewProjectionMatrix,
    camera3D: cameraFrame.camera3D,
    frustum: cameraFrame.camera3D.frustum,
    position: cameraFrame.position,
    near: cameraFrame.camera.near,
    far: cameraFrame.camera.far,
    clearState: {
        color: cameraFrame.camera.clearColor,
        depth: cameraFrame.camera.clearDepth,
    },
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
    resolvedMaterial: SceneRenderPlannerParams['resolveMaterial'] extends (
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

export class SceneRenderPlanner {
    private readonly _graph = new RenderTextureRegistry();
    private readonly _classifier: RenderFrameClassifier;
    private readonly _planner: RenderPassPlanner;
    private readonly _warnings = new ReusableList<string>(16);
    private readonly _primitiveLookup = new Map<string, SceneRenderItem>();
    private readonly _orderedItems: SceneRenderItem[] = [];

    constructor(options: SceneRenderPlanningOptions = {}) {
        this._classifier = new RenderFrameClassifier({
            maxTransparentPrimitives: Math.max(
                0,
                Math.floor(options.maxTransparentPrimitives ?? Number.MAX_SAFE_INTEGER)
            ),
            maxActiveLocalLights: 4,
            maxActiveReflectionProbes: 0,
            maxShadowedLights: 0,
        });
        this._planner = new RenderPassPlanner(this._graph, {
            shadows: {
                atlasSize: 1024,
                cascadeCount: 1,
                cascadeSplitLambda: 0.5,
                maxDistance: 1000,
                filter: 'pcf',
            },
            tonemapping: {
                mode: 'none',
            },
            lightBaking: {
                budgetMs: 0,
            },
            enableDepthPrepass: false,
        });
    }

    plan(params: SceneRenderPlannerParams): SceneRenderPlannerResult {
        if (params.renderItems.length === 0) {
            return {
                orderedItems: Object.freeze([]) as readonly SceneRenderItem[],
                stats: DEFAULT_SCENE_RENDER_PLANNING_STATS,
            };
        }

        this._primitiveLookup.clear();
        this._orderedItems.length = 0;

        const camera = toRenderCameraState(params.cameraFrame);
        const primitives: RenderPrimitiveInstance[] = [];
        for (let index = 0; index < params.renderItems.length; index += 1) {
            const item = params.renderItems[index]!;
            const primitiveId = `scene-primitive:${item.renderer.id}`;
            const material = createMaterialSnapshot(
                item,
                params.resolveMaterial(item.renderer)
            );

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

        const lights = toRenderLights(params.lighting);

        this._warnings.reset();
        this._classifier.reset();
        this._graph.beginFrame(params.frame);

        try {
            this._classifier.classify(
                {
                    frame: params.frame,
                    deltaTime: params.deltaSeconds,
                    camera,
                    primitives,
                    lights,
                    viewport: {
                        width: params.viewportWidth,
                        height: params.viewportHeight,
                    },
                },
                params.frame,
                this._warnings
            );

            const plannedPasses = this._planner.plan({
                frame: params.frame,
                viewport: {
                    width: params.viewportWidth,
                    height: params.viewportHeight,
                },
                camera,
                environment: undefined,
                hdr: {
                    enabled: false,
                    colorFormat: 'rgba8',
                    outputColorSpace: 'srgb',
                    exposure: null,
                },
                gi: {
                    mode: 'disabled',
                },
                volumetrics: {
                    enabled: false,
                    froxelResolution: [1, 1, 1],
                    temporalReprojection: false,
                },
                shadowEnabled: false,
                probeUpdateCount: 0,
                postEffects: Object.freeze([]),
                bakeTasks: Object.freeze([]),
                opaque: this._classifier.opaque,
                transparent: this._classifier.transparent,
                shadowCasters: this._classifier.shadowCasters,
                activeLights: this._classifier.activeLights,
                shadowLights: this._classifier.shadowLights,
                activeProbes: this._classifier.activeProbes,
                probeUpdates: this._classifier.probeUpdates,
            });

            for (let passIndex = 0; passIndex < plannedPasses.length; passIndex += 1) {
                const pass = plannedPasses[passIndex]!;
                if (pass.kind !== 'opaque' && pass.kind !== 'transparent') {
                    continue;
                }

                for (const primitive of pass.items?.toArray() ?? []) {
                    const item = this._primitiveLookup.get(primitive.id);
                    if (item) {
                        this._orderedItems.push(item);
                    }
                }
            }

            return {
                orderedItems: Object.freeze([...this._orderedItems]),
                stats: Object.freeze({
                    passCount: plannedPasses.length,
                    opaqueCount: this._classifier.opaque.length,
                    transparentCount: this._classifier.transparent.length,
                    warnings: Object.freeze(this._warnings.toArray()),
                }),
            };
        } finally {
            this._graph.endFrame();
        }
    }

    reset(): void {
        this._planner.clear();
        this._classifier.clear();
        this._warnings.clear();
        this._primitiveLookup.clear();
        this._orderedItems.length = 0;
    }

    dispose(): void {
        this.reset();
        this._graph.dispose();
    }
}