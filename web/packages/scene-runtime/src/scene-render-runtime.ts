import { Vec3, Vec4 } from '@axrone/numeric';
import type { Actor, Transform } from '@axrone/ecs-runtime';
import { selectSceneCamera } from './camera-selector';
import { SceneCameraFrameStateCollector } from './camera-frame-state';
import { SceneDrawExecutionContextCache } from './draw-execution-context';
import { SceneDrawExecutor } from './draw-executor';
import { SceneFrameUniformBinder } from './frame-uniform-binder';
import { SceneLightingCollector } from './lighting-collector';
import { SceneLightingUniformBinder } from './lighting-uniform-binder';
import { resolveSceneMaterialPass } from './material-registry';
import { SceneMaterialTextureBinder } from './material-texture-binder';
import { SceneMorphMeshRuntime } from './morph-mesh-runtime';
import { SceneRenderFrameState } from './render-frame-state';
import { SceneRenderItemCollector } from './render-item-collector';
import { SceneRenderPlanner } from './scene-render-planner';
import { SceneRenderPassPreparer } from './render-pass-preparer';
import { SceneRenderStateApplier } from './render-state-applier';
import type { SceneResourceRuntime } from './scene-resource-runtime';
import { SceneSkinningUniformBinder } from './skinning-uniform-binder';
import { SceneSpriteBatchRuntime } from './sprite-batch-runtime';
import type { SceneMeshResource } from './mesh-registry';
import type {
    SceneMeshDefinition,
    SceneRenderPlanningOptions,
    SceneRenderPlanningStats,
    SceneRenderStats,
    SceneUniformValue,
} from './types';
import { SceneUniformWriter } from './uniform-writer';

export interface SceneRenderRuntimeOptions {
    readonly gl: WebGL2RenderingContext;
    readonly resources: SceneResourceRuntime;
    readonly ambientLight: Vec3;
    readonly skyLight: Vec3;
    readonly groundLight: Vec3;
    readonly defaultClearColor: Vec4;
    readonly getActors: () => readonly Actor[];
    readonly createMeshResource: (definition: SceneMeshDefinition) => SceneMeshResource;
    readonly disposeMesh: (mesh: SceneMeshResource) => void;
    readonly applyMissingVertexAttributeDefaults: (mesh: SceneMeshResource) => void;
    readonly planning?: SceneRenderPlanningOptions;
}

export interface SceneRenderRuntimeParams {
    readonly frame: number;
    readonly elapsedSeconds: number;
    readonly deltaSeconds: number;
    readonly viewportWidth: number;
    readonly viewportHeight: number;
}

export class SceneRenderRuntime {
    private static readonly _EMPTY_PLANNING_STATS: SceneRenderPlanningStats = Object.freeze({
        passCount: 0,
        opaqueCount: 0,
        transparentCount: 0,
        warnings: Object.freeze([]) as readonly string[],
    });

    private readonly _lightingCollector: SceneLightingCollector;
    private readonly _cameraFrameCollector = new SceneCameraFrameStateCollector();
    private readonly _renderItemCollector = new SceneRenderItemCollector();
    private readonly _renderFrameState = new SceneRenderFrameState();
    private readonly _drawExecutionContextCache = new SceneDrawExecutionContextCache();
    private readonly _materialTextureBinder: SceneMaterialTextureBinder;
    private readonly _renderPassPreparer: SceneRenderPassPreparer;
    private readonly _renderStateApplier: SceneRenderStateApplier;
    private readonly _uniformWriter: SceneUniformWriter;
    private readonly _frameUniformBinder: SceneFrameUniformBinder;
    private readonly _lightingUniformBinder: SceneLightingUniformBinder;
    private readonly _skinningUniformBinder: SceneSkinningUniformBinder;
    private readonly _morphMeshRuntime: SceneMorphMeshRuntime;
    private readonly _renderPlanner: SceneRenderPlanner;
    private readonly _drawExecutor: SceneDrawExecutor;
    private readonly _spriteBatchRuntime: SceneSpriteBatchRuntime;
    private _planningStats: SceneRenderPlanningStats = SceneRenderRuntime._EMPTY_PLANNING_STATS;
    private readonly _textureUniformSetter = (
        shader: Parameters<SceneUniformWriter['write']>[0],
        name: string,
        value: SceneUniformValue | null | undefined
    ): void => {
        this._uniformWriter.write(shader, name, value);
    };

