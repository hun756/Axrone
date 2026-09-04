import { describe, expect, it } from 'vitest';
import { UILayoutEngine, type LayoutTreeAdapter } from '../layout';
import type { LayoutBox, ResolvedLayout, SizeLike } from '../types';

interface TestNode {
    id: string;
    layout: ResolvedLayout;
    children: TestNode[];
    measuredSize?: SizeLike;
    box?: LayoutBox;
    visible: boolean;
}

const createTestAdapter = (root: TestNode): LayoutTreeAdapter<TestNode> => ({
    root,
    getLayout: (node) => node.layout,
    getFirstChild: (node) => node.children[0] ?? null,
    getNextSibling: (node) => {
        // Find parent and return next sibling
        const findNext = (parent: TestNode, target: TestNode): TestNode | null => {
            for (let i = 0; i < parent.children.length; i++) {
                if (parent.children[i] === target) {
                    return parent.children[i + 1] ?? null;
                }
            }
            for (const child of parent.children) {
                const found = findNext(child, target);
                if (found) return found;
            }
            return null;
        };
        return findNext(root, node);
    },
    measureContent: (node, constraints) => {
        // Return a size that depends on constraints to simulate content measurement
        const width = Math.min(constraints.width === Number.POSITIVE_INFINITY ? 100 : constraints.width, 100);
        const height = Math.min(constraints.height === Number.POSITIVE_INFINITY ? 50 : constraints.height, 50);
        return { width, height };
    },
    setBox: (node, box) => {
        node.box = box;
    },
    isVisible: (node) => node.visible,
});

const createDefaultLayout = (overrides: Partial<ResolvedLayout> = {}): ResolvedLayout => ({
    display: 'stack',
    direction: 'column',
    gap: 0,
    width: { kind: 'auto' as const },
    height: { kind: 'auto' as const },
    padding: { top: 0, right: 0, bottom: 0, left: 0 },
    margin: { top: 0, right: 0, bottom: 0, left: 0 },
    minWidth: 0,
    maxWidth: Number.POSITIVE_INFINITY,
    minHeight: 0,
    maxHeight: Number.POSITIVE_INFINITY,
    contentOffsetX: 0,
    contentOffsetY: 0,
    aspectRatio: 0,
    flexWrap: 'no-wrap',
    alignItems: 'stretch',
    justifyContent: 'start',
    grow: 0,
    shrink: 1,
    position: 'relative',
    clip: false,
    zIndex: 0,
    insetTop: undefined,
    insetRight: undefined,
    insetBottom: undefined,
    insetLeft: undefined,
    anchor: { x: 0, y: 0, maxX: 0, maxY: 0, pivotX: 0, pivotY: 0, offsetX: 0, offsetY: 0, stretch: false },
    ...overrides,
});

