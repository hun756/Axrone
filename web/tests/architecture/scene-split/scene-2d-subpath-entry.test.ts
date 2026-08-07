import { beforeAll, describe, expect, it } from 'vitest';
import { installWebGL2Constants } from '../../../packages/scene-3d/src/__tests__/test-harness';

describe('scene-2d subpath entries', () => {
    beforeAll(() => {
        installWebGL2Constants();
    });

    it(
        'separates facade, support, profile, and error entrypoints for 2d consumers',
        async () => {
            const scene2DFacade = await import('@axrone/scene-2d/facade');
            const scene2DSupport = await import('@axrone/scene-2d/support');
            const scene2DProfile = await import('@axrone/scene-2d/profile');
            const scene2DErrors = await import('@axrone/scene-2d/errors');

            expect(scene2DFacade.Scene2D).toBeDefined();
            expect(scene2DFacade.Camera).toBeDefined();
            expect(scene2DFacade.SceneRuntimeFacade).toBeDefined();
            expect('SpriteRenderer' in scene2DFacade).toBe(false);

            expect(scene2DSupport.SpriteRenderer).toBeDefined();
            expect(scene2DSupport.SpriteMask).toBeDefined();
            expect(scene2DSupport.createSpriteAtlas).toBeDefined();
            expect('Scene2D' in scene2DSupport).toBe(false);

            expect(scene2DProfile.get2DSceneRuntimeProfile).toBeDefined();
            expect(scene2DProfile.getCoreSceneRuntimeProfile).toBeDefined();
            expect(scene2DProfile.resolveSceneRegistryFromProfile).toBeDefined();
            const registry = scene2DProfile.resolveSceneRegistryFromProfile(undefined, {});
            expect(registry.SpriteRenderer).toBeDefined();
            expect('MeshRenderer' in registry).toBe(false);
            expect('Scene2D' in scene2DProfile).toBe(false);

            expect(scene2DErrors.SceneCapabilityError).toBeDefined();
            expect(scene2DErrors.SceneCanvasError).toBeDefined();
            expect('Scene2D' in scene2DErrors).toBe(false);
        },
        30_000,
    );
});
