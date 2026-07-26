import { describe, expect, it } from 'vitest';
import { StructState, structHash, mixStruct } from '../hash/struct';

interface Vec2 { x: number; y: number; hashInto(h: any): void }
interface Vec3 { x: number; y: number; z: number; hashInto(h: any): void }
interface Color { r: number; g: number; b: number; a: number; hashInto(h: any): void }

const makeVec2 = (x: number, y: number): Vec2 => ({
    x, y,
    hashInto(h: any) { h.updateF32(this.x).updateF32(this.y); },
});

const makeVec3 = (x: number, y: number, z: number): Vec3 => ({
    x, y, z,
    hashInto(h: any) { h.updateF32(this.x).updateF32(this.y).updateF32(this.z); },
});

const makeColor = (r: number, g: number, b: number, a: number): Color => ({
    r, g, b, a,
    hashInto(h: any) { h.updateU32(this.r).updateU32(this.g).updateU32(this.b).updateU32(this.a); },
});

describe('structHash', () => {
    it('produces consistent hash for same fields', () => {
        const a = makeVec2(1, 2);
        const b = makeVec2(1, 2);
        expect(structHash(a)).toBe(structHash(b));
    });

    it('different fields produce different hash', () => {
        const a = makeVec2(1, 2);
        const b = makeVec2(1, 3);
        expect(structHash(a)).not.toBe(structHash(b));
    });

    it('returns 32-bit unsigned value', () => {
        const v = structHash(makeVec3(1, 2, 3));
        expect(v).toBeGreaterThanOrEqual(0);
        expect(v).toBeLessThan(0x100000000);
    });

    it('Vec2 hashCode consistency', () => {
        const a = makeVec2(3.14, 2.71);
        const b = makeVec2(3.14, 2.71);
        const c = makeVec2(3.14, 2.72);
        expect(structHash(a)).toBe(structHash(b));
        expect(structHash(a)).not.toBe(structHash(c));
    });

    it('Vec3 hashCode', () => {
        const a = makeVec3(1, 2, 3);
        const b = makeVec3(1, 2, 3);
        expect(structHash(a)).toBe(structHash(b));
        expect(structHash(a)).not.toBe(structHash(makeVec3(1, 2, 4)));
    });

    it('Color hashCode', () => {
        const a = makeColor(255, 128, 0, 255);
        const b = makeColor(255, 128, 0, 255);
        expect(structHash(a)).toBe(structHash(b));
    });

    it('handles null/undefined', () => {
        expect(structHash(null)).toBeGreaterThanOrEqual(0);
        expect(structHash(undefined)).toBeGreaterThanOrEqual(0);
    });
});

describe('StructState', () => {
    it('byteLength tracks updates', () => {
        const s = new StructState();
        expect(s.byteLength).toBe(0);
        s.mixString('hello');
        expect(s.byteLength).toBe(10);
    });

    it('digest returns final hash', () => {
        const s = new StructState();
        s.mixIn(0xdeadbeef);
        const v = s.digest();
        expect(v).toBeGreaterThanOrEqual(0);
        expect(v).toBeLessThan(0x100000000);
    });

    it('reset clears state', () => {
        const s = new StructState();
        s.mixIn(123);
        s.reset();
        expect(s.byteLength).toBe(0);
        s.mixIn(456);
        const v1 = s.digest();
        const s2 = new StructState();
        s2.mixIn(456);
        expect(v1).toBe(s2.digest());
    });
});