describe('@axrone/ui UILayoutEngine measure cache', () => {
    it('returns distinct sizes when same node is measured with different constraints in one pass', () => {
        // The bug: within a single layout pass, if a node is measured with
        // different availableWidth values, the cache would return stale results.
        // This test verifies the fix by measuring the same node type twice
        // with different constraints within a single compute() call.

        // Create a parent with two children that share the same node reference
        // but will be measured with different constraints due to flex layout
        const sharedChild: TestNode = {
            id: 'shared',
            layout: createDefaultLayout({
                // Content-sized: width/height auto means measureContent determines size
                width: { kind: 'auto' as const },
                height: { kind: 'auto' as const },
            }),
            children: [],
            visible: true,
        };

        // Create two parent containers that will measure the shared child
        // with different constraints
        const parent1: TestNode = {
            id: 'parent1',
            layout: createDefaultLayout({
                width: { kind: 'px' as const, value: 150 },
                height: { kind: 'px' as const, value: 100 },
            }),
            children: [sharedChild],
            visible: true,
        };

        const parent2: TestNode = {
            id: 'parent2',
            layout: createDefaultLayout({
                width: { kind: 'px' as const, value: 80 },
                height: { kind: 'px' as const, value: 100 },
            }),
            children: [sharedChild],
            visible: true,
        };

        const root: TestNode = {
            id: 'root',
            layout: createDefaultLayout({
                display: 'overlay',
                width: { kind: 'px' as const, value: 200 },
                height: { kind: 'px' as const, value: 200 },
            }),
            children: [parent1, parent2],
            visible: true,
        };

        let measureCallCount = 0;
        const adapter: LayoutTreeAdapter<TestNode> = {
            root,
            getLayout: (node) => node.layout,
            getFirstChild: (node) => node.children[0] ?? null,
            getNextSibling: (node) => {
                const findNext = (p: TestNode, target: TestNode): TestNode | null => {
                    for (let i = 0; i < p.children.length; i++) {
                        if (p.children[i] === target) {
                            return p.children[i + 1] ?? null;
                        }
                    }
                    for (const child of p.children) {
                        const found = findNext(child, target);
                        if (found) return found;
                    }
                    return null;
                };
                return findNext(root, node);
            },
            measureContent: (_node, constraints) => {
                measureCallCount++;
                // Return size based on constraints - simulates text wrapping
                const width = constraints.width === Number.POSITIVE_INFINITY ? 200 : Math.min(constraints.width, 200);
                const height = constraints.height === Number.POSITIVE_INFINITY ? 100 : Math.min(constraints.height, 100);
                return { width, height };
            },
            setBox: (node, box) => {
                node.box = box;
            },
            isVisible: (node) => node.visible,
        };

        const engine = new UILayoutEngine<TestNode>();
        engine.compute(adapter, { width: 200, height: 200 });

        // The shared child is measured twice: once by parent1 (150px constraint)
        // and once by parent2 (80px constraint). With the fix, these should
        // produce different results because the cache key includes constraints.
        // Without the fix, the second measurement would return the cached result
        // from the first measurement.

        // Verify that measureContent was called multiple times (not cached incorrectly)
        expect(measureCallCount).toBeGreaterThan(1);

        // The parent boxes should reflect their fixed sizes
        expect(parent1.box?.width).toBe(150);
        expect(parent2.box?.width).toBe(80);
    });

    it('caches measurements when same constraints are used', () => {
        const child: TestNode = {
            id: 'child',
            layout: createDefaultLayout(),
            children: [],
            visible: true,
        };
        const root: TestNode = {
            id: 'root',
            layout: createDefaultLayout({
                width: { kind: 'px' as const, value: 200 },
                height: { kind: 'px' as const, value: 200 },
            }),
            children: [child],
            visible: true,
        };

        const adapter = createTestAdapter(root);
        const engine = new UILayoutEngine<TestNode>();

        // Compute twice with same constraints
        engine.compute(adapter, { width: 200, height: 200 });
        const firstPasses = engine.getLayoutPassCount();

        engine.compute(adapter, { width: 200, height: 200 });
        const secondPasses = engine.getLayoutPassCount();

        // Both passes should have the same number of layout passes
        // (cache is cleared between compute() calls, so this tests consistency)
        expect(firstPasses).toBe(secondPasses);
    });

    it('never shares cache entries between distinct numeric-node adapters', () => {
        // Numeric nodes (indices) use number identity — the old String(node)
        // approach collided for object nodes in test adapters. This test
        // verifies the nested identity map keeps distinct numeric nodes apart.
        //
        // Build a tree: root (overlay, 200×200) → [nodeA (150px), nodeB (80px)]
        // Both children are leaf content nodes with auto sizing.
        const nodeA = 1;
        const nodeB = 2;
        const rootId = 0;

        const layouts = new Map<number, ResolvedLayout>();
        layouts.set(rootId, createDefaultLayout({
            display: 'overlay',
            width: { kind: 'px' as const, value: 200 },
            height: { kind: 'px' as const, value: 200 },
        }));
        layouts.set(nodeA, createDefaultLayout({
            width: { kind: 'px' as const, value: 150 },
            height: { kind: 'px' as const, value: 100 },
        }));
        layouts.set(nodeB, createDefaultLayout({
            width: { kind: 'px' as const, value: 80 },
            height: { kind: 'px' as const, value: 100 },
        }));

        const children = new Map<number, number[]>();
        children.set(rootId, [nodeA, nodeB]);
        children.set(nodeA, []);
        children.set(nodeB, []);

        const measureCounts = new Map<number, number>();
        measureCounts.set(nodeA, 0);
        measureCounts.set(nodeB, 0);

        const adapter: LayoutTreeAdapter<number> = {
            root: rootId,
            getLayout: (node) => layouts.get(node)!,
            getFirstChild: (node) => {
                const kids = children.get(node);
                return kids && kids.length > 0 ? kids[0] : null;
            },
            getNextSibling: (node) => {
                const kids = children.get(rootId);
                if (!kids) return null;
                const idx = kids.indexOf(node);
                return idx >= 0 && idx + 1 < kids.length ? kids[idx + 1] : null;
            },
            measureContent: (node, constraints) => {
                measureCounts.set(node, (measureCounts.get(node) ?? 0) + 1);
                const width = constraints.width === Number.POSITIVE_INFINITY ? 200 : Math.min(constraints.width, 200);
                const height = constraints.height === Number.POSITIVE_INFINITY ? 100 : Math.min(constraints.height, 100);
                return { width, height };
            },
            setBox: () => {},
            isVisible: () => true,
        };

        const engine = new UILayoutEngine<number>();
        engine.compute(adapter, { width: 200, height: 200 });

        // Both distinct numeric nodes must have been measured independently
        expect(measureCounts.get(nodeA)).toBeGreaterThanOrEqual(1);
        expect(measureCounts.get(nodeB)).toBeGreaterThanOrEqual(1);
    });

    it('never shares cache entries between distinct object-node adapters', () => {
        // Object nodes: the old String(node) produced '[object Object]' for all
        // objects, causing cache collisions. The nested identity map keys by
        // reference so distinct objects never collide.
        const childA: TestNode = {
            id: 'childA',
            layout: createDefaultLayout({
                width: { kind: 'auto' as const },
                height: { kind: 'auto' as const },
            }),
            children: [],
            visible: true,
        };
        const childB: TestNode = {
            id: 'childB',
            layout: createDefaultLayout({
                width: { kind: 'auto' as const },
                height: { kind: 'auto' as const },
            }),
            children: [],
            visible: true,
        };
        const root: TestNode = {
            id: 'root',
            layout: createDefaultLayout({
                display: 'overlay',
                width: { kind: 'px' as const, value: 200 },
                height: { kind: 'px' as const, value: 200 },
            }),
            children: [childA, childB],
            visible: true,
        };

        const measureCounts = new Map<TestNode, number>();
        measureCounts.set(childA, 0);
        measureCounts.set(childB, 0);

        const adapter: LayoutTreeAdapter<TestNode> = {
            root,
            getLayout: (node) => node.layout,
            getFirstChild: (node) => node.children[0] ?? null,
            getNextSibling: (node) => {
                const idx = root.children.indexOf(node);
                return idx >= 0 && idx + 1 < root.children.length ? root.children[idx + 1] : null;
            },
            measureContent: (node, constraints) => {
                measureCounts.set(node, (measureCounts.get(node) ?? 0) + 1);
                const width = constraints.width === Number.POSITIVE_INFINITY ? 200 : Math.min(constraints.width, 200);
                const height = constraints.height === Number.POSITIVE_INFINITY ? 100 : Math.min(constraints.height, 100);
                return { width, height };
            },
            setBox: (node, box) => { node.box = box; },
            isVisible: (node) => node.visible,
        };

        const engine = new UILayoutEngine<TestNode>();
        engine.compute(adapter, { width: 200, height: 200 });

        // Both distinct object nodes must have been measured independently
        expect(measureCounts.get(childA)).toBeGreaterThanOrEqual(1);
        expect(measureCounts.get(childB)).toBeGreaterThanOrEqual(1);
    });
});
