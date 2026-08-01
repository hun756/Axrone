import { describe, expect, it } from 'vitest';
import { LightKind } from '../constants';
import { LightingValidationError } from '../errors';
import {
    applyDirectionalLightPatch,
    applyLightPatch,
    applyPointLightPatch,
    applySpotLightPatch,
    createDirectionalLightDefinition,
    createLightDefinition,
    createLightingEnvironment,
    createPointLightDefinition,
    createSpotLightDefinition,
    resolveLightingCapacity,
    serializeVec3,
    updateLightingEnvironment,
} from '../validation';

describe('lighting validation', () => {
    it('creates frozen environments and updates them immutably', () => {
        const environment = createLightingEnvironment({
            ambient: [0.1, 0.2, 0.3],
            exposure: 1.5,
        });
        const updated = updateLightingEnvironment(environment, {
            gamma: 1.8,
        });

        expect(serializeVec3(environment.ambient)).toEqual([0.1, 0.2, 0.3]);
        expect(environment.exposure).toBe(1.5);
        expect(environment.gamma).toBe(2.2);
        expect(Object.isFrozen(environment)).toBe(true);
        expect(Object.isFrozen(environment.ambient)).toBe(true);
        expect(updated).not.toBe(environment);
        expect(updated.exposure).toBe(1.5);
        expect(updated.gamma).toBe(1.8);
    });

    it('resolves capacity and rejects invalid environment or capacity inputs', () => {
        expect(
            resolveLightingCapacity({
                maxPointLights: 2,
                maxSpotLights: 3,
                maxLocalLights: 99,
            })
        ).toEqual({
            maxDirectionalLights: 1,
            maxPointLights: 2,
            maxSpotLights: 3,
            maxLocalLights: 5,
        });

        expect(() => createLightingEnvironment({ exposure: -1 })).toThrow(
            LightingValidationError
        );
        expect(() => createLightingEnvironment({ gamma: 0 })).toThrow(
            LightingValidationError
        );
        expect(() => resolveLightingCapacity({ maxPointLights: 1.5 })).toThrow(
            LightingValidationError
        );
        expect(() => resolveLightingCapacity({ maxSpotLights: -1 })).toThrow(
            LightingValidationError
        );
    });

    it('normalizes directional lights and deep-clones metadata', () => {
        const metadata = {
            nested: { enabled: true },
            tags: ['a'],
        };
        const light = createDirectionalLightDefinition(
            {
                id: 'sun',
                direction: [0, -2, 0],
                ambient: [0.1, 0.2, 0.3],
                metadata,
            },
            'fallback'
        );
        const clonedMetadata = light.metadata as {
            nested: { enabled: boolean };
            tags: string[];
        };

        metadata.nested.enabled = false;
        metadata.tags.push('b');

        expect(light.kind).toBe(LightKind.Directional);
        expect(serializeVec3(light.direction)).toEqual([0, -1, 0]);
        expect(clonedMetadata.nested.enabled).toBe(true);
        expect(clonedMetadata.tags).toEqual(['a']);
        expect(Object.isFrozen(light)).toBe(true);
        expect(Object.isFrozen(clonedMetadata)).toBe(true);
        expect(Object.isFrozen(clonedMetadata.nested)).toBe(true);
        expect(() =>
            createDirectionalLightDefinition(
                {
                    direction: [0, 0, 0],
                },
                'bad-direction'
            )
        ).toThrow(LightingValidationError);
    });

    it('validates point and spot definitions with angle and cosine cones', () => {
        const point = createPointLightDefinition(
            {
                range: 5,
                attenuation: 1.5,
            },
            'point'
        );
        const spotFromAngles = createSpotLightDefinition(
            {
                direction: [0, -1, 0],
                coneMode: 'angle',
                innerConeAngle: 0.1,
                outerConeAngle: 0.3,
            },
            'spot-angle'
        );
        const spotFromCosines = createSpotLightDefinition(
            {
                direction: [0, -1, 0],
                coneMode: 'cosine',
                innerConeCosine: 0.9,
                outerConeCosine: 0.7,
            },
            'spot-cosine'
        );

        expect(point.range).toBe(5);
        expect(point.attenuation).toBe(1.5);
        expect(spotFromAngles.innerConeCosine).toBeCloseTo(Math.cos(0.1));
        expect(spotFromAngles.outerConeCosine).toBeCloseTo(Math.cos(0.3));
        expect(spotFromCosines.innerConeCosine).toBeCloseTo(0.9);
        expect(spotFromCosines.outerConeCosine).toBeCloseTo(0.7);

        expect(() => createPointLightDefinition({ range: 0 }, 'bad-point')).toThrow(
            LightingValidationError
        );
        expect(() =>
            createSpotLightDefinition(
                {
                    direction: [0, -1, 0],
                    coneMode: 'cosine',
                    innerConeCosine: 0.4,
                    outerConeCosine: 0.7,
                },
                'bad-spot-cosine'
            )
        ).toThrow(LightingValidationError);
        expect(() =>
            createSpotLightDefinition(
                {
                    direction: [0, -1, 0],
                    coneMode: 'angle',
                    innerConeAngle: 0.5,
                    outerConeAngle: 0.2,
                },
                'bad-spot-angle'
            )
        ).toThrow(LightingValidationError);
    });

    it('dispatches generic creation and patch helpers across light kinds', () => {
        const directional = createLightDefinition(
            LightKind.Directional,
            {
                direction: [1, -1, 0],
            },
            'directional'
        );
        const point = createLightDefinition(
            LightKind.Point,
            {
                position: [1, 2, 3],
                range: 4,
            },
            'point'
        );
        const spot = createLightDefinition(
            LightKind.Spot,
            {
                direction: [0, -1, 0],
                coneMode: 'cosine',
                innerConeCosine: 0.8,
                outerConeCosine: 0.6,
            },
            'spot'
        );
        const nextDirectional = applyDirectionalLightPatch(directional, {
            intensity: 5,
        });
        const nextPoint = applyPointLightPatch(point, {
            position: [3, 2, 1],
            range: 9,
        });
        const nextSpot = applySpotLightPatch(spot, {
            coneMode: 'angle',
            innerConeAngle: 0.2,
            outerConeAngle: 0.4,
        });
        const genericDirectional = applyLightPatch(directional, {
            priority: 7,
        });
        const genericPoint = applyLightPatch(point, {
            attenuation: 5,
        });
        const genericSpot = applyLightPatch(spot, {
            coneMode: 'cosine',
            innerConeCosine: 0.91,
            outerConeCosine: 0.81,
        });

        expect(nextDirectional.intensity).toBe(5);
        expect(serializeVec3(nextPoint.position)).toEqual([3, 2, 1]);
        expect(nextPoint.range).toBe(9);
        expect(nextSpot.innerConeCosine).toBeCloseTo(Math.cos(0.2));
        expect(genericDirectional.priority).toBe(7);
        expect(genericPoint.attenuation).toBe(5);
        expect(genericSpot.innerConeCosine).toBeCloseTo(0.91);
    });

    it('rejects Vec3 tuples with wrong length', () => {
        expect(() =>
            createDirectionalLightDefinition(
                {
                    direction: [0, -1],
                },
                'bad-tuple-2'
            )
        ).toThrow(LightingValidationError);

        expect(() =>
            createPointLightDefinition(
                {
                    position: [1, 2, 3, 4] as unknown as [number, number, number],
                    range: 5,
                },
                'bad-tuple-4'
            )
        ).toThrow(LightingValidationError);
    });

    it('infers spot cone mode from input fields when coneMode is undefined', () => {
        const spotFromAnglesOnly = createSpotLightDefinition(
            {
                direction: [0, -1, 0],
                innerConeAngle: 0.15,
                outerConeAngle: 0.45,
            },
            'spot-angle-infer'
        );
        expect(spotFromAnglesOnly.innerConeCosine).toBeCloseTo(Math.cos(0.15));
        expect(spotFromAnglesOnly.outerConeCosine).toBeCloseTo(Math.cos(0.45));

        const spotFromCosinesOnly = createSpotLightDefinition(
            {
                direction: [0, -1, 0],
                innerConeCosine: 0.85,
                outerConeCosine: 0.65,
            },
            'spot-cosine-infer'
        );
        expect(spotFromCosinesOnly.innerConeCosine).toBeCloseTo(0.85);
        expect(spotFromCosinesOnly.outerConeCosine).toBeCloseTo(0.65);
    });

    it('uses default cone values when no cone fields are provided', () => {
        const spotDefaults = createSpotLightDefinition(
            {
                direction: [0, -1, 0],
            },
            'spot-defaults'
        );

        expect(spotDefaults.innerConeCosine).toBeCloseTo(Math.cos(Math.PI / 8));
        expect(spotDefaults.outerConeCosine).toBeCloseTo(Math.cos(Math.PI / 4));
    });

    it('rejects gamma = 0 with exclusive minimum boundary', () => {
        expect(() => createLightingEnvironment({ gamma: 0 })).toThrow(
            LightingValidationError
        );
    });

    it('rejects NaN, Infinity, and negative values for numeric fields', () => {
        expect(() =>
            createDirectionalLightDefinition({ direction: [0, -1, 0], intensity: NaN }, 'nan-int')
        ).toThrow(LightingValidationError);
        expect(() =>
            createDirectionalLightDefinition(
                { direction: [0, -1, 0], intensity: Infinity },
                'inf-int'
            )
        ).toThrow(LightingValidationError);
        expect(() =>
            createDirectionalLightDefinition({ direction: [0, -1, 0], intensity: -1 }, 'neg-int')
        ).toThrow(LightingValidationError);
        expect(() =>
            createPointLightDefinition({ range: 5, attenuation: NaN }, 'nan-att')
        ).toThrow(LightingValidationError);
        expect(() =>
            createPointLightDefinition({ range: 5, attenuation: -1 }, 'neg-att')
        ).toThrow(LightingValidationError);
    });

    it('produces undefined metadata when input is null or undefined', () => {
        const withNull = createDirectionalLightDefinition(
            { direction: [0, -1, 0], metadata: null },
            'null-meta'
        );
        expect(withNull.metadata).toBeUndefined();

        const withUndefined = createDirectionalLightDefinition(
            { direction: [0, -1, 0], metadata: undefined },
            'undef-meta'
        );
        expect(withUndefined.metadata).toBeUndefined();

        const withNoField = createDirectionalLightDefinition(
            { direction: [0, -1, 0] },
            'no-meta'
        );
        expect(withNoField.metadata).toBeUndefined();
    });

    it('applySpotLightPatch preserves existing cones when no cone fields are given', () => {
        const spot = createSpotLightDefinition(
            {
                direction: [0, -1, 0],
                coneMode: 'cosine',
                innerConeCosine: 0.9,
                outerConeCosine: 0.7,
            },
            'spot-preserve'
        );

        const patched = applySpotLightPatch(spot, { intensity: 5 });

        expect(patched.innerConeCosine).toBeCloseTo(0.9);
        expect(patched.outerConeCosine).toBeCloseTo(0.7);
        expect(patched.intensity).toBe(5);
    });

    it('applySpotLightPatch handles angle mode with partial angle inputs', () => {
        const spot = createSpotLightDefinition(
            {
                direction: [0, -1, 0],
                coneMode: 'angle',
                innerConeAngle: 0.2,
                outerConeAngle: 0.5,
            },
            'spot-partial'
        );

        const patchedInnerOnly = applySpotLightPatch(spot, {
            coneMode: 'angle',
            innerConeAngle: 0.1,
        });
        expect(patchedInnerOnly.innerConeCosine).toBeCloseTo(Math.cos(0.1));
        expect(patchedInnerOnly.outerConeCosine).toBeCloseTo(Math.cos(Math.PI / 4));

        const patchedOuterOnly = applySpotLightPatch(spot, {
            coneMode: 'angle',
            outerConeAngle: 0.6,
        });
        expect(patchedOuterOnly.innerConeCosine).toBeCloseTo(Math.cos(Math.PI / 8));
        expect(patchedOuterOnly.outerConeCosine).toBeCloseTo(Math.cos(0.6));
    });

    it('rejects non-finite values in Vec3 tuple members', () => {
        expect(() =>
            createDirectionalLightDefinition(
                { direction: [NaN, -1, 0] },
                'nan-dir'
            )
        ).toThrow(LightingValidationError);
        expect(() =>
            createPointLightDefinition(
                { position: [Infinity, 0, 0], range: 5 },
                'inf-pos'
            )
        ).toThrow(LightingValidationError);
    });
});