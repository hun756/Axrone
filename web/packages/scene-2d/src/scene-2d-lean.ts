import type { ComponentRegistry } from '@axrone/ecs-runtime';
import { getCoreSceneRuntimeProfile } from '@axrone/scene-runtime/scene-core-profile';
import {
    createSceneRuntimeProfile,
    type SceneRuntimeProfile,
} from '@axrone/scene-runtime/scene-profile-contract';
import { Camera } from '@axrone/scene-runtime/scene-facade';
import { SpriteRenderer } from '@axrone/scene-runtime/scene-2d-support';
import type { SceneOptions, SceneRegistry } from '@axrone/scene-runtime/types';
import { Scene2D as Scene2DBase } from './scene-2d';

/**
 * Lean 2D scene: core built-ins (Hierarchy/Transform/PrefabNodeBinding) plus
 * only the components this entry exposes (Camera, SpriteRenderer). Animated
 * sprites and masks are intentionally absent, which keeps `SpriteAnimator`,
 * `SpriteMask`, `Animator` and the `@axrone/animation` chain out of the graph.
 */
export const LEAN_2D_SCENE_PROFILE_ID = 'scene/2d-lean';

const LEAN_2D_SCENE_PROFILE: SceneRuntimeProfile<any> = Object.freeze(
    createSceneRuntimeProfile({
        id: LEAN_2D_SCENE_PROFILE_ID,
        resolveRegistry: (context) => {
            const core = getCoreSceneRuntimeProfile<any>().resolveRegistry({});
            return {
                ...core,
                Camera,
                SpriteRenderer,
                ...(context.registry ?? {}),
            } as SceneRegistry<any>;
        },
    })
);

export const getLean2DSceneRuntimeProfile = <
    R extends ComponentRegistry = Record<string, never>,
>(): SceneRuntimeProfile<R> => LEAN_2D_SCENE_PROFILE as SceneRuntimeProfile<R>;

export class Scene2D<R extends ComponentRegistry = Record<string, never>> extends Scene2DBase<R> {
    constructor(options: SceneOptions<R> = {}) {
        super({
            ...options,
            profile: options.profile ?? getLean2DSceneRuntimeProfile<R>(),
        });
    }
}
