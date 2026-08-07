import { describe, expect, it } from 'vitest';
import {
    StructState,
    structHash,
    fieldString,
    fieldNumber,
    fieldInt,
    fieldU32,
    fieldF32,
    fieldF64,
    fieldBoolean,
    fieldHash,
    fieldStruct,
} from '../hash/struct';
import { createHasher } from '../hash/factory';
import { asHash32 } from '../hash/types';
import type { IHasher } from '../hash/interfaces';
import type { HashValue } from '../hash/types';

describe('StructState gaps — individual mix methods', () => {
    it('mixBoolean produces different hashes for true vs false', () => {
        const st = new StructState();
        st.mixBoolean(true);
        const sf = new StructState();
        sf.mixBoolean(false);
        expect(st.digest()).not.toBe(sf.digest());
    });

    it('mixBoolean tracks byteLength', () => {
        const s = new StructState();
        s.mixBoolean(true).mixBoolean(false);
        expect(s.byteLength).toBe(2);
    });

    it('mixNumber (Float64) produces consistent results', () => {
        const a = new StructState();
        a.mixNumber(3.14);
        const b = new StructState();
        b.mixNumber(3.14);
        expect(a.digest()).toBe(b.digest());
    });

    it('mixNumber different values produce different hashes', () => {
        const a = new StructState();
        a.mixNumber(1.0);
        const b = new StructState();
        b.mixNumber(2.0);
        expect(a.digest()).not.toBe(b.digest());
    });

    it('mixNumber tracks byteLength (8 bytes per number)', () => {
        const s = new StructState();
        s.mixNumber(1.5);
        expect(s.byteLength).toBe(8);
    });

    it('mixF64 delegates to mixNumber', () => {
        const a = new StructState();
        a.mixF64(2.718);
        const b = new StructState();
        b.mixNumber(2.718);
        expect(a.digest()).toBe(b.digest());
    });

    it('mixInt produces consistent results', () => {
        const a = new StructState();
        a.mixInt(42);
        const b = new StructState();
        b.mixInt(42);
        expect(a.digest()).toBe(b.digest());
    });

    it('mixInt truncates to 32-bit', () => {
        const a = new StructState();
        a.mixInt(0x1_0000002a); // truncated to 42
        const b = new StructState();
        b.mixInt(42);
        expect(a.digest()).toBe(b.digest());
    });

    it('mixInt tracks byteLength (4 bytes)', () => {
        const s = new StructState();
        s.mixInt(100);
        expect(s.byteLength).toBe(4);
    });

    it('mixU32 produces consistent results', () => {
        const a = new StructState();
        a.mixU32(0xdeadbeef);
        const b = new StructState();
        b.mixU32(0xdeadbeef);
        expect(a.digest()).toBe(b.digest());
    });

    it('mixU32 treats value as unsigned', () => {
        const a = new StructState();
        a.mixU32(-1); // -1 >>> 0 = 0xffffffff
        const b = new StructState();
        b.mixU32(0xffffffff);
        expect(a.digest()).toBe(b.digest());
    });

    it('mixF32 produces consistent results', () => {
        const a = new StructState();
        a.mixF32(1.5);
        const b = new StructState();
        b.mixF32(1.5);
        expect(a.digest()).toBe(b.digest());
    });

    it('mixF32 tracks byteLength (4 bytes)', () => {
        const s = new StructState();
        s.mixF32(1.0);
        expect(s.byteLength).toBe(4);
    });

    it('mixF32 differs from mixF64 for same value', () => {
        const a = new StructState();
        a.mixF32(3.14);
        const b = new StructState();
        b.mixF64(3.14);
        // F32 and F64 have different bit representations
        expect(a.digest()).not.toBe(b.digest());
    });
});

describe('StructState gaps — mixHash with bigint', () => {
    it('mixHash with number', () => {
        const s = new StructState();
        s.mixHash(asHash32(0xdeadbeef));
        expect(s.byteLength).toBe(4);
        expect(s.digest()).toBeGreaterThanOrEqual(0);
    });

    it('mixHash with bigint processes 8 bytes', () => {
        const s = new StructState();
        s.mixHash(0x0102030405060708n as HashValue);
        expect(s.byteLength).toBe(8);
    });

    it('mixHash bigint is consistent', () => {
        const a = new StructState();
        a.mixHash(0xdeadbeefcafebaben as HashValue);
        const b = new StructState();
        b.mixHash(0xdeadbeefcafebaben as HashValue);
        expect(a.digest()).toBe(b.digest());
    });

    it('mixHash different bigints produce different hashes', () => {
        const a = new StructState();
        a.mixHash(1n as HashValue);
        const b = new StructState();
        b.mixHash(2n as HashValue);
        expect(a.digest()).not.toBe(b.digest());
    });
});

