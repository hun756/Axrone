import { describe, expect, it } from 'vitest';
import { UIRuntime } from '../index';
import type { WidgetId, LayoutBox } from '../index';
import { createTestFontAsset } from './test-font';

const createTestRuntime = (width = 800, height = 600): UIRuntime => {
    const runtime = new UIRuntime({ width, height });
    runtime.fonts.registerFace(createTestFontAsset());
    return runtime;
};

/** Collect all layout boxes keyed by widget id for comparison. */
const collectBoxes = (runtime: UIRuntime, widgets: WidgetId[]): Map<WidgetId, LayoutBox> => {
    const boxes = new Map<WidgetId, LayoutBox>();
    for (const w of widgets) {
        boxes.set(w, { ...runtime.getLayoutBox(w) });
    }
    return boxes;
};

const boxesEqual = (a: Map<WidgetId, LayoutBox>, b: Map<WidgetId, LayoutBox>): boolean => {
    if (a.size !== b.size) return false;
    for (const [key, boxA] of a) {
        const boxB = b.get(key);
        if (!boxB) return false;
        if (Math.abs(boxA.x - boxB.x) > 0.01) return false;
        if (Math.abs(boxA.y - boxB.y) > 0.01) return false;
        if (Math.abs(boxA.width - boxB.width) > 0.01) return false;
        if (Math.abs(boxA.height - boxB.height) > 0.01) return false;
        if (Math.abs(boxA.contentX - boxB.contentX) > 0.01) return false;
        if (Math.abs(boxA.contentY - boxB.contentY) > 0.01) return false;
        if (Math.abs(boxA.contentWidth - boxB.contentWidth) > 0.01) return false;
        if (Math.abs(boxA.contentHeight - boxB.contentHeight) > 0.01) return false;
    }
    return true;
};

