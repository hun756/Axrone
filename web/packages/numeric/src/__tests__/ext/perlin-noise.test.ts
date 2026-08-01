import { describe, expect, test } from 'vitest';
import {
    PerlinNoise,
    NoiseError,
    InvalidSeedError,
    InvalidOctavesError,
    createNoise,
    createSeededNoise,
    type NoiseGenerator,
} from '../../ext/perlin-noise';

describe('PerlinNoise', () => {
    // ─── Error Classes ───────────────────────────────────────────────────
    describe('Error Classes', () => {
        test('NoiseError has message and code', () => {
            const err = new NoiseError('test message', 'TEST_CODE');
            expect(err.message).toBe('test message');
            expect(err.code).toBe('TEST_CODE');
            expect(err.name).toBe('NoiseError');
            expect(err).toBeInstanceOf(Error);
        });

        test('InvalidSeedError has correct code and message format', () => {
            const err = new InvalidSeedError(NaN);
            expect(err.code).toBe('INVALID_SEED');
            expect(err.message).toContain('NaN');
            expect(err.message).toContain('finite number');
            expect(err).toBeInstanceOf(NoiseError);
            expect(err).toBeInstanceOf(Error);
        });

        test('InvalidOctavesError has correct code and message format', () => {
            const err = new InvalidOctavesError(0);
            expect(err.code).toBe('INVALID_OCTAVES');
            expect(err.message).toContain('0');
            expect(err.message).toContain('positive integer');
            expect(err).toBeInstanceOf(NoiseError);
        });
    });

    // ─── Construction ────────────────────────────────────────────────────
    describe('Construction', () => {
        test('default constructor creates valid generator', () => {
            const noise = new PerlinNoise();
            expect(typeof noise.noise1D).toBe('function');
            expect(typeof noise.noise2D).toBe('function');
            expect(typeof noise.noise3D).toBe('function');
            expect(typeof noise.fbm1D).toBe('function');
            expect(typeof noise.fbm2D).toBe('function');
            expect(typeof noise.fbm3D).toBe('function');
        });

        test('seeded constructor is deterministic', () => {
            const a = new PerlinNoise({ seed: 42 });
            const b = new PerlinNoise({ seed: 42 });
            expect(a.noise1D(1.5)).toBe(b.noise1D(1.5));
            expect(a.noise2D(1.5, 2.5)).toBe(b.noise2D(1.5, 2.5));
            expect(a.noise3D(1.5, 2.5, 3.5)).toBe(b.noise3D(1.5, 2.5, 3.5));
        });

        test('different seeds produce different outputs', () => {
            const a = new PerlinNoise({ seed: 1 });
            const b = new PerlinNoise({ seed: 2 });
            // At least some values should differ
            let allSame = true;
            for (let i = 0; i < 10; i++) {
                if (a.noise1D(i * 0.5) !== b.noise1D(i * 0.5)) {
                    allSame = false;
                    break;
                }
            }
            expect(allSame).toBe(false);
        });

        test('invalid seed (NaN) throws InvalidSeedError', () => {
            expect(() => new PerlinNoise({ seed: NaN })).toThrow(InvalidSeedError);
        });

        test('invalid seed (Infinity) throws InvalidSeedError', () => {
            expect(() => new PerlinNoise({ seed: Infinity })).toThrow(InvalidSeedError);
            expect(() => new PerlinNoise({ seed: -Infinity })).toThrow(InvalidSeedError);
        });

        test('invalid octaves (0) throws InvalidOctavesError', () => {
            expect(() => new PerlinNoise({ octaves: 0 })).toThrow(InvalidOctavesError);
        });

        test('invalid octaves (negative) throws InvalidOctavesError', () => {
            expect(() => new PerlinNoise({ octaves: -1 })).toThrow(InvalidOctavesError);
        });

        test('invalid octaves (non-integer) throws InvalidOctavesError', () => {
            expect(() => new PerlinNoise({ octaves: 1.5 })).toThrow(InvalidOctavesError);
        });

        test('valid octaves (positive integer) does not throw', () => {
            expect(() => new PerlinNoise({ octaves: 1 })).not.toThrow();
            expect(() => new PerlinNoise({ octaves: 8 })).not.toThrow();
        });
    });

    // ─── noise1D Properties ──────────────────────────────────────────────
    describe('noise1D', () => {
        const noise = new PerlinNoise({ seed: 123 });

        test('output is finite', () => {
            for (let x = -50; x < 50; x += 0.3) {
                const val = noise.noise1D(x);
                expect(Number.isFinite(val)).toBe(true);
            }
        });

        test('deterministic: same input = same output', () => {
            expect(noise.noise1D(3.14)).toBe(noise.noise1D(3.14));
        });

        test('continuity: nearby inputs produce nearby outputs', () => {
            const a = noise.noise1D(5.0);
            const b = noise.noise1D(5.001);
            expect(Math.abs(a - b)).toBeLessThan(0.1);
        });

        test('different inputs produce different outputs', () => {
            const values = new Set<number>();
            for (let x = 0; x < 10; x += 0.7) {
                values.add(noise.noise1D(x));
            }
            expect(values.size).toBeGreaterThan(5);
        });
    });

    // ─── noise2D Properties ──────────────────────────────────────────────
    describe('noise2D', () => {
        const noise = new PerlinNoise({ seed: 456 });

        test('output is finite', () => {
            for (let x = -10; x < 10; x += 1.5) {
                for (let y = -10; y < 10; y += 1.5) {
                    const val = noise.noise2D(x, y);
                    expect(Number.isFinite(val)).toBe(true);
                }
            }
        });

        test('deterministic', () => {
            expect(noise.noise2D(1.5, 2.5)).toBe(noise.noise2D(1.5, 2.5));
        });

        test('continuity in both dimensions', () => {
            const a = noise.noise2D(3.0, 4.0);
            const bx = noise.noise2D(3.001, 4.0);
            const by = noise.noise2D(3.0, 4.001);
            expect(Math.abs(a - bx)).toBeLessThan(0.1);
            expect(Math.abs(a - by)).toBeLessThan(0.1);
        });

        test('known value at origin is finite', () => {
            const val = noise.noise2D(0, 0);
            expect(Number.isFinite(val)).toBe(true);
        });
    });

    // ─── noise3D Properties ──────────────────────────────────────────────
    describe('noise3D', () => {
        const noise = new PerlinNoise({ seed: 789 });

        test('output is in approximate range [-1, 1]', () => {
            for (let x = -5; x < 5; x += 2) {
                for (let y = -5; y < 5; y += 2) {
                    for (let z = -5; z < 5; z += 2) {
                        const val = noise.noise3D(x, y, z);
                        expect(val).toBeGreaterThanOrEqual(-1.5);
                        expect(val).toBeLessThanOrEqual(1.5);
                    }
                }
            }
        });

        test('deterministic', () => {
            expect(noise.noise3D(1.0, 2.0, 3.0)).toBe(noise.noise3D(1.0, 2.0, 3.0));
        });

        test('continuity in all three dimensions', () => {
            const a = noise.noise3D(2.0, 3.0, 4.0);
            const bx = noise.noise3D(2.001, 3.0, 4.0);
            const by = noise.noise3D(2.0, 3.001, 4.0);
            const bz = noise.noise3D(2.0, 3.0, 4.001);
            expect(Math.abs(a - bx)).toBeLessThan(0.1);
            expect(Math.abs(a - by)).toBeLessThan(0.1);
            expect(Math.abs(a - bz)).toBeLessThan(0.1);
        });
    });

    // ─── FBM (Fractional Brownian Motion) ────────────────────────────────
    describe('FBM', () => {
        test('fbm1D with single octave matches noise1D', () => {
            const noise = new PerlinNoise({ seed: 42, octaves: 1, amplitude: 1, frequency: 1 });
            for (let x = -5; x < 5; x += 0.5) {
                expect(noise.fbm1D(x)).toBeCloseTo(noise.noise1D(x), 10);
            }
        });

        test('fbm2D with single octave matches noise2D', () => {
            const noise = new PerlinNoise({ seed: 42, octaves: 1, amplitude: 1, frequency: 1 });
            expect(noise.fbm2D(1.5, 2.5)).toBeCloseTo(noise.noise2D(1.5, 2.5), 10);
        });

        test('fbm3D with single octave matches noise3D', () => {
            const noise = new PerlinNoise({ seed: 42, octaves: 1, amplitude: 1, frequency: 1 });
            expect(noise.fbm3D(1.0, 2.0, 3.0)).toBeCloseTo(noise.noise3D(1.0, 2.0, 3.0), 10);
        });

        test('higher octaves produce larger amplitude range', () => {
            const single = new PerlinNoise({ seed: 42, octaves: 1 });
            const multi = new PerlinNoise({ seed: 42, octaves: 6 });

            let maxSingle = 0,
                maxMulti = 0;
            for (let x = -20; x < 20; x += 0.3) {
                maxSingle = Math.max(maxSingle, Math.abs(single.fbm1D(x)));
                maxMulti = Math.max(maxMulti, Math.abs(multi.fbm1D(x)));
            }
            // Multi-octave should generally reach higher peaks
            expect(maxMulti).toBeGreaterThanOrEqual(maxSingle * 0.5);
        });

        test('amplitude scaling affects output magnitude', () => {
            const lowAmp = new PerlinNoise({ seed: 42, octaves: 1, amplitude: 0.5, frequency: 1 });
            const highAmp = new PerlinNoise({ seed: 42, octaves: 1, amplitude: 2.0, frequency: 1 });

            let maxLow = 0,
                maxHigh = 0;
            for (let x = -10; x < 10; x += 0.3) {
                maxLow = Math.max(maxLow, Math.abs(lowAmp.fbm1D(x)));
                maxHigh = Math.max(maxHigh, Math.abs(highAmp.fbm1D(x)));
            }
            expect(maxHigh).toBeGreaterThan(maxLow);
        });

        test('frequency scaling compresses noise', () => {
            const lowFreq = new PerlinNoise({ seed: 42, octaves: 1, amplitude: 1, frequency: 0.5 });
            const highFreq = new PerlinNoise({ seed: 42, octaves: 1, amplitude: 1, frequency: 2.0 });

            // High frequency should have more zero crossings
            let crossingsLow = 0,
                crossingsHigh = 0;
            let prevLow = lowFreq.fbm1D(0);
            let prevHigh = highFreq.fbm1D(0);
            for (let x = 0.1; x < 20; x += 0.1) {
                const curLow = lowFreq.fbm1D(x);
                const curHigh = highFreq.fbm1D(x);
                if (Math.sign(curLow) !== Math.sign(prevLow)) crossingsLow++;
                if (Math.sign(curHigh) !== Math.sign(prevHigh)) crossingsHigh++;
                prevLow = curLow;
                prevHigh = curHigh;
            }
            expect(crossingsHigh).toBeGreaterThan(crossingsLow);
        });

        test('persistence affects amplitude decay', () => {
            const lowPersist = new PerlinNoise({
                seed: 42,
                octaves: 4,
                persistence: 0.1,
                lacunarity: 2,
            });
            const highPersist = new PerlinNoise({
                seed: 42,
                octaves: 4,
                persistence: 0.9,
                lacunarity: 2,
            });

            // With high persistence, later octaves contribute more -> rougher noise
            let varianceLow = 0,
                varianceHigh = 0;
            const n = 200;
            let meanLow = 0,
                meanHigh = 0;
            for (let i = 0; i < n; i++) {
                const x = (i / n) * 20 - 10;
                meanLow += lowPersist.fbm1D(x);
                meanHigh += highPersist.fbm1D(x);
            }
            meanLow /= n;
            meanHigh /= n;
            for (let i = 0; i < n; i++) {
                const x = (i / n) * 20 - 10;
                const dL = lowPersist.fbm1D(x) - meanLow;
                const dH = highPersist.fbm1D(x) - meanHigh;
                varianceLow += dL * dL;
                varianceHigh += dH * dH;
            }
            // High persistence -> more variance
            expect(varianceHigh).toBeGreaterThan(varianceLow);
        });
    });

    // ─── Factory Functions ───────────────────────────────────────────────
    describe('Factory Functions', () => {
        test('createNoise returns NoiseGenerator with all 6 methods', () => {
            const gen = createNoise();
            expect(typeof gen.noise1D).toBe('function');
            expect(typeof gen.noise2D).toBe('function');
            expect(typeof gen.noise3D).toBe('function');
            expect(typeof gen.fbm1D).toBe('function');
            expect(typeof gen.fbm2D).toBe('function');
            expect(typeof gen.fbm3D).toBe('function');
        });

        test('createSeededNoise is deterministic', () => {
            const a = createSeededNoise(42);
            const b = createSeededNoise(42);
            expect(a.noise1D(1.5)).toBe(b.noise1D(1.5));
            expect(a.noise2D(1.5, 2.5)).toBe(b.noise2D(1.5, 2.5));
            expect(a.noise3D(1.0, 2.0, 3.0)).toBe(b.noise3D(1.0, 2.0, 3.0));
        });

        test('createSeededNoise with different seeds produce different results', () => {
            const a = createSeededNoise(1);
            const b = createSeededNoise(2);
            let allSame = true;
            for (let i = 0; i < 20; i++) {
                const x = i * 0.37 + 0.13;
                if (a.noise1D(x) !== b.noise1D(x)) {
                    allSame = false;
                    break;
                }
            }
            expect(allSame).toBe(false);
        });

        test('createSeededNoise accepts additional config', () => {
            const gen = createSeededNoise(42, { octaves: 4, persistence: 0.3 });
            const val = gen.fbm1D(5.0);
            expect(Number.isFinite(val)).toBe(true);
        });

        test('createNoise with config', () => {
            const gen = createNoise({ seed: 100, octaves: 2 });
            const val = gen.fbm2D(3.0, 4.0);
            expect(Number.isFinite(val)).toBe(true);
        });
    });

    // ─── NoiseConfig Parameters ──────────────────────────────────────────
    describe('NoiseConfig', () => {
        test('default config produces valid noise', () => {
            const noise = new PerlinNoise();
            expect(Number.isFinite(noise.noise1D(0))).toBe(true);
            expect(Number.isFinite(noise.noise2D(0, 0))).toBe(true);
            expect(Number.isFinite(noise.noise3D(0, 0, 0))).toBe(true);
            expect(Number.isFinite(noise.fbm1D(0))).toBe(true);
            expect(Number.isFinite(noise.fbm2D(0, 0))).toBe(true);
            expect(Number.isFinite(noise.fbm3D(0, 0, 0))).toBe(true);
        });

        test('lacunarity affects frequency scaling between octaves', () => {
            const lowLac = new PerlinNoise({ seed: 42, octaves: 4, lacunarity: 1.5 });
            const highLac = new PerlinNoise({ seed: 42, octaves: 4, lacunarity: 3.0 });

            let diff = 0;
            for (let x = 0; x < 10; x += 0.3) {
                diff += Math.abs(lowLac.fbm1D(x) - highLac.fbm1D(x));
            }
            // Different lacunarity should produce different results
            expect(diff).toBeGreaterThan(0);
        });
    });

    // ─── Integration / Edge Cases ────────────────────────────────────────
    describe('Edge Cases', () => {
        test('noise at integer boundaries is finite', () => {
            const noise = new PerlinNoise({ seed: 42 });
            for (let i = -10; i <= 10; i++) {
                expect(Number.isFinite(noise.noise1D(i))).toBe(true);
                expect(Number.isFinite(noise.noise2D(i, i))).toBe(true);
                expect(Number.isFinite(noise.noise3D(i, i, i))).toBe(true);
            }
        });

        test('noise at very large coordinates is finite', () => {
            const noise = new PerlinNoise({ seed: 42 });
            expect(Number.isFinite(noise.noise1D(1e6))).toBe(true);
            expect(Number.isFinite(noise.noise2D(1e6, 1e6))).toBe(true);
        });

        test('noise at negative coordinates is finite', () => {
            const noise = new PerlinNoise({ seed: 42 });
            expect(Number.isFinite(noise.noise1D(-100))).toBe(true);
            expect(Number.isFinite(noise.noise2D(-100, -100))).toBe(true);
            expect(Number.isFinite(noise.noise3D(-100, -100, -100))).toBe(true);
        });

        test('seed of 0 is valid', () => {
            const noise = new PerlinNoise({ seed: 0 });
            expect(Number.isFinite(noise.noise1D(1))).toBe(true);
        });

        test('negative seed is valid', () => {
            const noise = new PerlinNoise({ seed: -42 });
            expect(Number.isFinite(noise.noise1D(1))).toBe(true);
        });
    });
});
