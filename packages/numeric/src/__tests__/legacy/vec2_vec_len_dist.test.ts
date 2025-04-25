import {
    create,
    length,
    distance,
    distanceSq,
    EPSILON,
    fastDistance,
    fastLength,
    lengthSq,
    normalize,
    normalizeFast,
    ReadonlyVec2,
    Vec2,
    ZERO,
} from '../../vec2_legacy';

describe('Vector Length, Normalization, and Distance', () => {
    const v34: ReadonlyVec2 = Object.freeze({ x: 3, y: 4 }); // length 5
    const vNeg12: ReadonlyVec2 = Object.freeze({ x: -1, y: 2 }); // length sqrt(5) ~ 2.236
    const vZero = ZERO; // {0,0}

    // --- Length ---
    test('lengthSq should return the squared magnitude', () => {
        expect(lengthSq(v34)).toBeCloseTo(25);
        expect(lengthSq(vNeg12)).toBeCloseTo(5); // (-1)^2 + 2^2 = 1 + 4 = 5
        expect(lengthSq(vZero)).toBeCloseTo(0);
    });

    test('length should return the magnitude', () => {
        expect(length(v34)).toBeCloseTo(5);
        expect(length(vNeg12)).toBeCloseTo(Math.sqrt(5));
        expect(length(vZero)).toBeCloseTo(0);
    });

    test('fastLength should return an approximation of the magnitude', () => {
        const len34 = length(v34);
        const fastLen34 = fastLength(v34); // 4 + 0.3*3 = 4.9
        expect(fastLen34).toBeCloseTo(4.9);
        expect(Math.abs(len34 - fastLen34)).toBeLessThan(len34 * 0.04); // Max ~%4 error check

        const lenNeg12 = length(vNeg12); // sqrt(5) ~ 2.236
        const fastLenNeg12 = fastLength(vNeg12); // max(1,2)+0.3*min(1,2) = 2 + 0.3*1 = 2.3
        expect(fastLenNeg12).toBeCloseTo(2.3);
        expect(Math.abs(lenNeg12 - fastLenNeg12)).toBeLessThan(lenNeg12 * 0.04);
    });

    // --- Normalization ---
    let out: Vec2;
    beforeEach(() => {
        out = create();
    });

    test('normalize should produce a unit vector', () => {
        normalize(out, v34);
        expect(out.x).toBeCloseTo(3 / 5);
        expect(out.y).toBeCloseTo(4 / 5);
        expect(length(out)).toBeCloseTo(1.0, 6); // chk if length is close to 1

        normalize(out, vNeg12);
        expect(out.x).toBeCloseTo(-1 / Math.sqrt(5));
        expect(out.y).toBeCloseTo(2 / Math.sqrt(5));
        expect(length(out)).toBeCloseTo(1.0, 6);
    });

    test('normalize should return zero vector for a zero vector input', () => {
        normalize(out, vZero);
        expect(out).toEqual({ x: 0, y: 0 });
        normalize(out, create(EPSILON / 2, -EPSILON / 2)); // Near zero
        expect(out).toEqual({ x: 0, y: 0 });
    });

    test('normalizeFast should produce an approximately unit vector', () => {
        normalizeFast(out, v34);
        // length should be very close to 1
        expect(length(out)).toBeCloseTo(1.0, 3); // low precision for fast approx

        normalizeFast(out, create(100, -200)); // with larger numbers
        expect(length(out)).toBeCloseTo(1.0, 3);
    });

    test('normalizeFast should return zero vector for a zero vector input', () => {
        normalizeFast(out, vZero);
        expect(out).toEqual({ x: 0, y: 0 });
        normalizeFast(out, create(EPSILON / 2, -EPSILON / 2)); // Near zero
        expect(out).toEqual({ x: 0, y: 0 });
    });

    // --- Distance ---
    const p1: Vec2 = { x: 1, y: 2 };
    const p2: Vec2 = { x: 4, y: 6 }; // Diff = (3, 4), dist = 5
    const p3: ReadonlyVec2 = { x: 1, y: 2 }; // Same as p1

    test('distanceSq should return the squared distance between points', () => {
        expect(distanceSq(p1, p2)).toBeCloseTo(25);
        expect(distanceSq(p2, p1)).toBeCloseTo(25);
        expect(distanceSq(p1, p3)).toBeCloseTo(0);
        expect(distanceSq(p1, vZero)).toBeCloseTo(lengthSq(p1));
    });

    test('distance should return the distance between points', () => {
        expect(distance(p1, p2)).toBeCloseTo(5);
        expect(distance(p2, p1)).toBeCloseTo(5);
        expect(distance(p1, p3)).toBeCloseTo(0);
        expect(distance(p1, vZero)).toBeCloseTo(length(p1));
    });

    test('fastDistance should return an approximation of the distance', () => {
        const distP1P2 = distance(p1, p2); // 5
        const fastDistP1P2 = fastDistance(p1, p2); // Diff (3,4) -> fastLength = 4.9
        expect(fastDistP1P2).toBeCloseTo(4.9);
        expect(Math.abs(distP1P2 - fastDistP1P2)).toBeLessThan(distP1P2 * 0.04);

        const fastDistP1P3 = fastDistance(p1, p3); // Diff (0,0) -> 0
        expect(fastDistP1P3).toBeCloseTo(0);
    });
});
