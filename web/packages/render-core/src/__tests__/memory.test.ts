import { describe, expect, it } from 'vitest';
import {
    StringKeyCache,
    ReusableList,
    MutableObjectArena,
    SortableRenderList,
} from '../memory';

describe('StringKeyCache', () => {
    it('returns consistent hash for same string', () => {
        const cache = new StringKeyCache();
        const h1 = cache.get('hello');
        const h2 = cache.get('hello');
        expect(h1).toBe(h2);
    });

    it('returns different hashes for different strings', () => {
        const cache = new StringKeyCache();
        expect(cache.get('abc')).not.toBe(cache.get('xyz'));
    });

    it('returns a number', () => {
        const cache = new StringKeyCache();
        expect(typeof cache.get('test')).toBe('number');
    });

    it('clear resets the cache', () => {
        const cache = new StringKeyCache();
        cache.get('a');
        cache.clear();
        const h = cache.get('a');
        expect(typeof h).toBe('number');
    });
});

describe('ReusableList', () => {
    it('starts with length 0', () => {
        const list = new ReusableList<number>();
        expect(list.length).toBe(0);
    });

    it('push increments length and returns new length', () => {
        const list = new ReusableList<string>();
        expect(list.push('a')).toBe(1);
        expect(list.push('b')).toBe(2);
        expect(list.length).toBe(2);
    });

    it('at returns item at index', () => {
        const list = new ReusableList<number>();
        list.push(10);
        list.push(20);
        expect(list.at(0)).toBe(10);
        expect(list.at(1)).toBe(20);
    });

    it('at throws RangeError for out-of-bounds index', () => {
        const list = new ReusableList<number>();
        list.push(1);
        expect(() => list.at(-1)).toThrow(RangeError);
        expect(() => list.at(1)).toThrow(RangeError);
    });

    it('reset sets length to 0 without deallocating', () => {
        const list = new ReusableList<number>();
        list.push(1);
        list.push(2);
        list.reset();
        expect(list.length).toBe(0);
        list.push(99);
        expect(list.at(0)).toBe(99);
    });

    it('clear resets length and empties backing array', () => {
        const list = new ReusableList<number>();
        list.push(1);
        list.clear();
        expect(list.length).toBe(0);
    });

    it('toArray returns snapshot of active items', () => {
        const list = new ReusableList<number>();
        list.push(1);
        list.push(2);
        list.push(3);
        expect(list.toArray()).toEqual([1, 2, 3]);
    });

    it('toArray respects length after reset', () => {
        const list = new ReusableList<number>();
        list.push(1);
        list.push(2);
        list.reset();
        list.push(99);
        expect(list.toArray()).toEqual([99]);
    });

    it('supports iteration via Symbol.iterator', () => {
        const list = new ReusableList<number>();
        list.push(10);
        list.push(20);
        list.push(30);
        const collected: number[] = [];
        for (const item of list) {
            collected.push(item);
        }
        expect(collected).toEqual([10, 20, 30]);
    });

    it('respects initial capacity', () => {
        const list = new ReusableList<number>(16);
        expect(list.length).toBe(0);
        list.push(1);
        expect(list.at(0)).toBe(1);
    });
});

describe('MutableObjectArena', () => {
    it('acquires objects via factory', () => {
        const arena = new MutableObjectArena((i) => ({ id: i, value: 0 }));
        const obj = arena.acquire();
        expect(obj.id).toBe(0);
        expect(arena.count).toBe(1);
    });

    it('reuses objects after reset', () => {
        const arena = new MutableObjectArena((i) => ({ id: i, value: 0 }));
        const first = arena.acquire();
        first.value = 42;
        arena.reset();
        const reused = arena.acquire();
        expect(reused).toBe(first);
        expect(reused.value).toBe(42);
    });

    it('values returns only active items', () => {
        const arena = new MutableObjectArena((i) => ({ id: i }));
        arena.acquire();
        arena.acquire();
        arena.acquire();
        arena.reset();
        arena.acquire();
        expect(arena.values()).toHaveLength(1);
    });

    it('creates new objects when exceeding previous capacity', () => {
        let factoryCalls = 0;
        const arena = new MutableObjectArena((i) => {
            factoryCalls++;
            return { id: i };
        });
        arena.acquire();
        arena.acquire();
        arena.acquire();
        expect(factoryCalls).toBe(3);
        arena.reset();
        arena.acquire();
        arena.acquire();
        arena.acquire();
        arena.acquire();
        expect(factoryCalls).toBe(4);
    });
});

