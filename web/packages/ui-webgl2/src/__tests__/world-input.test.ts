import { describe, expect, it } from 'vitest';
import { intersectRayWithUIQuad, toColumnMajorMatrix } from '../world-input';

/** Column-major identity with an optional translation. */
const identity = (tx = 0, ty = 0, tz = 0): Float32Array =>
    new Float32Array([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, tx, ty, tz, 1]);

/** Column-major rotation about the Y axis. */
const rotationY = (radians: number): Float32Array => {
    const c = Math.cos(radians);
    const s = Math.sin(radians);
    return new Float32Array([c, 0, -s, 0, 0, 1, 0, 0, s, 0, c, 0, 0, 0, 0, 1]);
};

const scaled = (sx: number, sy: number): Float32Array =>
    new Float32Array([sx, 0, 0, 0, 0, sy, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);

describe('intersectRayWithUIQuad', () => {
    it('maps a centered hit to the middle of the quad', () => {
        // Quad at the origin facing +Z, ray shot from +Z straight back at it.
        const hit = intersectRayWithUIQuad(
            { origin: [0, 0, 5], direction: [0, 0, -1] },
            identity(),
            2,
            1
        );

        expect(hit).not.toBeNull();
        expect(hit!.u).toBeCloseTo(0.5);
        expect(hit!.v).toBeCloseTo(0.5);
        expect(hit!.distance).toBeCloseTo(5);
    });

    it('reports canvas-style coordinates with a top-left origin', () => {
        // Hit the top-left region: local -X, +Y.
        const hit = intersectRayWithUIQuad(
            { origin: [-0.5, 0.25, 3], direction: [0, 0, -1] },
            identity(),
            2,
            1
        );

        expect(hit).not.toBeNull();
        expect(hit!.u).toBeCloseTo(0.25); // 0.5 units left of center on a 2-wide quad
        expect(hit!.v).toBeCloseTo(0.25); // local +Y maps to a smaller v
    });

    it('misses when the ray passes outside the quad bounds', () => {
        expect(
            intersectRayWithUIQuad({ origin: [5, 0, 5], direction: [0, 0, -1] }, identity(), 2, 1)
        ).toBeNull();
    });

    it('misses when the ray runs parallel to the quad plane', () => {
        expect(
            intersectRayWithUIQuad({ origin: [0, 0, 5], direction: [1, 0, 0] }, identity(), 2, 1)
        ).toBeNull();
    });

    it('misses when the quad sits behind the ray origin', () => {
        expect(
            intersectRayWithUIQuad({ origin: [0, 0, 5], direction: [0, 0, 1] }, identity(), 2, 1)
        ).toBeNull();
    });

    it('honors the entity translation', () => {
        const hit = intersectRayWithUIQuad(
            { origin: [3, 1, 5], direction: [0, 0, -1] },
            identity(3, 1, 0),
            2,
            1
        );

        expect(hit).not.toBeNull();
        expect(hit!.u).toBeCloseTo(0.5);
        expect(hit!.v).toBeCloseTo(0.5);
    });

    it('handles a rotated quad', () => {
        // Rotated 90 degrees about Y: the quad now faces +X.
        const hit = intersectRayWithUIQuad(
            { origin: [5, 0, 0], direction: [-1, 0, 0] },
            rotationY(Math.PI / 2),
            2,
            1
        );

        expect(hit).not.toBeNull();
        expect(hit!.u).toBeCloseTo(0.5);
        expect(hit!.v).toBeCloseTo(0.5);
    });

    it('accounts for entity scale when projecting onto the quad', () => {
        // A 2x scale on X doubles the world span of the same local extent, so a
        // point 1 world unit right of center lands halfway to the right edge.
        const hit = intersectRayWithUIQuad(
            { origin: [1, 0, 5], direction: [0, 0, -1] },
            scaled(2, 1),
            2,
            1
        );

        expect(hit).not.toBeNull();
        expect(hit!.u).toBeCloseTo(0.75);
        expect(hit!.v).toBeCloseTo(0.5);
    });

    it('rejects degenerate quad sizes', () => {
        expect(
            intersectRayWithUIQuad({ origin: [0, 0, 5], direction: [0, 0, -1] }, identity(), 0, 1)
        ).toBeNull();
    });
});

describe('toColumnMajorMatrix', () => {
    it('transposes an Axrone row-major matrix into GL column-major order', () => {
        // Axrone Mat4 keeps translation at 3/7/11 (row-major).
        const rowMajor = {
            data: [1, 0, 0, 7, 0, 1, 0, 8, 0, 0, 1, 9, 0, 0, 0, 1],
        };

        const columnMajor = toColumnMajorMatrix(rowMajor);

        // GL expects translation at 12/13/14.
        expect(columnMajor[12]).toBe(7);
        expect(columnMajor[13]).toBe(8);
        expect(columnMajor[14]).toBe(9);
        expect(columnMajor[15]).toBe(1);
    });

    it('accepts a bare array and matches the transpose of the input', () => {
        const source = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

        expect([...toColumnMajorMatrix(source)]).toEqual([
            1, 5, 9, 13, 2, 6, 10, 14, 3, 7, 11, 15, 4, 8, 12, 16,
        ]);
    });

    it('produces a matrix the quad intersection understands', () => {
        // Row-major translation of (3, 1, 0) must land the hit dead center.
        const rowMajor = { data: [1, 0, 0, 3, 0, 1, 0, 1, 0, 0, 1, 0, 0, 0, 0, 1] };

        const hit = intersectRayWithUIQuad(
            { origin: [3, 1, 5], direction: [0, 0, -1] },
            toColumnMajorMatrix(rowMajor),
            2,
            1
        );

        expect(hit).not.toBeNull();
        expect(hit!.u).toBeCloseTo(0.5);
        expect(hit!.v).toBeCloseTo(0.5);
    });
});
