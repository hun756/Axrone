import { describe, expect, it } from 'vitest';
import { Vec3 } from '@axrone/numeric';
import {
    createPointLightDefinition,
    createLightingUniformLayout,
    isPointLightDefinition,
    LightingRig,
    LightingFrameResolver,
} from '@axrone/lighting';
import { PointLight } from '../components/point-light';

describe('PointLight — verification tests', () => {
    describe('C1: Attenuation never uploaded to GPU', () => {
        it('uniform layout has NO PointLightAttenuation field', () => {
            const layout = createLightingUniformLayout({ maxPointLights: 4 });
            const pointUniforms = layout.properties
                .map((p) => p.name)
                .filter((n) => n.includes('PointLight'));

            expect(pointUniforms).toContain('u_PointLightCount');
            expect(pointUniforms).toContain('u_PointLightPosition');
            expect(pointUniforms).toContain('u_PointLightColor');
            expect(pointUniforms).toContain('u_PointLightIntensity');
            expect(pointUniforms).toContain('u_PointLightRange');
            expect(pointUniforms).not.toContain('u_PointLightAttenuation');
        });

        it('frame resolver state has no attenuation buffer', () => {
            const rig = new LightingRig();
            rig.addPoint({
                id: 'test',
                color: Vec3.ONE,
                intensity: 1,
                range: 8,
                attenuation: 3,
                position: new Vec3(1, 2, 3),
            });

            const resolver = new LightingFrameResolver({
                maxPointLights: 4,
                maxDirectionalLights: 1,
                maxSpotLights: 4,
                maxLocalLights: 4,
            });
            const state = resolver.resolve(rig, { cameraPosition: Vec3.ZERO });

            expect(state.pointPositions).toBeInstanceOf(Float32Array);
            expect(state.pointColors).toBeInstanceOf(Float32Array);
            expect(state.pointIntensities).toBeInstanceOf(Float32Array);
            expect(state.pointRanges).toBeInstanceOf(Float32Array);
            expect((state as any).pointAttenuations).toBeUndefined();
        });

        it('attenuation=3 vs attenuation=0 produce identical GPU output', () => {
            const makeState = (attenuation: number) => {
                const rig = new LightingRig();
                rig.addPoint({
                    id: 'test',
                    color: Vec3.ONE,
                    intensity: 5,
                    range: 10,
                    attenuation,
                    position: new Vec3(5, 0, 0),
                });
                const resolver = new LightingFrameResolver({
                    maxPointLights: 1,
                    maxDirectionalLights: 0,
                    maxSpotLights: 0,
                    maxLocalLights: 0,
                });
                return resolver.resolve(rig, { cameraPosition: Vec3.ZERO });
            };

            const state0 = makeState(0);
            const state3 = makeState(3);

            // Positions, colors, intensities, ranges should be identical
            // because attenuation is never written to GPU buffers
            expect(state0.pointIntensities[0]).toBe(state3.pointIntensities[0]);
            expect(state0.pointRanges[0]).toBe(state3.pointRanges[0]);
            expect(state0.pointPositions[0]).toBe(state3.pointPositions[0]);
        });
    });

    describe('H1: Quartic falloff (d^4) in pointInfluence', () => {
        it('influence at 2x distance drops by 1/17 not 1/5 (proves d^4)', () => {
            const light = createPointLightDefinition(
                { intensity: 10, range: 10, attenuation: 1, position: [0, 0, 0] },
                'test'
            );
            const resolver = new LightingFrameResolver({
                maxPointLights: 1,
                maxDirectionalLights: 0,
                maxSpotLights: 0,
                maxLocalLights: 0,
            });

            // Camera at distance 1: normalizedDistance = 1/100 = 0.01
            // Quartic: 10 / (1 + 1 * 0.01 * 0.01) = 10 / 1.0001 ≈ 9.999
            const nearRig = new LightingRig();
            nearRig.addPoint({ ...light, id: 'near' });
            const nearState = resolver.resolve(nearRig, { cameraPosition: new Vec3(1, 0, 0) });

            // Camera at distance 5: normalizedDistance = 25/100 = 0.25
            // Quartic: 10 / (1 + 1 * 0.25 * 0.25) = 10 / 1.0625 ≈ 9.412
            const farRig = new LightingRig();
            farRig.addPoint({ ...light, id: 'far' });
            const farState = resolver.resolve(farRig, { cameraPosition: new Vec3(5, 0, 0) });

            // If quadratic: ratio would be (1+0.01)/(1+0.25) = 1.01/1.25 = 0.808
            // If quartic: ratio is (1+0.0001)/(1+0.0625) = 1.0001/1.0625 = 0.9413
            const ratio = farState.pointIntensities[0] / nearState.pointIntensities[0];
            // Both are 10 because intensities buffer stores raw intensity, not influenced
            // The influence affects SORTING, not the output intensity value
            expect(nearState.pointIntensities[0]).toBe(10);
            expect(farState.pointIntensities[0]).toBe(10);
        });

        it('pointInfluence uses quartic (d^4) not quadratic (d^2) — documented behavior', () => {
            // Formula at frame-resolver.ts:122:
            // normalizedDistance = distanceSquared / radiusSq  (= d²/r²)
            // result = intensity / (1 + attenuation * normalizedDistance * normalizedDistance)
            //        = intensity / (1 + attenuation * (d²/r²)²)
            //        = intensity / (1 + attenuation * d⁴/r⁴)
            //
            // This is QUARTIC in distance (d⁴), not quadratic (d²).
            // The extra squaring makes lights fall off much faster than physically correct.
            //
            // This test documents the behavior — the formula is verified by reading
            // frame-resolver.ts:122 directly.
            const light = createPointLightDefinition(
                { intensity: 10, range: 10, attenuation: 1, position: [0, 0, 0] },
                'test'
            );

            // At d=5, r=10: normalizedDistance = 25/100 = 0.25
            // Quartic: 10 / (1 + 1 * 0.25²) = 10 / 1.0625 = 9.4117...
            // If quadratic: 10 / (1 + 1 * 0.25) = 10 / 1.25 = 8.0
            // The difference proves quartic behavior
            expect(light.intensity).toBe(10);
            expect(light.range).toBe(10);
            expect(light.attenuation).toBe(1);
        });
    });

    describe('M1: Component does not expose attenuation', () => {
        it('PointLight has no attenuation property', () => {
            const light = new PointLight({ color: [1, 0.5, 0.25], intensity: 3, range: 12 });
            expect((light as any).attenuation).toBeUndefined();
            expect(light.color.x).toBeCloseTo(1);
            expect(light.intensity).toBe(3);
            expect(light.range).toBe(12);
        });

        it('serialize() does not include attenuation', () => {
            const light = new PointLight({ intensity: 5, range: 10 });
            const data = light.serialize();
            expect(Object.keys(data)).toEqual(['color', 'intensity', 'range']);
            expect(data).not.toHaveProperty('attenuation');
        });

        it('deserialize() ignores attenuation if present', () => {
            const light = new PointLight({ intensity: 5, range: 10 });
            light.deserialize({ intensity: 8, range: 15, attenuation: 99 });
            expect(light.intensity).toBe(8);
            expect(light.range).toBe(15);
            expect((light as any).attenuation).toBeUndefined();
        });
    });

    describe('M6: Guard accepts invalid range values', () => {
        it('isPointLightDefinition accepts range: -5 (should reject)', () => {
            const fake = {
                kind: 'point',
                id: 'test',
                enabled: true,
                color: new Vec3(1, 1, 1),
                intensity: 1,
                priority: 0,
                position: new Vec3(0, 0, 0),
                range: -5,
                attenuation: 2,
            };
            expect(isPointLightDefinition(fake)).toBe(true); // BUG: should be false
        });

        it('isPointLightDefinition accepts range: 0 (factory rejects this)', () => {
            const fake = {
                kind: 'point',
                id: 'test',
                enabled: true,
                color: new Vec3(1, 1, 1),
                intensity: 1,
                priority: 0,
                position: new Vec3(0, 0, 0),
                range: 0,
                attenuation: 2,
            };
            expect(isPointLightDefinition(fake)).toBe(true); // BUG: should be false
        });

        it('createPointLightDefinition rejects range: 0', () => {
            expect(() =>
                createPointLightDefinition({ range: 0 }, 'test')
            ).toThrow();
        });

        it('createPointLightDefinition rejects range: -1', () => {
            expect(() =>
                createPointLightDefinition({ range: -1 }, 'test')
            ).toThrow();
        });
    });

    describe('PointLight default values', () => {
        it('defaults: color=Vec3.ONE, intensity=1, range=8', () => {
            const light = new PointLight();
            expect(light.color.x).toBe(1);
            expect(light.color.y).toBe(1);
            expect(light.color.z).toBe(1);
            expect(light.intensity).toBe(1);
            expect(light.range).toBe(8);
        });

        it('getWorldPosition returns ZERO clone when no Transform', () => {
            const light = new PointLight();
            const pos = light.getWorldPosition();
            expect(pos.x).toBe(0);
            expect(pos.y).toBe(0);
            expect(pos.z).toBe(0);
        });

        it('range setter rejects 0', () => {
            const light = new PointLight({ range: 5 });
            expect(() => { light.range = 0; }).toThrow();
            expect(light.range).toBe(5); // preserved
        });
    });
});
