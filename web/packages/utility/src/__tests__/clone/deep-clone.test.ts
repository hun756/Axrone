import { describe, it, expect } from 'vitest';
import { deepClone, cloneData } from '../../clone/deep-clone';

describe('deepClone', () => {
    describe('primitives', () => {
        it('returns null as-is', () => expect(deepClone(null)).toBeNull());
        it('returns undefined as-is', () => expect(deepClone(undefined)).toBeUndefined());
        it('returns number as-is', () => expect(deepClone(42)).toBe(42));
        it('returns string as-is', () => expect(deepClone('hello')).toBe('hello'));
        it('returns boolean as-is', () => expect(deepClone(true)).toBe(true));
        it('returns symbol as-is', () => {
            const s = Symbol('x');
            expect(deepClone(s)).toBe(s);
        });
        it('returns bigint as-is', () => expect(deepClone(10n)).toBe(10n));
    });

    describe('arrays', () => {
        it('clones empty array', () => {
            const arr: number[] = [];
            const cloned = deepClone(arr);
            expect(cloned).toEqual([]);
            expect(cloned).not.toBe(arr);
        });
        it('clones flat array', () => {
            const arr = [1, 2, 3];
            const cloned = deepClone(arr);
            expect(cloned).toEqual([1, 2, 3]);
            expect(cloned).not.toBe(arr);
        });
        it('deep-clones nested arrays', () => {
            const arr = [[1, [2]], 3];
            const cloned = deepClone(arr);
            expect(cloned).toEqual([[1, [2]], 3]);
            expect(cloned[0]).not.toBe(arr[0]);
            expect((cloned[0] as number[])[1]).not.toBe((arr[0] as unknown[])[1]);
        });
    });

    describe('Date', () => {
        it('creates new Date with same time', () => {
            const d = new Date('2024-01-15T12:00:00Z');
            const cloned = deepClone(d);
            expect(cloned).toBeInstanceOf(Date);
            expect(cloned).not.toBe(d);
            expect(cloned.getTime()).toBe(d.getTime());
        });
    });

    describe('Map', () => {
        it('clones empty Map', () => {
            const m = new Map();
            const cloned = deepClone(m);
            expect(cloned).toBeInstanceOf(Map);
            expect(cloned).not.toBe(m);
            expect(cloned.size).toBe(0);
        });
        it('deep-clones keys and values', () => {
            const inner = { x: 1 };
            const m = new Map<string, object>([['a', inner]]);
            const cloned = deepClone(m);
            expect(cloned.size).toBe(1);
            expect(cloned.get('a')).toEqual({ x: 1 });
            expect(cloned.get('a')).not.toBe(inner);
        });
    });

    describe('Set', () => {
        it('clones empty Set', () => {
            const s = new Set();
            const cloned = deepClone(s);
            expect(cloned).toBeInstanceOf(Set);
            expect(cloned).not.toBe(s);
            expect(cloned.size).toBe(0);
        });
        it('deep-clones values', () => {
            const inner = { x: 1 };
            const s = new Set([inner, 42]);
            const cloned = deepClone(s);
            expect(cloned.size).toBe(2);
            expect(cloned.has(42)).toBe(true);
            const values = [...cloned];
            expect(values[0]).toEqual({ x: 1 });
            expect(values[0]).not.toBe(inner);
        });
    });

    describe('TypedArrays', () => {
        it('clones Uint8Array', () => {
            const arr = new Uint8Array([1, 2, 3]);
            const cloned = deepClone(arr);
            expect(cloned).toBeInstanceOf(Uint8Array);
            expect(cloned).not.toBe(arr);
            expect(Array.from(cloned)).toEqual([1, 2, 3]);
        });
        it('clones Float32Array', () => {
            const arr = new Float32Array([1.5, 2.5]);
            const cloned = deepClone(arr);
            expect(cloned).toBeInstanceOf(Float32Array);
            expect(cloned).not.toBe(arr);
            expect(cloned[0]).toBeCloseTo(1.5);
        });
        it('clones Int32Array', () => {
            const arr = new Int32Array([10, 20]);
            const cloned = deepClone(arr);
            expect(cloned).toBeInstanceOf(Int32Array);
            expect(cloned).not.toBe(arr);
            expect(Array.from(cloned)).toEqual([10, 20]);
        });
    });

    describe('DataView', () => {
        it('clones DataView with correct buffer content', () => {
            const buf = new ArrayBuffer(8);
            const view = new DataView(buf);
            view.setUint8(0, 0xff);
            view.setUint8(7, 0xab);
            const cloned = deepClone(view);
            expect(cloned).toBeInstanceOf(DataView);
            expect(cloned).not.toBe(view);
            expect(cloned.byteLength).toBe(8);
            expect(cloned.getUint8(0)).toBe(0xff);
            expect(cloned.getUint8(7)).toBe(0xab);
        });
    });

    describe('ArrayBuffer', () => {
        it('clones ArrayBuffer', () => {
            const buf = new ArrayBuffer(4);
            new Uint8Array(buf).set([1, 2, 3, 4]);
            const cloned = deepClone(buf);
            expect(cloned).toBeInstanceOf(ArrayBuffer);
            expect(cloned).not.toBe(buf);
            expect(cloned.byteLength).toBe(4);
            expect(Array.from(new Uint8Array(cloned))).toEqual([1, 2, 3, 4]);
        });
    });

    describe('plain objects', () => {
        it('clones empty object', () => {
            const cloned = deepClone({});
            expect(cloned).toEqual({});
            expect(cloned).not.toBe({});
        });
        it('clones flat object', () => {
            const obj = { a: 1, b: 'two' };
            const cloned = deepClone(obj);
            expect(cloned).toEqual({ a: 1, b: 'two' });
            expect(cloned).not.toBe(obj);
        });
        it('deep-clones nested objects', () => {
            const inner = { x: { y: 1 } };
            const obj = { nested: inner };
            const cloned = deepClone(obj);
            expect(cloned.nested.x).toEqual({ y: 1 });
            expect(cloned.nested).not.toBe(inner);
            expect(cloned.nested.x).not.toBe(inner.x);
        });
    });

    describe('circular references', () => {
        it('handles self-referencing object', () => {
            const obj: Record<string, unknown> = { a: 1 };
            obj.self = obj;
            const cloned = deepClone(obj);
            expect(cloned.a).toBe(1);
            expect(cloned.self).toBe(cloned);
            expect(cloned.self).not.toBe(obj);
        });
        it('handles mutual references', () => {
            const a: Record<string, unknown> = { name: 'a' };
            const b: Record<string, unknown> = { name: 'b' };
            a.ref = b;
            b.ref = a;
            const cloned = deepClone(a);
            expect(cloned.ref).not.toBe(b);
            expect((cloned.ref as Record<string, unknown>).ref).toBe(cloned);
        });
    });

    describe('non-plain objects', () => {
        it('returns class instances as-is', () => {
            class Box {
                constructor(readonly value: number) {}
            }
            const box = new Box(42);
            const cloned = deepClone(box);
            expect(cloned).toBe(box);
        });
    });

    describe('cloneData alias', () => {
        it('cloneData is the same function as deepClone', () => {
            expect(cloneData).toBe(deepClone);
        });
    });
});
