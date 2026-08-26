import { Vec3 } from '@axrone/numeric';
import { describe, expect, it } from 'vitest';
import {
    DEFAULT_SCENE_FOG_STATE,
    resolveSceneFogState,
    type SceneFogState,
} from '../fog-state';

describe('SceneFogState', () => {
    describe('DEFAULT_SCENE_FOG_STATE', () => {
        it('has fog disabled by default', () => {
            expect(DEFAULT_SCENE_FOG_STATE.enabled).toBe(false);
        });

        it('has exponential mode by default', () => {
            expect(DEFAULT_SCENE_FOG_STATE.mode).toBe(1);
        });

        it('has sensible default density', () => {
            expect(DEFAULT_SCENE_FOG_STATE.density).toBeGreaterThan(0);
            expect(DEFAULT_SCENE_FOG_STATE.density).toBeLessThanOrEqual(1);
        });

        it('has a sorted fog range', () => {
            const [start, end] = DEFAULT_SCENE_FOG_STATE.range;
            expect(start).toBeLessThanOrEqual(end);
        });
    });

    describe('resolveSceneFogState', () => {
        it('returns default state when environment is null', () => {
            const result = resolveSceneFogState(null);
            expect(result.enabled).toBe(DEFAULT_SCENE_FOG_STATE.enabled);
            expect(result.mode).toBe(DEFAULT_SCENE_FOG_STATE.mode);
        });

        it('returns default state when environment is undefined', () => {
            const result = resolveSceneFogState(undefined);
            expect(result.enabled).toBe(DEFAULT_SCENE_FOG_STATE.enabled);
        });

        it('resolves fogEnabled from environment', () => {
            const result = resolveSceneFogState({ fogEnabled: true });
            expect(result.enabled).toBe(true);
        });

        it('falls back to disabled when fogEnabled is missing', () => {
            const result = resolveSceneFogState({});
            expect(result.enabled).toBe(false);
        });

        it('parses 6-digit hex fogColor', () => {
            const result = resolveSceneFogState({ fogColor: '#ff8040' });
            expect(result.color).toBeInstanceOf(Vec3);
            expect(result.color.x).toBeCloseTo(1, 1);
            expect(result.color.y).toBeCloseTo(0.502, 1);
            expect(result.color.z).toBeCloseTo(0.251, 1);
        });

        it('parses 3-digit hex fogColor', () => {
            const result = resolveSceneFogState({ fogColor: '#f80' });
            expect(result.color).toBeInstanceOf(Vec3);
            expect(result.color.x).toBeCloseTo(1, 1);
            expect(result.color.y).toBeCloseTo(0.533, 1);
            expect(result.color.z).toBeCloseTo(0, 1);
        });

        it('falls back to default color for invalid hex', () => {
            const result = resolveSceneFogState({ fogColor: 'not-a-color' });
            const defaultColor = DEFAULT_SCENE_FOG_STATE.color as Vec3;
            expect(result.color.x).toBeCloseTo(defaultColor.x, 3);
            expect(result.color.y).toBeCloseTo(defaultColor.y, 3);
        });

        it('resolves Linear fog mode', () => {
            const result = resolveSceneFogState({ fogMode: 'Linear' });
            expect(result.mode).toBe(0);
        });

        it('resolves Exponential fog mode', () => {
            const result = resolveSceneFogState({ fogMode: 'Exponential' });
            expect(result.mode).toBe(1);
        });

        it('resolves ExponentialSquared fog mode', () => {
            const result = resolveSceneFogState({ fogMode: 'ExponentialSquared' });
            expect(result.mode).toBe(2);
        });

        it('falls back to exponential for unknown mode', () => {
            const result = resolveSceneFogState({ fogMode: 'Unknown' });
            expect(result.mode).toBe(2);
        });

        it('clamps fogDensity to [0, 1]', () => {
            const low = resolveSceneFogState({ fogDensity: -0.5 });
            expect(low.density).toBe(0);

            const high = resolveSceneFogState({ fogDensity: 5 });
            expect(high.density).toBe(1);

            const valid = resolveSceneFogState({ fogDensity: 0.03 });
            expect(valid.density).toBeCloseTo(0.03);
        });

        it('resolves fogRange and sorts start/end', () => {
            const result = resolveSceneFogState({ fogRange: [300, 10] });
            expect(result.range[0]).toBe(10);
            expect(result.range[1]).toBe(300);
        });

        it('falls back to default range for invalid input', () => {
            const result = resolveSceneFogState({ fogRange: 'bad' });
            expect(result.range[0]).toBe(DEFAULT_SCENE_FOG_STATE.range[0]);
            expect(result.range[1]).toBe(DEFAULT_SCENE_FOG_STATE.range[1]);
        });

        it('produces a complete state from a full environment bag', () => {
            const result = resolveSceneFogState({
                fogEnabled: true,
                fogColor: '#8899aa',
                fogMode: 'Exponential',
                fogDensity: 0.015,
                fogRange: [0, 300],
            });
            expect(result.enabled).toBe(true);
            expect(result.mode).toBe(1);
            expect(result.density).toBeCloseTo(0.015);
            expect(result.range).toEqual([0, 300]);
            expect(result.color).toBeInstanceOf(Vec3);
        });
    });
});
