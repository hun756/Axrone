import { describe, expect, it } from 'vitest';
import { StructState, structHash, fieldString, fieldNumber, fieldInt, fieldU32, fieldF32, fieldF64, fieldBoolean, fieldHash, fieldStruct } from '../../hash/struct';
import { asHash32, type Hash32 } from '../../hash/types';

interface Vec2 { x: number; y: number; hashCode(): number; hashInto(h: any): void }
interface Vec3 { x: number; y: number; z: number; hashCode(): number; hashInto(h: any): void }
interface Color { r: number; g: number; b: number; a: number; hashCode(): number; hashInto(h: any): void }

const makeVec2 = (x: number, y: number): Vec2 => ({
    x, y,
    hashCode() { return structHash(this, fieldNumber('x'), fieldNumber('y')); },
    hashInto(h: any) { h.updateF32(this.x).updateF32(this.y); },
});

const makeVec3 = (x: number, y: number, z: number): Vec3 => ({
    x, y, z,
    hashCode() { return structHash(this, fieldNumber('x'), fieldNumber('y'), fieldNumber('z')); },
    hashInto(h: any) { h.updateF32(this.x).updateF32(this.y).updateF32(this.z); },
});

const makeColor = (r: number, g: number, b: number, a: number): Color => ({
    r, g, b, a,
    hashCode() { return structHash(this, fieldU32('r'), fieldU32('g'), fieldU32('b'), fieldU32('a')); },
    hashInto(h: any) { h.updateU32(this.r).updateU32(this.g).updateU32(this.b).updateU32(this.a); },
});

describe('StructState + structHash', () => {
    it('produces consistent hash for same fields', () => {
        const a = makeVec2(1, 2);
        const b = makeVec2(1, 2);
        expect(a.hashCode()).toBe(b.hashCode());
    });

    it('different fields produce different hash', () => {
        const a = makeVec2(1, 2);
        const b = makeVec2(1, 3);
        expect(a.hashCode()).not.toBe(b.hashCode());
    });

    it('order of field writers matters', () => {
        const a = structHash({ x: 1, y: 2 }, fieldNumber('x'), fieldNumber('y'));
        const b = structHash({ x: 1, y: 2 }, fieldNumber('y'), fieldNumber('x'));
        expect(a).not.toBe(b);
    });

    it('returns 32-bit unsigned value', () => {
        const v = makeVec3(1, 2, 3).hashCode();
        expect(v).toBeGreaterThanOrEqual(0);
        expect(v).toBeLessThan(0x100000000);
    });
});

describe('Vec2/Vec3/Color integration', () => {
    it('Vec2 hashCode', () => {
        const a = makeVec2(3.14, 2.71);
        const b = makeVec2(3.14, 2.71);
        const c = makeVec2(3.14, 2.72);
        expect(a.hashCode()).toBe(b.hashCode());
        expect(a.hashCode()).not.toBe(c.hashCode());
    });

    it('Vec3 hashCode', () => {
        const a = makeVec3(1, 2, 3);
        const b = makeVec3(1, 2, 3);
        expect(a.hashCode()).toBe(b.hashCode());
        expect(a.hashCode()).not.toBe(makeVec3(1, 2, 4).hashCode());
    });

    it('Color hashCode', () => {
        const a = makeColor(255, 128, 0, 255);
        const b = makeColor(255, 128, 0, 255);
        expect(a.hashCode()).toBe(b.hashCode());
    });

    it('different primitive types use different update methods', () => {
        // Using fieldInt vs fieldNumber should differ
        const a = structHash({ v: 1 }, fieldInt('v'));
        const b = structHash({ v: 1.0 }, fieldNumber('v'));
        expect(a).not.toBe(b);
    });
});

describe('fieldStruct (nested)', () => {
    it('nested struct updates parent', () => {
        const a = makeVec2(1, 2);
        const s = structHash(
            { pos: a, scale: 2 },
            fieldStruct('pos'),
            fieldNumber('scale')
        );
        expect(s).toBeGreaterThanOrEqual(0);
    });
});

describe('StructState usage', () => {
    it('StructState tracks fields', () => {
        const s = new StructState();
        s.updateString('hello');
        s.updateU32(42);
        s.updateBoolean(true);
        expect(s.byteLength).toBeGreaterThan(0);
    });
});
