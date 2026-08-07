import { describe, expect, it } from 'vitest';
import { PriorityQueue } from '../containers/queue/priority-queue';

describe('PriorityQueue — Advanced Methods', () => {
    describe('enqueueEntries()', () => {
        it('batch inserts entries into empty queue', () => {
            const queue = new PriorityQueue<string, number>();
            queue.enqueueEntries([
                { value: 'c', priority: 3 },
                { value: 'a', priority: 1 },
                { value: 'b', priority: 2 },
            ]);
            expect(queue.size).toBe(3);
            expect(queue.peek()).toBe('c');
        });

        it('batch inserts entries into non-empty queue', () => {
            const queue = new PriorityQueue<string, number>();
            queue.enqueue('existing', 5);
            queue.enqueueEntries([
                { value: 'new1', priority: 10 },
                { value: 'new2', priority: 1 },
            ]);
            expect(queue.size).toBe(3);
            expect(queue.peek()).toBe('new1');
        });

        it('handles empty input', () => {
            const queue = new PriorityQueue<string, number>();
            queue.enqueueEntries([]);
            expect(queue.size).toBe(0);
        });
    });

    describe('enqueueRange()', () => {
        it('is an alias for enqueueEntries', () => {
            const queue = new PriorityQueue<string, number>();
            queue.enqueueRange([
                { value: 'x', priority: 10 },
                { value: 'y', priority: 5 },
            ]);
            expect(queue.size).toBe(2);
            expect(queue.peek()).toBe('x');
        });
    });

    describe('enqueueValues()', () => {
        it('enqueues values using priority selector', () => {
            const queue = new PriorityQueue<string, number>({
                priority: (v) => v.length,
            });
            queue.enqueueValues(['a', 'bbb', 'cc']);
            expect(queue.size).toBe(3);
            expect(queue.peek()).toBe('bbb');
        });

        it('handles empty input', () => {
            const queue = new PriorityQueue<string, number>({
                priority: (v) => v.length,
            });
            queue.enqueueValues([]);
            expect(queue.size).toBe(0);
        });
    });

    describe('drain() / dequeueAll() / drainEntries()', () => {
        it('drain returns all values in priority order', () => {
            const queue = new PriorityQueue<string, number>();
            queue.enqueue('low', 1);
            queue.enqueue('high', 10);
            queue.enqueue('mid', 5);
            expect(queue.drain()).toEqual(['high', 'mid', 'low']);
            expect(queue.size).toBe(0);
        });

        it('dequeueAll is an alias for drain', () => {
            const queue = new PriorityQueue<string, number>();
            queue.enqueue('a', 1);
            queue.enqueue('b', 2);
            expect(queue.dequeueAll()).toEqual(['b', 'a']);
        });

        it('drainEntries returns entries with handles', () => {
            const queue = new PriorityQueue<string, number>();
            queue.enqueue('x', 10);
            queue.enqueue('y', 5);
            const entries = queue.drainEntries();
            expect(entries.length).toBe(2);
            expect(entries[0].value).toBe('x');
            expect(entries[0].priority).toBe(10);
            expect(entries[1].value).toBe('y');
            expect(queue.size).toBe(0);
        });

        it('drain on empty queue returns empty array', () => {
            const queue = new PriorityQueue<string, number>();
            expect(queue.drain()).toEqual([]);
        });
    });

    describe('forEach()', () => {
        it('iterates over all values', () => {
            const queue = new PriorityQueue<string, number>();
            queue.enqueue('a', 1);
            queue.enqueue('b', 2);
            queue.enqueue('c', 3);

            const visited: string[] = [];
            queue.forEach((value) => visited.push(value));
            expect(visited.length).toBe(3);
            expect(new Set(visited).size).toBe(3);
        });

        it('passes handle and queue reference', () => {
            const queue = new PriorityQueue<string, number>();
            queue.enqueue('test', 5);
            queue.forEach((value, handle, q) => {
                expect(value).toBe('test');
                expect(typeof handle).toBe('number');
                expect(q).toBe(queue);
            });
        });
    });

    describe('map()', () => {
        it('transforms all values', () => {
            const queue = new PriorityQueue<number, number>();
            queue.enqueue(1, 1);
            queue.enqueue(2, 2);
            queue.enqueue(3, 3);
            const doubled = queue.map((v) => v * 2);
            expect(doubled.sort()).toEqual([2, 4, 6]);
        });
    });

    describe('filter()', () => {
        it('filters values by predicate', () => {
            const queue = new PriorityQueue<number, number>();
            queue.enqueue(1, 1);
            queue.enqueue(2, 2);
            queue.enqueue(3, 3);
            queue.enqueue(4, 4);
            const evens = queue.filter((v) => v % 2 === 0);
            expect(evens.sort()).toEqual([2, 4]);
        });
    });

    describe('find()', () => {
        it('finds first matching value', () => {
            const queue = new PriorityQueue<string, number>();
            queue.enqueue('apple', 1);
            queue.enqueue('banana', 2);
            queue.enqueue('cherry', 3);
            expect(queue.find((v) => v.startsWith('b'))).toBe('banana');
        });

        it('returns undefined when no match', () => {
            const queue = new PriorityQueue<string, number>();
            queue.enqueue('apple', 1);
            expect(queue.find(() => false)).toBeUndefined();
        });
    });

    describe('some()', () => {
        it('returns true when at least one matches', () => {
            const queue = new PriorityQueue<number, number>();
            queue.enqueue(1, 1);
            queue.enqueue(2, 2);
            expect(queue.some((v) => v > 1)).toBe(true);
        });

        it('returns false when none match', () => {
            const queue = new PriorityQueue<number, number>();
            queue.enqueue(1, 1);
            queue.enqueue(2, 2);
            expect(queue.some((v) => v > 10)).toBe(false);
        });
    });

    describe('every()', () => {
        it('returns true when all match', () => {
            const queue = new PriorityQueue<number, number>();
            queue.enqueue(10, 1);
            queue.enqueue(20, 2);
            expect(queue.every((v) => v > 5)).toBe(true);
        });

        it('returns false when any fails', () => {
            const queue = new PriorityQueue<number, number>();
            queue.enqueue(10, 1);
            queue.enqueue(2, 2);
            expect(queue.every((v) => v > 5)).toBe(false);
        });
    });

    describe('reduce()', () => {
        it('accumulates values', () => {
            const queue = new PriorityQueue<number, number>();
            queue.enqueue(1, 1);
            queue.enqueue(2, 2);
            queue.enqueue(3, 3);
            const sum = queue.reduce((acc, v) => acc + v, 0);
            expect(sum).toBe(6);
        });
    });

    describe('values() / priorities() / handles() / entries()', () => {
        it('values() iterates all values', () => {
            const queue = new PriorityQueue<string, number>();
            queue.enqueue('a', 1);
            queue.enqueue('b', 2);
            const values = [...queue.values()];
            expect(values.length).toBe(2);
        });

        it('priorities() iterates all priorities', () => {
            const queue = new PriorityQueue<string, number>();
            queue.enqueue('a', 10);
            queue.enqueue('b', 20);
            const priorities = [...queue.priorities()];
            expect(priorities.length).toBe(2);
            expect(new Set(priorities).size).toBe(2);
        });

        it('handles() iterates all handles', () => {
            const queue = new PriorityQueue<string, number>();
            const h1 = queue.enqueue('a', 1);
            const h2 = queue.enqueue('b', 2);
            const handles = [...queue.handles()];
            expect(handles.length).toBe(2);
            expect(handles).toContain(h1);
            expect(handles).toContain(h2);
        });

        it('entries() iterates [handle, value] pairs', () => {
            const queue = new PriorityQueue<string, number>();
            const h1 = queue.enqueue('a', 1);
            const h2 = queue.enqueue('b', 2);
            const entries = [...queue.entries()];
            expect(entries.length).toBe(2);
            // Max-heap: 'b' (priority 2) is at root
            const handleValueMap = new Map(entries.map(([h, v]) => [h, v]));
            expect(handleValueMap.get(h1)).toBe('a');
            expect(handleValueMap.get(h2)).toBe('b');
        });
    });

    describe('delete()', () => {
        it('removes entry by value equality', () => {
            const queue = new PriorityQueue<string, number>();
            queue.enqueue('a', 1);
            queue.enqueue('b', 2);
            queue.enqueue('c', 3);
            expect(queue.delete('b')).toBe(true);
            expect(queue.size).toBe(2);
            expect(queue.contains('b')).toBe(false);
        });

        it('returns false when value not found', () => {
            const queue = new PriorityQueue<string, number>();
            queue.enqueue('a', 1);
            expect(queue.delete('z')).toBe(false);
            expect(queue.size).toBe(1);
        });
    });

    describe('offer() / tryDequeue() / tryPeek() / tryDequeueEntry() / tryPeekEntry()', () => {
        it('offer is an alias for enqueue', () => {
            const queue = new PriorityQueue<string, number>();
            const handle = queue.offer('test', 5);
            expect(queue.has(handle)).toBe(true);
            expect(queue.peek()).toBe('test');
        });

        it('tryDequeue is an alias for dequeue', () => {
            const queue = new PriorityQueue<string, number>();
            queue.enqueue('a', 1);
            expect(queue.tryDequeue()).toBe('a');
            expect(queue.size).toBe(0);
        });

        it('tryPeek is an alias for peek', () => {
            const queue = new PriorityQueue<string, number>();
            queue.enqueue('a', 1);
            expect(queue.tryPeek()).toBe('a');
            expect(queue.size).toBe(1); // peek doesn't remove
        });

        it('tryDequeueEntry returns entry', () => {
            const queue = new PriorityQueue<string, number>();
            queue.enqueue('a', 10);
            const entry = queue.tryDequeueEntry();
            expect(entry).toBeDefined();
            expect(entry!.value).toBe('a');
            expect(entry!.priority).toBe(10);
        });

        it('tryPeekEntry returns entry without removing', () => {
            const queue = new PriorityQueue<string, number>();
            queue.enqueue('a', 10);
            const entry = queue.tryPeekEntry();
            expect(entry).toBeDefined();
            expect(entry!.value).toBe('a');
            expect(queue.size).toBe(1);
        });

        it('tryDequeue on empty returns undefined', () => {
            const queue = new PriorityQueue<string, number>();
            expect(queue.tryDequeue()).toBeUndefined();
        });

        it('tryPeek on empty returns undefined', () => {
            const queue = new PriorityQueue<string, number>();
            expect(queue.tryPeek()).toBeUndefined();
        });
    });

    describe('update()', () => {
        it('updates value and priority', () => {
            const queue = new PriorityQueue<string, number>();
            const handle = queue.enqueue('old', 1);
            expect(queue.update(handle, 'new', 100)).toBe(true);
            expect(queue.peek()).toBe('new');
            expect(queue.peekPriority()).toBe(100);
        });

        it('updates value only (uses selector if available)', () => {
            const queue = new PriorityQueue<{ name: string; score: number }, number>({
                priority: (v) => v.score,
            });
            const handle = queue.enqueue({ name: 'a', score: 1 });
            expect(queue.update(handle, { name: 'b', score: 50 })).toBe(true);
            expect(queue.peek()!.name).toBe('b');
            expect(queue.peekPriority()).toBe(50);
        });

        it('returns false for invalid handle', () => {
            const queue = new PriorityQueue<string, number>();
            expect(queue.update(999 as never, 'x', 1)).toBe(false);
        });
    });
});
