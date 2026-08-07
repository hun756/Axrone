import { describe, expect, it } from 'vitest';
import { StructState } from '../hash/struct';
import { createHasher } from '../hash/factory';
import { Fnv1a32, XxHash32, Murmur3_32, Djb2, Crc32 } from '../hash/algorithms';
import type { IHasher } from '../hash/interfaces';
import type { HashValue } from '../hash/types';

interface Vec2 { x: number; y: number }
interface Vec3 { x: number; y: number; z: number }
interface Color { r: number; g: number; b: number; a: number }

const hashVec2 = (v: Vec2, ctor: new (...args: any[]) => IHasher<HashValue>): number => {
    const h = new ctor();
    (h as any).updateF32(v.x).updateF32(v.y);
    return (h.digest() as unknown as number) >>> 0;
};

const hashVec3 = (v: Vec3, ctor: new (...args: any[]) => IHasher<HashValue>): number => {
    const h = new ctor();
    (h as any).updateF32(v.x).updateF32(v.y).updateF32(v.z);
    return (h.digest() as unknown as number) >>> 0;
};

const hashColor = (v: Color, ctor: new (...args: any[]) => IHasher<HashValue>): number => {
    const h = new ctor();
    (h as any).updateU32(v.r).updateU32(v.g).updateU32(v.b).updateU32(v.a);
    return (h.digest() as unknown as number) >>> 0;
};

interface HashableVec3 { x: number; y: number; z: number; hashInto(h: IHasher<any>): void }

const makeHashableVec3 = (x: number, y: number, z: number): HashableVec3 => ({
    x, y, z,
    hashInto(h: IHasher<any>) { h.updateF32(this.x).updateF32(this.y).updateF32(this.z); },
});

describe('integration: hashable types via StructState', () => {
    it('StructState produces consistent results for hashable objects', () => {
        const hashInto = function (this: HashableVec3, h: IHasher<any>) {
            h.updateF32(this.x).updateF32(this.y).updateF32(this.z);
        };
        const v: HashableVec3 = { x: 1, y: 2, z: 3, hashInto };
        const s = new StructState();
        s.mixStruct(v);
        const viaStruct = s.digest();
        expect(viaStruct).toBeGreaterThanOrEqual(0);
        expect(viaStruct).toBeLessThan(0x100000000);
        // same fields → same hash
        const v2: HashableVec3 = { x: 1, y: 2, z: 3, hashInto };
        const s2 = new StructState();
        s2.mixStruct(v2);
        expect(s2.digest()).toBe(viaStruct);
    });

    it('StructState different fields produce different hash', () => {
        const hashInto = function (this: HashableVec3, h: IHasher<any>) {
            h.updateF32(this.x).updateF32(this.y).updateF32(this.z);
        };
        const a: HashableVec3 = { x: 1, y: 2, z: 3, hashInto };
        const b: HashableVec3 = { x: 1, y: 2, z: 4, hashInto };
        const sa = new StructState();
        sa.mixStruct(a);
        const sb = new StructState();
        sb.mixStruct(b);
        expect(sa.digest()).not.toBe(sb.digest());
    });
});

describe('integration: same input through different algorithms', () => {
    const algos: Array<[string, new (...args: any[]) => IHasher<HashValue>]> = [
        ['Fnv1a32', Fnv1a32 as any],
        ['XxHash32', XxHash32 as any],
        ['Murmur3_32', Murmur3_32 as any],
        ['Djb2', Djb2 as any],
        ['Crc32', Crc32 as any],
    ];

    for (const [name, ctor] of algos) {
        it(`${name}: hashes Vec2 consistently`, () => {
            const v: Vec2 = { x: 1.5, y: 2.5 };
            const a = hashVec2(v, ctor);
            const b = hashVec2(v, ctor);
            expect(a).toBe(b);
        });

        it(`${name}: hashes Vec3 consistently`, () => {
            const v: Vec3 = { x: 1, y: 2, z: 3 };
            expect(hashVec3(v, ctor)).toBe(hashVec3(v, ctor));
        });

        it(`${name}: hashes Color consistently`, () => {
            const v: Color = { r: 255, g: 128, b: 64, a: 255 };
            expect(hashColor(v, ctor)).toBe(hashColor(v, ctor));
        });
    }
});

describe('integration: cross-algorithm independence', () => {
    it('different algorithms produce different outputs', () => {
        const v: Vec2 = { x: 1, y: 2 };
        const results = new Set<number>();
        for (const [, ctor] of [
            ['Fnv1a32', Fnv1a32 as any],
            ['XxHash32', XxHash32 as any],
            ['Murmur3_32', Murmur3_32 as any],
            ['Djb2', Djb2 as any],
        ] as Array<[string, new (...args: any[]) => IHasher<HashValue>]>) {
            results.add(hashVec2(v, ctor));
        }
        // 4 algorithms → 4 distinct outputs
        expect(results.size).toBe(4);
    });
});

describe('integration: large batch performance smoke', () => {
    it('hashes 10k items without error', () => {
        const h = new Fnv1a32();
        for (let i = 0; i < 10000; i++) {
            h.reset();
            h.updateString(`item-${i}`);
            h.digest();
        }
        expect(true).toBe(true);
    });
});
