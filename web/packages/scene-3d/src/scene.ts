import { Actor, type ActorConfig } from '@axrone/ecs-runtime';
import type { ComponentRegistry } from '@axrone/ecs-runtime';
import type { World } from '@axrone/ecs-runtime';
import type { TextureFormat } from '@axrone/render-webgl2';
import {
    getDefaultSceneRuntimeProfile,
    type SceneOptions,
    type SceneRegistry,
} from '@axrone/scene-runtime';
import {
    SceneAssetFacade,
    type CameraConfig,
} from '@axrone/scene-runtime/scene-facade';
import { type MeshRendererConfig } from '@axrone/scene-runtime/scene-3d-support';
import {
    Scene3DActorRuntime,
    type Scene3DActorRuntimeOptions,
    type SceneRenderableActorCreateOptions,
    type SceneRenderableActorInstance,
} from './scene-3d-actor-runtime';

export class Scene<R extends ComponentRegistry = Record<string, never>> extends SceneAssetFacade<R> {
    private readonly _actors3d: Scene3DActorRuntime<R>;

    constructor(options: SceneOptions<R> = {}) {
        super({
            ...options,
            profile: options.profile ?? getDefaultSceneRuntimeProfile<R>(),
        } as any);
        this._actors3d = new Scene3DActorRuntime({
            actors: this._kernel.actors as unknown as Scene3DActorRuntimeOptions<R>['actors'],
        });
    }

    createCameraActor(
        actorConfig: ActorConfig = {},
        cameraConfig: CameraConfig = {}
    ): Actor<World<SceneRegistry<R>>> {
        this.assertNotDisposed();
        return this._actors3d.createCameraActor(actorConfig, cameraConfig);
    }

    createRenderableActor(
        actorConfig: ActorConfig = {},
        rendererConfig: MeshRendererConfig = {}
    ): Actor<World<SceneRegistry<R>>> {
        this.assertNotDisposed();
        return this._actors3d.createRenderableActor(actorConfig, rendererConfig);
    }

    createRenderableActors(
        configs: readonly SceneRenderableActorCreateOptions[],
        profiling?: Record<string, number>
    ): readonly SceneRenderableActorInstance<R>[] {
        this.assertNotDisposed();
        return this._actors3d.createRenderableActors(configs, profiling);
    }

    getSupportedCompressedTextureFormats(
        preferredFormats?: readonly TextureFormat[]
    ): readonly TextureFormat[] {
        return super.getSupportedCompressedTextureFormats(preferredFormats);
    }
}