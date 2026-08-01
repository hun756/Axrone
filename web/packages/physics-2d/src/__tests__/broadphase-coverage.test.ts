import { describe, it, expect } from 'vitest';
import { DynamicAABBTree2D } from '@axrone/physics-2d';
import { AABB2D } from '@axrone/geometry';

describe('DynamicAABBTree2D — move integrity (regression for _removeLeaf)', () => {
    it('keeps a moved proxy queryable after remove + reinsert', () => {
        const tree = new DynamicAABBTree2D(64);
        const id = tree.createProxy(new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 }), { tag: 'probe' });

        const moved = tree.moveProxy(id, new AABB2D({ x: 50, y: 50 }, { x: 51, y: 51 }), { x: 50, y: 50 });
        expect(moved).toBe(true);

        const results: number[] = [];
        tree.query((pid) => {
            results.push(pid);
            return true;
        }, new AABB2D({ x: 50.5, y: 50.5 }, { x: 51.5, y: 51.5 }));

        expect(results).toContain(id);
    });

    it('preserves queryability for every proxy after heavy move churn', () => {
        const tree = new DynamicAABBTree2D(64);
        const ids: number[] = [];
        for (let i = 0; i < 20; i++) {
            ids.push(tree.createProxy(new AABB2D({ x: i, y: i }, { x: i + 1, y: i + 1 }), { i }));
        }

        for (let step = 0; step < 5; step++) {
            for (let i = 0; i < ids.length; i++) {
                const o = (i + step * 3) % 30;
                tree.moveProxy(ids[i], new AABB2D({ x: o, y: o }, { x: o + 1, y: o + 1 }), { x: 1, y: 1 });
            }
        }

        for (let i = 0; i < ids.length; i++) {
            const o = (i + 4 * 3) % 30;
            const results: number[] = [];
            tree.query(
                (pid) => {
                    results.push(pid);
                    return true;
                },
                new AABB2D({ x: o + 0.5, y: o + 0.5 }, { x: o + 0.6, y: o + 0.6 })
            );
            expect(results).toContain(ids[i]);
        }
    });

    it('keeps ancestor AABBs consistent after a move (no corruption from leaf removal)', () => {
        const tree = new DynamicAABBTree2D(64);
        const probe = tree.createProxy(new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 }), { probe: true });
        tree.createProxy(new AABB2D({ x: 2, y: 2 }, { x: 3, y: 3 }), { other: true });

        tree.moveProxy(probe, new AABB2D({ x: 20, y: 20 }, { x: 21, y: 21 }), { x: 20, y: 20 });

        const results: number[] = [];
        tree.query(
            (pid) => {
                results.push(pid);
                return true;
            },
            new AABB2D({ x: 20.5, y: 20.5 }, { x: 21.5, y: 21.5 })
        );
        expect(results).toContain(probe);
    });
});
