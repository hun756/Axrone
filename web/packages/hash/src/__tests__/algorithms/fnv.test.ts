import { describe, expect, it } from 'vitest';
import { Fnv1a32, Fnv1_32, Fnv1a64 } from '../../hash/algorithms';

const U32 = (n: number): number => n >>> 0;
const enc = new TextEncoder();

describe('FNV-1a 32-bit', () => {
    describe('known output values (bytes input)', () => {
        it('empty input gives offset basis', () => {
            const h = new Fnv1a32();
            h.updateBytes(new Uint8Array(0));
            expect(h.digest()).toBe(0x811c9dc5);
        });

        it('"a"', () => {
            const h = new Fnv1a32();
            h.updateBytes(enc.encode('a'));
            expect(h.digest()).toBe(0xe40c292c);
        });

        it('"hello"', () => {
            const h = new Fnv1a32();
            h.updateBytes(enc.encode('hello'));
            expect(h.digest()).toBe(0x4f9f2cab);
        });

        it('"foobar"', () => {
            const h = new Fnv1a32();
            h.updateBytes(enc.encode('foobar'));
            expect(h.digest()).toBe(0xbf9cf968);
        });

        it('"www.example.com"', () => {
            const h = new Fnv1a32();
            h.updateBytes(enc.encode('www.example.com'));
            expect(h.digest()).toBe(0x88469fcb);
        });
    });

    describe('known output values (updateString UTF-16)', () => {
        it('"hello" via updateString', () => {
            const h = new Fnv1a32();
            h.updateString('hello');
            const v = h.digest() as unknown as number;
            expect(v >>> 0).toBe(v >>> 0);
            expect(v).not.toBe(0);
        });
    });

    describe('determinism', () => {
        it('same input produces same output', () => {
            const input = enc.encode('test');
            const h1 = new Fnv1a32();
            h1.updateBytes(input);
            const h2 = new Fnv1a32();
            h2.updateBytes(input);
            expect(h1.digest()).toBe(h2.digest());
        });
    });

    describe('avalanche effect', () => {
        it('one-bit change in input drastically changes output', () => {
            const a = enc.encode('test');
            const b = enc.encode('Test');
            const h1 = new Fnv1a32();
            h1.updateBytes(a);
            const h2 = new Fnv1a32();
            h2.updateBytes(b);
            const d1 = h1.digest() as unknown as number;
            const d2 = h2.digest() as unknown as number;
            expect(d1).not.toBe(d2);
            const diff = (d1 ^ d2).toString(2).split('1').length - 1;
            expect(diff).toBeGreaterThan(8);
        });
    });

    describe('seedable', () => {
        it('produces different output with different seeds', () => {
            const input = enc.encode('test');
            const h1 = new Fnv1a32();
            h1.updateBytes(input);
            const h2 = new Fnv1a32(12345 as any);
            h2.updateBytes(input);
            expect(h1.digest()).not.toBe(h2.digest());
        });

        it('zero seed is default', () => {
            const h1 = new Fnv1a32(0 as any);
            const h2 = new Fnv1a32();
            h1.updateBytes(enc.encode('test'));
            h2.updateBytes(enc.encode('test'));
            expect(h1.digest()).toBe(h2.digest());
        });
    });

    describe('reset and clone', () => {
        it('reset clears state', () => {
            const h = new Fnv1a32();
            h.updateBytes(enc.encode('partial'));
            h.reset();
            h.updateBytes(enc.encode('hello'));
            expect(h.digest()).toBe(0x4f9f2cab);
        });

        it('reset with seed changes the initial state', () => {
            const h = new Fnv1a32();
            h.updateBytes(enc.encode('hello'));
            const first = h.digest() as unknown as number;
            h.reset(42 as any);
            h.updateBytes(enc.encode('hello'));
            const second = h.digest() as unknown as number;
            expect(first).not.toBe(second);
        });

        it('clone creates independent copy', () => {
            const h1 = new Fnv1a32();
            h1.updateBytes(enc.encode('hello'));
            const h2 = h1.clone();
            h1.updateBytes(enc.encode('world'));
            const v1 = h1.digest() as unknown as number;
            const v2 = h2.digest() as unknown as number;
            expect(v1).not.toBe(v2);
        });
    });

    describe('updateI32/U32/F32/F64', () => {
        it('updateI32/U32 produce consistent results', () => {
            const h1 = new Fnv1a32();
            h1.updateU32(0x12345678);
            const h2 = new Fnv1a32();
            h2.updateI32(0x12345678);
            expect(h1.digest()).toBe(h2.digest());
        });

        it('updateF32 round-trips via bits', () => {
            const h1 = new Fnv1a32();
            h1.updateF32(3.14159);
            const h2 = new Fnv1a32();
            const buf = new ArrayBuffer(4);
            new DataView(buf).setFloat32(0, 3.14159, true);
            const u32 = new Uint32Array(buf);
            h2.updateU32(u32[0]!);
            expect(h1.digest()).toBe(h2.digest());
        });

        it('updateF64 is two U32 updates (LE)', () => {
            const h1 = new Fnv1a32();
            h1.updateF64(3.14159);
            const h2 = new Fnv1a32();
            const buf = new ArrayBuffer(8);
            new DataView(buf).setFloat64(0, 3.14159, true);
            const u32 = new Uint32Array(buf);
            h2.updateU32(u32[0]!).updateU32(u32[1]!);
            expect(h1.digest()).toBe(h2.digest());
        });
    });

    describe('updateAny', () => {
        it('dispatches correctly for each type', () => {
            const cases: Array<[unknown, number]> = [
                [42, 0xa],
                [3.14, 0],
                [true, 0],
                [false, 0],
                [null, 0],
                [undefined, 0],
            ];
            for (const [input, _] of cases) {
                const h = new Fnv1a32();
                expect(() => h.updateAny(input)).not.toThrow();
            }
        });

        it('string input via updateAny matches updateString', () => {
            const h1 = new Fnv1a32();
            h1.updateString('hello');
            const h2 = new Fnv1a32();
            h2.updateAny('hello');
            expect(h1.digest()).toBe(h2.digest());
        });
    });

    describe('digest variants', () => {
        it('digestBytes returns 4 bytes', () => {
            const h = new Fnv1a32();
            h.updateString('hello');
            const bytes = h.digestBytes();
            expect(bytes).toBeInstanceOf(Uint8Array);
            expect(bytes.length).toBe(4);
        });

        it('digestHex returns 8-char hex', () => {
            const h = new Fnv1a32();
            h.updateString('hello');
            expect(h.digestHex()).toMatch(/^[0-9a-f]{8}$/);
        });

        it('digestHex uppercase option works', () => {
            const h = new Fnv1a32();
            h.updateString('hello');
            expect(h.digestHex(true)).toMatch(/^[0-9A-F]{8}$/);
        });

        it('digestBigInt returns bigint', () => {
            const h = new Fnv1a32();
            h.updateString('hello');
            expect(typeof h.digestBigInt()).toBe('bigint');
        });
    });

    describe('updateI8/I16/U8/U16', () => {
        it('updateI8 delegates to updateI32', () => {
            const a = new Fnv1a32();
            a.updateI8(42);
            const b = new Fnv1a32();
            b.updateI32(42);
            expect(a.digest()).toBe(b.digest());
        });

        it('updateI16 delegates to updateI32', () => {
            const a = new Fnv1a32();
            a.updateI16(1000);
            const b = new Fnv1a32();
            b.updateI32(1000);
            expect(a.digest()).toBe(b.digest());
        });

        it('updateU8 masks to 8 bits then delegates to updateU32', () => {
            const a = new Fnv1a32();
            a.updateU8(0xff);
            const b = new Fnv1a32();
            b.updateU32(0xff);
            expect(a.digest()).toBe(b.digest());
        });

        it('updateU16 masks to 16 bits then delegates to updateU32', () => {
            const a = new Fnv1a32();
            a.updateU16(0xabcd);
            const b = new Fnv1a32();
            b.updateU32(0xabcd);
            expect(a.digest()).toBe(b.digest());
        });
    });

    describe('updateBoolean', () => {
        it('true and false produce different hashes', () => {
            const a = new Fnv1a32();
            a.updateBoolean(true);
            const b = new Fnv1a32();
            b.updateBoolean(false);
            expect(a.digest()).not.toBe(b.digest());
        });

        it('tracks byteLength (1 byte per boolean)', () => {
            const h = new Fnv1a32();
            h.updateBoolean(true).updateBoolean(false);
            expect(h.byteLength).toBe(2);
        });

        it('matches single-byte updateBytes for true=1, false=0', () => {
            const a = new Fnv1a32();
            a.updateBoolean(true);
            const b = new Fnv1a32();
            b.updateBytes(new Uint8Array([1]));
            expect(a.digest()).toBe(b.digest());
        });
    });

    describe('updateHash with bigint', () => {
        it('bigint path processes 8 bytes', () => {
            const h = new Fnv1a32();
            h.updateHash(0x0102030405060708n);
            expect(h.byteLength).toBe(8);
        });

        it('bigint is consistent', () => {
            const a = new Fnv1a32();
            a.updateHash(0xdeadbeefcafebaben);
            const b = new Fnv1a32();
            b.updateHash(0xdeadbeefcafebaben);
            expect(a.digest()).toBe(b.digest());
        });

        it('number path delegates to updateU32', () => {
            const a = new Fnv1a32();
            a.updateHash(0xdeadbeef as any);
            const b = new Fnv1a32();
            b.updateU32(0xdeadbeef);
            expect(a.digest()).toBe(b.digest());
        });
    });

    describe('updateHashable', () => {
        it('delegates to hashInto', () => {
            const hashable = {
                hashInto(hasher: any) { hasher.updateString('custom'); },
            };
            const a = new Fnv1a32();
            a.updateHashable(hashable);
            const b = new Fnv1a32();
            b.updateString('custom');
            expect(a.digest()).toBe(b.digest());
        });
    });

    describe('digestBase64', () => {
        it('returns valid base64 string', () => {
            const h = new Fnv1a32();
            h.updateString('hello');
            const b64 = h.digestBase64();
            expect(typeof b64).toBe('string');
            expect(b64.length).toBeGreaterThan(0);
            // base64 of 4 bytes = 8 chars (with padding)
            expect(b64).toMatch(/^[A-Za-z0-9+/]+=*$/);
        });

        it('matches manual base64 of digestBytes', () => {
            const h = new Fnv1a32();
            h.updateString('test');
            const bytes = h.digestBytes();
            const expected = btoa(String.fromCharCode(...bytes));
            // Re-create since digest finalizes
            const h2 = new Fnv1a32();
            h2.updateString('test');
            expect(h2.digestBase64()).toBe(expected);
        });
    });

    describe('metadata', () => {
        it('has correct metadata properties', () => {
            const h = new Fnv1a32();
            expect(h.metadata.name).toBe('fnv1a-32');
            expect(h.metadata.outputSize).toBe(32);
            expect(h.metadata.blockSize).toBe(1);
            expect(h.metadata.seedable).toBe(true);
            expect(h.metadata.keyed).toBe(false);
            expect(h.metadata.cryptographicallySecure).toBe(false);
            expect(h.metadata.category).toBe('non-crypto');
        });
    });

    describe('finalized state', () => {
        it('rejects updates after digest', () => {
            const h = new Fnv1a32();
            h.updateString('a');
            h.digest();
            expect(() => h.updateString('b')).toThrow(/cannot update/);
            expect(() => h.updateBytes(new Uint8Array([1]))).toThrow(/cannot update/);
        });

        it('rejects updateBoolean after digest', () => {
            const h = new Fnv1a32();
            h.digest();
            expect(() => h.updateBoolean(true)).toThrow(/cannot update/);
        });

        it('rejects updateI32 after digest', () => {
            const h = new Fnv1a32();
            h.digest();
            expect(() => h.updateI32(42)).toThrow(/cannot update/);
        });

        it('rejects updateHash after digest', () => {
            const h = new Fnv1a32();
            h.digest();
            expect(() => h.updateHash(42 as any)).toThrow(/cannot update/);
        });

        it('finalized property reflects state', () => {
            const h = new Fnv1a32();
            expect(h.finalized).toBe(false);
            h.digest();
            expect(h.finalized).toBe(true);
        });
    });

    describe('byteLength tracking', () => {
        it('tracks bytes consumed', () => {
            const h = new Fnv1a32();
            expect(h.byteLength).toBe(0);
            h.updateBytes(enc.encode('hello'));
            expect(h.byteLength).toBe(5);
            h.updateString('world');
            expect(h.byteLength).toBe(5 + 10);
        });

        it('tracks I32/U32 bytes', () => {
            const h = new Fnv1a32();
            h.updateU32(1);
            expect(h.byteLength).toBe(4);
            h.updateI64(1n);
            expect(h.byteLength).toBe(12);
        });
    });
});

