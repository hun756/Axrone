import { describe, expect, it } from 'vitest';
import {
    isTweenTypedArray,
    cloneTweenArrayLike,
    allocateSequenceLike,
    deepCloneTweenValue,
} from '../runtime-utils';

describe('runtime-utils', () => {
    describe('isTweenTypedArray', () => {
        it('Float32Array returns true', () => {
            expect(isTweenTypedArray(new Float32Array(4))).toBe(true);
        });

        it('Uint8Array returns true', () => {
            expect(isTweenTypedArray(new Uint8Array(4))).toBe(true);
        });

        it('Int32Array returns true', () => {
            expect(isTweenTypedArray(new Int32Array(4))).toBe(true);
        });

        it('Float64Array returns true', () => {
            expect(isTweenTypedArray(new Float64Array(2))).toBe(true);
        });

        it('DataView returns false', () => {
            expect(isTweenTypedArray(new DataView(new ArrayBuffer(8)))).toBe(false);
        });

        it('BigInt64Array returns false', () => {
            expect(isTweenTypedArray(new BigInt64Array(2))).toBe(false);
        });

        it('BigUint64Array returns false', () => {
            expect(isTweenTypedArray(new BigUint64Array(2))).toBe(false);
        });

        it('regular array returns false', () => {
            expect(isTweenTypedArray([1, 2, 3])).toBe(false);
        });

        it('null returns false', () => {
            expect(isTweenTypedArray(null)).toBe(false);
        });

        it('undefined returns false', () => {
            expect(isTweenTypedArray(undefined)).toBe(false);
        });

        it('number returns false', () => {
            expect(isTweenTypedArray(42)).toBe(false);
        });
    });

    describe('cloneTweenArrayLike', () => {
        it('clones typed array as independent reference', () => {
            const original = new Float32Array([1, 2, 3]);
            const clone = cloneTweenArrayLike(original);
            expect(clone).toBeInstanceOf(Float32Array);
            expect(Array.from(clone)).toEqual([1, 2, 3]);
            clone[0] = 99;
            expect(original[0]).toBe(1);
        });

        it('clones regular array', () => {
            const original = [10, 20, 30];
            const clone = cloneTweenArrayLike(original);
            expect(Array.isArray(clone)).toBe(true);
            expect(clone).toEqual([10, 20, 30]);
            (clone as number[])[0] = 99;
            expect(original[0]).toBe(10);
        });

        it('clones ArrayLike object', () => {
            const arrayLike = { 0: 5, 1: 10, 2: 15, length: 3 };
            const clone = cloneTweenArrayLike(arrayLike);
            expect(Array.isArray(clone)).toBe(true);
            expect(clone).toEqual([5, 10, 15]);
        });

        it('handles empty typed array', () => {
            const clone = cloneTweenArrayLike(new Uint8Array(0));
            expect(clone).toBeInstanceOf(Uint8Array);
            expect(clone.length).toBe(0);
        });

        it('handles empty regular array', () => {
            const clone = cloneTweenArrayLike([]);
            expect(Array.isArray(clone)).toBe(true);
            expect(clone).toEqual([]);
        });
    });

    describe('allocateSequenceLike', () => {
        it('typed array template returns typed array', () => {
            const result = allocateSequenceLike(new Float32Array(0), 5);
            expect(result).toBeInstanceOf(Float32Array);
            expect(result.length).toBe(5);
        });

        it('regular array template returns Array', () => {
            const result = allocateSequenceLike([1, 2], 3);
            expect(Array.isArray(result)).toBe(true);
            expect(result.length).toBe(3);
        });
    });

    describe('deepCloneTweenValue', () => {
        it('null returns null', () => {
            expect(deepCloneTweenValue(null)).toBeNull();
        });

        it('undefined returns undefined', () => {
            expect(deepCloneTweenValue(undefined)).toBeUndefined();
        });

        it('number returns same value', () => {
            expect(deepCloneTweenValue(42)).toBe(42);
        });

        it('string returns same value', () => {
            expect(deepCloneTweenValue('hello')).toBe('hello');
        });

        it('clones Date', () => {
            const date = new Date(2024, 0, 1);
            const clone = deepCloneTweenValue(date);
            expect(clone).toBeInstanceOf(Date);
            expect(clone.getTime()).toBe(date.getTime());
            expect(clone).not.toBe(date);
        });

        it('clones Map', () => {
            const map = new Map([['a', 1], ['b', 2]]);
            const clone = deepCloneTweenValue(map);
            expect(clone).toBeInstanceOf(Map);
            expect(clone.get('a')).toBe(1);
            expect(clone.get('b')).toBe(2);
            expect(clone).not.toBe(map);
        });

        it('clones Set', () => {
            const set = new Set([1, 2, 3]);
            const clone = deepCloneTweenValue(set);
            expect(clone).toBeInstanceOf(Set);
            expect(clone.has(1)).toBe(true);
            expect(clone.has(3)).toBe(true);
            expect(clone).not.toBe(set);
        });

        it('clones nested object', () => {
            const obj = { a: { b: { c: 42 } }, d: [1, 2] };
            const clone = deepCloneTweenValue(obj);
            expect(clone.a.b.c).toBe(42);
            expect(clone.d).toEqual([1, 2]);
            expect(clone.a).not.toBe(obj.a);
            expect(clone.d).not.toBe(obj.d);
        });

        it('clones array (top-level independent)', () => {
            const arr = [1, 2, 3];
            const clone = deepCloneTweenValue(arr);
            expect(clone).toEqual([1, 2, 3]);
            expect(clone).not.toBe(arr);
        });

        it('clones typed array', () => {
            const typed = new Float32Array([1, 2, 3]);
            const clone = deepCloneTweenValue(typed);
            expect(clone).toBeInstanceOf(Float32Array);
            expect(Array.from(clone)).toEqual([1, 2, 3]);
            expect(clone).not.toBe(typed);
        });

        it('preserves prototype', () => {
            class Custom {
                value = 42;
            }
            const obj = new Custom();
            const clone = deepCloneTweenValue(obj);
            expect(clone).toBeInstanceOf(Custom);
            expect(clone.value).toBe(42);
        });
    });
});
