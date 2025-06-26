import {
    PriorityQueue,
    EmptyQueueError,
    InvalidCapacityError,
    QueueError,
    PriorityQueueNode,
    createCapacity,
    createQueueSize,
    createHeapIndex,
    defaultComparator,
    type Comparator,
    type ReadonlyQueueNode,
    type PriorityQueueOptions,
} from '../../memory/priority-queue';

describe('PriorityQueue', () => {
    describe('constructor', () => {
        it('should create empty queue with default options', () => {
            const queue = new PriorityQueue<string>();

            expect(queue.size).toBe(0);
            expect(queue.isEmpty).toBe(true);
            expect(queue.capacity).toBeGreaterThan(0);
        });

        it('should create queue with custom comparator', () => {
            const reverseComparator: Comparator<number> = (a, b) => b - a;
            const queue = new PriorityQueue<string, number>({ comparator: reverseComparator });

            queue.enqueue('low', 1);
            queue.enqueue('high', 10);

            expect(queue.dequeue()).toBe('high');
        });

        it('should create queue with initial capacity', () => {
            const capacity = createCapacity(100);
            const queue = new PriorityQueue<string>({ initialCapacity: capacity });

            expect(queue.capacity).toBe(capacity);
        });

        it('should create queue with auto trim enabled', () => {
            const queue = new PriorityQueue<string>({ autoTrim: true });

            expect(queue).toBeDefined();
        });
    });

    describe('enqueue', () => {
        let queue: PriorityQueue<string, number>;

        beforeEach(() => {
            queue = new PriorityQueue<string, number>();
        });

        it('should add single element', () => {
            queue.enqueue('test', 1);

            expect(queue.size).toBe(1);
            expect(queue.isEmpty).toBe(false);
        });

        it('should maintain heap property with multiple elements', () => {
            queue.enqueue('high', 10);
            queue.enqueue('low', 1);
            queue.enqueue('medium', 5);

            expect(queue.peek()).toBe('low');
        });

        it('should handle duplicate priorities', () => {
            queue.enqueue('first', 5);
            queue.enqueue('second', 5);

            expect(queue.size).toBe(2);
        });

        it('should grow capacity automatically', () => {
            const initialCapacity = queue.capacity;

            for (let i = 0; i < initialCapacity + 10; i++) {
                queue.enqueue(`item-${i}`, i);
            }

            expect(queue.capacity).toBeGreaterThan(initialCapacity);
            expect(queue.size).toBe(initialCapacity + 10);
        });
    });

    describe('dequeue', () => {
        let queue: PriorityQueue<string, number>;

        beforeEach(() => {
            queue = new PriorityQueue<string, number>();
        });

        it('should throw EmptyQueueError when queue is empty', () => {
            expect(() => queue.dequeue()).toThrow(EmptyQueueError);
        });

        it('should return element with highest priority', () => {
            queue.enqueue('medium', 5);
            queue.enqueue('high', 10);
            queue.enqueue('low', 1);

            expect(queue.dequeue()).toBe('low');
        });

        it('should maintain heap property after removal', () => {
            const items = [
                { element: 'a', priority: 3 },
                { element: 'b', priority: 1 },
                { element: 'c', priority: 4 },
                { element: 'd', priority: 2 },
                { element: 'e', priority: 5 },
            ];

            items.forEach((item) => queue.enqueue(item.element, item.priority));

            const results = [];
            while (!queue.isEmpty) {
                results.push(queue.dequeue());
            }

            expect(results).toEqual(['b', 'd', 'a', 'c', 'e']);
        });

        it('should update size correctly', () => {
            queue.enqueue('test', 1);
            expect(queue.size).toBe(1);

            queue.dequeue();
            expect(queue.size).toBe(0);
            expect(queue.isEmpty).toBe(true);
        });

        it('should handle auto trim when enabled', () => {
            const autoTrimQueue = new PriorityQueue<string, number>({ autoTrim: true });

            for (let i = 0; i < 100; i++) {
                autoTrimQueue.enqueue(`item-${i}`, i);
            }

            const capacityBeforeTrim = autoTrimQueue.capacity;

            for (let i = 0; i < 90; i++) {
                autoTrimQueue.dequeue();
            }

            expect(autoTrimQueue.capacity).toBeLessThanOrEqual(capacityBeforeTrim);
        });
    });

    describe('peek', () => {
        let queue: PriorityQueue<string, number>;

        beforeEach(() => {
            queue = new PriorityQueue<string, number>();
        });

        it('should throw EmptyQueueError when queue is empty', () => {
            expect(() => queue.peek()).toThrow(EmptyQueueError);
        });

        it('should return highest priority element without removing it', () => {
            queue.enqueue('low', 1);
            queue.enqueue('high', 10);

            expect(queue.peek()).toBe('low');
            expect(queue.size).toBe(2);
        });

        it('should return same element on multiple calls', () => {
            queue.enqueue('test', 1);

            expect(queue.peek()).toBe('test');
            expect(queue.peek()).toBe('test');
        });
    });

    describe('tryDequeue', () => {
        let queue: PriorityQueue<string, number>;

        beforeEach(() => {
            queue = new PriorityQueue<string, number>();
        });

        it('should return undefined when queue is empty', () => {
            expect(queue.tryDequeue()).toBeUndefined();
        });

        it('should return element when queue is not empty', () => {
            queue.enqueue('test', 1);

            expect(queue.tryDequeue()).toBe('test');
            expect(queue.isEmpty).toBe(true);
        });
    });

    describe('tryPeek', () => {
        let queue: PriorityQueue<string, number>;

        beforeEach(() => {
            queue = new PriorityQueue<string, number>();
        });

        it('should return undefined when queue is empty', () => {
            expect(queue.tryPeek()).toBeUndefined();
        });

        it('should return element when queue is not empty', () => {
            queue.enqueue('test', 1);

            expect(queue.tryPeek()).toBe('test');
            expect(queue.size).toBe(1);
        });
    });

    describe('dequeueAll', () => {
        let queue: PriorityQueue<string, number>;

        beforeEach(() => {
            queue = new PriorityQueue<string, number>();
        });

        it('should return empty array when queue is empty', () => {
            expect(queue.dequeueAll()).toEqual([]);
        });

        it('should return all elements in priority order', () => {
            queue.enqueue('c', 3);
            queue.enqueue('a', 1);
            queue.enqueue('b', 2);

            const result = queue.dequeueAll();

            expect(result).toEqual(['a', 'b', 'c']);
            expect(queue.isEmpty).toBe(true);
        });
    });

    describe('enqueueRange', () => {
        let queue: PriorityQueue<string, number>;

        beforeEach(() => {
            queue = new PriorityQueue<string, number>();
        });

        it('should handle empty array', () => {
            queue.enqueueRange([]);

            expect(queue.isEmpty).toBe(true);
        });

        it('should add multiple items maintaining priority order', () => {
            const items: ReadonlyQueueNode<string, number>[] = [
                { element: 'c', priority: 3 },
                { element: 'a', priority: 1 },
                { element: 'b', priority: 2 },
            ];

            queue.enqueueRange(items);

            expect(queue.size).toBe(3);
            expect(queue.dequeue()).toBe('a');
            expect(queue.dequeue()).toBe('b');
            expect(queue.dequeue()).toBe('c');
        });

        it('should ensure capacity for large ranges', () => {
            const items: ReadonlyQueueNode<string, number>[] = [];
            for (let i = 0; i < 1000; i++) {
                items.push({ element: `item-${i}`, priority: i });
            }

            queue.enqueueRange(items);

            expect(queue.size).toBe(1000);
        });
    });

    describe('contains', () => {
        let queue: PriorityQueue<string, number>;

        beforeEach(() => {
            queue = new PriorityQueue<string, number>();
        });

        it('should return false for empty queue', () => {
            expect(queue.contains('test')).toBe(false);
        });

        it('should return true for existing element', () => {
            queue.enqueue('test', 1);

            expect(queue.contains('test')).toBe(true);
        });

        it('should return false for non-existing element', () => {
            queue.enqueue('test', 1);

            expect(queue.contains('other')).toBe(false);
        });

        it('should work with object elements', () => {
            const objectQueue = new PriorityQueue<{ id: number }, number>();
            const obj1 = { id: 1 };
            const obj2 = { id: 2 };

            objectQueue.enqueue(obj1, 1);

            expect(objectQueue.contains(obj1)).toBe(true);
            expect(objectQueue.contains(obj2)).toBe(false);
        });
    });

    describe('clear', () => {
        let queue: PriorityQueue<string, number>;

        beforeEach(() => {
            queue = new PriorityQueue<string, number>();
        });

        it('should clear empty queue', () => {
            queue.clear();

            expect(queue.isEmpty).toBe(true);
            expect(queue.size).toBe(0);
        });

        it('should clear non-empty queue', () => {
            queue.enqueue('a', 1);
            queue.enqueue('b', 2);

            queue.clear();

            expect(queue.isEmpty).toBe(true);
            expect(queue.size).toBe(0);
        });
    });

    describe('ensureCapacity', () => {
        let queue: PriorityQueue<string, number>;

        beforeEach(() => {
            queue = new PriorityQueue<string, number>();
        });

        it('should increase capacity when needed', () => {
            const newCapacity = createCapacity(100);
            const oldCapacity = queue.capacity;

            queue.ensureCapacity(newCapacity);

            if (newCapacity > oldCapacity) {
                expect(queue.capacity).toBeGreaterThanOrEqual(newCapacity);
            }
        });

        it('should not decrease capacity', () => {
            const oldCapacity = queue.capacity;
            const smallerCapacity = createCapacity(1);

            queue.ensureCapacity(smallerCapacity);

            expect(queue.capacity).toBe(oldCapacity);
        });
    });

    describe('trimExcess', () => {
        let queue: PriorityQueue<string, number>;

        beforeEach(() => {
            queue = new PriorityQueue<string, number>();
        });

        it('should trim excess capacity', () => {
            for (let i = 0; i < 50; i++) {
                queue.enqueue(`item-${i}`, i);
            }

            for (let i = 0; i < 40; i++) {
                queue.dequeue();
            }

            const capacityBeforeTrim = queue.capacity;
            queue.trimExcess();

            expect(queue.capacity).toBeLessThanOrEqual(capacityBeforeTrim);
        });
    });

    describe('toArray', () => {
        let queue: PriorityQueue<string, number>;

        beforeEach(() => {
            queue = new PriorityQueue<string, number>();
        });

        it('should return empty array for empty queue', () => {
            const array = queue.toArray();

            expect(array).toEqual([]);
            expect(Object.isFrozen(array)).toBe(true);
        });

        it('should return frozen array of elements', () => {
            queue.enqueue('a', 3);
            queue.enqueue('b', 1);
            queue.enqueue('c', 2);

            const array = queue.toArray();

            expect(array).toHaveLength(3);
            expect(Object.isFrozen(array)).toBe(true);
            expect(array).toContain('a');
            expect(array).toContain('b');
            expect(array).toContain('c');
        });
    });
});
