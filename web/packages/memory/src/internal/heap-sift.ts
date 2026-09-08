/**
 * Shared binary-heap sift operations used by BinaryHeap, PriorityQueue, and IndexedHeap.
 *
 * The structural algorithm (walk the tree, compare, swap) is identical across all three
 * implementations. The only variation is how each implementation tracks handle-to-index
 * mappings after a move. This module factors out the common algorithm and accepts an
 * `onMove` callback for handle-tracking side-effects.
 *
 * @module heap-sift
 * @internal
 */

/**
 * Predicate: returns `true` when `a` should be placed above `b` in the heap
 * (i.e. `a` has higher priority / comes before `b`).
 */
export type HeapComesBefore<T> = (a: T, b: T) => boolean;

/**
 * Callback invoked every time an item is written to a new position in the store.
 * Used by handle-based heaps (PriorityQueue, IndexedHeap) to keep their
 * handle-to-index mapping in sync as items move during sift operations.
 *
 * For heaps without handle tracking (BinaryHeap), pass a no-op.
 */
export type HeapOnMove<T> = (item: T, newIndex: number) => void;

/** No-op move callback for heaps that do not track handles. */
export const NO_OP_MOVE: HeapOnMove<unknown> = () => {};

/**
 * Sift an element upward toward the root until the heap property is restored.
 *
 * @param store     The backing array.
 * @param index     The index of the element to sift up.
 * @param comesBefore  Comparison predicate.
 * @param onMove    Side-effect callback for handle tracking.
 */
export function heapSiftUp<T>(
    store: T[],
    index: number,
    comesBefore: HeapComesBefore<T>,
    onMove: HeapOnMove<T>
): void {
    const item = store[index]!;

    while (index > 0) {
        const parentIndex = (index - 1) >> 1;
        const parent = store[parentIndex]!;

        if (!comesBefore(item, parent)) {
            break;
        }

        store[index] = parent;
        onMove(parent, index);
        index = parentIndex;
    }

    store[index] = item;
    onMove(item, index);
}

/**
 * Sift an element downward toward the leaves until the heap property is restored.
 *
 * @param store     The backing array.
 * @param length    The logical length of the heap (may be less than store.length).
 * @param index     The index of the element to sift down.
 * @param comesBefore  Comparison predicate.
 * @param onMove    Side-effect callback for handle tracking.
 */
export function heapSiftDown<T>(
    store: T[],
    length: number,
    index: number,
    comesBefore: HeapComesBefore<T>,
    onMove: HeapOnMove<T>
): void {
    const item = store[index]!;
    const half = length >> 1;

    while (index < half) {
        let bestIndex = (index << 1) + 1;
        let bestChild = store[bestIndex]!;
        const rightIndex = bestIndex + 1;

        if (rightIndex < length) {
            const right = store[rightIndex]!;

            if (comesBefore(right, bestChild)) {
                bestIndex = rightIndex;
                bestChild = right;
            }
        }

        if (!comesBefore(bestChild, item)) {
            break;
        }

        store[index] = bestChild;
        onMove(bestChild, index);
        index = bestIndex;
    }

    store[index] = item;
    onMove(item, index);
}

/**
 * Build a heap in-place from an unordered array (Floyd's algorithm).
 *
 * @param store     The backing array.
 * @param comesBefore  Comparison predicate.
 * @param onMove    Side-effect callback for handle tracking.
 */
export function heapHeapify<T>(
    store: T[],
    comesBefore: HeapComesBefore<T>,
    onMove: HeapOnMove<T>
): void {
    for (let index = (store.length >> 1) - 1; index >= 0; index--) {
        heapSiftDown(store, store.length, index, comesBefore, onMove);
    }
}
