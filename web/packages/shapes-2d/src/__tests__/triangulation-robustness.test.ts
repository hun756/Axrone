import { describe, expect, it } from 'vitest';
import { polygonAbsoluteArea, triangulateEarClipping } from '../common';

const triangleAreaSum = (flat: Float32Array, indices: Uint16Array | Uint32Array): number => {
    let area = 0;
    for (let i = 0; i < indices.length; i += 3) {
        const ax = flat[indices[i]! * 2]!;
        const ay = flat[indices[i]! * 2 + 1]!;
        const bx = flat[indices[i + 1]! * 2]!;
        const by = flat[indices[i + 1]! * 2 + 1]!;
        const cx = flat[indices[i + 2]! * 2]!;
        const cy = flat[indices[i + 2]! * 2 + 1]!;
        area += (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by)) * 0.5;
    }
    return area;
};

const regularPolygon = (count: number, radius = 100): Float32Array => {
    const flat = new Float32Array(count * 2);
    for (let i = 0; i < count; i++) {
        const angle = (i / count) * Math.PI * 2;
        flat[i * 2] = Math.cos(angle) * radius;
        flat[i * 2 + 1] = Math.sin(angle) * radius;
    }
    return flat;
};

describe('triangulateEarClipping vertex remapping', () => {
    it('triangulates a counterclockwise polygon into n-2 triangles with preserved area', () => {
        const flat = regularPolygon(64);
        const indices = triangulateEarClipping(flat);
        expect(indices.length).toBe((64 - 2) * 3);
        expect(Math.abs(triangleAreaSum(flat, indices))).toBeCloseTo(
            polygonAbsoluteArea(flat),
            4
        );
    });

    it('triangulates a concave polygon exactly', () => {
        const lShape = new Float32Array([0, 0, 10, 0, 10, 10, 6, 10, 6, 4, 0, 4]);
        const indices = triangulateEarClipping(lShape);
        expect(indices.length).toBe((6 - 2) * 3);
        expect(Math.abs(triangleAreaSum(lShape, indices))).toBeCloseTo(64, 4);
    });

    it('references only valid original vertex indices', () => {
        const flat = regularPolygon(32);
        const indices = triangulateEarClipping(flat);
        for (let i = 0; i < indices.length; i++) {
            expect(indices[i]!).toBeLessThan(32);
            expect(indices[i]!).toBeGreaterThanOrEqual(0);
        }
    });
});
