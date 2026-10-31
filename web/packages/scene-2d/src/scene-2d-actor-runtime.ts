import { Transform, type Actor, type ActorConfig } from '@axrone/ecs-runtime';
import type { World } from '@axrone/ecs-runtime';
import type { ComponentRegistry } from '@axrone/ecs-runtime';
import { Quat, Vec3 } from '@axrone/numeric';
import {
    type SceneActorRuntime,
    type SceneRegistry,
} from '@axrone/scene-runtime';
import { SceneCapabilityError } from '@axrone/scene-runtime/errors';
import {
    Camera,
    type CameraConfig,
    requireRegisteredComponent,
} from '@axrone/scene-runtime/scene-facade';
import {
    SpriteRenderer,
    type SpriteAnimatorConfig,
    type SpriteMaskConfig,
    type SpriteRendererConfig,
} from '@axrone/scene-runtime/scene-2d-support';
import type {
    Scene2DAnimationRuntime,
} from './scene-2d-animation-runtime';

export interface Scene2DActorRuntimeOptions<
    R extends ComponentRegistry = Record<string, never>,
> {
    readonly actors: SceneActorRuntime<R>;
    /**
     * Animated/mask factories. Root `Scene2D` installs them; a lean sprite-only
     * scene omits them so `SpriteAnimator`/`SpriteMask` stay out of the graph.
     */
    readonly animation?: Scene2DAnimationRuntime<R>;
}

export interface Scene2DBulkSpriteConfig {
    readonly actorConfig?: ActorConfig;
    readonly spriteConfig?: SpriteRendererConfig;
    /** Initial world position; omitted fields keep the default transform. */
    readonly position?: readonly [number, number, number];
    /** Initial local rotation quaternion (x, y, z, w). */
    readonly rotation?: readonly [number, number, number, number];
    /**
     * Initial rotation about +Z in radians. Equivalent to `rotation` set to
     * `Quat.fromAxisAngle((0, 0, 1), rotationZ)`; wins over `rotation`.
     */
    readonly rotationZ?: number;
    readonly scale?: readonly [number, number, number];
}

export class Scene2DActorRuntime<R extends ComponentRegistry = Record<string, never>> {
    private readonly _actors: SceneActorRuntime<R>;
    private readonly _animation: Scene2DAnimationRuntime<R> | undefined;

    constructor(options: Scene2DActorRuntimeOptions<R>) {
        this._actors = options.actors;
        this._animation = options.animation;
    }

    createCameraActor(
        actorConfig: ActorConfig = {},
        cameraConfig: CameraConfig = {}
    ): Actor<World<SceneRegistry<R>>> {
        requireRegisteredComponent(
            this._actors,
            Camera,
            'camera actor creation requires the 2D scene capability/profile'
        );
        const isOrthographic = cameraConfig.orthographic ?? true;
        const actor = this._actors.createActor(actorConfig);
        actor.addComponent(Camera, {
            ...cameraConfig,
            orthographic: isOrthographic,
            ...(isOrthographic
                ? {
                      near: cameraConfig.near ?? 0.1,
                      far: cameraConfig.far ?? 1000,
                  }
                : {}),
        });

        if (isOrthographic) {
            const transform = actor.getComponent(Transform);
            if (transform) {
                transform.position = new Vec3(0, 0, 1);
            }
        }

        return actor;
    }

    createSpriteActor(
        actorConfig: ActorConfig = {},
        spriteConfig: SpriteRendererConfig = {}
    ): Actor<World<SceneRegistry<R>>> {
        requireRegisteredComponent(
            this._actors,
            SpriteRenderer,
            'sprite actor creation requires the 2D scene capability/profile'
        );
        const actor = this._actors.createActor(actorConfig);
        actor.addComponent(SpriteRenderer, spriteConfig);
        return actor;
    }

