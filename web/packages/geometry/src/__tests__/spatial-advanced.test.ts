import { describe, expect, it } from 'vitest';
import {
    QuadTree,
    Octree,
    SpatialError,
    SpatialBoundsError,
    SpatialItemError,
    SpatialConfigError,
} from '@axrone/geometry';

// ─── QuadTree ───────────────────────────────────────────────────────────────

type Bounds2D = readonly [{ x: number; y: number }, { x: number; y: number }];

const qtBounds: Bounds2D = [
    { x: -50, y: -50 },
    { x: 50, y: 50 },
];

const qtItem = (id: string, x1: number, y1: number, x2: number, y2: number) =>
    ({
        id,
        bounds: [
            { x: x1, y: y1 },
            { x: x2, y: y2 },
        ] as Bounds2D,
    });

describe('QuadTree – advanced', () => {
    describe('raycast', () => {
        it('returns hits sorted by distance', () => {
            const qt = new QuadTree<string>(qtBounds);
            qt.insert(
                [
                    { x: 5, y: -1 },
                    { x: 6, y: 1 },
                ],
                'near'
            );
            qt.insert(
                [
                    { x: 20, y: -1 },
                    { x: 21, y: 1 },
                ],
                'far'
            );

            const hits = qt.raycast({ x: 0, y: 0 }, { x: 1, y: 0 });
            expect(hits.length).toBe(2);
            expect(hits[0]!.item).toBe('near');
            expect(hits[1]!.item).toBe('far');
        });

        it('returns empty array when ray misses all items', () => {
            const qt = new QuadTree<string>(qtBounds);
            qt.insert(
                [
                    { x: 10, y: 10 },
                    { x: 20, y: 20 },
                ],
                'item'
            );

            // Ray pointing away from the tree entirely
            const hits = qt.raycast({ x: -60, y: 0 }, { x: -1, y: 0 });
            expect(hits.length).toBe(0);
        });

        it('respects maxDistance', () => {
            const qt = new QuadTree<string>(qtBounds);
            qt.insert(
                [
                    { x: 5, y: -1 },
                    { x: 6, y: 1 },
                ],
                'near'
            );
            qt.insert(
                [
                    { x: 40, y: -1 },
                    { x: 41, y: 1 },
                ],
                'far'
            );

            const hits = qt.raycast({ x: 0, y: 0 }, { x: 1, y: 0 }, 10);
            expect(hits.length).toBe(1);
            expect(hits[0]!.item).toBe('near');
        });
    });

    describe('clear', () => {
        it('resets size to 0', () => {
            const qt = new QuadTree<string>(qtBounds);
            qt.insert(
                [
                    { x: 0, y: 0 },
                    { x: 1, y: 1 },
                ],
                'a'
            );
            qt.insert(
                [
                    { x: 2, y: 2 },
                    { x: 3, y: 3 },
                ],
                'b'
            );
            expect(qt.size).toBe(2);

            qt.clear();
            expect(qt.size).toBe(0);
        });
    });

    describe('rebuild', () => {
        it('preserves all items after rebuild', () => {
            const qt = new QuadTree<string>(qtBounds);
            qt.insert(
                [
                    { x: 0, y: 0 },
                    { x: 1, y: 1 },
                ],
                'a'
            );
            qt.insert(
                [
                    { x: 10, y: 10 },
                    { x: 11, y: 11 },
                ],
                'b'
            );

            qt.rebuild();
            expect(qt.size).toBe(2);

            const results = qt.query([
                { x: -1, y: -1 },
                { x: 2, y: 2 },
            ]);
            expect(results.map((r) => r.item)).toContain('a');
        });
    });

    describe('subdivision', () => {
        it('creates child nodes when exceeding maxItemsPerNode', () => {
            const qt = new QuadTree<string>(qtBounds, {
                maxItemsPerNode: 2,
                maxDepth: 4,
                minNodeSize: 0.1,
            });

            for (let i = 0; i < 5; i++) {
                const offset = i * 8;
                qt.insert(
                    [
                        { x: offset, y: offset },
                        { x: offset + 5, y: offset + 5 },
                    ],
                    `item-${i}`
                );
            }

            expect(qt.stats.nodeCount).toBeGreaterThan(1);
            expect(qt.stats.depth).toBeGreaterThan(0);
        });
    });

    describe('errors', () => {
        it('duplicate item throws SpatialItemError', () => {
            const qt = new QuadTree<string>(qtBounds);
            const b: Bounds2D = [
                { x: 0, y: 0 },
                { x: 1, y: 1 },
            ];
            qt.insert(b, 'dup');
            expect(() => qt.insert(b, 'dup')).toThrow(SpatialItemError);
        });

        it('out-of-bounds item throws SpatialBoundsError', () => {
            const qt = new QuadTree<string>(qtBounds);
            expect(() =>
                qt.insert(
                    [
                        { x: 200, y: 200 },
                        { x: 300, y: 300 },
                    ],
                    'oob'
                )
            ).toThrow(SpatialBoundsError);
        });

        it('inverted bounds throw SpatialBoundsError', () => {
            const qt = new QuadTree<string>(qtBounds);
            expect(() =>
                qt.insert(
                    [
                        { x: 10, y: 10 },
                        { x: 5, y: 5 },
                    ],
                    'inverted'
                )
            ).toThrow(SpatialBoundsError);
        });

        it('invalid config throws SpatialConfigError', () => {
            expect(
                () => new QuadTree<string>(qtBounds, { maxDepth: 0 })
            ).toThrow(SpatialConfigError);
            expect(
                () => new QuadTree<string>(qtBounds, { maxItemsPerNode: -1 })
            ).toThrow(SpatialConfigError);
            expect(
                () => new QuadTree<string>(qtBounds, { splitThreshold: 2 })
            ).toThrow(SpatialConfigError);
        });
    });
});

