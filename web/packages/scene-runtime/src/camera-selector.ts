import type { Entity } from '@axrone/ecs-runtime';
import { Camera } from './components/camera';

export const selectSceneCamera = (entities: readonly Entity[]): Camera | undefined => {
    let fallback: Camera | undefined;

    for (const entity of entities) {
        if (!entity.active) {
            continue;
        }

        const camera = entity.getComponent(Camera);
        if (!camera || !camera.enabled) {
            continue;
        }

        if (camera.primary) {
            return camera;
        }

        if (!fallback) {
            fallback = camera;
        }
    }

    return fallback;
};
