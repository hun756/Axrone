import { cubicBezier, lerp, lerpUnclamped, slerp, smootherStep, smoothStep, Vec2Tuple } from '../vec2';

describe('lerp function', () => {
    it('should return the starting point when t = 0', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        const result = lerp(out, a, b, 0);
        expect(result.x).toBeCloseTo(10);
        expect(result.y).toBeCloseTo(20);
        expect(result).toBe(out); // Reference check
    });

    it('should return the end point when t = 1', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        const result = lerp(out, a, b, 1);
        expect(result.x).toBeCloseTo(30);
        expect(result.y).toBeCloseTo(40);
    });

    it('should return the midpoint when t = 0.5', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        const result = lerp(out, a, b, 0.5);
        expect(result.x).toBeCloseTo(20);
        expect(result.y).toBeCloseTo(30);
    });

    it('should clamp values of t < 0 to 0', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        const result = lerp(out, a, b, -0.5);
        expect(result.x).toBeCloseTo(10);
        expect(result.y).toBeCloseTo(20);
    });

    it('should clamp values of t > 1 to 1', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        const result = lerp(out, a, b, 1.5);
        expect(result.x).toBeCloseTo(30);
        expect(result.y).toBeCloseTo(40);
    });

    it('should process vectors in array format correctly', () => {
        const out = { x: 0, y: 0 };
        const a = [10, 20] as Vec2Tuple;
        const b = [30, 40] as Vec2Tuple;
        const result = lerp(out, a, b, 0.5);
        expect(result.x).toBeCloseTo(20);
        expect(result.y).toBeCloseTo(30);
    });

    it('should work correctly with zero vectors', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 0, y: 0 };
        const b = { x: 0, y: 0 };
        const result = lerp(out, a, b, 0.5);
        expect(result.x).toBe(0);
        expect(result.y).toBe(0);
    });

    it('should work correctly with negative vectors', () => {
        const out = { x: 0, y: 0 };
        const a = { x: -10, y: -20 };
        const b = { x: -30, y: -40 };
        const result = lerp(out, a, b, 0.5);
        expect(result.x).toBeCloseTo(-20);
        expect(result.y).toBeCloseTo(-30);
    });

    it('should be able to reuse the same output object', () => {
        const out = { x: 99, y: 99 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        lerp(out, a, b, 0.5);
        const result = lerp(out, a, b, 0.7);
        expect(result.x).toBeCloseTo(24);
        expect(result.y).toBeCloseTo(34);
    });
});

describe('lerpUnclamped function', () => {
    it('should return the starting point when t = 0', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        const result = lerpUnclamped(out, a, b, 0);
        expect(result.x).toBeCloseTo(10);
        expect(result.y).toBeCloseTo(20);
    });

    it('should return the end point when t = 1', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        const result = lerpUnclamped(out, a, b, 1);
        expect(result.x).toBeCloseTo(30);
        expect(result.y).toBeCloseTo(40);
    });

    it('should extrapolate for t > 1', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        const result = lerpUnclamped(out, a, b, 2);
        expect(result.x).toBeCloseTo(50);
        expect(result.y).toBeCloseTo(60);
    });

    it('should extrapolate for t < 0', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        const result = lerpUnclamped(out, a, b, -1);
        expect(result.x).toBeCloseTo(-10);
        expect(result.y).toBeCloseTo(0);
    });

    it('should work correctly for mixed vector formats', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = [30, 40] as Vec2Tuple;
        const result = lerpUnclamped(out, a, b, 0.5);
        expect(result.x).toBeCloseTo(20);
        expect(result.y).toBeCloseTo(30);
    });

    it('should work correctly for extreme t values', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        const result = lerpUnclamped(out, a, b, 1000);
        expect(result.x).toBeCloseTo(20010);
        expect(result.y).toBeCloseTo(20020);
    });
});

