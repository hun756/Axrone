import { describe, expect, it } from 'vitest';
import { Easing } from '../easing-functions';

const ALL_CATEGORIES = [
    'Quadratic',
    'Cubic',
    'Quartic',
    'Quintic',
    'Sinusoidal',
    'Exponential',
    'Circular',
    'Elastic',
    'Back',
    'Bounce',
] as const;

const VARIANTS = ['In', 'Out', 'InOut'] as const;

describe('Easing Functions', () => {
    describe('Linear', () => {
        it('None(0) = 0, None(1) = 1, None(0.5) = 0.5', () => {
            expect(Easing.Linear.None(0)).toBe(0);
            expect(Easing.Linear.None(1)).toBe(1);
            expect(Easing.Linear.None(0.5)).toBe(0.5);
        });
    });

    describe('Boundary validation: f(0)=0 and f(1)=1', () => {
        for (const category of ALL_CATEGORIES) {
            for (const variant of VARIANTS) {
                it(`${category}.${variant}(0) = 0 and ${category}.${variant}(1) = 1`, () => {
                    const fn = (Easing as any)[category][variant];
                    expect(fn(0)).toBeCloseTo(0, 10);
                    expect(fn(1)).toBeCloseTo(1, 10);
                });
            }
        }
    });

    describe('Known-answer tests', () => {
        it('Quadratic.In(0.5) = 0.25', () => {
            expect(Easing.Quadratic.In(0.5)).toBe(0.25);
        });

        it('Quadratic.Out(0.5) = 0.75', () => {
            expect(Easing.Quadratic.Out(0.5)).toBe(0.75);
        });

        it('Cubic.In(0.5) = 0.125', () => {
            expect(Easing.Cubic.In(0.5)).toBe(0.125);
        });

        it('Quartic.In(0.5) = 0.0625', () => {
            expect(Easing.Quartic.In(0.5)).toBeCloseTo(0.0625, 10);
        });

        it('Quintic.In(0.5) = 0.03125', () => {
            expect(Easing.Quintic.In(0.5)).toBeCloseTo(0.03125, 10);
        });

        it('Sinusoidal.In(0.5) ~ 0.2929', () => {
            expect(Easing.Sinusoidal.In(0.5)).toBeCloseTo(1 - Math.cos(Math.PI / 4), 5);
        });

        it('Sinusoidal.Out(0.5) ~ 0.7071', () => {
            expect(Easing.Sinusoidal.Out(0.5)).toBeCloseTo(Math.sin(Math.PI / 4), 5);
        });

        it('Circular.In(0.5) ~ 0.1340', () => {
            expect(Easing.Circular.In(0.5)).toBeCloseTo(1 - Math.sqrt(0.75), 5);
        });

        it('Exponential.In(0) = 0 (special case)', () => {
            expect(Easing.Exponential.In(0)).toBe(0);
        });

        it('Exponential.Out(1) = 1 (special case)', () => {
            expect(Easing.Exponential.Out(1)).toBe(1);
        });

        it('Exponential.InOut(0) = 0 and InOut(1) = 1', () => {
            expect(Easing.Exponential.InOut(0)).toBe(0);
            expect(Easing.Exponential.InOut(1)).toBe(1);
        });

        it('Elastic.In(0) = 0 and In(1) = 1 (special cases)', () => {
            expect(Easing.Elastic.In(0)).toBe(0);
            expect(Easing.Elastic.In(1)).toBe(1);
        });

        it('Elastic.Out(0) = 0 and Out(1) = 1 (special cases)', () => {
            expect(Easing.Elastic.Out(0)).toBe(0);
            expect(Easing.Elastic.Out(1)).toBe(1);
        });

        it('Elastic.InOut(0) = 0 and InOut(1) = 1 (special cases)', () => {
            expect(Easing.Elastic.InOut(0)).toBe(0);
            expect(Easing.Elastic.InOut(1)).toBe(1);
        });

        it('Back.In(0) = 0', () => {
            expect(Easing.Back.In(0)).toBeCloseTo(0, 10);
        });
    });

    describe('Bounce sub-branches', () => {
        it('Bounce.Out covers all 4 segments', () => {
            const t1 = 1 / 2.75;
            const t2 = 2 / 2.75;
            const t3 = 2.5 / 2.75;

            expect(Easing.Bounce.Out(t1 * 0.5)).toBeGreaterThan(0);
            expect(Easing.Bounce.Out((t1 + t2) / 2)).toBeGreaterThan(0.5);
            expect(Easing.Bounce.Out((t2 + t3) / 2)).toBeGreaterThan(0.75);
            expect(Easing.Bounce.Out((t3 + 1) / 2)).toBeGreaterThan(0.9);
            expect(Easing.Bounce.Out(1)).toBeCloseTo(1, 10);
        });

        it('Bounce.In is inverse of Bounce.Out', () => {
            for (const t of [0, 0.25, 0.5, 0.75, 1]) {
                expect(Easing.Bounce.In(t)).toBeCloseTo(1 - Easing.Bounce.Out(1 - t), 10);
            }
        });

        it('Bounce.InOut is symmetric', () => {
            expect(Easing.Bounce.InOut(0.5)).toBeCloseTo(0.5, 5);
        });
    });

    describe('Finite values for [0, 1] range', () => {
        for (const category of ALL_CATEGORIES) {
            for (const variant of VARIANTS) {
                it(`${category}.${variant} produces finite values for t in [0, 1]`, () => {
                    const fn = (Easing as any)[category][variant];
                    for (let t = 0; t <= 1; t += 0.05) {
                        const result = fn(t);
                        expect(Number.isFinite(result)).toBe(true);
                    }
                });
            }
        }
    });

    describe('Monotonicity for In variants', () => {
        for (const category of ['Quadratic', 'Cubic', 'Quartic', 'Quintic', 'Sinusoidal', 'Exponential', 'Circular'] as const) {
            it(`${category}.In is monotonically non-decreasing on [0, 1]`, () => {
                const fn = (Easing as any)[category].In;
                let prev = fn(0);
                for (let t = 0.05; t <= 1; t += 0.05) {
                    const current = fn(t);
                    expect(current).toBeGreaterThanOrEqual(prev - 1e-10);
                    prev = current;
                }
            });
        }
    });

    describe('Frozen objects', () => {
        it('all easing categories are frozen', () => {
            for (const category of ALL_CATEGORIES) {
                expect(Object.isFrozen((Easing as any)[category])).toBe(true);
            }
            expect(Object.isFrozen(Easing.Linear)).toBe(true);
        });
    });
});
