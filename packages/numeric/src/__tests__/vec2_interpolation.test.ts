import { lerp, lerpUnclamped, slerp, smoothStep, Vec2Tuple } from '../vec2';

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
