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
});