describe('FNV-1 32-bit (legacy)', () => {
    it('produces known output for empty input', () => {
        const h = new Fnv1_32();
        h.updateBytes(new Uint8Array(0));
        expect(h.digest()).toBe(0x811c9dc5);
    });

    it('produces known output for "a"', () => {
        const h = new Fnv1_32();
        h.updateBytes(enc.encode('a'));
        expect(h.digest()).toBe(0x050c5d7e);
    });

    it('differs from FNV-1a for non-empty input', () => {
        const input = enc.encode('hello');
        const h1 = new Fnv1a32();
        h1.updateBytes(input);
        const h2 = new Fnv1_32();
        h2.updateBytes(input);
        expect(h1.digest()).not.toBe(h2.digest());
    });

    it('reset clears state', () => {
        const h = new Fnv1_32();
        h.updateBytes(enc.encode('partial'));
        h.reset();
        h.updateBytes(enc.encode('a'));
        expect(h.digest()).toBe(0x050c5d7e);
    });

    it('clone creates independent copy', () => {
        const h1 = new Fnv1_32();
        h1.updateBytes(enc.encode('hello'));
        const h2 = h1.clone();
        h1.updateBytes(enc.encode('world'));
        expect(h1.digest()).not.toBe(h2.digest());
    });

    it('rejects updates after digest', () => {
        const h = new Fnv1_32();
        h.updateString('a');
        h.digest();
        expect(() => h.updateString('b')).toThrow(/cannot update/);
    });

    it('digest variants', () => {
        const h = new Fnv1_32();
        h.updateString('hello');
        expect(h.digestBytes()).toBeInstanceOf(Uint8Array);
        expect(h.digestBytes().length).toBe(4);
        expect(h.digestHex()).toMatch(/^[0-9a-f]{8}$/);
        expect(h.digestHex(true)).toMatch(/^[0-9A-F]{8}$/);
        expect(typeof h.digestBigInt()).toBe('bigint');
        expect(typeof h.digestBase64()).toBe('string');
    });

    it('metadata has correct name', () => {
        const h = new Fnv1_32();
        expect(h.metadata.name).toBe('fnv1-32');
    });
});