    constructor(private readonly _options: SceneRenderRuntimeOptions) {
        this._lightingCollector = new SceneLightingCollector(4);
        this._materialTextureBinder = new SceneMaterialTextureBinder(_options.gl);
        this._renderPassPreparer = new SceneRenderPassPreparer(
            _options.gl,
            _options.defaultClearColor
        );
        this._renderStateApplier = new SceneRenderStateApplier(_options.gl);
        this._uniformWriter = new SceneUniformWriter(_options.gl);
        this._frameUniformBinder = new SceneFrameUniformBinder(this._uniformWriter);
        this._lightingUniformBinder = new SceneLightingUniformBinder(this._uniformWriter);
        this._skinningUniformBinder = new SceneSkinningUniformBinder(this._uniformWriter);
        this._morphMeshRuntime = new SceneMorphMeshRuntime({
            gl: _options.gl,
            createMeshResource: _options.createMeshResource,
            disposeMesh: _options.disposeMesh,
        });
        this._renderPlanner = new SceneRenderPlanner(_options.planning);
        this._drawExecutor = new SceneDrawExecutor({
            gl: _options.gl,
            resources: _options.resources,
            morphMeshRuntime: this._morphMeshRuntime,
            renderStateApplier: this._renderStateApplier,
            frameUniformBinder: this._frameUniformBinder,
            lightingUniformBinder: this._lightingUniformBinder,
            skinningUniformBinder: this._skinningUniformBinder,
            materialTextureBinder: this._materialTextureBinder,
            uniformWriter: this._uniformWriter,
            textureUniformSetter: this._textureUniformSetter,
            applyMissingVertexAttributeDefaults: _options.applyMissingVertexAttributeDefaults,
        });
        this._spriteBatchRuntime = new SceneSpriteBatchRuntime({
            gl: _options.gl,
            resources: _options.resources,
            renderStateApplier: this._renderStateApplier,
            uniformWriter: this._uniformWriter,
            materialTextureBinder: this._materialTextureBinder,
            textureUniformSetter: this._textureUniformSetter,
        });
    }

    get stats(): SceneRenderStats {
        return {
            frame: this._renderFrameState.frame,
            drawCalls: this._renderFrameState.drawCalls,
            trianglesSubmitted: this._renderFrameState.trianglesSubmitted,
            planning: this._planningStats,
        };
    }

    private _isBlendedRenderer(renderer: import('./components/mesh-renderer').MeshRenderer, renderPass: import('./render-pass-registry').SceneRenderPassResource): boolean {
        if (renderer.materialId === null) {
            return false;
        }

        const material = this._options.resources.materials.get(renderer.materialId);
        if (!material) {
            return false;
        }

        const materialPass = resolveSceneMaterialPass(material, renderPass.materialPassId);
        if (renderPass.materialPassId !== null && !materialPass) {
            return false;
        }

        const shader = this._options.resources.shaders.get(material.shaderId);
        if (!shader) {
            return false;
        }

        return this._renderStateApplier.resolveBlendEnabled(shader, renderPass, materialPass);
    }

