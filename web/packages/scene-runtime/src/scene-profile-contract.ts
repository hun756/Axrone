import type { ComponentRegistry } from '@axrone/ecs-runtime';
import type { SceneRegistry } from './types';

export interface SceneRuntimeProfileContext<
    R extends ComponentRegistry = Record<string, never>,
> {
    readonly registry?: R;
}

export interface SceneRuntimeProfile<R extends ComponentRegistry = Record<string, never>> {
    readonly id: string;
    resolveRegistry(context: SceneRuntimeProfileContext<R>): SceneRegistry<R>;
}

export const createSceneRuntimeProfile = <
    R extends ComponentRegistry = Record<string, never>,
>(
    profile: SceneRuntimeProfile<R>
): SceneRuntimeProfile<R> => profile;

export const resolveSceneRegistryFromProfile = <
    R extends ComponentRegistry = Record<string, never>,
>(
    profile: SceneRuntimeProfile<R> | undefined,
    fallbackProfile: SceneRuntimeProfile<R>,
    context: SceneRuntimeProfileContext<R> = {}
): SceneRegistry<R> => (profile ?? fallbackProfile).resolveRegistry(context);