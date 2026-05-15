import { beforeAll, describe, expect, it } from 'vitest';
import { installWebGL2Constants } from '../../../packages/scene-3d/src/__tests__/test-harness';

describe('scene-3d subpath entries', () => {
    beforeAll(() => {
        installWebGL2Constants();
    });

    it('separates facade, support, profile, and error entrypoints for 3d consumers', async () => {
        const scene3DFacade = await import('@axrone/scene-3d/facade');
        const scene3DSupport = await import('@axrone/scene-3d/support');
        const scene3DProfile = await import('@axrone/scene-3d/profile');
        const scene3DErrors = await import('@axrone/scene-3d/errors');

        expect(scene3DFacade.Scene).toBeDefined();
        expect(scene3DFacade.Camera).toBeDefined();
        expect(scene3DFacade.SceneRuntimeFacade).toBeDefined();
        expect('MeshRenderer' in scene3DFacade).toBe(false);

        expect(scene3DSupport.MeshRenderer).toBeDefined();
        expect(scene3DSupport.OrbitCameraController).toBeDefined();
        expect(scene3DSupport.Scene3DActorRuntime).toBeDefined();
        expect(scene3DSupport.createUnlitColorShaderDefinition).toBeDefined();
        expect('Scene' in scene3DSupport).toBe(false);

        expect(scene3DProfile.get3DSceneRuntimeProfile).toBeDefined();
        expect(scene3DProfile.getDefaultSceneRuntimeProfile).toBeDefined();
        expect('Scene' in scene3DProfile).toBe(false);

        expect(scene3DErrors.SceneCapabilityError).toBeDefined();
        expect(scene3DErrors.SceneCanvasError).toBeDefined();
        expect('Scene' in scene3DErrors).toBe(false);
    });
});