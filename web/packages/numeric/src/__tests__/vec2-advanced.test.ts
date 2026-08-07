import { describe, expect, test } from 'vitest';
import { EPSILON, HALF_PI, PI_2 } from '../common';
import {
    Vec2,
    Vec2ComparisonMode,
    Vec2Comparer,
    Vec2EqualityComparer,
    type IVec2Like,
} from '../vec2';
import { Fnv1a32 } from '@axrone/hash';

const expectVecClose = (actual: IVec2Like, expected: IVec2Like, eps = EPSILON) => {
    expect(Math.abs(actual.x - expected.x)).toBeLessThan(eps);
    expect(Math.abs(actual.y - expected.y)).toBeLessThan(eps);
};

const expectNumClose = (actual: number, expected: number, eps = EPSILON) => {
    expect(Math.abs(actual - expected)).toBeLessThan(eps);
};

describe('Vec2 Advanced Operations', () => {
    // ─── Negate, Inverse, InverseSafe ────────────────────────────────────
    describe('Negate', () => {
        test('static negate', () => {
            expectVecClose(Vec2.negate({ x: 3, y: -4 }), { x: -3, y: 4 });
        });

        test('static negate with out', () => {
            const out = { x: 0, y: 0 };
            const result = Vec2.negate({ x: 1, y: 2 }, out);
            expect(result).toBe(out);
            expectVecClose(out, { x: -1, y: -2 });
        });

        test('instance multiplyScalar by -1 (negate equivalent)', () => {
            const v = new Vec2(3, -4);
            const result = v.multiplyScalar(-1);
            expect(result).toBe(v);
            expectVecClose(v, { x: -3, y: 4 });
        });
    });

    describe('Inverse', () => {
        test('static inverse', () => {
            expectVecClose(Vec2.inverse({ x: 2, y: 4 }), { x: 0.5, y: 0.25 });
        });

        test('static inverse with out', () => {
            const out = { x: 0, y: 0 };
            Vec2.inverse({ x: 2, y: 5 }, out);
            expectNumClose(out.x, 0.5);
            expectNumClose(out.y, 0.2);
        });

        test('instance inverse', () => {
            const v = new Vec2(4, 5);
            v.inverse();
            expectNumClose(v.x, 0.25);
            expectNumClose(v.y, 0.2);
        });

        test('static inverseSafe with non-zero values', () => {
            expectVecClose(Vec2.inverseSafe({ x: 2, y: 4 }), { x: 0.5, y: 0.25 });
        });

        test('static inverseSafe throws on zero', () => {
            expect(() => Vec2.inverseSafe({ x: 0, y: 1 })).toThrow('zero or near-zero');
        });

        test('instance inverseSafe uses default for zero components', () => {
            const v = new Vec2(0, 4);
            const result = v.inverseSafe(99);
            expect(result).toBe(v);
            expectNumClose(v.x, 99); // zero component replaced by default
            expectNumClose(v.y, 0.25);
        });
    });

    // ─── Perpendicular ───────────────────────────────────────────────────
    describe('Perpendicular', () => {
        test('perpendicular (CW) is orthogonal', () => {
            const v = { x: 3, y: 4 };
            const perp = Vec2.perpendicular(v);
            expect(Vec2.dot(v, perp)).toBeCloseTo(0, 10);
        });

        test('perpendicular CW: (-y, x)', () => {
            expectVecClose(Vec2.perpendicular({ x: 1, y: 2 }), { x: -2, y: 1 });
        });

        test('perpendicularCCW: (y, -x)', () => {
            expectVecClose(Vec2.perpendicularCCW({ x: 1, y: 2 }), { x: 2, y: -1 });
        });

        test('perpendicular preserves length', () => {
            const v = { x: 3, y: 4 };
            const perp = Vec2.perpendicular(v);
            expectNumClose(Vec2.len(perp), Vec2.len(v));
        });

        test('perpendicular with out', () => {
            const out = { x: 0, y: 0 };
            Vec2.perpendicular({ x: 1, y: 2 }, out);
            expectVecClose(out, { x: -2, y: 1 });
        });

        test('perpendicularCCW with out', () => {
            const out = { x: 0, y: 0 };
            Vec2.perpendicularCCW({ x: 1, y: 2 }, out);
            expectVecClose(out, { x: 2, y: -1 });
        });
    });

    // ─── Dot, Cross, Length, Normalize ───────────────────────────────────
    describe('Dot / Cross', () => {
        test('dot product', () => {
            expect(Vec2.dot({ x: 1, y: 2 }, { x: 3, y: 4 })).toBe(11);
        });

        test('instance dot', () => {
            const v = new Vec2(1, 2);
            expect(v.dot({ x: 3, y: 4 })).toBe(11);
        });

        test('cross product (2D scalar)', () => {
            // cross = ax*by - ay*bx = 1*4 - 2*3 = -2
            expect(Vec2.cross({ x: 1, y: 2 }, { x: 3, y: 4 })).toBe(-2);
        });

        test('instance cross', () => {
            const v = new Vec2(1, 2);
            expect(v.cross({ x: 3, y: 4 })).toBe(-2);
        });

        test('perpendicular vectors have zero dot', () => {
            expectNumClose(Vec2.dot({ x: 1, y: 0 }, { x: 0, y: 1 }), 0);
        });
    });

    describe('Length / Normalize', () => {
        test('len of (3,4) = 5', () => {
            expect(Vec2.len({ x: 3, y: 4 })).toBe(5);
        });

        test('instance length', () => {
            expect(new Vec2(3, 4).length()).toBe(5);
        });

        test('lengthSquared', () => {
            expect(Vec2.lengthSquared({ x: 3, y: 4 })).toBe(25);
        });

        test('instance lengthSquared', () => {
            expect(new Vec2(3, 4).lengthSquared()).toBe(25);
        });

        test('normalize produces unit vector', () => {
            const n = Vec2.normalize({ x: 3, y: 4 });
            expectNumClose(Vec2.len(n), 1);
        });

        test('normalize with out', () => {
            const out = { x: 0, y: 0 };
            Vec2.normalize({ x: 3, y: 4 }, out);
            expectNumClose(Vec2.len(out), 1);
        });

        test('instance normalize', () => {
            const v = new Vec2(3, 4);
            v.normalize();
            expectNumClose(v.length(), 1);
        });

        test('normalize zero vector throws', () => {
            expect(() => Vec2.normalize({ x: 0, y: 0 })).toThrow('Cannot normalize');
            expect(() => new Vec2(0, 0).normalize()).toThrow('Cannot normalize');
        });
    });

    // ─── Distance Metrics ────────────────────────────────────────────────
    describe('Distance Metrics', () => {
        const a = { x: 1, y: 2 };
        const b = { x: 4, y: 6 };

        test('Euclidean distance', () => {
            expectNumClose(Vec2.distance(a, b), 5);
        });

        test('instance distance', () => {
            const v = new Vec2(1, 2);
            expectNumClose(v.distance(b), 5);
        });

        test('distance squared', () => {
            expect(Vec2.distanceSquared(a, b)).toBe(25);
        });

        test('instance distanceSquared', () => {
            expect(new Vec2(1, 2).distanceSquared(b)).toBe(25);
        });

        test('Manhattan distance', () => {
            expect(Vec2.manhattanDistance(a, b)).toBe(7);
        });

        test('instance manhattanDistance', () => {
            expect(new Vec2(1, 2).manhattanDistance(b)).toBe(7);
        });

        test('Chebyshev distance', () => {
            expect(Vec2.chebyshevDistance(a, b)).toBe(4);
        });

        test('instance chebyshevDistance', () => {
            expect(new Vec2(1, 2).chebyshevDistance(b)).toBe(4);
        });

        test('same point distance = 0', () => {
            expect(Vec2.distance(a, a)).toBe(0);
            expect(Vec2.manhattanDistance(a, a)).toBe(0);
            expect(Vec2.chebyshevDistance(a, a)).toBe(0);
        });
    });

    // ─── Angles ──────────────────────────────────────────────────────────
    describe('Angles', () => {
        test('angleBetween orthogonal vectors = PI/2', () => {
            expectNumClose(Vec2.angleBetween({ x: 1, y: 0 }, { x: 0, y: 1 }), HALF_PI);
        });

        test('angleBetween same direction = 0', () => {
            expectNumClose(Vec2.angleBetween({ x: 1, y: 0 }, { x: 2, y: 0 }), 0);
        });

        test('angleBetween opposite = PI', () => {
            expectNumClose(Vec2.angleBetween({ x: 1, y: 0 }, { x: -1, y: 0 }), Math.PI);
        });

        test('instance angleBetween', () => {
            const v = new Vec2(1, 0);
            expectNumClose(v.angleBetween({ x: 0, y: 1 }), HALF_PI);
        });

        test('angleBetween zero vector throws', () => {
            expect(() => Vec2.angleBetween({ x: 0, y: 0 }, { x: 1, y: 0 })).toThrow('zero-length');
        });

        test('angle (atan2-based, [0, 2PI))', () => {
            expectNumClose(new Vec2(1, 0).angle(), 0);
            expectNumClose(new Vec2(0, 1).angle(), HALF_PI);
            expectNumClose(new Vec2(-1, 0).angle(), Math.PI);
            expectNumClose(new Vec2(0, -1).angle(), Math.PI * 1.5, 1e-6);
        });

        test('angle2Deg', () => {
            expectNumClose(Vec2.angle2Deg({ x: 1, y: 0 }, { x: 0, y: 1 }), 90);
        });

        test('instance angle2Deg', () => {
            expectNumClose(new Vec2(1, 0).angle2Deg({ x: 0, y: 1 }), 90);
        });
    });

    // ─── Rotations ───────────────────────────────────────────────────────
    describe('Rotations', () => {
        test('rotate by PI/2', () => {
            const r = Vec2.rotate({ x: 1, y: 0 }, HALF_PI);
            expectVecClose(r, { x: 0, y: 1 }, 1e-10);
        });

        test('rotate preserves length', () => {
            const r = Vec2.rotate({ x: 3, y: 4 }, 1.23);
            expectNumClose(Vec2.len(r), 5, 1e-10);
        });

        test('rotate with out', () => {
            const out = { x: 0, y: 0 };
            Vec2.rotate({ x: 1, y: 0 }, HALF_PI, out);
            expectVecClose(out, { x: 0, y: 1 }, 1e-10);
        });

        test('instance rotate', () => {
            const v = new Vec2(1, 0);
            v.rotate(HALF_PI);
            expectVecClose(v, { x: 0, y: 1 }, 1e-10);
        });

        test('fastRotate special angle: PI', () => {
            const r = Vec2.fastRotate({ x: 3, y: 4 }, Math.PI);
            expectVecClose(r, { x: -3, y: -4 }, 1e-10);
        });

        test('fastRotate special angle: HALF_PI', () => {
            const r = Vec2.fastRotate({ x: 3, y: 4 }, HALF_PI);
            expectVecClose(r, { x: -4, y: 3 }, 1e-10);
        });

        test('fastRotate special angle: -HALF_PI', () => {
            const r = Vec2.fastRotate({ x: 3, y: 4 }, -HALF_PI);
            expectVecClose(r, { x: 4, y: -3 }, 1e-10);
        });

        test('fastRotate small angle approximation', () => {
            const r = Vec2.fastRotate({ x: 1, y: 0 }, 0.05);
            const exact = Vec2.rotate({ x: 1, y: 0 }, 0.05);
            // Small angle approximation should be close
            expectVecClose(r, exact, 0.01);
        });

        test('instance fastRotate', () => {
            const v = new Vec2(1, 0);
            v.fastRotate(HALF_PI);
            expectVecClose(v, { x: 0, y: 1 }, 1e-10);
        });

        test('rotateAround pivot', () => {
            const r = Vec2.rotateAround({ x: 1, y: 0 }, HALF_PI, { x: 0, y: 0 });
            expectVecClose(r, { x: 0, y: 1 }, 1e-10);
        });

        test('rotateAround non-origin pivot', () => {
            const r = Vec2.rotateAround({ x: 2, y: 0 }, Math.PI, { x: 1, y: 0 });
            expectVecClose(r, { x: 0, y: 0 }, 1e-10);
        });

        test('instance rotateAround', () => {
            const v = new Vec2(2, 0);
            v.rotateAround({ x: 1, y: 0 }, Math.PI);
            expectVecClose(v, { x: 0, y: 0 }, 1e-10);
        });

        test('full rotation returns to original', () => {
            const v = new Vec2(3, 4);
            v.rotate(PI_2);
            expectVecClose(v, { x: 3, y: 4 }, 1e-8);
        });
    });

    // ─── Interpolation ───────────────────────────────────────────────────
    describe('Interpolation', () => {
        const a = { x: 0, y: 0 };
        const b = { x: 4, y: 8 };

        describe('lerp', () => {
            test('t=0 returns a', () => expectVecClose(Vec2.lerp(a, b, 0), a));
            test('t=1 returns b', () => expectVecClose(Vec2.lerp(a, b, 1), b));
            test('t=0.5 midpoint', () => expectVecClose(Vec2.lerp(a, b, 0.5), { x: 2, y: 4 }));
            test('clamps t > 1', () => expectVecClose(Vec2.lerp(a, b, 2), b));
            test('clamps t < 0', () => expectVecClose(Vec2.lerp(a, b, -1), a));

            test('out parameter', () => {
                const out = { x: 0, y: 0 };
                Vec2.lerp(a, b, 0.5, out);
                expectVecClose(out, { x: 2, y: 4 });
            });
        });

        describe('lerpUnClamped', () => {
            test('extrapolates t=-0.5', () => {
                expectVecClose(Vec2.lerpUnClamped(a, b, -0.5), { x: -2, y: -4 });
            });
            test('extrapolates t=1.5', () => {
                expectVecClose(Vec2.lerpUnClamped(a, b, 1.5), { x: 6, y: 12 });
            });
        });

        describe('slerp', () => {
            test('preserves unit length for unit vectors', () => {
                const r = Vec2.slerp({ x: 1, y: 0 }, { x: 0, y: 1 }, 0.5);
                expectNumClose(Vec2.len(r), 1, 1e-10);
            });

            test('t=0 returns start', () => {
                expectVecClose(Vec2.slerp({ x: 1, y: 0 }, { x: 0, y: 1 }, 0), { x: 1, y: 0 }, 1e-10);
            });

            test('falls back to lerp for zero-length vectors', () => {
                const r = Vec2.slerp({ x: 0, y: 0 }, { x: 1, y: 1 }, 0.5);
                const l = Vec2.lerpUnClamped({ x: 0, y: 0 }, { x: 1, y: 1 }, 0.5);
                expectVecClose(r, l, 1e-10);
            });
        });

        describe('smoothStep', () => {
            test('t=0 returns a', () => expectVecClose(Vec2.smoothStep(a, b, 0), a));
            test('t=1 returns b', () => expectVecClose(Vec2.smoothStep(a, b, 1), b));
            test('t=0.5 midpoint', () => expectVecClose(Vec2.smoothStep(a, b, 0.5), { x: 2, y: 4 }));
        });

        describe('smootherStep', () => {
            test('t=0 returns a', () => expectVecClose(Vec2.smootherStep(a, b, 0), a));
            test('t=1 returns b', () => expectVecClose(Vec2.smootherStep(a, b, 1), b));
            test('t=0.5 midpoint', () => expectVecClose(Vec2.smootherStep(a, b, 0.5), { x: 2, y: 4 }));
        });

        describe('cubicBezier', () => {
            test('t=0 returns start', () => {
                const p0 = { x: 0, y: 0 }, c1 = { x: 1, y: 2 }, c2 = { x: 3, y: 2 }, p1 = { x: 4, y: 0 };
                expectVecClose(Vec2.cubicBezier(p0, c1, c2, p1, 0), p0);
            });
            test('t=1 returns end', () => {
                const p0 = { x: 0, y: 0 }, c1 = { x: 1, y: 2 }, c2 = { x: 3, y: 2 }, p1 = { x: 4, y: 0 };
                expectVecClose(Vec2.cubicBezier(p0, c1, c2, p1, 1), p1);
            });
        });

        describe('hermite', () => {
            test('t=0 returns p0', () => {
                const p0 = { x: 0, y: 0 }, m0 = { x: 1, y: 0 }, p1 = { x: 1, y: 1 }, m1 = { x: 0, y: 1 };
                expectVecClose(Vec2.hermite(p0, m0, p1, m1, 0), p0);
            });
            test('t=1 returns p1', () => {
                const p0 = { x: 0, y: 0 }, m0 = { x: 1, y: 0 }, p1 = { x: 1, y: 1 }, m1 = { x: 0, y: 1 };
                expectVecClose(Vec2.hermite(p0, m0, p1, m1, 1), p1);
            });
        });

        describe('catmullRom', () => {
            test('t=0 returns p1', () => {
                const p0 = { x: 0, y: 0 }, p1 = { x: 1, y: 1 }, p2 = { x: 2, y: 2 }, p3 = { x: 3, y: 3 };
                expectVecClose(Vec2.catmullRom(p0, p1, p2, p3, 0), p1);
            });
            test('t=1 returns p2', () => {
                const p0 = { x: 0, y: 0 }, p1 = { x: 1, y: 1 }, p2 = { x: 2, y: 2 }, p3 = { x: 3, y: 3 };
                expectVecClose(Vec2.catmullRom(p0, p1, p2, p3, 1), p2);
            });
            test('t=0.5 midpoint of linear spline', () => {
                const p0 = { x: 0, y: 0 }, p1 = { x: 1, y: 1 }, p2 = { x: 2, y: 2 }, p3 = { x: 3, y: 3 };
                expectVecClose(Vec2.catmullRom(p0, p1, p2, p3, 0.5), { x: 1.5, y: 1.5 });
            });
        });
    });

    // ─── Random Generation ───────────────────────────────────────────────
    describe('Random', () => {
        test('fastRandom produces unit vectors', () => {
            for (let i = 0; i < 20; i++) {
                const v = Vec2.fastRandom();
                expectNumClose(Vec2.len(v), 1, 1e-10);
            }
        });

        test('fastRandom with scale', () => {
            for (let i = 0; i < 10; i++) {
                const v = Vec2.fastRandom(5);
                expectNumClose(Vec2.len(v), 5, 1e-10);
            }
        });

        test('randomNormal produces finite values', () => {
            for (let i = 0; i < 20; i++) {
                const v = Vec2.randomNormal();
                expect(Number.isFinite(v.x)).toBe(true);
                expect(Number.isFinite(v.y)).toBe(true);
            }
        });

        test('randomNormal mean approximately 0', () => {
            const samples = Array.from({ length: 3000 }, () => Vec2.randomNormal());
            const meanX = samples.reduce((s, v) => s + v.x, 0) / samples.length;
            const meanY = samples.reduce((s, v) => s + v.y, 0) / samples.length;
            expect(Math.abs(meanX)).toBeLessThan(0.2);
            expect(Math.abs(meanY)).toBeLessThan(0.2);
        });

        test('instance randomBox within bounds', () => {
            const v = new Vec2();
            for (let i = 0; i < 20; i++) {
                v.randomBox(-5, 5, -10, 10);
                expect(v.x).toBeGreaterThanOrEqual(-5);
                expect(v.x).toBeLessThanOrEqual(5);
                expect(v.y).toBeGreaterThanOrEqual(-10);
                expect(v.y).toBeLessThanOrEqual(10);
            }
        });

        test('instance randomBoxNormal within bounds', () => {
            const v = new Vec2();
            for (let i = 0; i < 20; i++) {
                v.randomBoxNormal(-5, 5, -10, 10);
                expect(v.x).toBeGreaterThanOrEqual(-5);
                expect(v.x).toBeLessThanOrEqual(5);
                expect(v.y).toBeGreaterThanOrEqual(-10);
                expect(v.y).toBeLessThanOrEqual(10);
            }
        });
    });

    // ─── Equals / Hash ───────────────────────────────────────────────────
    describe('Equals / Hash', () => {
        test('equals within epsilon', () => {
            const a = new Vec2(1, 2);
            const b = new Vec2(1 + EPSILON / 2, 2);
            expect(a.equals(b)).toBe(true);
        });

        test('non-Vec2 returns false', () => {
            expect(new Vec2(1, 2).equals({ x: 1, y: 2 })).toBe(false);
            expect(new Vec2(1, 2).equals(null)).toBe(false);
        });

        test('getHashCode deterministic', () => {
            const a = new Vec2(1, 2);
            const b = new Vec2(1, 2);
            expect(a.getHashCode()).toBe(b.getHashCode());
        });

        test('hashInto integrates with Fnv1a32', () => {
            const v = new Vec2(1, 2);
            const hasher = new Fnv1a32();
            v.hashInto(hasher);
            const hash = hasher.digest();
            expect(Number.isInteger(hash)).toBe(true);
        });
    });

    // ─── Vec2Comparer ────────────────────────────────────────────────────
    describe('Vec2Comparer', () => {
        describe('LEXICOGRAPHIC mode', () => {
            const comparer = new Vec2Comparer(Vec2ComparisonMode.LEXICOGRAPHIC);

            test('x comparison dominates', () => {
                expect(comparer.compare(new Vec2(1, 10), new Vec2(2, 0))).toBe(-1);
                expect(comparer.compare(new Vec2(2, 0), new Vec2(1, 10))).toBe(1);
            });

            test('equal x falls back to y', () => {
                expect(comparer.compare(new Vec2(1, 1), new Vec2(1, 2))).toBe(-1);
                expect(comparer.compare(new Vec2(1, 2), new Vec2(1, 1))).toBe(1);
            });

            test('equal vectors return 0', () => {
                expect(comparer.compare(new Vec2(1, 2), new Vec2(1, 2))).toBe(0);
            });
        });

        describe('MAGNITUDE mode', () => {
            const comparer = new Vec2Comparer(Vec2ComparisonMode.MAGNITUDE);

            test('larger magnitude returns 1', () => {
                expect(comparer.compare(new Vec2(3, 4), new Vec2(1, 1))).toBe(1);
            });

            test('smaller magnitude returns -1', () => {
                expect(comparer.compare(new Vec2(1, 1), new Vec2(3, 4))).toBe(-1);
            });

            test('equal magnitude returns 0', () => {
                expect(comparer.compare(new Vec2(3, 4), new Vec2(4, 3))).toBe(0);
            });
        });

        describe('ANGLE mode', () => {
            const comparer = new Vec2Comparer(Vec2ComparisonMode.ANGLE);

            test('smaller angle returns -1', () => {
                expect(comparer.compare(new Vec2(1, 0), new Vec2(0, 1))).toBe(-1);
            });

            test('larger angle returns 1', () => {
                expect(comparer.compare(new Vec2(0, 1), new Vec2(1, 0))).toBe(1);
            });

            test('same angle returns 0', () => {
                expect(comparer.compare(new Vec2(1, 0), new Vec2(2, 0))).toBe(0);
            });
        });

        describe('MANHATTAN mode', () => {
            const comparer = new Vec2Comparer(Vec2ComparisonMode.MANHATTAN);

            test('larger manhattan returns 1', () => {
                expect(comparer.compare(new Vec2(5, 5), new Vec2(1, 1))).toBe(1);
            });

            test('equal manhattan returns 0', () => {
                expect(comparer.compare(new Vec2(3, 2), new Vec2(2, 3))).toBe(0);
            });
        });

        test('default mode is LEXICOGRAPHIC', () => {
            const comparer = new Vec2Comparer();
            expect(comparer.compare(new Vec2(1, 10), new Vec2(2, 0))).toBe(-1);
        });
    });

    // ─── Vec2EqualityComparer ────────────────────────────────────────────
    describe('Vec2EqualityComparer', () => {
        test('epsilon-based equality', () => {
            const eq = new Vec2EqualityComparer(0.01);
            expect(eq.equals(new Vec2(1, 2), new Vec2(1.005, 2.005))).toBe(true);
            expect(eq.equals(new Vec2(1, 2), new Vec2(1.02, 2))).toBe(false);
        });

        test('same reference returns true', () => {
            const eq = new Vec2EqualityComparer();
            const v = new Vec2(1, 2);
            expect(eq.equals(v, v)).toBe(true);
        });

        test('null handling', () => {
            const eq = new Vec2EqualityComparer();
            expect(eq.equals(null as any, null as any)).toBe(true); // same reference (===) short-circuits before null guard
            expect(eq.equals(new Vec2(1, 2), null as any)).toBe(false);
        });

        test('hash is deterministic', () => {
            const eq = new Vec2EqualityComparer();
            const a = new Vec2(1, 2);
            const b = new Vec2(1, 2);
            expect(eq.hash(a)).toBe(eq.hash(b));
        });

        test('hash of null is 0', () => {
            const eq = new Vec2EqualityComparer();
            expect(eq.hash(null as any)).toBe(0);
        });
    });

    // ─── Integration Tests ───────────────────────────────────────────────
    describe('Integration', () => {
        test('rotate then inverse-rotate = identity', () => {
            const v = new Vec2(3, 4);
            const original = v.clone();
            v.rotate(1.23);
            v.rotate(-1.23);
            expectVecClose(v, original, 1e-10);
        });

        test('normalize then dot with self = 1', () => {
            const v = new Vec2(3, 4);
            v.normalize();
            expectNumClose(v.dot(v), 1, 1e-10);
        });

        test('perpendicular dot product = 0', () => {
            const v = new Vec2(5, 7);
            const perp = Vec2.perpendicular(v);
            expectNumClose(Vec2.dot(v, perp), 0);
        });

        test('projection: v = proj + reject', () => {
            const v = { x: 3, y: 4 };
            const n = { x: 1, y: 0 };
            const projScalar = Vec2.dot(v, n) / Vec2.dot(n, n);
            const proj = { x: n.x * projScalar, y: n.y * projScalar };
            const reject = Vec2.subtract(v, proj);
            expectNumClose(Vec2.dot(proj, reject), 0, 1e-10);
        });
    });
});