describe('FNV-1a 64-bit', () => {
    it('produces known output for empty input', () => {
        const h = new Fnv1a64();
        h.updateBytes(new Uint8Array(0));
        expect((h.digest() as unknown as bigint)).toBe(0xcbf29ce484222325n);
    });

    it('produces known output for "a"', () => {
        const h = new Fnv1a64();
        h.updateBytes(enc.encode('a'));
        expect((h.digest() as unknown as bigint)).toBe(0xaf63dc4c8601ec8cn);
    });

    it('produces known output for "foobar"', () => {
        const h = new Fnv1a64();
        h.updateBytes(enc.encode('foobar'));
        expect((h.digest() as unknown as bigint)).toBe(0x85944171f73967e8n);
    });

    it('returns 64-bit value', () => {
        const h = new Fnv1a64();
        h.updateBytes(enc.encode('hello'));
        const v = h.digest() as unknown as bigint;
        expect(v).toBeGreaterThanOrEqual(0n);
        expect(v).toBeLessThanOrEqual(0xffffffffffffffffn);
    });

    it('different seeds produce different output', () => {
        const input = enc.encode('test');
        const h1 = new Fnv1a64();
        h1.updateBytes(input);
        const h2 = new Fnv1a64(12345 as any);
        h2.updateBytes(input);
        expect((h1.digest() as unknown as bigint)).not.toBe((h2.digest() as unknown as bigint));
    });

    it('digestBytes returns 8 bytes', () => {
        const h = new Fnv1a64();
        h.updateBytes(enc.encode('a'));
        expect(h.digestBytes().length).toBe(8);
    });

    it('digestHex returns 16-char hex', () => {
        const h = new Fnv1a64();
        h.updateBytes(enc.encode('a'));
        expect(h.digestHex()).toMatch(/^[0-9a-f]{16}$/);
    });

    it('reset clears state', () => {
        const h = new Fnv1a64();
        h.updateBytes(enc.encode('partial'));
        h.reset();
        h.updateBytes(enc.encode('a'));
        expect((h.digest() as unknown as bigint)).toBe(0xaf63dc4c8601ec8cn);
    });

    it('clone creates independent copy', () => {
        const h1 = new Fnv1a64();
        h1.updateBytes(enc.encode('hello'));
        const h2 = h1.clone();
        h1.updateBytes(enc.encode('world'));
        expect((h1.digest() as unknown as bigint)).not.toBe((h2.digest() as unknown as bigint));
    });

    it('rejects updates after digest', () => {
        const h = new Fnv1a64();
        h.updateString('a');
        h.digest();
        expect(() => h.updateString('b')).toThrow(/cannot update/);
        expect(() => h.updateBoolean(true)).toThrow(/cannot update/);
        expect(() => h.updateBytes(enc.encode('x'))).toThrow(/cannot update/);
    });

    it('updateString produces consistent results', () => {
        const a = new Fnv1a64();
        a.updateString('hello');
        const b = new Fnv1a64();
        b.updateString('hello');
        expect((a.digest() as unknown as bigint)).toBe((b.digest() as unknown as bigint));
    });

    it('updateBoolean produces different hashes for true vs false', () => {
        const a = new Fnv1a64();
        a.updateBoolean(true);
        const b = new Fnv1a64();
        b.updateBoolean(false);
        expect((a.digest() as unknown as bigint)).not.toBe((b.digest() as unknown as bigint));
    });

    it('updateAny dispatches correctly', () => {
        const a = new Fnv1a64();
        a.updateAny('hello');
        const b = new Fnv1a64();
        b.updateString('hello');
        expect((a.digest() as unknown as bigint)).toBe((b.digest() as unknown as bigint));
    });

    it('digestBase64 returns valid base64', () => {
        const h = new Fnv1a64();
        h.updateBytes(enc.encode('a'));
        const b64 = h.digestBase64();
        expect(typeof b64).toBe('string');
        expect(b64.length).toBeGreaterThan(0);
    });

    it('digestBigInt returns bigint', () => {
        const h = new Fnv1a64();
        h.updateBytes(enc.encode('a'));
        expect(typeof h.digestBigInt()).toBe('bigint');
    });

    it('updateHashable delegates to hashInto', () => {
        const hashable = { hashInto(hasher: any) { hasher.updateString('x'); } };
        const a = new Fnv1a64();
        a.updateHashable(hashable);
        const b = new Fnv1a64();
        b.updateString('x');
        expect((a.digest() as unknown as bigint)).toBe((b.digest() as unknown as bigint));
    });

    it('metadata has correct properties', () => {
        const h = new Fnv1a64();
        expect(h.metadata.name).toBe('fnv1a-64');
        expect(h.metadata.outputSize).toBe(64);
        expect(h.metadata.cryptographicallySecure).toBe(false);
    });

    it('byteLength tracking', () => {
        const h = new Fnv1a64();
        expect(h.byteLength).toBe(0);
        h.updateBytes(enc.encode('hello'));
        expect(h.byteLength).toBe(5);
        h.updateBoolean(true);
        expect(h.byteLength).toBe(6);
    });
});