describe('slerp function', () => {
    it('should return the starting point when t = 0', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 1, y: 0 }; // Unit vector, 0°
        const b = { x: 0, y: 1 }; // Unit vector, 90°
        const result = slerp(out, a, b, 0);
        expect(result.x).toBeCloseTo(1);
        expect(result.y).toBeCloseTo(0);
    });

    it('should return the end point when t = 1', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 1, y: 0 }; // Unit vector, 0°
        const b = { x: 0, y: 1 }; // Unit vector, 90°
        const result = slerp(out, a, b, 1);
        expect(result.x).toBeCloseTo(0);
        expect(result.y).toBeCloseTo(1);
    });

    it('should return the correct angular point when t = 0.5', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 1, y: 0 }; // Unit vector, 0°
        const b = { x: 0, y: 1 }; // Unit vector, 90°
        const result = slerp(out, a, b, 0.5);
        // Should be at 45° angle, like (1/√2, 1/√2)
        expect(result.x).toBeCloseTo(Math.sqrt(0.5));
        expect(result.y).toBeCloseTo(Math.sqrt(0.5));

        expect(Math.sqrt(result.x * result.x + result.y * result.y)).toBeCloseTo(1);
    });

    it('should clamp values of t < 0 to 0', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 1, y: 0 };
        const b = { x: 0, y: 1 };
        const result = slerp(out, a, b, -0.5);
        expect(result.x).toBeCloseTo(1);
        expect(result.y).toBeCloseTo(0);
    });

    it('should clamp values of t > 1 to 1', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 1, y: 0 };
        const b = { x: 0, y: 1 };
        const result = slerp(out, a, b, 1.5);
        expect(result.x).toBeCloseTo(0);
        expect(result.y).toBeCloseTo(1);
    });

    it('should choose the correct direction for 180° angle vectors', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 1, y: 0 }; // 0°
        const b = { x: -1, y: 0 }; // 180°
        const result = slerp(out, a, b, 0.5);

        // Should be at 90° angle, either (0,1) or (0,-1)
        const length = Math.sqrt(result.x * result.x + result.y * result.y);
        expect(length).toBeCloseTo(1);
        expect(Math.abs(result.x)).toBeCloseTo(0);
        expect(Math.abs(result.y)).toBeCloseTo(1);
    });

    it('should correctly interpolate length for vectors of different magnitudes', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 1, y: 0 }; // Len 1
        const b = { x: 0, y: 2 }; // Len 2
        const result = slerp(out, a, b, 0.5);

        // Angle 90°, length should be 1.5
        expect(Math.sqrt(result.x * result.x + result.y * result.y)).toBeCloseTo(1.5);
    });

    it('should return a safe result for zero vectors', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 0, y: 0 };
        const b = { x: 1, y: 0 };
        const result = slerp(out, a, b, 0.5);

        expect(result.x).toBeCloseTo(0.5);
        expect(result.y).toBeCloseTo(0);
    });

    it('should correctly normalize angular difference (>PI case)', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 1, y: 0 }; // 0°
        const b = { x: -0.5, y: -0.866 }; // 240°
        const result = slerp(out, a, b, 0.5);

        // Expect interpolation in -60° direction (shortest path)
        const angle = Math.atan2(result.y, result.x);
        expect(angle < 0).toBe(true); // Should interpolate clockwise =)
    });
});

