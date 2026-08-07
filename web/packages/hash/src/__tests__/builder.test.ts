import { describe, expect, it } from 'vitest';
import { HashBuilder } from '../hash/builder';
import { createHasher } from '../hash/factory';

describe('HashBuilder', () => {
    describe('fluent API', () => {
        it('builds a hasher and digests', () => {
            const b = new HashBuilder('fnv1a-32').update('hello');
            expect(b.digestHex()).toMatch(/^[0-9a-f]{8}$/);
        });

        it('chained update methods', () => {
            const b = new HashBuilder('fnv1a-32')
                .update('a')
                .update('b')
                .update('c');
            const v = b.digest();
            const a = createHasher('fnv1a-32');
            a.updateString('abc');
            expect(v).toBe(a.digest());
        });

        it('updateMany accepts an array of values', () => {
            const b = new HashBuilder('fnv1a-32').updateMany(['a', 'b', 'c']);
            const a = createHasher('fnv1a-32');
            a.updateString('abc');
            expect(b.digest()).toBe(a.digest());
        });

        it('digestBytes returns bytes', () => {
            const b = new HashBuilder('fnv1a-32').update('a');
            expect(b.digestBytes().length).toBe(4);
        });

        it('digestBase64 returns base64', () => {
            const b = new HashBuilder('fnv1a-32').update('a');
            const s = b.digestBase64();
            expect(typeof s).toBe('string');
        });

        it('buildHasher returns the configured hasher', () => {
            const b = new HashBuilder('fnv1a-32').update('a');
            const h = b.buildHasher();
            expect(h).toBeDefined();
            expect(h.digest()).toBe(b.digest());
        });

        it('reset clears state', () => {
            const b = new HashBuilder('fnv1a-32').update('partial');
            b.reset().update('a');
            const a = createHasher('fnv1a-32');
            a.updateString('a');
            expect(b.digest()).toBe(a.digest());
        });
    });

    describe('algorithm switching via use()', () => {
        it('switches to new algorithm', () => {
            const b = new HashBuilder('fnv1a-32').update('hello');
            b.use('fnv1a-64').update('hello');
            const v = b.digest();
            expect(typeof v).toBe('bigint');
        });
    });
});