describe('T-24: Scoped relayout correctness', () => {
    it('scoped relayout produces same result as full compute for style-only changes', () => {
        // Build runtime A: will use scoped relayout after style change
        const rtA = createTestRuntime();
        const container = rtA.createWidget({
            layout: { display: 'stack', direction: 'column', width: 400, height: 300, gap: 5 },
            style: { background: '#111111ff' },
        });
        const child1 = rtA.createWidget({
            layout: { width: 200, height: 50 },
            style: { background: '#ff0000ff' },
        });
        const child2 = rtA.createWidget({
            layout: { width: 200, height: 50 },
            style: { background: '#00ff00ff' },
        });
        const child3 = rtA.createWidget({
            layout: { width: 100, height: 30 },
            style: { background: '#0000ffff' },
        });
        rtA.appendChild(rtA.root, container);
        rtA.appendChild(container, child1);
        rtA.appendChild(container, child2);
        rtA.appendChild(container, child3);
        rtA.commit();

        // Apply style-only change to child2 (no layout/structural change)
        rtA.updateWidget(child2, { style: { background: '#ffff00ff' } });
        rtA.commit();

        // Build runtime B: identical tree with the same final state, full compute
        const rtB = createTestRuntime();
        const cB = rtB.createWidget({
            layout: { display: 'stack', direction: 'column', width: 400, height: 300, gap: 5 },
            style: { background: '#111111ff' },
        });
        const c1B = rtB.createWidget({
            layout: { width: 200, height: 50 },
            style: { background: '#ff0000ff' },
        });
        const c2B = rtB.createWidget({
            layout: { width: 200, height: 50 },
            style: { background: '#ffff00ff' },
        });
        const c3B = rtB.createWidget({
            layout: { width: 100, height: 30 },
            style: { background: '#0000ffff' },
        });
        rtB.appendChild(rtB.root, cB);
        rtB.appendChild(cB, c1B);
        rtB.appendChild(cB, c2B);
        rtB.appendChild(cB, c3B);
        rtB.commit();

        const boxesA = collectBoxes(rtA, [container, child1, child2, child3]);
        const boxesB = collectBoxes(rtB, [cB, c1B, c2B, c3B]);

        // Map by structural position rather than id
        const listA = [container, child1, child2, child3].map(w => rtA.getLayoutBox(w));
        const listB = [cB, c1B, c2B, c3B].map(w => rtB.getLayoutBox(w));
        for (let i = 0; i < listA.length; i++) {
            expect(listA[i].x).toBeCloseTo(listB[i].x);
            expect(listA[i].y).toBeCloseTo(listB[i].y);
            expect(listA[i].width).toBeCloseTo(listB[i].width);
            expect(listA[i].height).toBeCloseTo(listB[i].height);
        }
    });

    it('scoped relayout matches full compute for layout property changes', () => {
        // Build runtime A: will use scoped relayout after layout change
        const rtA = createTestRuntime();
        const container = rtA.createWidget({
            layout: { display: 'stack', direction: 'column', width: 400, height: 300, gap: 5 },
        });
        const child1 = rtA.createWidget({
            layout: { width: 200, height: 50 },
        });
        const child2 = rtA.createWidget({
            layout: { width: 200, height: 50 },
        });
        rtA.appendChild(rtA.root, container);
        rtA.appendChild(container, child1);
        rtA.appendChild(container, child2);
        rtA.commit();

        // Change child1 size (layout change, but no structural change)
        rtA.updateWidget(child1, { layout: { width: 300, height: 80 } });
        rtA.commit();

        // Build runtime B: identical final state, full compute from scratch
        const rtB = createTestRuntime();
        const cB = rtB.createWidget({
            layout: { display: 'stack', direction: 'column', width: 400, height: 300, gap: 5 },
        });
        const c1B = rtB.createWidget({
            layout: { width: 300, height: 80 },
        });
        const c2B = rtB.createWidget({
            layout: { width: 200, height: 50 },
        });
        rtB.appendChild(rtB.root, cB);
        rtB.appendChild(cB, c1B);
        rtB.appendChild(cB, c2B);
        rtB.commit();

        const listA = [container, child1, child2].map(w => rtA.getLayoutBox(w));
        const listB = [cB, c1B, c2B].map(w => rtB.getLayoutBox(w));
        for (let i = 0; i < listA.length; i++) {
            expect(listA[i].x).toBeCloseTo(listB[i].x);
            expect(listA[i].y).toBeCloseTo(listB[i].y);
            expect(listA[i].width).toBeCloseTo(listB[i].width);
            expect(listA[i].height).toBeCloseTo(listB[i].height);
        }
    });

    it('falls back to full compute on structural change (insert/remove)', () => {
        const runtime = createTestRuntime();
        const container = runtime.createWidget({
            layout: { display: 'stack', direction: 'column', width: 400, height: 300, gap: 5 },
        });
        const child1 = runtime.createWidget({
            layout: { width: 200, height: 50 },
        });
        runtime.appendChild(runtime.root, container);
        runtime.appendChild(container, child1);
        runtime.commit();

        // Structural change: add a new child
        const child2 = runtime.createWidget({
            layout: { width: 200, height: 50 },
        });
        runtime.appendChild(container, child2);
        runtime.commit();

        // Verify layout is correct (full compute was used due to structural change)
        const containerBox = runtime.getLayoutBox(container);
        const child1Box = runtime.getLayoutBox(child1);
        const child2Box = runtime.getLayoutBox(child2);

        expect(containerBox.width).toBe(400);
        expect(containerBox.height).toBe(300);
        expect(child1Box.width).toBe(200);
        expect(child1Box.height).toBe(50);
        expect(child1Box.y).toBeCloseTo(containerBox.contentY);
        // child2 should be below child1 with gap
        expect(child2Box.y).toBeCloseTo(child1Box.y + child1Box.height + 5);
    });

    it('falls back to full compute when root is dirty', () => {
        const runtime = createTestRuntime(800, 600);
        const widget = runtime.createWidget({
            layout: { width: 100, height: 50 },
        });
        runtime.appendChild(runtime.root, widget);
        runtime.commit();

        // Update root's layout (root becomes dirty → full compute)
        runtime.updateWidget(runtime.root, {
            layout: { display: 'overlay', width: '100%', height: '100%' },
        });
        runtime.commit();

        const box = runtime.getLayoutBox(widget);
        expect(box.width).toBe(100);
        expect(box.height).toBe(50);
    });

    it('relayout dirty parents in depth order produces same layout as full compute', () => {
        // Build a tree: root -> grandparent -> parent -> child
        // Mark both grandparent and child as dirty (via layout changes)
        // The scoped relayout should process grandparent before parent
        // to avoid stale availWidth propagation.

        // Runtime A: uses scoped relayout
        const rtA = createTestRuntime();
        const grandparent = rtA.createWidget({
            layout: { display: 'stack', direction: 'column', width: 400, height: 300, gap: 5 },
        });
        const parent = rtA.createWidget({
            layout: { display: 'stack', direction: 'column', width: 300, height: 200, gap: 5 },
        });
        const child = rtA.createWidget({
            layout: { width: 100, height: 50 },
        });
        rtA.appendChild(rtA.root, grandparent);
        rtA.appendChild(grandparent, parent);
        rtA.appendChild(parent, child);
        rtA.commit();

        // Change both grandparent and child layout (adverse order: child first)
        rtA.updateWidget(child, { layout: { width: 150, height: 60 } });
        rtA.updateWidget(grandparent, { layout: { display: 'stack', direction: 'column', width: 350, height: 250, gap: 5 } });
        rtA.commit();

        // Runtime B: identical final state, full compute from scratch
        const rtB = createTestRuntime();
        const gpB = rtB.createWidget({
            layout: { display: 'stack', direction: 'column', width: 350, height: 250, gap: 5 },
        });
        const pB = rtB.createWidget({
            layout: { display: 'stack', direction: 'column', width: 300, height: 200, gap: 5 },
        });
        const cB = rtB.createWidget({
            layout: { width: 150, height: 60 },
        });
        rtB.appendChild(rtB.root, gpB);
        rtB.appendChild(gpB, pB);
        rtB.appendChild(pB, cB);
        rtB.commit();

        // Compare layouts
        const listA = [grandparent, parent, child].map(w => rtA.getLayoutBox(w));
        const listB = [gpB, pB, cB].map(w => rtB.getLayoutBox(w));
        for (let i = 0; i < listA.length; i++) {
            expect(listA[i].x).toBeCloseTo(listB[i].x);
            expect(listA[i].y).toBeCloseTo(listB[i].y);
            expect(listA[i].width).toBeCloseTo(listB[i].width);
            expect(listA[i].height).toBeCloseTo(listB[i].height);
        }
    });
});