    render(params: SceneRenderRuntimeParams): void {
        const renderFrame = this._renderFrameState.begin(params.frame);
        this._planningStats = SceneRenderRuntime._EMPTY_PLANNING_STATS;
        const actors = this._options.getActors();
        const camera = selectSceneCamera(actors);
        const cameraFrame = this._cameraFrameCollector.collect(
            camera,
            params.viewportWidth,
            params.viewportHeight
        );
        const lighting = this._lightingCollector.collect(
            actors,
            this._options.ambientLight,
            this._options.skyLight,
            this._options.groundLight,
            cameraFrame?.position
        );
        const renderPasses = this._options.resources.renderPasses.getEnabledResources();

        if (renderPasses.length === 0) {
            return;
        }
        this._options.gl.viewport(0, 0, params.viewportWidth, params.viewportHeight);

        let planningPassCount = 0;
        let planningOpaqueCount = 0;
        let planningTransparentCount = 0;
        const planningWarnings: string[] = [];

        for (const renderPass of renderPasses) {
            this._renderPassPreparer.prepare(renderPass, cameraFrame?.camera);

            if (!cameraFrame) {
                continue;
            }

            const drawContext = this._drawExecutionContextCache.prepare({
                renderPass,
                cameraFrame,
                lighting,
                elapsedSeconds: params.elapsedSeconds,
                deltaSeconds: params.deltaSeconds,
                frame: params.frame,
                viewportWidth: params.viewportWidth,
                viewportHeight: params.viewportHeight,
            });

            const renderItems = this._renderItemCollector.collect(
                actors,
                renderPass.rendererPassId,
                {
                    cameraPosition: cameraFrame.position,
                    cameraFrustum: cameraFrame.camera3D.frustum,
                    resolveBounds: (renderer) => {
                        const meshId = renderer.meshId;
                        return meshId
                            ? this._options.resources.meshes.getDefinition(meshId)?.bounds
                            : undefined;
                    },
                    isBlended: (renderer) => this._isBlendedRenderer(renderer, renderPass),
                }
            );
            const plannedRenderItems = this._renderPlanner.plan({
                frame: params.frame,
                deltaSeconds: params.deltaSeconds,
                viewportWidth: params.viewportWidth,
                viewportHeight: params.viewportHeight,
                cameraFrame,
                lighting,
                renderItems,
                resolveBounds: (renderer) => {
                    const meshId = renderer.meshId;
                    return meshId
                        ? this._options.resources.meshes.getDefinition(meshId)?.bounds
                        : undefined;
                },
                resolveMaterial: (renderer) => {
                    const material =
                        renderer.materialId !== null
                            ? this._options.resources.materials.get(renderer.materialId)
                            : undefined;
                    return {
                        materialId: material?.id ?? renderer.materialId,
                        shadingModel: material?.surface?.shadingModel ?? 'custom',
                        alphaMode: material?.surface?.alphaMode ?? 'opaque',
                        transparent:
                            (material?.surface?.alphaMode ?? 'opaque') === 'blend' ||
                            this._isBlendedRenderer(renderer, renderPass),
                    };
                },
            });

            planningPassCount += plannedRenderItems.stats.passCount;
            planningOpaqueCount += plannedRenderItems.stats.opaqueCount;
            planningTransparentCount += plannedRenderItems.stats.transparentCount;
            for (const warning of plannedRenderItems.stats.warnings) {
                if (!planningWarnings.includes(warning)) {
                    planningWarnings.push(warning);
                }
            }

            for (const item of plannedRenderItems.orderedItems) {
                this._drawExecutor.execute(item, drawContext, renderFrame);
            }

            this._spriteBatchRuntime.render({
                actors,
                cameraFrame,
                renderPass,
                frameState: renderFrame,
                viewportWidth: params.viewportWidth,
                viewportHeight: params.viewportHeight,
            });
        }

        this._planningStats = Object.freeze({
            passCount: planningPassCount,
            opaqueCount: planningOpaqueCount,
            transparentCount: planningTransparentCount,
            warnings: Object.freeze(planningWarnings),
        });

        this._options.gl.bindVertexArray(null);
        this._morphMeshRuntime.prune(renderFrame.activeRendererIds);
    }

    releaseBaseMesh(meshId: string): void {
        this._morphMeshRuntime.releaseBaseMesh(meshId);
    }

    clear(): void {
        this._planningStats = SceneRenderRuntime._EMPTY_PLANNING_STATS;
        this._renderPlanner.reset();
        this._morphMeshRuntime.clear();
        this._spriteBatchRuntime.clear();
    }
}