// ─── Octree ─────────────────────────────────────────────────────────────────

type Bounds3D = readonly [
    { x: number; y: number; z: number },
    { x: number; y: number; z: number },
];

const otBounds: Bounds3D = [
    { x: -50, y: -50, z: -50 },
    { x: 50, y: 50, z: 50 },
];

describe('Octree – advanced', () => {
    describe('raycast', () => {
        it('returns hits sorted by distance', () => {
            const ot = new Octree<string>(otBounds);
            ot.insert(
                [
                    { x: 5, y: -1, z: -1 },
                    { x: 6, y: 1, z: 1 },
                ],
                'near'
            );
            ot.insert(
                [
                    { x: 30, y: -1, z: -1 },
                    { x: 31, y: 1, z: 1 },
                ],
                'far'
            );

            const hits = ot.raycast({ x: 0, y: 0, z: 0 }, { x: 1, y: 0, z: 0 });
            expect(hits.length).toBe(2);
            expect(hits[0]!.item).toBe('near');
            expect(hits[1]!.item).toBe('far');
        });

        it('returns empty when ray misses', () => {
            const ot = new Octree<string>(otBounds);
            ot.insert(
                [
                    { x: 10, y: 10, z: 10 },
                    { x: 20, y: 20, z: 20 },
                ],
                'item'
            );

            // Ray pointing away from the tree entirely
            const hits = ot.raycast({ x: -60, y: 0, z: 0 }, { x: -1, y: 0, z: 0 });
            expect(hits.length).toBe(0);
        });

        it('respects maxDistance', () => {
            const ot = new Octree<string>(otBounds);
            ot.insert(
                [
                    { x: 5, y: -1, z: -1 },
                    { x: 6, y: 1, z: 1 },
                ],
                'near'
            );
            ot.insert(
                [
                    { x: 40, y: -1, z: -1 },
                    { x: 41, y: 1, z: 1 },
                ],
                'far'
            );

            const hits = ot.raycast({ x: 0, y: 0, z: 0 }, { x: 1, y: 0, z: 0 }, 10);
            expect(hits.length).toBe(1);
            expect(hits[0]!.item).toBe('near');
        });
    });

    describe('clear', () => {
        it('resets size to 0', () => {
            const ot = new Octree<string>(otBounds);
            ot.insert(
                [
                    { x: 0, y: 0, z: 0 },
                    { x: 1, y: 1, z: 1 },
                ],
                'a'
            );
            ot.clear();
            expect(ot.size).toBe(0);
        });
    });

    describe('rebuild', () => {
        it('preserves items after rebuild', () => {
            const ot = new Octree<string>(otBounds);
            ot.insert(
                [
                    { x: 0, y: 0, z: 0 },
                    { x: 1, y: 1, z: 1 },
                ],
                'a'
            );
            ot.insert(
                [
                    { x: 10, y: 10, z: 10 },
                    { x: 11, y: 11, z: 11 },
                ],
                'b'
            );

            ot.rebuild();
            expect(ot.size).toBe(2);

            const results = ot.query([
                { x: -1, y: -1, z: -1 },
                { x: 2, y: 2, z: 2 },
            ]);
            expect(results.map((r) => r.item)).toContain('a');
        });
    });

    describe('subdivision', () => {
        it('creates child nodes when exceeding maxItemsPerNode', () => {
            const ot = new Octree<string>(otBounds, {
                maxItemsPerNode: 2,
                maxDepth: 4,
                minNodeSize: 0.1,
            });

            for (let i = 0; i < 5; i++) {
                const offset = i * 8;
                ot.insert(
                    [
                        { x: offset, y: offset, z: offset },
                        { x: offset + 5, y: offset + 5, z: offset + 5 },
                    ],
                    `item-${i}`
                );
            }

            expect(ot.stats.nodeCount).toBeGreaterThan(1);
            expect(ot.stats.depth).toBeGreaterThan(0);
        });
    });

    describe('errors', () => {
        it('duplicate item throws SpatialItemError', () => {
            const ot = new Octree<string>(otBounds);
            const b: Bounds3D = [
                { x: 0, y: 0, z: 0 },
                { x: 1, y: 1, z: 1 },
            ];
            ot.insert(b, 'dup');
            expect(() => ot.insert(b, 'dup')).toThrow(SpatialItemError);
        });

        it('out-of-bounds item throws SpatialBoundsError', () => {
            const ot = new Octree<string>(otBounds);
            expect(() =>
                ot.insert(
                    [
                        { x: 200, y: 200, z: 200 },
                        { x: 300, y: 300, z: 300 },
                    ],
                    'oob'
                )
            ).toThrow(SpatialBoundsError);
        });

        it('inverted bounds throw SpatialBoundsError', () => {
            const ot = new Octree<string>(otBounds);
            expect(() =>
                ot.insert(
                    [
                        { x: 10, y: 10, z: 10 },
                        { x: 5, y: 5, z: 5 },
                    ],
                    'inverted'
                )
            ).toThrow(SpatialBoundsError);
        });

        it('invalid config throws SpatialConfigError', () => {
            expect(
                () => new Octree<string>(otBounds, { maxDepth: 0 })
            ).toThrow(SpatialConfigError);
            expect(
                () => new Octree<string>(otBounds, { minNodeSize: -5 })
            ).toThrow(SpatialConfigError);
        });
    });
});

