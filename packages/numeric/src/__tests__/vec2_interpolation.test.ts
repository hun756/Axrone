import { lerp, Vec2Tuple } from '../vec2';

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