describe('StructState gaps — mixStruct with IHashable', () => {
    it('mixes IHashable objects via hashInto', () => {
        const hashable = {
            hashInto(h: IHasher<any>) {
                h.updateString('custom-data');
            },
        };
        const s = new StructState();
        s.mixStruct(hashable);
        expect(s.digest()).toBeGreaterThanOrEqual(0);
    });

    it('different IHashable objects produce different hashes', () => {
        const h1 = { hashInto(h: IHasher<any>) { h.updateString('alpha'); } };
        const h2 = { hashInto(h: IHasher<any>) { h.updateString('beta'); } };
        const s1 = new StructState();
        s1.mixStruct(h1);
        const s2 = new StructState();
        s2.mixStruct(h2);
        expect(s1.digest()).not.toBe(s2.digest());
    });

    it('returns this for non-hashable non-null objects', () => {
        const s = new StructState();
        const before = s.digest();
        s.mixStruct({ foo: 'bar' }); // no hashInto → no-op
        // hash is unchanged because mixStruct returns this without mixing
        expect(s.digest()).toBe(before);
    });
});

describe('StructState gaps — custom seed', () => {
    it('constructor accepts custom seed', () => {
        const a = new StructState(0);
        const b = new StructState(42);
        a.mixIn(123);
        b.mixIn(123);
        expect(a.digest()).not.toBe(b.digest());
    });

    it('reset accepts custom seed', () => {
        const s = new StructState();
        s.reset(99);
        s.mixIn(1);
        const s2 = new StructState(99);
        s2.mixIn(1);
        expect(s.digest()).toBe(s2.digest());
    });
});

describe('field writer factories', () => {
    it('fieldString writes string via mixString', () => {
        const writer = fieldString();
        const s = new StructState();
        writer(s, 'hello');
        expect(s.byteLength).toBe(10); // 5 chars * 2 bytes
    });

    it('fieldNumber writes number via mixNumber', () => {
        const writer = fieldNumber();
        const s = new StructState();
        writer(s, 3.14);
        expect(s.byteLength).toBe(8);
    });

    it('fieldInt writes int via mixInt', () => {
        const writer = fieldInt();
        const s = new StructState();
        writer(s, 42);
        expect(s.byteLength).toBe(4);
    });

    it('fieldU32 writes unsigned via mixU32', () => {
        const writer = fieldU32();
        const s = new StructState();
        writer(s, 0xffffffff);
        expect(s.byteLength).toBe(4);
    });

    it('fieldF32 writes float32 via mixF32', () => {
        const writer = fieldF32();
        const s = new StructState();
        writer(s, 1.5);
        expect(s.byteLength).toBe(4);
    });

    it('fieldF64 writes float64 via mixF64', () => {
        const writer = fieldF64();
        const s = new StructState();
        writer(s, 2.718);
        expect(s.byteLength).toBe(8);
    });

    it('fieldBoolean writes boolean via mixBoolean', () => {
        const writer = fieldBoolean();
        const s = new StructState();
        writer(s, true);
        expect(s.byteLength).toBe(1);
    });

    it('fieldHash writes hash value via mixHash', () => {
        const writer = fieldHash();
        const s = new StructState();
        writer(s, asHash32(0xdeadbeef));
        expect(s.byteLength).toBe(4);
    });

    it('fieldStruct writes via mixStruct', () => {
        const writer = fieldStruct();
        const s = new StructState();
        writer(s, null); // null → multiply-only path
        expect(s.digest()).toBeGreaterThanOrEqual(0);
    });

    it('field writers produce same result as direct method calls', () => {
        const s1 = new StructState();
        fieldString()(s1, 'hello');
        fieldNumber()(s1, 42);
        fieldBoolean()(s1, true);

        const s2 = new StructState();
        s2.mixString('hello');
        s2.mixNumber(42);
        s2.mixBoolean(true);

        expect(s1.digest()).toBe(s2.digest());
    });
});