// ─── Spatial error hierarchy ────────────────────────────────────────────────

describe('Spatial error hierarchy', () => {
    it('SpatialError is instanceof Error', () => {
        const err = new SpatialError('test');
        expect(err).toBeInstanceOf(Error);
        expect(err).toBeInstanceOf(SpatialError);
        expect(err.name).toBe('SpatialError');
    });

    it('SpatialBoundsError is instanceof SpatialError', () => {
        const err = new SpatialBoundsError('bounds', { foo: 1 });
        expect(err).toBeInstanceOf(Error);
        expect(err).toBeInstanceOf(SpatialError);
        expect(err).toBeInstanceOf(SpatialBoundsError);
        expect(err.name).toBe('SpatialBoundsError');
        expect(err.context).toEqual({ foo: 1 });
    });

    it('SpatialItemError is instanceof SpatialError', () => {
        const err = new SpatialItemError('item', { bar: 2 });
        expect(err).toBeInstanceOf(SpatialError);
        expect(err).toBeInstanceOf(SpatialItemError);
        expect(err.name).toBe('SpatialItemError');
        expect(err.context).toEqual({ bar: 2 });
    });

    it('SpatialConfigError is instanceof SpatialError', () => {
        const err = new SpatialConfigError('config');
        expect(err).toBeInstanceOf(SpatialError);
        expect(err).toBeInstanceOf(SpatialConfigError);
        expect(err.name).toBe('SpatialConfigError');
    });

    it('SpatialBoundsError is NOT instanceof SpatialItemError', () => {
        const err = new SpatialBoundsError('bounds');
        expect(err).not.toBeInstanceOf(SpatialItemError);
    });
});
