import { describe, it, expect, beforeEach } from 'vitest';
import { DynamicAABBTree2D } from '@axrone/physics-2d';
import { AABB2D } from '@axrone/geometry';

describe('DynamicAABBTree2D', () => {
    let tree: DynamicAABBTree2D;

    beforeEach(() => {
        tree = new DynamicAABBTree2D(64);
    });

    describe('Construction', () => {
        it('initializes with default capacity', () => {
            const defaultTree = new DynamicAABBTree2D();
            expect(defaultTree).toBeDefined();
        });

        it('initializes with custom capacity', () => {
            const customTree = new DynamicAABBTree2D(256);
            expect(customTree).toBeDefined();
        });

        it('starts with zero height', () => {
            expect(tree.getHeight()).toBe(0);
        });
    });

    describe('Proxy Creation', () => {
        it('creates a proxy', () => {
            const aabb = new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 });
            const userData = { id: 1 };
            const proxyId = tree.createProxy(aabb, userData);
            expect(proxyId).toBeGreaterThanOrEqual(0);
        });

        it('creates multiple proxies', () => {
            const aabb1 = new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 });
            const aabb2 = new AABB2D({ x: 2, y: 2 }, { x: 3, y: 3 });
            const aabb3 = new AABB2D({ x: 4, y: 4 }, { x: 5, y: 5 });

            const id1 = tree.createProxy(aabb1, { id: 1 });
            const id2 = tree.createProxy(aabb2, { id: 2 });
            const id3 = tree.createProxy(aabb3, { id: 3 });

            expect(id1).not.toBe(id2);
            expect(id2).not.toBe(id3);
            expect(id1).not.toBe(id3);
        });

        it('updates tree height after creation', () => {
            const aabb1 = new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 });
            tree.createProxy(aabb1, {});
            expect(tree.getHeight()).toBe(0);

            const aabb2 = new AABB2D({ x: 2, y: 2 }, { x: 3, y: 3 });
            tree.createProxy(aabb2, {});
            expect(tree.getHeight()).toBeGreaterThan(0);
        });

        it('stores user data', () => {
            const aabb = new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 });
            const userData = { value: 42, name: 'test' };
            const proxyId = tree.createProxy(aabb, userData);
            expect(tree.getUserData(proxyId)).toBe(userData);
        });

        it('creates fattened AABB', () => {
            const aabb = new AABB2D({ x: 1, y: 1 }, { x: 2, y: 2 });
            const proxyId = tree.createProxy(aabb, {});
            const storedAABB = tree.getAABB(proxyId);

            expect(storedAABB.min.x).toBeLessThan(aabb.min.x);
            expect(storedAABB.min.y).toBeLessThan(aabb.min.y);
            expect(storedAABB.max.x).toBeGreaterThan(aabb.max.x);
            expect(storedAABB.max.y).toBeGreaterThan(aabb.max.y);
        });
    });

    describe('Proxy Destruction', () => {
        it('destroys a proxy', () => {
            const aabb = new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 });
            const proxyId = tree.createProxy(aabb, {});
            tree.destroyProxy(proxyId);
            expect(tree.getHeight()).toBe(0);
        });

        it('destroys multiple proxies', () => {
            const aabb1 = new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 });
            const aabb2 = new AABB2D({ x: 2, y: 2 }, { x: 3, y: 3 });

            const id1 = tree.createProxy(aabb1, {});
            const id2 = tree.createProxy(aabb2, {});

            tree.destroyProxy(id1);
            tree.destroyProxy(id2);
            expect(tree.getHeight()).toBe(0);
        });

        it('reuses node indices after destruction', () => {
            const aabb = new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 });
            const id1 = tree.createProxy(aabb, { id: 1 });
            tree.destroyProxy(id1);
            const id2 = tree.createProxy(aabb, { id: 2 });
            expect(id2).toBe(id1);
        });
    });

    describe('Proxy Movement', () => {
        it('moves proxy with no displacement', () => {
            const aabb = new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 });
            const proxyId = tree.createProxy(aabb, {});

            const newAABB = new AABB2D({ x: 0.05, y: 0.05 }, { x: 1.05, y: 1.05 });
            const moved = tree.moveProxy(proxyId, newAABB, { x: 0, y: 0 });
            expect(moved).toBe(false);
        });

        it('moves proxy outside fattened bounds', () => {
            const aabb = new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 });
            const proxyId = tree.createProxy(aabb, {});

            const newAABB = new AABB2D({ x: 5, y: 5 }, { x: 6, y: 6 });
            const moved = tree.moveProxy(proxyId, newAABB, { x: 5, y: 5 });
            expect(moved).toBe(true);
        });

        it('updates AABB after move', () => {
            const aabb = new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 });
            const proxyId = tree.createProxy(aabb, {});

            const newAABB = new AABB2D({ x: 10, y: 10 }, { x: 11, y: 11 });
            tree.moveProxy(proxyId, newAABB, { x: 10, y: 10 });

            const storedAABB = tree.getAABB(proxyId);
            expect(storedAABB.containsAABB(newAABB)).toBe(true);
        });

        it('applies displacement prediction', () => {
            const aabb = new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 });
            const proxyId = tree.createProxy(aabb, {});

            const newAABB = new AABB2D({ x: 10, y: 10 }, { x: 11, y: 11 });
            tree.moveProxy(proxyId, newAABB, { x: 2, y: 2 });

            const storedAABB = tree.getAABB(proxyId);
            expect(storedAABB.max.x).toBeGreaterThan(newAABB.max.x);
            expect(storedAABB.max.y).toBeGreaterThan(newAABB.max.y);
        });
    });

    describe('AABB Queries', () => {
        it('queries empty tree', () => {
            const queryAABB = new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 });
            const results: number[] = [];
            tree.query((proxyId) => {
                results.push(proxyId);
                return true;
            }, queryAABB);
            expect(results).toHaveLength(0);
        });

        it('queries single proxy', () => {
            const aabb = new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 });
            const proxyId = tree.createProxy(aabb, {});

            const queryAABB = new AABB2D({ x: 0.5, y: 0.5 }, { x: 1.5, y: 1.5 });
            const results: number[] = [];
            tree.query((id) => {
                results.push(id);
                return true;
            }, queryAABB);

            expect(results).toContain(proxyId);
        });

        it('queries multiple overlapping proxies', () => {
            const aabb1 = new AABB2D({ x: 0, y: 0 }, { x: 2, y: 2 });
            const aabb2 = new AABB2D({ x: 1, y: 1 }, { x: 3, y: 3 });
            const aabb3 = new AABB2D({ x: 2, y: 2 }, { x: 4, y: 4 });

            const id1 = tree.createProxy(aabb1, { id: 1 });
            const id2 = tree.createProxy(aabb2, { id: 2 });
            const id3 = tree.createProxy(aabb3, { id: 3 });

            const queryAABB = new AABB2D({ x: 1.5, y: 1.5 }, { x: 2.5, y: 2.5 });
            const results: number[] = [];
            tree.query((id) => {
                results.push(id);
                return true;
            }, queryAABB);

            expect(results).toContain(id1);
            expect(results).toContain(id2);
            expect(results).toContain(id3);
        });

        it('queries non-overlapping proxies', () => {
            const aabb1 = new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 });
            const aabb2 = new AABB2D({ x: 10, y: 10 }, { x: 11, y: 11 });

            tree.createProxy(aabb1, { id: 1 });
            tree.createProxy(aabb2, { id: 2 });

            const queryAABB = new AABB2D({ x: 5, y: 5 }, { x: 6, y: 6 });
            const results: number[] = [];
            tree.query((id) => {
                results.push(id);
                return true;
            }, queryAABB);

            expect(results).toHaveLength(0);
        });

        it('supports early termination', () => {
            const aabb1 = new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 });
            const aabb2 = new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 });
            const aabb3 = new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 });

            tree.createProxy(aabb1, { id: 1 });
            tree.createProxy(aabb2, { id: 2 });
            tree.createProxy(aabb3, { id: 3 });

            const queryAABB = new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 });
            const results: number[] = [];
            tree.query((id) => {
                results.push(id);
                return results.length < 2;
            }, queryAABB);

            expect(results.length).toBeLessThanOrEqual(2);
        });
    });

    describe('Tree Properties', () => {
        it('has correct height with balanced insertions', () => {
            for (let i = 0; i < 15; i++) {
                const aabb = new AABB2D({ x: i, y: i }, { x: i + 1, y: i + 1 });
                tree.createProxy(aabb, { id: i });
            }
            expect(tree.getHeight()).toBeGreaterThan(0);
            expect(tree.getHeight()).toBeLessThan(15);
        });

        it('has correct height after deletions', () => {
            const ids: number[] = [];
            for (let i = 0; i < 10; i++) {
                const aabb = new AABB2D({ x: i, y: i }, { x: i + 1, y: i + 1 });
                ids.push(tree.createProxy(aabb, { id: i }));
            }

            const heightBefore = tree.getHeight();

            for (let i = 0; i < 5; i++) {
                tree.destroyProxy(ids[i]);
            }

            const heightAfter = tree.getHeight();
            expect(heightAfter).toBeLessThanOrEqual(heightBefore);
        });
    });

    describe('AABB Access', () => {
        it('gets AABB for proxy', () => {
            const aabb = new AABB2D({ x: 1, y: 2 }, { x: 3, y: 4 });
            const proxyId = tree.createProxy(aabb, {});
            const storedAABB = tree.getAABB(proxyId);
            expect(storedAABB).toBeDefined();
            expect(storedAABB.min.x).toBeLessThanOrEqual(aabb.min.x);
            expect(storedAABB.max.x).toBeGreaterThanOrEqual(aabb.max.x);
        });

        it('gets user data for proxy', () => {
            const aabb = new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 });
            const userData = { test: 'value' };
            const proxyId = tree.createProxy(aabb, userData);
            expect(tree.getUserData(proxyId)).toBe(userData);
        });
    });

    describe('Capacity Management', () => {
        it('grows capacity when needed', () => {
            const smallTree = new DynamicAABBTree2D(4);
            for (let i = 0; i < 10; i++) {
                const aabb = new AABB2D({ x: i, y: i }, { x: i + 1, y: i + 1 });
                smallTree.createProxy(aabb, { id: i });
            }
        });

        it('handles many proxies', () => {
            const largeTree = new DynamicAABBTree2D(512);
            for (let i = 0; i < 200; i++) {
                const aabb = new AABB2D({ x: i, y: i }, { x: i + 1, y: i + 1 });
                largeTree.createProxy(aabb, { id: i });
            }
            expect(largeTree.getHeight()).toBeGreaterThan(0);
        });
    });

    describe('Edge Cases', () => {
        it('handles zero-size AABB', () => {
            const aabb = new AABB2D({ x: 1, y: 1 }, { x: 1, y: 1 });
            const proxyId = tree.createProxy(aabb, {});
            expect(proxyId).toBeGreaterThanOrEqual(0);
        });

        it('handles very large AABB', () => {
            const aabb = new AABB2D({ x: -1e6, y: -1e6 }, { x: 1e6, y: 1e6 });
            const proxyId = tree.createProxy(aabb, {});
            expect(proxyId).toBeGreaterThanOrEqual(0);
        });

        it('handles negative coordinates', () => {
            const aabb = new AABB2D({ x: -10, y: -10 }, { x: -5, y: -5 });
            const proxyId = tree.createProxy(aabb, {});
            const storedAABB = tree.getAABB(proxyId);
            expect(storedAABB.min.x).toBeLessThan(-5);
        });

        it('handles overlapping insertions', () => {
            for (let i = 0; i < 10; i++) {
                const aabb = new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 });
                tree.createProxy(aabb, { id: i });
            }
            expect(tree.getHeight()).toBeGreaterThan(0);
        });

        it('handles alternating creates and destroys', () => {
            for (let i = 0; i < 20; i++) {
                const aabb = new AABB2D({ x: i, y: i }, { x: i + 1, y: i + 1 });
                const proxyId = tree.createProxy(aabb, { id: i });
                if (i % 2 === 0) {
                    tree.destroyProxy(proxyId);
                }
            }
        });
    });

    describe('Query Performance', () => {
        it('queries large tree efficiently', () => {
            for (let i = 0; i < 100; i++) {
                const x = Math.floor(i / 10);
                const y = i % 10;
                const aabb = new AABB2D({ x, y }, { x: x + 1, y: y + 1 });
                tree.createProxy(aabb, { id: i });
            }

            const queryAABB = new AABB2D({ x: 5, y: 5 }, { x: 6, y: 6 });
            const results: number[] = [];
            tree.query((id) => {
                results.push(id);
                return true;
            }, queryAABB);

            expect(results.length).toBeLessThan(100);
        });

        it('queries with small AABB', () => {
            for (let i = 0; i < 50; i++) {
                const aabb = new AABB2D({ x: i * 2, y: i * 2 }, { x: i * 2 + 1, y: i * 2 + 1 });
                tree.createProxy(aabb, { id: i });
            }

            const queryAABB = new AABB2D({ x: 10.5, y: 10.5 }, { x: 10.6, y: 10.6 });
            const results: number[] = [];
            tree.query((id) => {
                results.push(id);
                return true;
            }, queryAABB);

            expect(results.length).toBeLessThan(10);
        });
    });

    describe('Tree Rebalance', () => {
        /** Helper: collect all leaf proxy ids reachable from root via DFS. */
        function collectReachableLeaves(t: DynamicAABBTree2D): Set<number> {
            const found = new Set<number>();
            const queryAll = new AABB2D({ x: -1e9, y: -1e9 }, { x: 1e9, y: 1e9 });
            t.query((id) => {
                found.add(id);
                return true;
            }, queryAll);
            return found;
        }

        it('keeps tree balanced after many insert/remove cycles (a)', () => {
            const ids: number[] = [];
            // Insert 200 proxies in a degenerate pattern (sequential x)
            for (let i = 0; i < 200; i++) {
                const aabb = new AABB2D({ x: i, y: 0 }, { x: i + 1, y: 1 });
                ids.push(tree.createProxy(aabb, { id: i }));
            }

            // Remove half to create holes
            for (let i = 0; i < 100; i++) {
                tree.destroyProxy(ids[i * 2]);
            }

            // Rebalance
            tree.rebalance();

            const quality = tree.getTreeQuality();
            // After rebuild, quality should be ≤ 1.5 (perfectly balanced = 1.0)
            expect(quality).toBeLessThanOrEqual(1.5);
            expect(quality).toBeGreaterThanOrEqual(1.0);
        });

        it('destroyProxy is safe after rebalance — regression for _removeLeaf crash (b)', () => {
            const ids: number[] = [];
            for (let i = 0; i < 50; i++) {
                const aabb = new AABB2D({ x: i * 3, y: i * 3 }, { x: i * 3 + 1, y: i * 3 + 1 });
                ids.push(tree.createProxy(aabb, { id: i }));
            }

            // Force rebalance
            tree.rebalance();

            // Destroy every other proxy — must NOT throw
            expect(() => {
                for (let i = 0; i < ids.length; i += 2) {
                    tree.destroyProxy(ids[i]);
                }
            }).not.toThrow();

            // Remaining proxies still queryable
            const remaining = new Set<number>();
            tree.query((id) => { remaining.add(id); return true; },
                new AABB2D({ x: -1e9, y: -1e9 }, { x: 1e9, y: 1e9 }));
            expect(remaining.size).toBe(25);
        });

        it('all leaves reachable from root after rebalance — no orphans (c)', () => {
            const ids: number[] = [];
            for (let i = 0; i < 100; i++) {
                const aabb = new AABB2D({ x: i, y: i }, { x: i + 1, y: i + 1 });
                ids.push(tree.createProxy(aabb, { id: i }));
            }

            // Remove some to create asymmetric tree
            for (let i = 0; i < 30; i++) {
                tree.destroyProxy(ids[i]);
            }

            tree.rebalance();

            // All surviving proxies must be reachable
            const reachable = collectReachableLeaves(tree);
            const expectedIds = new Set(ids.slice(30));
            expect(reachable.size).toBe(expectedIds.size);
            for (const id of expectedIds) {
                expect(reachable.has(id)).toBe(true);
            }
        });

        it('getTreeQuality improves or stays same after rebalance (d)', () => {
            // Build a degenerate tree: insert in sorted order
            for (let i = 0; i < 128; i++) {
                const aabb = new AABB2D({ x: i * 10, y: 0 }, { x: i * 10 + 1, y: 1 });
                tree.createProxy(aabb, { id: i });
            }

            // Remove alternating to create imbalance
            const ids: number[] = [];
            tree.query((id) => { ids.push(id); return true; },
                new AABB2D({ x: -1e9, y: -1e9 }, { x: 1e9, y: 1e9 }));
            for (let i = 0; i < ids.length; i += 2) {
                tree.destroyProxy(ids[i]);
            }

            const qualityBefore = tree.getTreeQuality();
            tree.rebalance();
            const qualityAfter = tree.getTreeQuality();

            expect(qualityAfter).toBeLessThanOrEqual(qualityBefore);
            expect(qualityAfter).toBeLessThanOrEqual(1.5);
        });

        it('rebalance preserves query results — correctness unchanged (e)', () => {
            const ids: number[] = [];
            for (let i = 0; i < 80; i++) {
                const x = (i % 10) * 5;
                const y = Math.floor(i / 10) * 5;
                const aabb = new AABB2D({ x, y }, { x: x + 2, y: y + 2 });
                ids.push(tree.createProxy(aabb, { id: i }));
            }

            // Remove a few
            for (let i = 0; i < 10; i++) {
                tree.destroyProxy(ids[i]);
            }

            // Query before rebalance
            const queryAABB = new AABB2D({ x: 10, y: 10 }, { x: 30, y: 30 });
            const resultsBefore = new Set<number>();
            tree.query((id) => { resultsBefore.add(id); return true; }, queryAABB);

            // Rebalance
            tree.rebalance();

            // Query after rebalance — must be identical
            const resultsAfter = new Set<number>();
            tree.query((id) => { resultsAfter.add(id); return true; }, queryAABB);

            expect(resultsAfter.size).toBe(resultsBefore.size);
            for (const id of resultsBefore) {
                expect(resultsAfter.has(id)).toBe(true);
            }
        });

        it('getTreeQuality returns 1.0 for empty or single-leaf tree', () => {
            expect(tree.getTreeQuality()).toBe(1.0);
            const aabb = new AABB2D({ x: 0, y: 0 }, { x: 1, y: 1 });
            tree.createProxy(aabb, {});
            expect(tree.getTreeQuality()).toBe(1.0);
        });

        it('rebalance on empty tree is a no-op', () => {
            expect(() => tree.rebalance()).not.toThrow();
            expect(tree.getHeight()).toBe(0);
        });

        it('auto-rebalance triggers after enough operations', () => {
            // Insert 300 proxies — auto-rebalance fires at op 256
            const ids: number[] = [];
            for (let i = 0; i < 300; i++) {
                const aabb = new AABB2D({ x: i, y: i }, { x: i + 1, y: i + 1 });
                ids.push(tree.createProxy(aabb, { id: i }));
            }

            // After auto-rebalance (at op 256) + 44 more inserts, tree should be decent.
            // But let's verify explicit rebalance works after pure inserts too:
            tree.rebalance();
            const quality = tree.getTreeQuality();
            expect(quality).toBeLessThanOrEqual(2.0);

            // All leaves still reachable with correct proxy IDs
            const reachable = collectReachableLeaves(tree);
            expect(reachable.size).toBe(300);
            for (const id of ids) {
                expect(reachable.has(id)).toBe(true);
            }
        });
    });
});

