import { Vec3 } from '@axrone/numeric';
import { describe, expect, it } from 'vitest';
import { LightKind, LightSortMode } from '../constants';
import {
    isDirectionalLightDefinition,
    isLightDefinition,
    isLightKind,
    isLightingDocument,
    isLightingMetadata,
    isLightSortMode,
    isPointLightDefinition,
    isReadonlyTuple3,
    isSerializedLight,
    isSpotLightDefinition,
} from '../guards';
import {
    createDirectionalLightDefinition,
    createPointLightDefinition,
    createSpotLightDefinition,
} from '../validation';

describe('lighting guards — negative paths', () => {
    describe('isReadonlyTuple3', () => {
        it('rejects non-array inputs', () => {
            expect(isReadonlyTuple3(null)).toBe(false);
            expect(isReadonlyTuple3(undefined)).toBe(false);
            expect(isReadonlyTuple3(42)).toBe(false);
            expect(isReadonlyTuple3('abc')).toBe(false);
            expect(isReadonlyTuple3({})).toBe(false);
        });

        it('rejects arrays with wrong length', () => {
            expect(isReadonlyTuple3([])).toBe(false);
            expect(isReadonlyTuple3([1])).toBe(false);
            expect(isReadonlyTuple3([1, 2])).toBe(false);
            expect(isReadonlyTuple3([1, 2, 3, 4])).toBe(false);
        });

        it('rejects arrays with non-finite members', () => {
            expect(isReadonlyTuple3([NaN, 0, 0])).toBe(false);
            expect(isReadonlyTuple3([0, Infinity, 0])).toBe(false);
            expect(isReadonlyTuple3([0, 0, -Infinity])).toBe(false);
            expect(isReadonlyTuple3(['a', 'b', 'c'])).toBe(false);
            expect(isReadonlyTuple3([1, null, 3])).toBe(false);
        });
    });

    describe('isLightingMetadata', () => {
        it('rejects non-object inputs', () => {
            expect(isLightingMetadata(null)).toBe(false);
            expect(isLightingMetadata(undefined)).toBe(false);
            expect(isLightingMetadata(42)).toBe(false);
            expect(isLightingMetadata('string')).toBe(false);
            expect(isLightingMetadata(true)).toBe(false);
        });

        it('accepts arrays as object records with JSON-safe values', () => {
            expect(isLightingMetadata([1, 2, 3])).toBe(true);
        });

        it('rejects arrays with non-JSON values', () => {
            expect(isLightingMetadata([() => true])).toBe(false);
        });

        it('rejects objects with function values', () => {
            expect(isLightingMetadata({ fn: () => 42 })).toBe(false);
        });

        it('rejects objects with deeply nested invalid values', () => {
            expect(isLightingMetadata({ nested: { fn: Symbol('x') } })).toBe(false);
            expect(isLightingMetadata({ nested: { deep: { bad: undefined } } })).toBe(false);
        });

        it('accepts empty objects', () => {
            expect(isLightingMetadata({})).toBe(true);
        });

        it('accepts objects with only JSON-safe values', () => {
            expect(isLightingMetadata({ a: 1, b: 'str', c: true, d: null, e: [1, 'x', null] })).toBe(true);
        });
    });

    describe('isLightKind', () => {
        it('rejects non-string values', () => {
            expect(isLightKind(42)).toBe(false);
            expect(isLightKind(null)).toBe(false);
            expect(isLightKind(undefined)).toBe(false);
        });
    });

    describe('isLightSortMode', () => {
        it('rejects non-string values', () => {
            expect(isLightSortMode(42)).toBe(false);
            expect(isLightSortMode(null)).toBe(false);
            expect(isLightSortMode(undefined)).toBe(false);
        });
    });

    describe('isDirectionalLightDefinition — negative paths', () => {
        const base = createDirectionalLightDefinition({ direction: [0, -1, 0] }, 'dir');

        it('rejects non-objects', () => {
            expect(isDirectionalLightDefinition(null)).toBe(false);
            expect(isDirectionalLightDefinition(undefined)).toBe(false);
            expect(isDirectionalLightDefinition(42)).toBe(false);
        });

        it('rejects wrong kind', () => {
            expect(isDirectionalLightDefinition({ ...base, kind: LightKind.Point })).toBe(false);
        });

        it('rejects non-string id', () => {
            expect(isDirectionalLightDefinition({ ...base, id: 42 })).toBe(false);
        });

        it('rejects non-boolean enabled', () => {
            expect(isDirectionalLightDefinition({ ...base, enabled: 1 })).toBe(false);
        });

        it('rejects non-Vec3 color', () => {
            expect(isDirectionalLightDefinition({ ...base, color: [1, 0, 0] })).toBe(false);
        });

        it('rejects NaN intensity', () => {
            expect(isDirectionalLightDefinition({ ...base, intensity: NaN })).toBe(false);
        });

        it('rejects NaN priority', () => {
            expect(isDirectionalLightDefinition({ ...base, priority: NaN })).toBe(false);
        });

        it('rejects non-Vec3 direction', () => {
            expect(isDirectionalLightDefinition({ ...base, direction: [0, 0, -1] })).toBe(false);
        });

        it('rejects non-Vec3 ambient', () => {
            expect(isDirectionalLightDefinition({ ...base, ambient: [0.1, 0.2, 0.3] })).toBe(false);
        });

        it('rejects invalid metadata', () => {
            expect(isDirectionalLightDefinition({ ...base, metadata: { fn: () => true } })).toBe(false);
        });

        it('accepts a valid definition', () => {
            expect(isDirectionalLightDefinition(base)).toBe(true);
        });
    });

    describe('isPointLightDefinition — negative paths', () => {
        const base = createPointLightDefinition({ position: [1, 2, 3], range: 5 }, 'pt');

        it('rejects non-objects', () => {
            expect(isPointLightDefinition(null)).toBe(false);
            expect(isPointLightDefinition(undefined)).toBe(false);
        });

        it('rejects wrong kind', () => {
            expect(isPointLightDefinition({ ...base, kind: LightKind.Directional })).toBe(false);
        });

        it('rejects non-string id', () => {
            expect(isPointLightDefinition({ ...base, id: 42 })).toBe(false);
        });

        it('rejects non-boolean enabled', () => {
            expect(isPointLightDefinition({ ...base, enabled: 'yes' })).toBe(false);
        });

        it('rejects non-Vec3 color', () => {
            expect(isPointLightDefinition({ ...base, color: [1, 1, 1] })).toBe(false);
        });

        it('rejects NaN intensity', () => {
            expect(isPointLightDefinition({ ...base, intensity: NaN })).toBe(false);
        });

        it('rejects NaN priority', () => {
            expect(isPointLightDefinition({ ...base, priority: NaN })).toBe(false);
        });

        it('rejects non-Vec3 position', () => {
            expect(isPointLightDefinition({ ...base, position: [0, 0, 0] })).toBe(false);
        });

        it('rejects NaN range', () => {
            expect(isPointLightDefinition({ ...base, range: NaN })).toBe(false);
        });

        it('rejects NaN attenuation', () => {
            expect(isPointLightDefinition({ ...base, attenuation: NaN })).toBe(false);
        });

        it('rejects invalid metadata', () => {
            expect(isPointLightDefinition({ ...base, metadata: { fn: () => true } })).toBe(false);
        });

        it('accepts a valid definition', () => {
            expect(isPointLightDefinition(base)).toBe(true);
        });
    });

    describe('isSpotLightDefinition — negative paths', () => {
        const base = createSpotLightDefinition(
            { direction: [0, -1, 0], coneMode: 'cosine', innerConeCosine: 0.9, outerConeCosine: 0.7 },
            'sp'
        );

        it('rejects non-objects', () => {
            expect(isSpotLightDefinition(null)).toBe(false);
            expect(isSpotLightDefinition(undefined)).toBe(false);
        });

        it('rejects wrong kind', () => {
            expect(isSpotLightDefinition({ ...base, kind: LightKind.Point })).toBe(false);
        });

        it('rejects non-string id', () => {
            expect(isSpotLightDefinition({ ...base, id: 42 })).toBe(false);
        });

        it('rejects non-boolean enabled', () => {
            expect(isSpotLightDefinition({ ...base, enabled: 0 })).toBe(false);
        });

        it('rejects non-Vec3 color', () => {
            expect(isSpotLightDefinition({ ...base, color: [1, 0, 0] })).toBe(false);
        });

        it('rejects NaN intensity', () => {
            expect(isSpotLightDefinition({ ...base, intensity: NaN })).toBe(false);
        });

        it('rejects NaN priority', () => {
            expect(isSpotLightDefinition({ ...base, priority: NaN })).toBe(false);
        });

        it('rejects non-Vec3 position', () => {
            expect(isSpotLightDefinition({ ...base, position: [0, 0, 0] })).toBe(false);
        });

        it('rejects non-Vec3 direction', () => {
            expect(isSpotLightDefinition({ ...base, direction: [0, -1, 0] })).toBe(false);
        });

        it('rejects NaN range', () => {
            expect(isSpotLightDefinition({ ...base, range: NaN })).toBe(false);
        });

        it('rejects NaN attenuation', () => {
            expect(isSpotLightDefinition({ ...base, attenuation: NaN })).toBe(false);
        });

        it('rejects NaN innerConeCosine', () => {
            expect(isSpotLightDefinition({ ...base, innerConeCosine: NaN })).toBe(false);
        });

        it('rejects NaN outerConeCosine', () => {
            expect(isSpotLightDefinition({ ...base, outerConeCosine: NaN })).toBe(false);
        });

        it('rejects invalid metadata', () => {
            expect(isSpotLightDefinition({ ...base, metadata: { fn: () => true } })).toBe(false);
        });

        it('accepts a valid definition', () => {
            expect(isSpotLightDefinition(base)).toBe(true);
        });
    });

    describe('isLightDefinition — negative paths', () => {
        it('rejects null and undefined', () => {
            expect(isLightDefinition(null)).toBe(false);
            expect(isLightDefinition(undefined)).toBe(false);
        });

        it('rejects primitives', () => {
            expect(isLightDefinition(42)).toBe(false);
            expect(isLightDefinition('string')).toBe(false);
            expect(isLightDefinition(true)).toBe(false);
        });

        it('rejects empty objects', () => {
            expect(isLightDefinition({})).toBe(false);
        });

        it('rejects unknown kind', () => {
            expect(isLightDefinition({ kind: 'area' })).toBe(false);
        });

        it('rejects incomplete definitions', () => {
            expect(isLightDefinition({ kind: LightKind.Point, id: 'x' })).toBe(false);
        });
    });

    describe('isSerializedLight — negative paths', () => {
        it('rejects non-objects', () => {
            expect(isSerializedLight(null)).toBe(false);
            expect(isSerializedLight(undefined)).toBe(false);
            expect(isSerializedLight(42)).toBe(false);
            expect(isSerializedLight('string')).toBe(false);
        });

        it('rejects missing kind', () => {
            expect(isSerializedLight({ id: 'x' })).toBe(false);
        });

        it('rejects unknown kind', () => {
            expect(isSerializedLight({ kind: 'area' })).toBe(false);
        });

        it('rejects non-string id', () => {
            expect(isSerializedLight({ kind: LightKind.Point, id: 42 })).toBe(false);
        });

        it('rejects invalid color tuple', () => {
            expect(isSerializedLight({ kind: LightKind.Point, color: [1, 2] })).toBe(false);
            expect(isSerializedLight({ kind: LightKind.Point, color: 'red' })).toBe(false);
        });

        it('rejects invalid intensity', () => {
            expect(isSerializedLight({ kind: LightKind.Point, intensity: 'high' })).toBe(false);
            expect(isSerializedLight({ kind: LightKind.Point, intensity: NaN })).toBe(false);
        });

        it('rejects invalid priority', () => {
            expect(isSerializedLight({ kind: LightKind.Point, priority: 'top' })).toBe(false);
        });

        it('rejects invalid metadata', () => {
            expect(
                isSerializedLight({
                    kind: LightKind.Point,
                    metadata: { fn: () => true },
                })
            ).toBe(false);
        });

        it('rejects directional with invalid direction', () => {
            expect(
                isSerializedLight({
                    kind: LightKind.Directional,
                    direction: [1, 2],
                })
            ).toBe(false);
        });

        it('rejects directional with invalid ambient', () => {
            expect(
                isSerializedLight({
                    kind: LightKind.Directional,
                    ambient: 'bright',
                })
            ).toBe(false);
        });

        it('rejects point with invalid position', () => {
            expect(
                isSerializedLight({
                    kind: LightKind.Point,
                    position: [1],
                })
            ).toBe(false);
        });

        it('rejects point with invalid range', () => {
            expect(
                isSerializedLight({
                    kind: LightKind.Point,
                    range: 'far',
                })
            ).toBe(false);
        });

        it('rejects point with invalid attenuation', () => {
            expect(
                isSerializedLight({
                    kind: LightKind.Point,
                    attenuation: NaN,
                })
            ).toBe(false);
        });

        it('rejects spot with invalid position', () => {
            expect(
                isSerializedLight({
                    kind: LightKind.Spot,
                    position: 'here',
                })
            ).toBe(false);
        });

        it('rejects spot with invalid direction', () => {
            expect(
                isSerializedLight({
                    kind: LightKind.Spot,
                    direction: [1, 2],
                })
            ).toBe(false);
        });

        it('rejects spot with invalid range', () => {
            expect(
                isSerializedLight({
                    kind: LightKind.Spot,
                    range: Infinity,
                })
            ).toBe(false);
        });

        it('rejects spot with invalid attenuation', () => {
            expect(
                isSerializedLight({
                    kind: LightKind.Spot,
                    attenuation: 'slow',
                })
            ).toBe(false);
        });

        it('rejects spot with invalid innerConeCosine', () => {
            expect(
                isSerializedLight({
                    kind: LightKind.Spot,
                    innerConeCosine: 'wide',
                })
            ).toBe(false);
        });

        it('rejects spot with invalid outerConeCosine', () => {
            expect(
                isSerializedLight({
                    kind: LightKind.Spot,
                    outerConeCosine: NaN,
                })
            ).toBe(false);
        });

        it('accepts valid serialized lights', () => {
            expect(isSerializedLight({ kind: LightKind.Directional })).toBe(true);
            expect(isSerializedLight({ kind: LightKind.Point, id: 'p', range: 4 })).toBe(true);
            expect(
                isSerializedLight({
                    kind: LightKind.Spot,
                    innerConeCosine: 0.9,
                    outerConeCosine: 0.7,
                })
            ).toBe(true);
        });
    });

    describe('isLightingDocument — negative paths', () => {
        it('rejects non-objects', () => {
            expect(isLightingDocument(null)).toBe(false);
            expect(isLightingDocument(undefined)).toBe(false);
            expect(isLightingDocument(42)).toBe(false);
            expect(isLightingDocument('string')).toBe(false);
        });

        it('rejects non-finite version', () => {
            expect(isLightingDocument({ version: NaN })).toBe(false);
            expect(isLightingDocument({ version: Infinity })).toBe(false);
            expect(isLightingDocument({ version: '1' })).toBe(false);
        });

        it('rejects non-string rigId', () => {
            expect(isLightingDocument({ rigId: 42 })).toBe(false);
            expect(isLightingDocument({ rigId: true })).toBe(false);
        });

        it('rejects non-object environment', () => {
            expect(isLightingDocument({ environment: 'dark' })).toBe(false);
            expect(isLightingDocument({ environment: 42 })).toBe(false);
            expect(isLightingDocument({ environment: null })).toBe(false);
        });

        it('rejects invalid environment sub-fields', () => {
            expect(isLightingDocument({ environment: { ambient: [1, 2] } })).toBe(false);
            expect(isLightingDocument({ environment: { sky: 'blue' } })).toBe(false);
            expect(isLightingDocument({ environment: { ground: null } })).toBe(false);
            expect(isLightingDocument({ environment: { exposure: 'high' } })).toBe(false);
            expect(isLightingDocument({ environment: { gamma: NaN } })).toBe(false);
        });

        it('rejects non-array lights', () => {
            expect(isLightingDocument({ lights: 'many' })).toBe(false);
            expect(isLightingDocument({ lights: 42 })).toBe(false);
            expect(isLightingDocument({ lights: {} })).toBe(false);
        });

        it('rejects lights array with invalid entries', () => {
            expect(isLightingDocument({ lights: [null] })).toBe(false);
            expect(isLightingDocument({ lights: [{ kind: 'area' }] })).toBe(false);
        });

        it('accepts valid documents', () => {
            expect(isLightingDocument({})).toBe(true);
            expect(isLightingDocument({ version: 1 })).toBe(true);
            expect(isLightingDocument({ lights: [] })).toBe(true);
            expect(
                isLightingDocument({
                    version: 1,
                    rigId: 'rig',
                    environment: { ambient: [0.1, 0.2, 0.3] },
                    lights: [{ kind: LightKind.Point, id: 'p' }],
                })
            ).toBe(true);
        });
    });
});
