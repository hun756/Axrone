import { describe, expect, it, beforeEach } from 'vitest';
import { GradientEvaluator, ColorSpace, BlendMode } from '../gradient-evaluator';
import type { IGradient } from '../interfaces';

function makeGradient(overrides: Partial<IGradient> = {}): IGradient {
    return {
        mode: 0,
        colorKeys: new Float32Array([
            0.0, 1.0, 0.0, 0.0, 1.0, // time=0, R=1 G=0 B=0 A=1
            1.0, 0.0, 0.0, 1.0, 1.0, // time=1, R=0 G=0 B=1 A=1
        ]),
        alphaKeys: new Float32Array([0.0, 1.0, 1.0, 1.0]),
        keyCount: 2,
        blendMode: 0,
        ...overrides,
    };
}

describe('GradientEvaluator', () => {
    beforeEach(() => {
        GradientEvaluator.clearCache();
        GradientEvaluator.resetStats();
    });

    describe('evaluate', () => {
        it('mode 0 (Blend) interpolates between color keys in RGB', () => {
            const gradient = makeGradient({ mode: 0 });
            const result = GradientEvaluator.evaluate(gradient, 0.0, 0, ColorSpace.RGB, false);
            expect(result.x).toBeCloseTo(1.0);
            expect(result.y).toBeCloseTo(0.0);
            expect(result.z).toBeCloseTo(0.0);
            expect(result.w).toBeCloseTo(1.0);
        });

        it('mode 0 at time=1 returns second key color', () => {
            const gradient = makeGradient({ mode: 0 });
            const result = GradientEvaluator.evaluate(gradient, 1.0, 0, ColorSpace.RGB, false);
            expect(result.x).toBeCloseTo(0.0);
            expect(result.z).toBeCloseTo(1.0);
        });

        it('mode 0 with gamma correction', () => {
            const gradient = makeGradient({ mode: 0 });
            const result = GradientEvaluator.evaluate(gradient, 0.5, 0, ColorSpace.RGB, true);
            // With gamma correction, the result should be different from linear
            expect(typeof result.x).toBe('number');
            expect(result.x).toBeGreaterThan(0);
            expect(result.x).toBeLessThan(1);
        });

        it('mode 1 (Fixed) returns closest color key', () => {
            const gradient = makeGradient({ mode: 1 });
            const result = GradientEvaluator.evaluate(gradient, 0.1, 0);
            // Closest to time=0 key: R=1 G=0 B=0 A=1
            expect(result.x).toBeCloseTo(1.0);
            expect(result.y).toBeCloseTo(0.0);
        });

        it('mode 2 (Random) returns a color key based on seed', () => {
            const gradient = makeGradient({ mode: 2 });
            const result = GradientEvaluator.evaluate(gradient, 0.5, 42);
            // Should return one of the two keys
            const isKey1 = result.x > 0.5;
            const isKey2 = result.z > 0.5;
            expect(isKey1 || isKey2).toBe(true);
        });

        it('default mode returns white', () => {
            const gradient = makeGradient({ mode: 99 });
            const result = GradientEvaluator.evaluate(gradient, 0.5, 0);
            expect(result.x).toBe(1);
            expect(result.y).toBe(1);
            expect(result.z).toBe(1);
            expect(result.w).toBe(1);
        });

        it('empty gradient returns white', () => {
            const gradient = makeGradient({ colorKeys: new Float32Array(0), keyCount: 0 });
            const result = GradientEvaluator.evaluate(gradient, 0.5, 0);
            expect(result.x).toBe(1);
            expect(result.y).toBe(1);
        });

        it('single key returns that key color', () => {
            const gradient = makeGradient({
                mode: 0,
                colorKeys: new Float32Array([0.5, 0.2, 0.4, 0.6, 0.8]),
                keyCount: 1,
            });
            const result = GradientEvaluator.evaluate(gradient, 0.5, 0, ColorSpace.RGB, false);
            expect(result.x).toBeCloseTo(0.2);
            expect(result.y).toBeCloseTo(0.4);
            expect(result.z).toBeCloseTo(0.6);
            expect(result.w).toBeCloseTo(0.8);
        });
    });

    describe('evaluateBatch', () => {
        it('uses lookup table when cached', () => {
            const gradient = makeGradient({ mode: 0 });
            GradientEvaluator.buildLookupTable(gradient, 'batch-key');

            const times = new Float32Array([0.0, 0.5, 1.0]);
            const seeds = new Uint32Array([0, 0, 0]);
            const results = {
                x: new Float32Array(3),
                y: new Float32Array(3),
                z: new Float32Array(3),
                w: new Float32Array(3),
            };

            GradientEvaluator.evaluateBatch(gradient, times, seeds, results, 3);
            // First entry should be close to red (key at time=0)
            expect(results.x[0]).toBeCloseTo(1.0, 0);
        });

        it('falls back to direct evaluation without cache', () => {
            const gradient = makeGradient({ mode: 0 });
            const times = new Float32Array([0.0, 1.0]);
            const seeds = new Uint32Array([0, 0]);
            const results = {
                x: new Float32Array(2),
                y: new Float32Array(2),
                z: new Float32Array(2),
                w: new Float32Array(2),
            };

            GradientEvaluator.evaluateBatch(gradient, times, seeds, results, 2);
            expect(results.x[0]).toBeCloseTo(1.0, 0);
        });
    });

    describe('buildLookupTable', () => {
        it('builds 1024x4 table for mode 0', () => {
            const gradient = makeGradient({ mode: 0 });
            GradientEvaluator.buildLookupTable(gradient, 'test-table');
            const stats = GradientEvaluator.getStats();
            expect(stats.memoryUsage).toBeGreaterThan(0);
        });

        it('no-op for other modes', () => {
            const gradient = makeGradient({ mode: 1 });
            const memoryBefore = GradientEvaluator.getStats().memoryUsage;
            GradientEvaluator.buildLookupTable(gradient, 'test-table');
            const stats = GradientEvaluator.getStats();
            // Mode 1 doesn't build a lookup table, so memory shouldn't increase
            expect(stats.memoryUsage).toBe(memoryBefore);
        });
    });

    describe('createGradient', () => {
        it('creates IGradient from ColorStop array', () => {
            const gradient = GradientEvaluator.createGradient([
                { time: 1.0, color: [0, 0, 1, 1] },
                { time: 0.0, color: [1, 0, 0, 1] },
            ]);
            expect(gradient.mode).toBe(0);
            expect(gradient.keyCount).toBe(2);
            // Should be sorted by time
            expect(gradient.colorKeys[0]).toBe(0.0); // first stop time
            expect(gradient.colorKeys[5]).toBe(1.0); // second stop time
        });

        it('sorts stops by time', () => {
            const gradient = GradientEvaluator.createGradient([
                { time: 0.8, color: [0, 1, 0, 1] },
                { time: 0.2, color: [1, 0, 0, 1] },
            ]);
            expect(gradient.colorKeys[0]).toBeCloseTo(0.2);
            expect(gradient.colorKeys[5]).toBeCloseTo(0.8);
        });
    });

    describe('color space interpolation', () => {
        it('RGB without gamma', () => {
            const gradient = makeGradient({ mode: 0 });
            const result = GradientEvaluator.evaluate(gradient, 0.5, 0, ColorSpace.RGB, false);
            expect(result.x).toBeCloseTo(0.5);
            expect(result.z).toBeCloseTo(0.5);
        });

        it('HSV interpolation path', () => {
            const gradient = makeGradient({ mode: 0 });
            const result = GradientEvaluator.evaluate(gradient, 0.5, 0, ColorSpace.HSV, false);
            expect(typeof result.x).toBe('number');
            expect(isNaN(result.x)).toBe(false);
        });

        it('HSL interpolation path', () => {
            const gradient = makeGradient({ mode: 0 });
            const result = GradientEvaluator.evaluate(gradient, 0.5, 0, ColorSpace.HSL, false);
            expect(typeof result.x).toBe('number');
        });

        it('LAB interpolation path', () => {
            const gradient = makeGradient({ mode: 0 });
            const result = GradientEvaluator.evaluate(gradient, 0.5, 0, ColorSpace.LAB, false);
            expect(typeof result.x).toBe('number');
        });

        it('LCH interpolation path', () => {
            const gradient = makeGradient({ mode: 0 });
            const result = GradientEvaluator.evaluate(gradient, 0.5, 0, ColorSpace.LCH, false);
            expect(typeof result.x).toBe('number');
        });
    });

    describe('clearCache / getStats / resetStats', () => {
        it('clearCache empties gradient cache', () => {
            const gradient = makeGradient({ mode: 0 });
            GradientEvaluator.buildLookupTable(gradient, 'key');
            GradientEvaluator.clearCache();
            expect(GradientEvaluator.getStats().cacheHitRatio).toBe(0);
        });

        it('resetStats zeroes stats', () => {
            const gradient = makeGradient({ mode: 0 });
            GradientEvaluator.evaluate(gradient, 0.5, 0);
            GradientEvaluator.resetStats();
            const stats = GradientEvaluator.getStats();
            expect(stats.evaluationsPerFrame).toBe(0);
            expect(stats.avgEvaluationTime).toBe(0);
        });
    });

    describe('_rgbToHsv / _hsvToRgb round-trip', () => {
        it('round-trip for pure red', () => {
            // Access private methods through the class
            const rgbToHsv = (GradientEvaluator as any)._rgbToHsv;
            const hsvToRgb = (GradientEvaluator as any)._hsvToRgb;

            const hsv = rgbToHsv(1, 0, 0);
            expect(hsv[0]).toBeCloseTo(0); // hue = 0
            expect(hsv[1]).toBeCloseTo(1); // saturation = 1
            expect(hsv[2]).toBeCloseTo(1); // value = 1

            const rgb = hsvToRgb(hsv[0], hsv[1], hsv[2]);
            expect(rgb[0]).toBeCloseTo(1);
            expect(rgb[1]).toBeCloseTo(0);
            expect(rgb[2]).toBeCloseTo(0);
        });

        it('round-trip for pure green', () => {
            const rgbToHsv = (GradientEvaluator as any)._rgbToHsv;
            const hsvToRgb = (GradientEvaluator as any)._hsvToRgb;

            const hsv = rgbToHsv(0, 1, 0);
            const rgb = hsvToRgb(hsv[0], hsv[1], hsv[2]);
            expect(rgb[0]).toBeCloseTo(0);
            expect(rgb[1]).toBeCloseTo(1);
            expect(rgb[2]).toBeCloseTo(0);
        });

        it('round-trip for pure blue', () => {
            const rgbToHsv = (GradientEvaluator as any)._rgbToHsv;
            const hsvToRgb = (GradientEvaluator as any)._hsvToRgb;

            const hsv = rgbToHsv(0, 0, 1);
            const rgb = hsvToRgb(hsv[0], hsv[1], hsv[2]);
            expect(rgb[0]).toBeCloseTo(0);
            expect(rgb[1]).toBeCloseTo(0);
            expect(rgb[2]).toBeCloseTo(1);
        });
    });
});
