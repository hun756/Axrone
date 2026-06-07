import { describe, it, expect } from 'vitest';
import {
    CircularBuffer,
    CircularBufferError,
    BufferEmptyError,
    BufferOverflowError,
    InvalidCapacityError,
    createCircularBuffer,
} from '../circular-buffer';

describe('CircularBuffer', () => {
    describe('construction', () => {
        it('creates buffer with given capacity', () => {
            const cb = new CircularBuffer<number>(10);
            expect(cb.capacity).toBe(10);
            expect(cb.size).toBe(0);
            expect(cb.isEmpty).toBe(true);
            expect(cb.isFull).toBe(false);
        });

        it('throws on invalid capacity (zero, negative, non-integer)', () => {
            expect(() => new CircularBuffer<number>(0)).toThrow(InvalidCapacityError);
            expect(() => new CircularBuffer<number>(-5)).toThrow(InvalidCapacityError);
            expect(() => new CircularBuffer<number>(1.5)).toThrow(InvalidCapacityError);
            expect(() => new CircularBuffer<number>(NaN)).toThrow(InvalidCapacityError);
        });

        it('createCircularBuffer factory works', () => {
            const cb = createCircularBuffer<string>(5);
            expect(cb).toBeInstanceOf(CircularBuffer);
            expect(cb.capacity).toBe(5);
        });
    });

    describe('push / pop / shift / unshift', () => {
        it('pushes and pops in LIFO order', () => {
            const cb = new CircularBuffer<number>(5);
            cb.push(1, 2, 3);
            expect(cb.size).toBe(3);
            expect(cb.pop()).toBe(3);
            expect(cb.pop()).toBe(2);
            expect(cb.size).toBe(1);
        });

        it('pops in FIFO order from front via shift', () => {
            const cb = new CircularBuffer<number>(5);
            cb.push(1, 2, 3);
            expect(cb.shift()).toBe(1);
            expect(cb.shift()).toBe(2);
            expect(cb.size).toBe(1);
        });

        it('unshift prepends', () => {
            const cb = new CircularBuffer<number>(5);
            cb.unshift(1, 2);
            cb.unshift(0);
            expect(cb.toArray()).toEqual([0, 1, 2]);
        });

        it('pop throws on empty', () => {
            const cb = new CircularBuffer<number>(3);
            expect(() => cb.pop()).toThrow(BufferEmptyError);
            expect(() => cb.shift()).toThrow(BufferEmptyError);
        });

        it('overwrite policy drops oldest on overflow', () => {
            const cb = new CircularBuffer<number>(3, { overflowPolicy: 'overwrite' });
            cb.push(1, 2, 3, 4, 5);
            expect(cb.toArray()).toEqual([3, 4, 5]);
            expect(cb.size).toBe(3);
        });

        it('reject policy throws on overflow', () => {
            const cb = new CircularBuffer<number>(3, { overflowPolicy: 'reject' });
            cb.push(1, 2, 3);
            expect(() => cb.push(4)).toThrow(BufferOverflowError);
        });

        it('onOverflow callback is invoked with dropped item', () => {
            const dropped: number[] = [];
            const cb = new CircularBuffer<number>(3, {
                onOverflow: (item) => dropped.push(item),
            });
            cb.push(1, 2, 3, 4, 5);
            expect(dropped).toEqual([1, 2]);
        });
    });

    describe('accessors', () => {
        it('first and last return correct items', () => {
            const cb = new CircularBuffer<string>(5);
            cb.push('a', 'b', 'c');
            expect(cb.first()).toBe('a');
            expect(cb.last()).toBe('c');
            expect(cb.at(0)).toBe('a');
            expect(cb.at(2)).toBe('c');
            expect(cb.at(-1)).toBe('c');
            expect(cb.at(5)).toBeUndefined();
        });

        it('indexOf and includes work', () => {
            const cb = new CircularBuffer<number>(5);
            cb.push(10, 20, 30, 40);
            expect(cb.indexOf(30)).toBe(2);
            expect(cb.indexOf(99)).toBe(-1);
            expect(cb.includes(20)).toBe(true);
            expect(cb.includes(99)).toBe(false);
        });
    });

    describe('iteration', () => {
        it('is iterable in order', () => {
            const cb = new CircularBuffer<number>(5);
            cb.push(1, 2, 3);
            expect([...cb]).toEqual([1, 2, 3]);
        });

        it('entries returns [index, value] pairs', () => {
            const cb = new CircularBuffer<string>(3);
            cb.push('x', 'y');
            expect([...cb.entries()]).toEqual([
                [0, 'x'],
                [1, 'y'],
            ]);
        });

        it('reverse iterator yields in reverse', () => {
            const cb = new CircularBuffer<number>(5);
            cb.push(1, 2, 3, 4);
            expect([...cb.reverse()]).toEqual([4, 3, 2, 1]);
        });
    });

    describe('reduce / map / filter', () => {
        it('reduce sums all values', () => {
            const cb = new CircularBuffer<number>(5);
            cb.push(1, 2, 3, 4);
            expect(cb.reduce((a, b) => a + b, 0)).toBe(10);
        });

        it('reduce with no initial value uses first', () => {
            const cb = new CircularBuffer<number>(5);
            cb.push(1, 2, 3, 4);
            expect(cb.reduce((a, b) => a + b)).toBe(10);
        });

        it('map transforms', () => {
            const cb = new CircularBuffer<number>(5);
            cb.push(1, 2, 3);
            expect(cb.map((x) => x * 2)).toEqual([2, 4, 6]);
        });

        it('filter keeps matching', () => {
            const cb = new CircularBuffer<number>(10);
            cb.push(1, 2, 3, 4, 5);
            expect(cb.filter((x) => x % 2 === 0)).toEqual([2, 4]);
        });
    });

    describe('static factories', () => {
        it('CircularBuffer.from', () => {
            const cb = CircularBuffer.from([1, 2, 3], { capacity: 5 });
            expect(cb.size).toBe(3);
            expect(cb.toArray()).toEqual([1, 2, 3]);
        });

        it('CircularBuffer.of', () => {
            const cb = CircularBuffer.of('a', 'b', 'c');
            expect(cb.size).toBe(3);
            expect(cb.toArray()).toEqual(['a', 'b', 'c']);
        });

        it('CircularBuffer.of throws on empty', () => {
            expect(() => CircularBuffer.of<number>()).toThrow(CircularBufferError);
        });
    });

    describe('serialization', () => {
        it('toJSON includes tag, capacity, items', () => {
            const cb = new CircularBuffer<number>(5);
            cb.push(1, 2, 3);
            const json = cb.toJSON();
            expect(json.$tag).toBe('CircularBuffer@1');
            expect(json.capacity).toBe(5);
            expect(json.items).toEqual([1, 2, 3]);
        });

        it('fromJSON roundtrip', () => {
            const original = new CircularBuffer<string>(4);
            original.push('a', 'b');
            const restored = CircularBuffer.fromJSON<string>(original.toJSON());
            expect(restored.toArray()).toEqual(['a', 'b']);
        });

        it('fromJSON rejects invalid tag', () => {
            expect(() => CircularBuffer.fromJSON({ $tag: 'wrong' })).toThrow(CircularBufferError);
        });
    });

    describe('lifecycle', () => {
        it('clear empties the buffer', () => {
            const cb = new CircularBuffer<number>(5);
            cb.push(1, 2, 3);
            cb.clear();
            expect(cb.size).toBe(0);
            expect(cb.isEmpty).toBe(true);
        });

        it('drain returns items and empties', () => {
            const cb = new CircularBuffer<number>(5);
            cb.push(1, 2, 3);
            expect(cb.drain()).toEqual([1, 2, 3]);
            expect(cb.size).toBe(0);
        });

        it('clone produces independent copy', () => {
            const cb = new CircularBuffer<number>(5);
            cb.push(1, 2, 3);
            const c = cb.clone();
            c.push(99);
            expect(cb.toArray()).toEqual([1, 2, 3]);
            expect(c.toArray()).toEqual([1, 2, 3, 99]);
        });
    });
});
