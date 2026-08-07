import { describe, expect, it } from 'vitest';

import { deepFreeze } from '../freeze';

describe('deepFreeze', () => {
    it('freezes nested objects without throwing on binary buffer views', () => {
        const buffer = new ArrayBuffer(8);
        const bytes = new Uint8Array(buffer);
        const view = new DataView(buffer);
        const value = {
            nested: {
                list: [{ enabled: true }],
            },
            buffer,
            bytes,
            view,
        };

        expect(() => deepFreeze(value)).not.toThrow();

        expect(Object.isFrozen(value)).toBe(true);
        expect(Object.isFrozen(value.nested)).toBe(true);
        expect(Object.isFrozen(value.nested.list)).toBe(true);
        expect(Object.isFrozen(value.nested.list[0])).toBe(true);
        expect(value.buffer).toBe(buffer);
        expect(value.bytes).toBe(bytes);
        expect(value.view).toBe(view);
    });

    it('preserves cyclic references', () => {
        const value: { self?: unknown; nested: { ready: boolean } } = {
            nested: { ready: true },
        };
        value.self = value;

        expect(() => deepFreeze(value)).not.toThrow();
        expect(Object.isFrozen(value)).toBe(true);
        expect(Object.isFrozen(value.nested)).toBe(true);
        expect(value.self).toBe(value);
    });

    it('returns primitives as-is without freezing', () => {
        expect(deepFreeze(42)).toBe(42);
        expect(deepFreeze('str')).toBe('str');
        expect(deepFreeze(null)).toBeNull();
        expect(deepFreeze(undefined)).toBeUndefined();
        expect(deepFreeze(true)).toBe(true);
    });

    it('skips freezing typed arrays (buffer-like)', () => {
        const arr = new Uint8Array([1, 2, 3]);
        const result = deepFreeze(arr);
        expect(Object.isFrozen(result)).toBe(false);
        expect(result).toBe(arr);
    });

    it('skips freezing DataView (buffer-like)', () => {
        const buf = new ArrayBuffer(8);
        const view = new DataView(buf);
        const result = deepFreeze(view);
        expect(Object.isFrozen(result)).toBe(false);
        expect(result).toBe(view);
    });

    it('skips freezing SharedArrayBuffer when available', () => {
        if (typeof SharedArrayBuffer === 'undefined') return;
        const sab = new SharedArrayBuffer(8);
        const result = deepFreeze(sab);
        expect(Object.isFrozen(result)).toBe(false);
        expect(result).toBe(sab);
    });
});