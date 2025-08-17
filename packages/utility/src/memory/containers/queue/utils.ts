import { HeapIndex, QueueSize, Capacity, Comparator } from './types';

export const createHeapIndex = (value: number): HeapIndex => value as HeapIndex;

export const createQueueSize = (value: number): QueueSize => value as QueueSize;

export const createCapacity = (value: number): Capacity => value as Capacity;

export const getParentIndex = (index: HeapIndex): HeapIndex => createHeapIndex((index - 1) >>> 1);

export const getLeftChildIndex = (index: HeapIndex): HeapIndex => createHeapIndex((index << 1) | 1);

export const getRightChildIndex = (index: HeapIndex): HeapIndex => createHeapIndex((index + 1) << 1);

export const hasParent = (index: HeapIndex): boolean => index > 0;

export const hasLeftChild = (index: HeapIndex, size: QueueSize): boolean =>
    getLeftChildIndex(index) < size;

export const hasRightChild = (index: HeapIndex, size: QueueSize): boolean =>
    getRightChildIndex(index) < size;

export const defaultComparator = <T>(a: T, b: T): number => {
    return a < b ? -1 : a > b ? 1 : 0;
};
