import { describe, expect, it } from 'vitest';
import { LightSortMode } from '../constants';
import { LightingFrameResolver } from '../frame-resolver';
import { LightingRig } from '../rig';
import {
    createLightingUniformLayout,
    createLightingUniformValueMap,
} from '../uniform-layout';
import { DEFAULT_LIGHTING_CAPACITY } from '../validation';

describe('lighting uniform layout', () => {
    it('exposes per-kind property metadata for the modern lighting contract', () => {
        const layout = createLightingUniformLayout({
            maxDirectionalLights: 1,
            maxPointLights: 3,
            maxSpotLights: 2,
            maxLocalLights: 4,
        });

        expect(layout.names.directionalLightCount).toBe('u_DirectionalLightCount');
        expect(layout.properties.find((property) => property.name === 'u_DirectionalLightDirection')?.arrayLength).toBe(1);
        expect(layout.properties.find((property) => property.name === 'u_PointLightPosition')?.arrayLength).toBe(3);
        expect(layout.properties.find((property) => property.name === 'u_SpotLightInnerConeCosine')?.arrayLength).toBe(2);
        expect(layout.properties.find((property) => property.name === 'u_LocalLightKind')?.type).toBe('int');
    });

    it('uses default capacity when called with no arguments', () => {
        const layout = createLightingUniformLayout();

        expect(layout.capacity).toEqual(DEFAULT_LIGHTING_CAPACITY);
        expect(layout.defines.AXRONE_LIGHTING_MAX_DIRECTIONAL_LIGHTS).toBe(
            String(DEFAULT_LIGHTING_CAPACITY.maxDirectionalLights)
        );
        expect(layout.defines.AXRONE_LIGHTING_MAX_POINT_LIGHTS).toBe(
            String(DEFAULT_LIGHTING_CAPACITY.maxPointLights)
        );
        expect(layout.defines.AXRONE_LIGHTING_MAX_SPOT_LIGHTS).toBe(
            String(DEFAULT_LIGHTING_CAPACITY.maxSpotLights)
        );
        expect(layout.defines.AXRONE_LIGHTING_MAX_LOCAL_LIGHTS).toBe(
            String(DEFAULT_LIGHTING_CAPACITY.maxLocalLights)
        );
    });

    it('produces correct property types for all uniform fields', () => {
        const layout = createLightingUniformLayout({
            maxDirectionalLights: 2,
            maxPointLights: 3,
            maxSpotLights: 4,
            maxLocalLights: 5,
        });

        const vec3Fields = layout.properties.filter((p) => p.type === 'vec3');
        const floatFields = layout.properties.filter((p) => p.type === 'float');
        const intFields = layout.properties.filter((p) => p.type === 'int');

        expect(vec3Fields.map((p) => p.name)).toEqual(
            expect.arrayContaining([
                'u_AmbientLight',
                'u_SkyLight',
                'u_GroundLight',
                'u_DirectionalLightDirection',
                'u_DirectionalLightColor',
                'u_DirectionalLightAmbientColor',
                'u_PointLightPosition',
                'u_PointLightColor',
                'u_SpotLightPosition',
                'u_SpotLightDirection',
                'u_SpotLightColor',
                'u_LocalLightPosition',
                'u_LocalLightDirection',
                'u_LocalLightColor',
            ])
        );

        expect(intFields.map((p) => p.name)).toEqual(
            expect.arrayContaining([
                'u_DirectionalLightCount',
                'u_PointLightCount',
                'u_SpotLightCount',
                'u_LocalLightCount',
                'u_LocalLightKind',
            ])
        );

        expect(floatFields.length).toBeGreaterThan(0);
        expect(layout.properties.every((p) => p.scope === 'frame')).toBe(true);
    });

    it('populates all 31 uniform value map fields from a resolved state', () => {
        const rig = new LightingRig({
            environment: {
                ambient: [0.1, 0.2, 0.3],
                sky: [0.4, 0.5, 0.6],
                ground: [0.05, 0.05, 0.05],
                exposure: 1.5,
                gamma: 2.0,
            },
        });

        rig.addDirectional({
            id: 'sun',
            direction: [0, -1, 0],
            color: [1, 0.9, 0.8],
            ambient: [0.1, 0.1, 0.08],
            intensity: 3,
        });
        rig.addPoint({
            id: 'bulb',
            position: [1, 2, 3],
            color: [0, 1, 0],
            range: 7,
            intensity: 2,
        });
        rig.addSpot({
            id: 'lamp',
            position: [4, 5, 6],
            direction: [0, 0, -1],
            color: [0, 0, 1],
            range: 9,
            intensity: 4,
            coneMode: 'cosine',
            innerConeCosine: 0.85,
            outerConeCosine: 0.65,
        });

        const resolver = new LightingFrameResolver({
            capacity: {
                maxDirectionalLights: 1,
                maxPointLights: 1,
                maxSpotLights: 1,
                maxLocalLights: 2,
            },
            sortMode: LightSortMode.None,
        });

        const state = resolver.resolve(rig);
        const uniforms = createLightingUniformValueMap(state);

        expect(uniforms.u_AmbientLight.x).toBeCloseTo(0.1);
        expect(uniforms.u_SkyLight.y).toBeCloseTo(0.5);
        expect(uniforms.u_GroundLight.z).toBeCloseTo(0.05);
        expect(uniforms.u_Exposure).toBe(1.5);
        expect(uniforms.u_Gamma).toBe(2.0);
        expect(uniforms.u_DirectionalLightCount).toBe(1);
        expect(uniforms.u_DirectionalLightDirection[0]).toBeCloseTo(0);
        expect(uniforms.u_DirectionalLightDirection[1]).toBeCloseTo(-1);
        expect(uniforms.u_DirectionalLightDirection[2]).toBeCloseTo(0);
        expect(uniforms.u_DirectionalLightColor[0]).toBeCloseTo(1);
        expect(uniforms.u_DirectionalLightColor[1]).toBeCloseTo(0.9);
        expect(uniforms.u_DirectionalLightColor[2]).toBeCloseTo(0.8);
        expect(uniforms.u_DirectionalLightAmbientColor[0]).toBeCloseTo(0.1);
        expect(uniforms.u_DirectionalLightAmbientColor[1]).toBeCloseTo(0.1);
        expect(uniforms.u_DirectionalLightAmbientColor[2]).toBeCloseTo(0.08);
        expect(Array.from(uniforms.u_DirectionalLightIntensity)).toEqual([3]);
        expect(uniforms.u_PointLightCount).toBe(1);
        expect(uniforms.u_PointLightPosition[0]).toBeCloseTo(1);
        expect(uniforms.u_PointLightPosition[1]).toBeCloseTo(2);
        expect(uniforms.u_PointLightPosition[2]).toBeCloseTo(3);
        expect(uniforms.u_PointLightColor[0]).toBeCloseTo(0);
        expect(uniforms.u_PointLightColor[1]).toBeCloseTo(1);
        expect(uniforms.u_PointLightColor[2]).toBeCloseTo(0);
        expect(Array.from(uniforms.u_PointLightIntensity)).toEqual([2]);
        expect(Array.from(uniforms.u_PointLightRange)).toEqual([7]);
        expect(uniforms.u_SpotLightCount).toBe(1);
        expect(uniforms.u_SpotLightPosition[0]).toBeCloseTo(4);
        expect(uniforms.u_SpotLightPosition[1]).toBeCloseTo(5);
        expect(uniforms.u_SpotLightPosition[2]).toBeCloseTo(6);
        expect(uniforms.u_SpotLightDirection[0]).toBeCloseTo(0);
        expect(uniforms.u_SpotLightDirection[1]).toBeCloseTo(0);
        expect(uniforms.u_SpotLightDirection[2]).toBeCloseTo(-1);
        expect(uniforms.u_SpotLightColor[0]).toBeCloseTo(0);
        expect(uniforms.u_SpotLightColor[1]).toBeCloseTo(0);
        expect(uniforms.u_SpotLightColor[2]).toBeCloseTo(1);
        expect(Array.from(uniforms.u_SpotLightIntensity)).toEqual([4]);
        expect(Array.from(uniforms.u_SpotLightRange)).toEqual([9]);
        expect(Array.from(uniforms.u_SpotLightInnerConeCosine)[0]).toBeCloseTo(0.85);
        expect(Array.from(uniforms.u_SpotLightOuterConeCosine)[0]).toBeCloseTo(0.65);
        expect(uniforms.u_LocalLightCount).toBe(2);
        expect(uniforms.u_LocalLightKind.length).toBe(2);
        expect(uniforms.u_LocalLightPosition.length).toBe(6);
        expect(uniforms.u_LocalLightDirection.length).toBe(6);
        expect(uniforms.u_LocalLightColor.length).toBe(6);
        expect(uniforms.u_LocalLightIntensity.length).toBe(2);
        expect(uniforms.u_LocalLightRange.length).toBe(2);
        expect(uniforms.u_LocalLightInnerConeCosine.length).toBe(2);
        expect(uniforms.u_LocalLightOuterConeCosine.length).toBe(2);
    });
});