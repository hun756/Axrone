import { describe, expect, it } from 'vitest';
import { HashBuilder } from '../../hash/builder';
import { Fnv1a32, Fnv1a64 } from '../../hash/algorithms';

describe('HashBuilder', () => {
    describe('fluent API', () => {
        it('builds a hasher and digests', () => {
            const b = new HashBuilder(Fnv1a32).updateString('hello');
            expect(b.digestHex()).toMatch(/^[0-9a-f]{8}$/);
        });

        it('chained use/withSeed/withKey/update', () => {
            const b = new HashBuilder(Fnv1a32)
                .withSeed(42 as any)
                .updateString('a')
                .updateString('b')
                .updateString('c');
            const v = b.digest();
            const a = new Fnv1a32(42 as any);
            a.updateString('abc');
            expect(v).toBe(a.digest());
        });

        it('updateMany accepts an array of values', () => {
            const b = new HashBuilder(Fnv1a32).updateMany(['a', 'b', 'c']);
            const a = new Fnv1a32();
            a.updateString('abc');
            expect(b.digest()).toBe(a.digest());
        });

        it('digestBytes returns bytes', () => {
            const b = new HashBuilder(Fnv1a32).updateString('a');
            expect(b.digestBytes().length).toBe(4);
        });

        it('digestBase64 returns base64', () => {
            const b = new HashBuilder(Fnv1a32).updateString('a');
            const s = b.digestBase64();
            expect(typeof s).toBe('string');
        });

        it('digestBigInt returns bigint', () => {
            const b = new HashBuilder(Fnv1a64).updateString('a');
            expect(typeof b.digestBigInt()).toBe('bigint');
        });

        it('buildHasher returns the configured hasher', () => {
            const b = new HashBuilder(Fnv1a32).updateString('a');
            const h = b.buildHasher();
            expect(h).toBeInstanceOf(Fnv1a32);
            expect(h.digest()).toBe(b.digest());
        });

        it('reset clears state', () => {
            const b = new HashBuilder(Fnv1a32).updateString('partial');
            b.reset().updateString('a');
            const a = new Fnv1a32();
            a.updateString('a');
            expect(b.digest()).toBe(a.digest());
        });
    });

    describe('algorithm switching via use()', () => {
        it('switches to new algorithm', () => {
            const b = new HashBuilder(Fnv1a32).updateString('hello');
            const v32 = b.digest();
            b.use(Fnv1a64).updateString('hello');
            const v64 = b.digestBigInt();
            expect(typeof v64).toBe('bigint');
        });
    });
});
