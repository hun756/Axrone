import type { ComponentRegistry } from '@axrone/ecs-runtime';
import {
    createSceneRegistryFromBuiltInManifests,
    type SceneBuiltInManifest,
} from './scene-registry';
import {
    createSceneRuntimeProfile,
    type SceneRuntimeProfile,
    type SceneRuntimeProfileContext,
} from './scene-profile-contract';

export interface SceneManifestRuntimeProfileOptions<
    R extends ComponentRegistry = Record<string, never>,
> {
    readonly id: string;
    readonly manifests: readonly SceneBuiltInManifest[];
}

export type { SceneRuntimeProfile, SceneRuntimeProfileContext } from './scene-profile-contract';
export { createSceneRuntimeProfile } from './scene-profile-contract';

export const createSceneManifestRuntimeProfile = <
    R extends ComponentRegistry = Record<string, never>,
>(
    options: SceneManifestRuntimeProfileOptions<R>
): SceneRuntimeProfile<R> =>
    createSceneRuntimeProfile({
        id: options.id,
        resolveRegistry: ({ registry }: SceneRuntimeProfileContext<R>) =>
            createSceneRegistryFromBuiltInManifests({
                registry,
                manifests: options.manifests,
            }),
    });