describe('SortableRenderList', () => {
    it('push adds items with sort keys', () => {
        const list = new SortableRenderList<string>();
        expect(list.push('a', 1)).toBe(1);
        expect(list.push('b', 2)).toBe(2);
        expect(list.length).toBe(2);
    });

    it('sort by primary key ascending (default)', () => {
        const list = new SortableRenderList<string>();
        list.push('c', 3);
        list.push('a', 1);
        list.push('b', 2);
        list.sort();
        expect(list.toArray()).toEqual(['a', 'b', 'c']);
    });

    it('sort by primary key descending with order [-1, 1, 1]', () => {
        const list = new SortableRenderList<string>();
        list.push('a', 1);
        list.push('c', 3);
        list.push('b', 2);
        list.sort([-1, 1, 1]);
        expect(list.toArray()).toEqual(['c', 'b', 'a']);
    });

    it('sort by secondary key when primary is equal', () => {
        const list = new SortableRenderList<string>();
        list.push('far', 1, 100);
        list.push('near', 1, 1);
        list.push('mid', 1, 50);
        list.sort();
        expect(list.toArray()).toEqual(['near', 'mid', 'far']);
    });

    it('sort by tertiary key when primary and secondary are equal', () => {
        const list = new SortableRenderList<string>();
        list.push('c', 1, 1, 30);
        list.push('a', 1, 1, 10);
        list.push('b', 1, 1, 20);
        list.sort();
        expect(list.toArray()).toEqual(['a', 'b', 'c']);
    });

    it('handles large dataset triggering quicksort partition (>16 items)', () => {
        const list = new SortableRenderList<number>();
        const expected: number[] = [];
        for (let i = 50; i >= 1; i--) {
            list.push(i, i);
            expected.push(i);
        }
        expected.sort((a, b) => a - b);
        list.sort();
        expect(list.toArray()).toEqual(expected);
    });

    it('sort with 0 or 1 items is a no-op', () => {
        const empty = new SortableRenderList<string>();
        empty.sort();
        expect(empty.length).toBe(0);

        const single = new SortableRenderList<string>();
        single.push('only', 1);
        single.sort();
        expect(single.toArray()).toEqual(['only']);
    });

    it('reset sets length to 0', () => {
        const list = new SortableRenderList<string>();
        list.push('a', 1);
        list.push('b', 2);
        list.reset();
        expect(list.length).toBe(0);
    });

    it('clear resets everything', () => {
        const list = new SortableRenderList<string>();
        list.push('a', 1);
        list.clear();
        expect(list.length).toBe(0);
        list.push('b', 5);
        expect(list.toArray()).toEqual(['b']);
    });

    it('at throws RangeError for out-of-bounds index', () => {
        const list = new SortableRenderList<number>();
        list.push(1, 1);
        expect(() => list.at(-1)).toThrow(RangeError);
        expect(() => list.at(1)).toThrow(RangeError);
    });

    it('supports iteration via Symbol.iterator', () => {
        const list = new SortableRenderList<number>();
        list.push(10, 1);
        list.push(20, 2);
        const collected: number[] = [];
        for (const item of list) {
            collected.push(item);
        }
        expect(collected).toEqual([10, 20]);
    });

    it('grows capacity via power-of-2 doubling', () => {
        const list = new SortableRenderList<number>(4);
        for (let i = 0; i < 100; i++) {
            list.push(i, i);
        }
        expect(list.length).toBe(100);
        list.sort();
        const arr = list.toArray();
        for (let i = 1; i < arr.length; i++) {
            expect(arr[i]).toBeGreaterThanOrEqual(arr[i - 1]!);
        }
    });
});
