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
});
