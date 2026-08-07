import { describe, expect, it } from 'vitest';
import {
    DEFAULT_SCENE_ATTRIBUTE_NAMES,
    SCENE_ATTRIBUTE_LOCATIONS,
} from '../scene-vertex-layout';

describe('scene vertex layout', () => {
    describe('DEFAULT_SCENE_ATTRIBUTE_NAMES', () => {
        it('maps position semantic to a_Position', () => {
            expect(DEFAULT_SCENE_ATTRIBUTE_NAMES.position).toBe('a_Position');
        });

        it('maps normal semantic to a_Normal', () => {
            expect(DEFAULT_SCENE_ATTRIBUTE_NAMES.normal).toBe('a_Normal');
        });

        it('maps uv0 semantic to a_UV0', () => {
            expect(DEFAULT_SCENE_ATTRIBUTE_NAMES.uv0).toBe('a_UV0');
        });

        it('maps uv1 semantic to a_UV1', () => {
            expect(DEFAULT_SCENE_ATTRIBUTE_NAMES.uv1).toBe('a_UV1');
        });

        it('maps tangent semantic to a_Tangent', () => {
            expect(DEFAULT_SCENE_ATTRIBUTE_NAMES.tangent).toBe('a_Tangent');
        });

        it('maps color0 semantic to a_Color0', () => {
            expect(DEFAULT_SCENE_ATTRIBUTE_NAMES.color0).toBe('a_Color0');
        });

        it('maps joints0 semantic to a_Joints0', () => {
            expect(DEFAULT_SCENE_ATTRIBUTE_NAMES.joints0).toBe('a_Joints0');
        });

        it('maps weights0 semantic to a_Weights0', () => {
            expect(DEFAULT_SCENE_ATTRIBUTE_NAMES.weights0).toBe('a_Weights0');
        });

        it('is frozen', () => {
            expect(Object.isFrozen(DEFAULT_SCENE_ATTRIBUTE_NAMES)).toBe(true);
        });

        it('has exactly 8 semantic entries', () => {
            expect(Object.keys(DEFAULT_SCENE_ATTRIBUTE_NAMES)).toHaveLength(8);
        });
    });

    describe('SCENE_ATTRIBUTE_LOCATIONS', () => {
        it('assigns position to location 0', () => {
            expect(SCENE_ATTRIBUTE_LOCATIONS.position).toBe(0);
        });

        it('assigns normal to location 1', () => {
            expect(SCENE_ATTRIBUTE_LOCATIONS.normal).toBe(1);
        });

        it('assigns uv0 to location 2', () => {
            expect(SCENE_ATTRIBUTE_LOCATIONS.uv0).toBe(2);
        });

        it('assigns color0 to location 3', () => {
            expect(SCENE_ATTRIBUTE_LOCATIONS.color0).toBe(3);
        });

        it('assigns tangent to location 4', () => {
            expect(SCENE_ATTRIBUTE_LOCATIONS.tangent).toBe(4);
        });

        it('assigns uv1 to location 5', () => {
            expect(SCENE_ATTRIBUTE_LOCATIONS.uv1).toBe(5);
        });

        it('assigns joints0 to location 9', () => {
            expect(SCENE_ATTRIBUTE_LOCATIONS.joints0).toBe(9);
        });

        it('assigns weights0 to location 10', () => {
            expect(SCENE_ATTRIBUTE_LOCATIONS.weights0).toBe(10);
        });

        it('has no duplicate locations', () => {
            const locations = Object.values(SCENE_ATTRIBUTE_LOCATIONS);
            const unique = new Set(locations);
            expect(unique.size).toBe(locations.length);
        });

        it('is frozen', () => {
            expect(Object.isFrozen(SCENE_ATTRIBUTE_LOCATIONS)).toBe(true);
        });
    });
});