describe('smoothStep function', () => {
    it('should return the starting point when t = 0', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        const result = smoothStep(out, a, b, 0);
        expect(result.x).toBeCloseTo(10);
        expect(result.y).toBeCloseTo(20);
    });

    it('should return the end point when t = 1', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        const result = smoothStep(out, a, b, 1);
        expect(result.x).toBeCloseTo(30);
        expect(result.y).toBeCloseTo(40);
    });

    it('should perform correct interpolation when t = 0.5', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        const result = smoothStep(out, a, b, 0.5);
        // smoothstep value for 0.5: 3(0.5)² - 2(0.5)³ = 0.75 - 0.25 = 0.5

        expect(result.x).toBeCloseTo(20);
        expect(result.y).toBeCloseTo(30);
    });

    it('should perform smooth interpolation when t = 0.25', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        const result = smoothStep(out, a, b, 0.25);
        // smoothstep value for 0.25: 3(0.25)² - 2(0.25)³ = 0.1875 - 0.03125 = 0.15625
        const t2 = 0.15625;
        expect(result.x).toBeCloseTo(10 + t2 * 20);
        expect(result.y).toBeCloseTo(20 + t2 * 20);
    });

    it('should perform smooth interpolation when t = 0.75', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        const result = smoothStep(out, a, b, 0.75);

        // smoothstep value for 0.75: 3(0.75)² - 2(0.75)³ = 1.6875 - 0.84375 = 0.84375
        const t2 = 0.84375;
        expect(result.x).toBeCloseTo(10 + t2 * 20);
        expect(result.y).toBeCloseTo(20 + t2 * 20);
    });

    it('should clamp values of t < 0 to 0', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        const result = smoothStep(out, a, b, -0.5);
        expect(result.x).toBeCloseTo(10);
        expect(result.y).toBeCloseTo(20);
    });

    it('should clamp values of t > 1 to 1', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        const result = smoothStep(out, a, b, 1.5);
        expect(result.x).toBeCloseTo(30);
        expect(result.y).toBeCloseTo(40);
    });

    it('should verify the correct application of the smoothStep polynomial', () => {
        // Calculate intermediate values for polynomial 3t² - 2t³
        const testPoints = [0.1, 0.2, 0.3, 0.4, 0.6, 0.7, 0.8, 0.9];
        const out = { x: 0, y: 0 };
        const a = { x: 0, y: 0 };
        const b = { x: 1, y: 1 };

        for (const t of testPoints) {
            const smoothT = t * t * (3 - 2 * t);
            const result = smoothStep(out, a, b, t);
            expect(result.x).toBeCloseTo(smoothT);
            expect(result.y).toBeCloseTo(smoothT);
        }
    });
});

describe('smootherStep function', () => {
    it('should return the starting point when t = 0', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        const result = smootherStep(out, a, b, 0);
        expect(result.x).toBeCloseTo(10);
        expect(result.y).toBeCloseTo(20);
    });

    it('should return the end point when t = 1', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        const result = smootherStep(out, a, b, 1);
        expect(result.x).toBeCloseTo(30);
        expect(result.y).toBeCloseTo(40);
    });

    it('should perform correct interpolation when t = 0.5', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        const result = smootherStep(out, a, b, 0.5);

        // smootherstep value for 0.5: 6(0.5)⁵ - 15(0.5)⁴ + 10(0.5)³ = 0.5
        expect(result.x).toBeCloseTo(20);
        expect(result.y).toBeCloseTo(30);
    });

    it('should perform smoother interpolation when t = 0.25', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };

        // calculated polynomial value for 0.25
        const t = 0.25;
        const t3 = t * t * t;
        const t4 = t3 * t;
        const t5 = t4 * t;
        const smootherT = 6 * t5 - 15 * t4 + 10 * t3;

        const result = smootherStep(out, a, b, 0.25);
        expect(result.x).toBeCloseTo(10 + smootherT * 20);
        expect(result.y).toBeCloseTo(20 + smootherT * 20);
    });

    it('should clamp values of t < 0 to 0', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        const result = smootherStep(out, a, b, -0.5);
        expect(result.x).toBeCloseTo(10);
        expect(result.y).toBeCloseTo(20);
    });

    it('should clamp values of t > 1 to 1', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const b = { x: 30, y: 40 };
        const result = smootherStep(out, a, b, 1.5);
        expect(result.x).toBeCloseTo(30);
        expect(result.y).toBeCloseTo(40);
    });

    it('should test the differences between smootherStep and smoothStep', () => {
        const out1 = { x: 0, y: 0 };
        const out2 = { x: 0, y: 0 };
        const a = { x: 0, y: 0 };
        const b = { x: 1, y: 1 };

        // At 0.3, smootherStep should provide a smoother transition
        const smoothResult = smoothStep(out1, a, b, 0.3);
        const smootherResult = smootherStep(out2, a, b, 0.3);

        // At 0.3, the smootherStep value should be less than smoothStep
        expect(smootherResult.x).toBeLessThan(smoothResult.x);

        // Conversely, at 0.7, the smootherStep value should be greater
        const smoothResult2 = smoothStep(out1, a, b, 0.7);
        const smootherResult2 = smootherStep(out2, a, b, 0.7);
        expect(smootherResult2.x).toBeGreaterThan(smoothResult2.x);
    });
});

