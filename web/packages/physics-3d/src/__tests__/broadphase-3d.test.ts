import { describe, it, expect, beforeEach } from 'vitest';
import { AABB3D } from '@axrone/geometry';

// DynamicAABBTree3D is not exported from the package index — import directly.
import { DynamicAABBTree3D } from '../core/broadphase-3d';

function box(cx: number, cy: number, cz: number, hx: number, hy: number, hz: number): AABB3D {
    return new AABB3D({ x: cx - hx, y: cy - hy, z: cz - hz }, { x: cx + hx, y: cy + hy, z: cz + hz });
}

describe('DynamicAABBTree3D', () => {
    let tree: DynamicAABBTree3D<string>;

    beforeEach(() => {
        tree = new DynamicAABBTree3D<string>(16);
    });

    describe('creation', () => {
        it('starts empty', () => {
            expect(tree.nodeCount).toBe(0);
            expect(tree.getHeight()).toBe(0);
        });
    });

    describe('createProxy', () => {
        it('returns a valid proxy id and increments nodeCount', () => {
            const id = tree.createProxy(box(0, 0, 0, 1, 1, 1), 'a');
            expect(id).toBeGreaterThanOrEqual(0);
            expect(tree.nodeCount).toBe(1);
        });

        it('stores user data retrievable via getUserData', () => {
            const id = tree.createProxy(box(0, 0, 0, 1, 1, 1), 'hello');
            expect(tree.getUserData(id)).toBe('hello');
        });

        it('returns a fat AABB that encloses the original', () => {
            const tight = box(0, 0, 0, 1, 1, 1);
            const id = tree.createProxy(tight, 'fat');
            const fat = tree.getAABB(id);
            expect(fat.min.x).toBeLessThanOrEqual(tight.min.x);
            expect(fat.max.x).toBeGreaterThanOrEqual(tight.max.x);
            expect(fat.min.y).toBeLessThanOrEqual(tight.min.y);
            expect(fat.max.y).toBeGreaterThanOrEqual(tight.max.y);
            expect(fat.min.z).toBeLessThanOrEqual(tight.min.z);
            expect(fat.max.z).toBeGreaterThanOrEqual(tight.max.z);
        });

        it('creates multiple proxies with unique ids', () => {
            const ids = new Set<number>();
            for (let i = 0; i < 10; i++) {
                ids.add(tree.createProxy(box(i * 3, 0, 0, 0.5, 0.5, 0.5), `n${i}`));
            }
            expect(ids.size).toBe(10);
            // nodeCount includes internal branch nodes created during tree insertion
            expect(tree.nodeCount).toBeGreaterThanOrEqual(10);
        });
    });

    describe('destroyProxy', () => {
        it('removes a proxy and decrements nodeCount', () => {
            const id = tree.createProxy(box(0, 0, 0, 1, 1, 1), 'x');
            tree.destroyProxy(id);
            expect(tree.nodeCount).toBe(0);
        });

        it('keeps remaining proxies queryable after destruction', () => {
            const a = tree.createProxy(box(0, 0, 0, 1, 1, 1), 'a');
            const b = tree.createProxy(box(10, 0, 0, 1, 1, 1), 'b');
            tree.destroyProxy(a);

            const hits: number[] = [];
            tree.query((pid) => { hits.push(pid); return true; }, box(10, 0, 0, 1, 1, 1));
            expect(hits).toContain(b);
        });
    });

    describe('query', () => {
        it('finds a proxy whose fat AABB overlaps the query region', () => {
            const id = tree.createProxy(box(0, 0, 0, 1, 1, 1), 'find-me');
            const hits: number[] = [];
            tree.query((pid) => { hits.push(pid); return true; }, box(0, 0, 0, 0.5, 0.5, 0.5));
            expect(hits).toContain(id);
        });

        it('does not return proxies outside the query region', () => {
            tree.createProxy(box(0, 0, 0, 1, 1, 1), 'near');
            const far = tree.createProxy(box(100, 100, 100, 1, 1, 1), 'far');
            const hits: number[] = [];
            tree.query((pid) => { hits.push(pid); return true; }, box(0, 0, 0, 2, 2, 2));
            expect(hits).not.toContain(far);
        });

        it('stops early when callback returns false', () => {
            tree.createProxy(box(0, 0, 0, 1, 1, 1), 'a');
            tree.createProxy(box(0.5, 0, 0, 1, 1, 1), 'b');
            let count = 0;
            tree.query(() => { count++; return false; }, box(0, 0, 0, 5, 5, 5));
            expect(count).toBe(1);
        });

        it('returns empty for a tree with no proxies', () => {
            const hits: number[] = [];
            tree.query((pid) => { hits.push(pid); return true; }, box(0, 0, 0, 1, 1, 1));
            expect(hits).toHaveLength(0);
        });
    });

    describe('moveProxy', () => {
        it('returns false when the proxy still fits inside its fat AABB', () => {
            const id = tree.createProxy(box(0, 0, 0, 1, 1, 1), 'still');
            const moved = tree.moveProxy(id, box(0.01, 0, 0, 0.5, 0.5, 0.5), { x: 0, y: 0, z: 0 });
            expect(moved).toBe(false);
        });

        it('returns true and re-inserts when proxy exceeds fat AABB', () => {
            const id = tree.createProxy(box(0, 0, 0, 1, 1, 1), 'move');
            const moved = tree.moveProxy(id, box(50, 50, 50, 1, 1, 1), { x: 50, y: 50, z: 50 });
            expect(moved).toBe(true);

            const hits: number[] = [];
            tree.query((pid) => { hits.push(pid); return true; }, box(50, 50, 50, 1, 1, 1));
            expect(hits).toContain(id);
        });

        it('keeps all proxies queryable after heavy move churn', () => {
            const ids: number[] = [];
            for (let i = 0; i < 10; i++) {
                ids.push(tree.createProxy(box(i * 3, 0, 0, 0.5, 0.5, 0.5), `m${i}`));
            }
            for (let step = 0; step < 5; step++) {
                for (let i = 0; i < ids.length; i++) {
                    const o = (i + step * 3) % 30;
                    tree.moveProxy(ids[i], box(o, 0, 0, 0.5, 0.5, 0.5), { x: 1, y: 0, z: 0 });
                }
            }
            for (let i = 0; i < ids.length; i++) {
                const o = (i + 4 * 3) % 30;
                const hits: number[] = [];
                tree.query((pid) => { hits.push(pid); return true; }, box(o, 0, 0, 0.1, 0.1, 0.1));
                expect(hits).toContain(ids[i]);
            }
        });
    });

    describe('queryPairs', () => {
        it('reports overlapping proxy pairs', () => {
            const a = tree.createProxy(box(0, 0, 0, 1, 1, 1), 'a');
            const b = tree.createProxy(box(0.5, 0, 0, 1, 1, 1), 'b');
            tree.createProxy(box(100, 100, 100, 1, 1, 1), 'c');

            const pairs: [number, number][] = [];
            tree.queryPairs((idA, idB) => { pairs.push([idA, idB]); return true; });

            const pairSet = pairs.map(([x, y]) => `${Math.min(x, y)}-${Math.max(x, y)}`);
            expect(pairSet).toContain(`${Math.min(a, b)}-${Math.max(a, b)}`);
        });

        it('returns nothing for an empty tree', () => {
            const pairs: [number, number][] = [];
            tree.queryPairs((idA, idB) => { pairs.push([idA, idB]); return true; });
            expect(pairs).toHaveLength(0);
        });
    });

    describe('capacity growth', () => {
        it('grows beyond initial capacity without errors', () => {
            const smallTree = new DynamicAABBTree3D<number>(4);
            const ids: number[] = [];
            for (let i = 0; i < 20; i++) {
                ids.push(smallTree.createProxy(box(i * 5, 0, 0, 1, 1, 1), i));
            }
            // nodeCount includes internal branch nodes
            expect(smallTree.nodeCount).toBeGreaterThanOrEqual(20);
            for (const id of ids) {
                expect(smallTree.getUserData(id)).toBeTypeOf('number');
            }
        });
    });

    describe('tree height', () => {
        it('has height 0 for a single proxy', () => {
            tree.createProxy(box(0, 0, 0, 1, 1, 1), 'solo');
            expect(tree.getHeight()).toBe(0);
        });

        it('has height > 0 for multiple proxies', () => {
            for (let i = 0; i < 8; i++) {
                tree.createProxy(box(i * 3, 0, 0, 0.5, 0.5, 0.5), `h${i}`);
            }
            expect(tree.getHeight()).toBeGreaterThan(0);
        });
    });
});
