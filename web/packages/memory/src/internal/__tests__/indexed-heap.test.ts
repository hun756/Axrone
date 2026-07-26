import { describe, expect, it } from 'vitest';
import {
    HeapHandle,
    IndexedHeap,
    createMaxHeap,
    createMinHeap,
    numericCompare,
} from '../indexed-heap';

describe('IndexedHeap', () => {
    it('behaves as a min-heap: peek returns the smallest key', () => {
        const heap = createMinHeap<number, string>(numericCompare);
        heap.push(5, 'e');
        heap.push(1, 'a');
        heap.push(3, 'c');
        heap.push(2, 'b');
        heap.push(4, 'd');

        expect(heap.size).toBe(5);
        expect(heap.peek()).toEqual({ key: 1, value: 'a' });
    });

    it('pops items in ascending order (min-heap)', () => {
        const heap = createMinHeap<number, string>(numericCompare);
        const items = [5, 1, 3, 2, 4, 8, 7, 6];
        for (const i of items) heap.push(i, `v${i}`);

        const popped: number[] = [];
        while (!heap.isEmpty) {
            const entry = heap.pop()!;
            popped.push(entry.key);
        }
        expect(popped).toEqual([1, 2, 3, 4, 5, 6, 7, 8]);
    });

    it('pops items in descending order (max-heap)', () => {
        const heap = createMaxHeap<number, string>(numericCompare);
        const items = [5, 1, 3, 2, 4, 8, 7, 6];
        for (const i of items) heap.push(i, `v${i}`);

        const popped: number[] = [];
        while (!heap.isEmpty) {
            popped.push(heap.pop()!.key);
        }
        expect(popped).toEqual([8, 7, 6, 5, 4, 3, 2, 1]);
    });

    it('update() reorders the heap in O(log n) when a key decreases', () => {
        const heap = createMinHeap<number, string>(numericCompare);
        const handles: HeapHandle<number, string>[] = [];
        for (let i = 1; i <= 10; i++) handles.push(heap.push(i, `v${i}`));

        heap.update(handles[8], -100, 'v9-neg');
        expect(heap.peek()).toEqual({ key: -100, value: 'v9-neg' });
        expect(heap.pop()!.key).toBe(-100);
    });

    it('update() reorders the heap in O(log n) when a key increases', () => {
        const heap = createMinHeap<number, string>(numericCompare);
        const handles: HeapHandle<number, string>[] = [];
        for (let i = 1; i <= 10; i++) handles.push(heap.push(i, `v${i}`));

        heap.update(handles[0], 999, 'v1-high');
        const peeked = heap.peek()!;
        expect(peeked.key).toBe(2);
    });

    it('remove() returns the entry and invalidates the handle', () => {
        const heap = createMinHeap<number, string>(numericCompare);
        const h1 = heap.push(1, 'a');
        const h2 = heap.push(2, 'b');
        const h3 = heap.push(3, 'c');

        const removed = heap.remove(h2);
        expect(removed).toEqual({ key: 2, value: 'b' });
        expect(heap.size).toBe(2);
        expect(heap.contains(h2)).toBe(false);
        expect(h2.isValid).toBe(false);
        expect(heap.contains(h1)).toBe(true);
        expect(heap.contains(h3)).toBe(true);
    });

    it('handles push/pop of 10,000 items in correct order', () => {
        const heap = createMinHeap<number, number>(numericCompare);
        const N = 10_000;
        const reference: number[] = [];
        for (let i = 0; i < N; i++) {
            const v = Math.floor(Math.random() * N);
            heap.push(v, v);
            reference.push(v);
        }
        reference.sort((a, b) => a - b);
        for (let i = 0; i < N; i++) {
            expect(heap.pop()!.key).toBe(reference[i]);
        }
        expect(heap.isEmpty).toBe(true);
    });

    it('update() preserves the heap invariant after many random updates', () => {
        const heap = createMinHeap<number, number>(numericCompare);
        const N = 1000;
        const handles: HeapHandle<number, number>[] = [];
        for (let i = 0; i < N; i++) {
            handles.push(heap.push(i, i));
        }

        for (let i = 0; i < 5_000; i++) {
            const idx = Math.floor(Math.random() * N);
            const newKey = Math.floor(Math.random() * N * 2);
            heap.update(handles[idx], newKey);
        }

        const reference: number[] = [];
        for (let i = 0; i < N; i++) {
            const entry = heap.get(handles[i]);
            reference.push(entry!.key);
        }
        reference.sort((a, b) => a - b);

        for (let i = 0; i < N; i++) {
            expect(heap.pop()!.key).toBe(reference[i]);
        }
    });

    it('throws TypeError on non-function comparator', () => {
        expect(() => new IndexedHeap({ compare: 42 as never })).toThrow(TypeError);
    });

    it('pop on empty heap returns undefined', () => {
        const heap = createMinHeap<number, string>(numericCompare);
        expect(heap.pop()).toBeUndefined();
        expect(heap.peek()).toBeUndefined();
    });

    it('clear() empties the heap and invalidates all handles', () => {
        const heap = createMinHeap<number, string>(numericCompare);
        const handles: HeapHandle<number, string>[] = [];
        for (let i = 0; i < 5; i++) handles.push(heap.push(i, `v${i}`));

        heap.clear();
        expect(heap.size).toBe(0);
        expect(heap.isEmpty).toBe(true);
        for (const h of handles) {
            expect(h.isValid).toBe(false);
            expect(heap.contains(h)).toBe(false);
        }
    });

    it('update on invalid handle returns false and is a no-op', () => {
        const heapA = createMinHeap<number, string>(numericCompare);
        const heapB = createMinHeap<number, string>(numericCompare);
        const h = heapA.push(1, 'a');
        expect(heapA.update(h, 99)).toBe(true);
        expect(heapA.peek()!.key).toBe(99);
        expect(heapB.update(h, 42)).toBe(false);
    });

    it('toSortedArray() returns all entries in heap order', () => {
        const heap = createMinHeap<number, string>(numericCompare);
        const items = [5, 1, 3, 2, 4];
        for (const i of items) heap.push(i, `v${i}`);

        const sorted = heap.toSortedArray().map((e) => e.key);
        expect(sorted).toEqual([1, 2, 3, 4, 5]);
        expect(heap.size).toBe(5);
    });

    it('modCount increments on mutating operations', () => {
        const heap = createMinHeap<number, string>(numericCompare);
        const initial = heap.modCount;
        const h = heap.push(1, 'a');
        expect(heap.modCount).toBe(initial + 1);
        heap.update(h, 2);
        expect(heap.modCount).toBe(initial + 2);
        heap.pop();
        expect(heap.modCount).toBe(initial + 3);
        heap.clear();
        expect(heap.modCount).toBeGreaterThan(initial + 3);
    });
});
