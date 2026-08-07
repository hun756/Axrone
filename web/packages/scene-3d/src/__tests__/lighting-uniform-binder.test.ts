import { Vec3 } from '@axrone/numeric';
import { describe, expect, it, vi } from 'vitest';
import type { SceneLightingState } from '@axrone/scene-3d';
import { SceneLightingUniformBinder } from '@axrone/scene-3d';
import type { SceneShaderResource } from '@axrone/scene-3d';
import type { SceneUniformWriteTarget } from '@axrone/scene-3d';

describe('SceneLightingUniformBinder', () => {
    it('writes zeroed lighting controls when the renderer does not receive lighting', () => {
        const writer = {
            write: vi.fn(),
        } as unknown as SceneUniformWriteTarget;
        const binder = new SceneLightingUniformBinder(writer);
        const shader = {} as SceneShaderResource;
        const lighting = {
            rigId: 'test-rig' as any,
            version: 1 as any,
            sortMode: 'influence' as const,
            capacity: {
                maxDirectionalLights: 1,
                maxPointLights: 4,
                maxSpotLights: 4,
                maxLocalLights: 4,
            },
            environment: {
                ambient: new Vec3(0.4, 0.3, 0.2),
                sky: new Vec3(0.5, 0.45, 0.4),
                ground: new Vec3(0.1, 0.08, 0.06),
                exposure: 1.0,
                gamma: 2.2,
            },
            stats: {
                totalLightCount: 0,
                totalDirectionalCount: 0,
                totalPointCount: 0,
                totalSpotCount: 0,
                selectedDirectionalCount: 0,
                selectedPointCount: 0,
                selectedSpotCount: 0,
                selectedLocalLightCount: 0,
                omittedDirectionalCount: 0,
                omittedPointCount: 0,
                omittedSpotCount: 0,
                omittedLocalLightCount: 0,
            },
            directionalDirections: new Float32Array(3),
            directionalColors: new Float32Array(3),
            directionalAmbientColors: new Float32Array(3),
            directionalIntensities: new Float32Array(1),
            pointPositions: new Float32Array(0),
            pointColors: new Float32Array(0),
            pointIntensities: new Float32Array(0),
            pointRanges: new Float32Array(0),
            spotPositions: new Float32Array(0),
            spotDirections: new Float32Array(0),
            spotColors: new Float32Array(0),
            spotIntensities: new Float32Array(0),
            spotRanges: new Float32Array(0),
            spotInnerConeCosines: new Float32Array(0),
            spotOuterConeCosines: new Float32Array(0),
            localLightKinds: new Int32Array(0),
            localLightPositions: new Float32Array(0),
            localLightDirections: new Float32Array(0),
            localLightColors: new Float32Array(0),
            localLightIntensities: new Float32Array(0),
            localLightRanges: new Float32Array(0),
            localLightInnerConeCosines: new Float32Array(0),
            localLightOuterConeCosines: new Float32Array(0),
        } satisfies SceneLightingState;

        binder.apply(shader, { receiveLighting: false }, lighting);

        expect(writer.write).toHaveBeenCalledWith(shader, 'u_ReceiveLighting', false);
        expect(writer.write).toHaveBeenCalledWith(shader, 'u_AmbientLight', Vec3.ZERO);
        expect(writer.write).toHaveBeenCalledWith(shader, 'u_SkyLight', Vec3.ZERO);
        expect(writer.write).toHaveBeenCalledWith(shader, 'u_GroundLight', Vec3.ZERO);
        expect(writer.write).toHaveBeenCalledWith(shader, 'u_DirectionalLightCount', 0);
        expect(writer.write).toHaveBeenCalledWith(shader, 'u_PointLightCount', 0);
        expect(writer.write).toHaveBeenCalledWith(shader, 'u_SpotLightCount', 0);
        expect(writer.write).toHaveBeenCalledWith(shader, 'u_LocalLightCount', 0);
    });
});
