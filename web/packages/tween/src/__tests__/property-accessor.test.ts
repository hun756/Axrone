import { describe, expect, it } from 'vitest';
import {
    createTweenPropertyAccessor,
    assignTweenPropertyValue,
    getOrCreateTweenPropertyAccessor,
} from '../property-accessor';

describe('property-accessor', () => {
    describe('createTweenPropertyAccessor', () => {
        it('simple path get', () => {
            const accessor = createTweenPropertyAccessor('x');
            expect(accessor.get({ x: 42 })).toBe(42);
        });

        it('simple path set', () => {
            const accessor = createTweenPropertyAccessor('x');
            const target: any = { x: 0 };
            accessor.set(target, 10);
            expect(target.x).toBe(10);
        });

        it('nested path get', () => {
            const accessor = createTweenPropertyAccessor('position.x');
            expect(accessor.get({ position: { x: 5 } })).toBe(5);
        });

        it('nested path set', () => {
            const accessor = createTweenPropertyAccessor('position.x');
            const target: any = { position: { x: 0 } };
            accessor.set(target, 99);
            expect(target.position.x).toBe(99);
        });

        it('deep nested path get', () => {
            const accessor = createTweenPropertyAccessor('a.b.c');
            expect(accessor.get({ a: { b: { c: 7 } } })).toBe(7);
        });

        it('numeric path parts', () => {
            const accessor = createTweenPropertyAccessor('0');
            expect(accessor.get([10, 20, 30])).toBe(10);
        });

        it('get returns undefined for null target', () => {
            const accessor = createTweenPropertyAccessor('x');
            expect(accessor.get(null)).toBeUndefined();
        });

        it('get returns undefined for undefined target', () => {
            const accessor = createTweenPropertyAccessor('x');
            expect(accessor.get(undefined)).toBeUndefined();
        });

        it('get returns undefined for missing intermediate', () => {
            const accessor = createTweenPropertyAccessor('a.b.c');
            expect(accessor.get({ a: {} })).toBeUndefined();
        });

        it('set is no-op for null target', () => {
            const accessor = createTweenPropertyAccessor('x');
            expect(() => accessor.set(null, 10)).not.toThrow();
        });

        it('set is no-op for non-object target', () => {
            const accessor = createTweenPropertyAccessor('x');
            expect(() => accessor.set(42, 10)).not.toThrow();
        });

        it('set auto-vivifies missing intermediate as object', () => {
            const accessor = createTweenPropertyAccessor('a.b');
            const target: any = {};
            accessor.set(target, 42);
            expect(target.a.b).toBe(42);
            expect(typeof target.a).toBe('object');
            expect(Array.isArray(target.a)).toBe(false);
        });

        it('set auto-vivifies missing intermediate as array when next part is numeric', () => {
            const accessor = createTweenPropertyAccessor('items.0');
            const target: any = {};
            accessor.set(target, 99);
            expect(Array.isArray(target.items)).toBe(true);
            expect(target.items[0]).toBe(99);
        });

        it('path and parts are exposed', () => {
            const accessor = createTweenPropertyAccessor('a.b.c');
            expect(accessor.path).toBe('a.b.c');
            expect(accessor.parts).toEqual(['a', 'b', 'c']);
        });
    });

    describe('assignTweenPropertyValue', () => {
        it('typed array in-place set', () => {
            const existing = new Float32Array([0, 0, 0]);
            const value = new Float32Array([1, 2, 3]);
            const result = assignTweenPropertyValue(existing, value);
            expect(result).toBe(true);
            expect(Array.from(existing)).toEqual([1, 2, 3]);
        });

        it('regular array element-wise copy', () => {
            const existing = [0, 0, 0];
            const value = [10, 20, 30];
            const result = assignTweenPropertyValue(existing, value);
            expect(result).toBe(true);
            expect(existing).toEqual([10, 20, 30]);
        });

        it('length mismatch returns false for typed arrays', () => {
            const existing = new Float32Array([0, 0]);
            const value = new Float32Array([1, 2, 3]);
            const result = assignTweenPropertyValue(existing, value);
            expect(result).toBe(false);
        });

        it('length mismatch returns false for regular arrays', () => {
            const existing = [0, 0];
            const value = [1, 2, 3];
            const result = assignTweenPropertyValue(existing, value);
            expect(result).toBe(false);
        });

        it('non-array types return false', () => {
            expect(assignTweenPropertyValue(1, 2)).toBe(false);
            expect(assignTweenPropertyValue('a', 'b')).toBe(false);
            expect(assignTweenPropertyValue({}, {})).toBe(false);
        });

        it('mixed array types return false', () => {
            const existing = [0, 0, 0];
            const value = new Float32Array([1, 2, 3]);
            expect(assignTweenPropertyValue(existing, value)).toBe(false);
        });
    });

    describe('getOrCreateTweenPropertyAccessor', () => {
        it('creates new accessor on cache miss', () => {
            const cache = new Map();
            const accessor = getOrCreateTweenPropertyAccessor(cache, 'x');
            expect(accessor.path).toBe('x');
            expect(cache.has('x')).toBe(true);
        });

        it('returns cached accessor on cache hit', () => {
            const cache = new Map();
            const first = getOrCreateTweenPropertyAccessor(cache, 'x');
            const second = getOrCreateTweenPropertyAccessor(cache, 'x');
            expect(first).toBe(second);
        });

        it('different paths create different accessors', () => {
            const cache = new Map();
            const a = getOrCreateTweenPropertyAccessor(cache, 'x');
            const b = getOrCreateTweenPropertyAccessor(cache, 'y');
            expect(a).not.toBe(b);
            expect(cache.size).toBe(2);
        });
    });
});
