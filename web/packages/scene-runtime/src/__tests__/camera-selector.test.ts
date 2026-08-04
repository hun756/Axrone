import { describe, expect, it } from 'vitest';
import { selectSceneCamera } from '../camera-selector';
import { Camera } from '../components/camera';
import { Actor, World } from '@axrone/ecs-runtime';
import { createSceneRegistry } from '../scene-registry';

const createWorldWithActors = (...cameraConfigs: Array<{ primary?: boolean; enabled?: boolean; active?: boolean }>): { world: World; actors: Actor[] } => {
    const world = new World(createSceneRegistry());
    const actors: Actor[] = [];

    for (const config of cameraConfigs) {
        const actor = new Actor(world);
        const camera = actor.addComponent(Camera, {
            primary: config.primary ?? false,
        });
        if (config.enabled === false) {
            camera.enabled = false;
        }
        if (config.active === false) {
            actor.active = false;
        }
        actors.push(actor);
    }

    return { world, actors };
};

describe('selectSceneCamera', () => {
    it('returns undefined for empty actor list', () => {
        expect(selectSceneCamera([])).toBeUndefined();
    });

    it('returns the primary camera when one exists', () => {
        const { actors } = createWorldWithActors(
            { primary: false },
            { primary: true },
            { primary: false }
        );
        const selected = selectSceneCamera(actors);
        expect(selected).toBeDefined();
        expect(selected!.primary).toBe(true);
    });

    it('returns the first enabled camera as fallback when no primary', () => {
        const { actors } = createWorldWithActors(
            { primary: false },
            { primary: false }
        );
        const selected = selectSceneCamera(actors);
        expect(selected).toBeDefined();
        expect(selected!.primary).toBe(false);
    });

    it('skips disabled cameras', () => {
        const { actors } = createWorldWithActors(
            { enabled: false },
            { primary: false }
        );
        const selected = selectSceneCamera(actors);
        expect(selected).toBeDefined();
        expect(selected!.enabled).toBe(true);
    });

    it('skips inactive actors', () => {
        const { actors } = createWorldWithActors(
            { active: false, primary: true },
            { primary: false }
        );
        const selected = selectSceneCamera(actors);
        expect(selected).toBeDefined();
        expect(selected!.primary).toBe(false);
    });

    it('returns undefined when all cameras are disabled', () => {
        const { actors } = createWorldWithActors(
            { enabled: false },
            { enabled: false }
        );
        expect(selectSceneCamera(actors)).toBeUndefined();
    });

    it('returns undefined when all actors are inactive', () => {
        const { actors } = createWorldWithActors(
            { active: false },
            { active: false }
        );
        expect(selectSceneCamera(actors)).toBeUndefined();
    });

    it('prefers primary over non-primary even if non-primary comes first', () => {
        const { actors } = createWorldWithActors(
            { primary: false },
            { primary: true }
        );
        const selected = selectSceneCamera(actors);
        expect(selected!.primary).toBe(true);
    });
});