describe('cubicBezier function', () => {
    it('should return the starting point when t = 0', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const c1 = { x: 15, y: 25 };
        const c2 = { x: 25, y: 35 };
        const b = { x: 30, y: 40 };
        const result = cubicBezier(out, a, c1, c2, b, 0);
        expect(result.x).toBeCloseTo(10);
        expect(result.y).toBeCloseTo(20);
    });

    it('should return the end point when t = 1', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const c1 = { x: 15, y: 25 };
        const c2 = { x: 25, y: 35 };
        const b = { x: 30, y: 40 };
        const result = cubicBezier(out, a, c1, c2, b, 1);
        expect(result.x).toBeCloseTo(30);
        expect(result.y).toBeCloseTo(40);
    });

    it('should return the correct Bezier value when t = 0.5', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const c1 = { x: 15, y: 25 };
        const c2 = { x: 25, y: 35 };
        const b = { x: 30, y: 40 };

        // Formula for t = 0.5:
        // B(0.5) = (1-0.5)³a + 3(1-0.5)²(0.5)c1 + 3(1-0.5)(0.5)²c2 + (0.5)³b
        //        = 0.125a + 0.375c1 + 0.375c2 + 0.125b
        const expected_x = 0.125 * 10 + 0.375 * 15 + 0.375 * 25 + 0.125 * 30;
        const expected_y = 0.125 * 20 + 0.375 * 25 + 0.375 * 35 + 0.125 * 40;

        const result = cubicBezier(out, a, c1, c2, b, 0.5);
        expect(result.x).toBeCloseTo(expected_x);
        expect(result.y).toBeCloseTo(expected_y);
    });

    it('should clamp values of t < 0 to 0', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const c1 = { x: 15, y: 25 };
        const c2 = { x: 25, y: 35 };
        const b = { x: 30, y: 40 };
        const result = cubicBezier(out, a, c1, c2, b, -0.5);
        expect(result.x).toBeCloseTo(10);
        expect(result.y).toBeCloseTo(20);
    });

    it('should clamp values of t > 1 to 1', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const c1 = { x: 15, y: 25 };
        const c2 = { x: 25, y: 35 };
        const b = { x: 30, y: 40 };
        const result = cubicBezier(out, a, c1, c2, b, 1.5);
        expect(result.x).toBeCloseTo(30);
        expect(result.y).toBeCloseTo(40);
    });

    it('should perform linear interpolation when control points are aligned', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const c1 = { x: 10 + (30 - 10) / 3, y: 20 + (40 - 20) / 3 };
        const c2 = { x: 10 + (2 * (30 - 10)) / 3, y: 20 + (2 * (40 - 20)) / 3 };
        const b = { x: 30, y: 40 };

        const result = cubicBezier(out, a, c1, c2, b, 0.5);
        expect(result.x).toBeCloseTo(20);
        expect(result.y).toBeCloseTo(30);
    });

    it('should process vectors in different formats correctly', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const c1 = [15, 25] as Vec2Tuple;
        const c2 = { x: 25, y: 35 };
        const b = [30, 40] as Vec2Tuple;

        const result = cubicBezier(out, a, c1, c2, b, 0.5);
        expect(result.x).toBeGreaterThan(10);
        expect(result.x).toBeLessThan(30);
    });

    it('should be consistent for high-precision t values', () => {
        const out = { x: 0, y: 0 };
        const a = { x: 10, y: 20 };
        const c1 = { x: 15, y: 25 };
        const c2 = { x: 25, y: 35 };
        const b = { x: 30, y: 40 };

        const t1 = 0.3333333;
        const t2 = 0.3333334;

        const result1 = cubicBezier(out, a, c1, c2, b, t1);
        const result2 = cubicBezier(out, a, c1, c2, b, t2);

        expect(Math.abs(result1.x - result2.x)).toBeLessThan(1e-6);
        expect(Math.abs(result1.y - result2.y)).toBeLessThan(1e-6);
    });
});
