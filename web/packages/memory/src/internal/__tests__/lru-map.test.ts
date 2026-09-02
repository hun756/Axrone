import { describe, expect, it } from 'vitest';
import { LruMap } from '../lru-map';

describe('LruMap', () => {
    it('respects LRU eviction order when capacity is reached', () => {
        const cache = new LruMap<string, number>({ capacity: 3, order: 'least-recently-used' });

        cache.set('a', 1);
        cache.set('b', 2);
        cache.set('c', 3);
        cache.set('d', 4);

        expect(cache.size).toBe(3);
        expect(cache.has('a')).toBe(false);
        expect(cache.has('b')).toBe(true);
        expect(cache.has('c')).toBe(true);
        expect(cache.has('d')).toBe(true);
    });

    it('touch() on a key makes it the most recently used', () => {
        const cache = new LruMap<string, number>({ capacity: 3, order: 'least-recently-used' });

        cache.set('a', 1);
        cache.set('b', 2);
        cache.set('c', 3);

        cache.touch('a');

        cache.set('d', 4);
        expect(cache.has('a')).toBe(true);
        expect(cache.has('b')).toBe(false);
        expect(cache.has('c')).toBe(true);
        expect(cache.has('d')).toBe(true);
    });

    it('get() implicitly touches the key', () => {
        const cache = new LruMap<string, number>({ capacity: 3, order: 'least-recently-used' });

        cache.set('a', 1);
        cache.set('b', 2);
        cache.set('c', 3);

        expect(cache.get('a')).toBe(1);

        cache.set('d', 4);
        expect(cache.has('a')).toBe(true);
        expect(cache.has('b')).toBe(false);
    });

    it('peek() does not affect recency', () => {
        const cache = new LruMap<string, number>({ capacity: 3, order: 'least-recently-used' });

        cache.set('a', 1);
        cache.set('b', 2);
        cache.set('c', 3);

        cache.peek('a');

        cache.set('d', 4);
        expect(cache.has('a')).toBe(false);
    });

    it('MRU order evicts the most recently used item first', () => {
        const cache = new LruMap<string, number>({ capacity: 3, order: 'most-recently-used' });

        cache.set('a', 1);
        cache.set('b', 2);
        cache.set('c', 3);

        cache.set('d', 4);
        expect(cache.has('c')).toBe(false);
        expect(cache.has('a')).toBe(true);
        expect(cache.has('b')).toBe(true);
        expect(cache.has('d')).toBe(true);
    });

    it('set() on existing key updates value and recency', () => {
        const cache = new LruMap<string, number>({ capacity: 3, order: 'least-recently-used' });

        cache.set('a', 1);
        cache.set('b', 2);
        cache.set('c', 3);

        cache.set('a', 99);

        cache.set('d', 4);
        expect(cache.peek('a')).toBe(99);
        expect(cache.has('b')).toBe(false);
    });

    it('pop() returns and removes the oldest entry', () => {
        const cache = new LruMap<string, number>({ capacity: 3, order: 'least-recently-used' });

        cache.set('a', 1);
        cache.set('b', 2);
        cache.set('c', 3);

        const popped = cache.pop();
        expect(popped).toEqual({ key: 'a', value: 1, recency: expect.any(Number) });
        expect(cache.has('a')).toBe(false);
        expect(cache.size).toBe(2);
    });

    it('peekOldest() returns the entry without removing', () => {
        const cache = new LruMap<string, number>({ capacity: 3, order: 'least-recently-used' });

        cache.set('a', 1);
        cache.set('b', 2);

        const oldest = cache.peekOldest();
        expect(oldest?.key).toBe('a');
        expect(cache.size).toBe(2);
    });

    it('delete() removes the entry and the recency record', () => {
        const cache = new LruMap<string, number>({ capacity: 3, order: 'least-recently-used' });

        cache.set('a', 1);
        cache.set('b', 2);

        expect(cache.delete('a')).toBe(true);
        expect(cache.has('a')).toBe(false);
        expect(cache.size).toBe(1);
    });

    it('clear() resets the cache', () => {
        const cache = new LruMap<string, number>({ capacity: 3, order: 'least-recently-used' });

        cache.set('a', 1);
        cache.set('b', 2);
        cache.clear();
        expect(cache.size).toBe(0);
        expect(cache.has('a')).toBe(false);
    });

    it('stress: 10,000 random operations preserve LRU invariant', () => {
        const cache = new LruMap<number, number>({ capacity: 50, order: 'least-recently-used' });

        const reference = new Map<number, number>();
        let expectedSize = 0;

        for (let i = 0; i < 10_000; i++) {
            const op = Math.random();
            if (op < 0.5 || expectedSize < 50) {
                const key = Math.floor(Math.random() * 200);
                const value = Math.floor(Math.random() * 1000);
                cache.set(key, value);
                if (reference.has(key)) {
                    reference.set(key, value);
                } else if (reference.size < 50) {
                    reference.set(key, value);
                    expectedSize++;
                } else {
                    let oldestKey: number | undefined;
                    let oldestRecency = Infinity;
                    for (const [k, v] of reference) {
                        const recency = v;
                        if (recency < oldestRecency) {
                            oldestRecency = recency;
                            oldestKey = k;
                        }
                    }
                    if (oldestKey !== undefined && oldestKey !== key) {
                        reference.delete(oldestKey);
                        reference.set(key, value);
                    }
                }
                reference.set(key, i);
            } else if (op < 0.7) {
                const key = Math.floor(Math.random() * 200);
                cache.touch(key);
                if (reference.has(key)) reference.set(key, i);
            } else if (op < 0.9) {
                const key = Math.floor(Math.random() * 200);
                cache.get(key);
                if (reference.has(key)) reference.set(key, i);
            } else {
                const key = Math.floor(Math.random() * 200);
                cache.delete(key);
                if (reference.delete(key)) expectedSize--;
            }
        }

        expect(cache.size).toBe(reference.size);
        for (const key of reference.keys()) {
            expect(cache.has(key)).toBe(true);
        }
    });

    it('zero capacity: set() always returns without storing', () => {
        const cache = new LruMap<string, number>({ capacity: 0, order: 'least-recently-used' });
        cache.set('a', 1);
        cache.set('b', 2);
        expect(cache.size).toBe(0);
        expect(cache.has('a')).toBe(false);
    });

    it('handles thousands of touch() calls without losing invariants', () => {
        const cache = new LruMap<number, number>({ capacity: 100, order: 'least-recently-used' });
        for (let i = 0; i < 100; i++) cache.set(i, i);
        for (let i = 0; i < 5_000; i++) {
            cache.touch(i % 100);
        }
        expect(cache.size).toBe(100);
        const oldest = cache.peekOldest();
        expect(oldest).toBeDefined();
    });

    it('H10: recency wraparound normalizes heap and preserves eviction order', () => {
        const cache = new LruMap<string, number>({ capacity: 3, order: 'least-recently-used' });

        // Insert A, B, C (recency 1, 2, 3)
        cache.set('a', 1);
        cache.set('b', 2);
        cache.set('c', 3);

        // Force sequence to near-wraparound
        (cache as any)._debugSetSequence(4294967294);

        // Insert D (recency 4294967295)
        cache.set('d', 4);
        // A should be evicted (oldest), B/C/D remain
        expect(cache.has('a')).toBe(false);
        expect(cache.has('b')).toBe(true);
        expect(cache.has('c')).toBe(true);
        expect(cache.has('d')).toBe(true);

        // Touch B (wraps sequence to 1)
        cache.touch('b');

        // Insert E (recency 2)
        cache.set('e', 5);
        // C should be evicted (oldest after normalization), B/D/E remain
        expect(cache.has('c')).toBe(false);
        expect(cache.has('b')).toBe(true);
        expect(cache.has('d')).toBe(true);
        expect(cache.has('e')).toBe(true);

        // Verify oldest is D (recency 4294967295 normalized to 3, then B touched to 1, E inserted at 2)
        // After normalization: B=1, E=2, D=3 → oldest is D
        const oldest = cache.peekOldest();
        expect(oldest?.key).toBe('d');
    });
});
