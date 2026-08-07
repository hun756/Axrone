import { describe, expect, it, beforeEach } from 'vitest';
import { CurveEvaluator } from '../curve-evaluator';
import type { ICurve } from '../interfaces';

function makeCurve(overrides: Partial<ICurve> = {}): ICurve {
    return {
        mode: 0,
        constant: 1.0,
        constantMin: 0,
        constantMax: 1,
        curveLength: 0,
        preWrapMode: 0,
        postWrapMode: 0,
        ...overrides,
    };
}

describe('CurveEvaluator', () => {
    beforeEach(() => {
        CurveEvaluator.clearCache();
    });

    describe('evaluate (static)', () => {
        it('mode 0 (Constant) returns curve.constant', () => {
            const curve = makeCurve({ mode: 0, constant: 42 });
            expect(CurveEvaluator.evaluate(curve, 0.5, 0)).toBe(42);
        });

        it('mode 1 (Curve) samples curve at time', () => {
            const data = new Float32Array([0, 5, 10]);
            const curve = makeCurve({ mode: 1, constant: 0, curve: data, curveLength: 3 });
            const result = CurveEvaluator.evaluate(curve, 0.5, 0);
            expect(result).toBeCloseTo(5); // midpoint of [0,5,10]
        });

        it('mode 1 uses lookup table when cached', () => {
            const data = new Float32Array([0, 10]);
            const curve = makeCurve({ mode: 1, constant: 0, curve: data, curveLength: 2 });
            CurveEvaluator.buildLookupTable(curve, 'test-key');

            const result = CurveEvaluator.evaluate(curve, 0.0, 0);
            expect(result).toBeCloseTo(0);
            const result2 = CurveEvaluator.evaluate(curve, 1.0, 0);
            expect(result2).toBeCloseTo(10);
        });

        it('mode 2 (TwoConstants/Random) returns random between constantMin and constantMax', () => {
            const curve = makeCurve({ mode: 2, constantMin: 10, constantMax: 20 });
            const result = CurveEvaluator.evaluate(curve, 0.5, 42);
            expect(result).toBeGreaterThanOrEqual(10);
            expect(result).toBeLessThanOrEqual(20);
        });

        it('mode 3 (TwoCurves/RandomCurve) returns random between min and max curves', () => {
            const minCurve = new Float32Array([5, 5]);
            const maxCurve = new Float32Array([15, 15]);
            const curve = makeCurve({
                mode: 3,
                constantMin: 5,
                constantMax: 15,
                curveMin: minCurve,
                curveMax: maxCurve,
                curveLength: 2,
            });
            const result = CurveEvaluator.evaluate(curve, 0.5, 42);
            expect(result).toBeGreaterThanOrEqual(5);
            expect(result).toBeLessThanOrEqual(15);
        });

        it('default mode returns constant', () => {
            const curve = makeCurve({ mode: 99, constant: 7 });
            expect(CurveEvaluator.evaluate(curve, 0.5, 0)).toBe(7);
        });

        it('time clamped to [0, 1]', () => {
            const data = new Float32Array([0, 10]);
            const curve = makeCurve({ mode: 1, constant: 0, curve: data, curveLength: 2 });
            expect(CurveEvaluator.evaluate(curve, -1, 0)).toBeCloseTo(0);
            expect(CurveEvaluator.evaluate(curve, 2, 0)).toBeCloseTo(10);
        });
    });

    describe('evaluateBatch', () => {
        it('fills results array for constant mode', () => {
            const curve = makeCurve({ mode: 0, constant: 5 });
            const times = new Float32Array([0, 0.5, 1]);
            const seeds = new Uint32Array([0, 0, 0]);
            const results = new Float32Array(3);
            CurveEvaluator.evaluateBatch(curve, times, seeds, results, 3);
            expect(results[0]).toBe(5);
            expect(results[1]).toBe(5);
            expect(results[2]).toBe(5);
        });

        it('fills result array for curve mode', () => {
            const data = new Float32Array([0, 10]);
            const curve = makeCurve({ mode: 1, constant: 0, curve: data, curveLength: 2 });
            const times = new Float32Array([0, 1]);
            const seeds = new Uint32Array([0, 0]);
            const results = new Float32Array(2);
            CurveEvaluator.evaluateBatch(curve, times, seeds, results, 2);
            expect(results[0]).toBeCloseTo(0);
            expect(results[1]).toBeCloseTo(10);
        });
    });

    describe('buildLookupTable', () => {
        it('builds table for curve mode 1', () => {
            const data = new Float32Array([0, 10]);
            const curve = makeCurve({ mode: 1, constant: 0, curve: data, curveLength: 2 });
            CurveEvaluator.buildLookupTable(curve, 'key-1');
            const stats = CurveEvaluator.getStats();
            expect(stats.cacheSize).toBe(1);
        });

        it('builds table for curve mode 3', () => {
            const curve = makeCurve({
                mode: 3,
                curveMin: new Float32Array([0, 5]),
                curveMax: new Float32Array([10, 15]),
                curveLength: 2,
            });
            CurveEvaluator.buildLookupTable(curve, 'key-3');
            const stats = CurveEvaluator.getStats();
            expect(stats.cacheSize).toBe(1);
        });

        it('no-op for other modes', () => {
            const curve = makeCurve({ mode: 0 });
            CurveEvaluator.buildLookupTable(curve, 'key-0');
            expect(CurveEvaluator.getStats().cacheSize).toBe(0);
        });
    });

    describe('clearCache / invalidateCache', () => {
        it('clearCache empties all cache', () => {
            const data = new Float32Array([0, 10]);
            const curve = makeCurve({ mode: 1, curve: data, curveLength: 2 });
            CurveEvaluator.buildLookupTable(curve, 'key');
            CurveEvaluator.clearCache();
            expect(CurveEvaluator.getStats().cacheSize).toBe(0);
        });

        it('invalidateCache removes specific entry', () => {
            const data = new Float32Array([0, 10]);
            const curve = makeCurve({ mode: 1, curve: data, curveLength: 2 });
            CurveEvaluator.buildLookupTable(curve, 'key-a');
            CurveEvaluator.buildLookupTable(curve, 'key-b');
            CurveEvaluator.invalidateCache('key-a');
            expect(CurveEvaluator.getStats().cacheSize).toBe(1);
        });
    });

    describe('sampleCurve', () => {
        it('linear interpolation', () => {
            const curve = new Float32Array([0, 10]);
            expect(CurveEvaluator.sampleCurve(curve, 0.5, 2)).toBeCloseTo(5);
        });

        it('edge case: empty curve', () => {
            expect(CurveEvaluator.sampleCurve(new Float32Array(0), 0.5, 0)).toBe(0);
        });

        it('edge case: single key', () => {
            const curve = new Float32Array([42]);
            expect(CurveEvaluator.sampleCurve(curve, 0.5, 1)).toBe(42);
        });

        it('time at boundaries', () => {
            const curve = new Float32Array([0, 10]);
            expect(CurveEvaluator.sampleCurve(curve, 0, 2)).toBeCloseTo(0);
            expect(CurveEvaluator.sampleCurve(curve, 1, 2)).toBeCloseTo(10);
        });
    });

    describe('sampleCurveAdvanced', () => {
        it('step interpolation', () => {
            const curve = new Float32Array([0, 10, 20]);
            const result = CurveEvaluator.sampleCurveAdvanced(curve, 0.25, 3, 'step');
            expect(result).toBeCloseTo(0); // step: returns curve[index]
        });

        it('linear interpolation', () => {
            const curve = new Float32Array([0, 10]);
            const result = CurveEvaluator.sampleCurveAdvanced(curve, 0.5, 2, 'linear');
            expect(result).toBeCloseTo(5);
        });

        it('cubic interpolation', () => {
            const curve = new Float32Array([0, 5, 10, 15]);
            const result = CurveEvaluator.sampleCurveAdvanced(curve, 0.5, 4, 'cubic');
            // Cubic should produce a smooth interpolation
            expect(typeof result).toBe('number');
            expect(isNaN(result)).toBe(false);
        });
    });

    describe('getStats', () => {
        it('returns cache size, version, table size', () => {
            const stats = CurveEvaluator.getStats();
            expect(typeof stats.cacheSize).toBe('number');
            expect(typeof stats.cacheVersion).toBe('number');
            expect(stats.lookupTableSize).toBe(512);
        });
    });
});