    /**
     * Bulk sprite creation. Hoists the per-call capability check and applies
     * optional initial transforms inline with reused scratch vectors, avoiding
     * the second `getComponent(Transform)` pass a caller would otherwise run.
     * Transforms that only need XY position, Z rotation and scale are written
     * through `Transform.setLocalTRS2D`, which bumps the render revision once.
     *
     * Note: this intentionally builds actors through the same
     * `createActor` + `addComponent` path as {@link createSpriteActor}.
     * The ECS `createEntityWithComponents` bulk archetype path allocates more
     * per actor and measured ~2× slower for 50k sprites.
     *
     * Fallback: sprites that need masks, clips, nine-slice or multiple textures
     * are still created here; only the *render* fast path declines them, so
     * this API never changes scene semantics.
     */
    createSpriteActors(
        configs: readonly Scene2DBulkSpriteConfig[]
    ): readonly Actor<World<SceneRegistry<R>>>[] {
        requireRegisteredComponent(
            this._actors,
            SpriteRenderer,
            'sprite actor creation requires the 2D scene capability/profile'
        );
        if (configs.length === 0) {
            return [];
        }

        const actors = new Array<Actor<World<SceneRegistry<R>>>>(configs.length);
        const position = new Vec3();
        const rotation = new Quat(0, 0, 0, 1);
        const scale = new Vec3(1, 1, 1);

        for (let index = 0; index < configs.length; index += 1) {
            const config = configs[index]!;
            const actor = this._actors.createActor(config.actorConfig ?? {});
            actor.addComponent(SpriteRenderer, config.spriteConfig ?? {});
            actors[index] = actor;

            const hasPosition = config.position !== undefined;
            const hasRotation = config.rotation !== undefined;
            const hasScale = config.scale !== undefined;
            const hasRotationZ = config.rotationZ !== undefined;
            if (!hasPosition && !hasRotation && !hasRotationZ && !hasScale) {
                continue;
            }
            const transform = actor.getComponent(Transform);
            if (!transform) {
                continue;
            }

            // Omitted fields fall back to the documented defaults; reset the
            // shared scratch so a previous config's values never leak forward.
            position.x = 0;
            position.y = 0;
            position.z = 0;
            scale.x = 1;
            scale.y = 1;
            scale.z = 1;

            if (hasPosition) {
                position.x = config.position![0];
                position.y = config.position![1];
                position.z = config.position![2];
            }
            if (hasScale) {
                scale.x = config.scale![0];
                scale.y = config.scale![1];
                scale.z = config.scale![2];
            }

            if (hasRotationZ) {
                // Scratch position/scale hold the documented defaults when the
                // caller omitted them, so this is a complete atomic write.
                transform.setLocalTRS2D(position.x, position.y, config.rotationZ!, scale.x, scale.y);
                continue;
            }

            if (hasRotation) {
                rotation.x = config.rotation![0];
                rotation.y = config.rotation![1];
                rotation.z = config.rotation![2];
                rotation.w = config.rotation![3];
            }

            if (hasPosition && hasRotation && hasScale) {
                transform.setLocalTRS(position, rotation, scale);
                continue;
            }

            if (hasPosition) {
                transform.position = position;
            }
            if (hasRotation) {
                transform.rotation = rotation;
            }
            if (hasScale) {
                transform.scale = scale;
            }
        }

        return actors;
    }

    createAnimatedSpriteActor(
        actorConfig: ActorConfig = {},
        spriteConfig: SpriteRendererConfig = {},
        animatorConfig: SpriteAnimatorConfig = {}
    ): Actor<World<SceneRegistry<R>>> {
        const animation = this._animation;
        if (!animation) {
            throw new SceneCapabilityError(
                'animated sprite creation requires the animated 2D scene profile; use the root @axrone/scene-2d entry or pass a custom profile'
            );
        }
        return animation.createAnimatedSpriteActor(
            actorConfig,
            spriteConfig,
            animatorConfig
        );
    }

    createMaskActor(
        actorConfig: ActorConfig = {},
        maskConfig: SpriteMaskConfig = {}
    ): Actor<World<SceneRegistry<R>>> {
        const animation = this._animation;
        if (!animation) {
            throw new SceneCapabilityError(
                'mask actor creation requires the animated 2D scene profile; use the root @axrone/scene-2d entry or pass a custom profile'
            );
        }
        return animation.createMaskActor(actorConfig, maskConfig);
    }
}