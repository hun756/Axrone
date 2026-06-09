import { describe, expect, it } from 'vitest';
import { DomainSeparatedHasher, createDomainSeparator } from '../hash/domain';
import { createHasher } from '../hash/factory';

const enc = new TextEncoder();

describe('DomainSeparatedHasher', () => {
    it('prefixes domain tag', () => {
        const data = enc.encode('data');
        const dsh = new DomainSeparatedHasher('fnv1a-32', 'my-domain');
        const v1 = dsh.hash(data) as unknown as number;

        const bare = createHasher('fnv1a-32');
        bare.updateBytes(enc.encode('my-domain'));
        bare.updateBytes(data);
        expect(v1).toBe(bare.digest());
    });

    it('different domains give different output', () => {
        const a = new DomainSeparatedHasher('fnv1a-32', 'domain-a');
        const v1 = a.hashString('data') as unknown as number;
        const b = new DomainSeparatedHasher('fnv1a-32', 'domain-b');
        const v2 = b.hashString('data') as unknown as number;
        expect(v1).not.toBe(v2);
    });

    it('same domain same data gives same output', () => {
        const a = new DomainSeparatedHasher('fnv1a-32', 'd');
        const v1 = a.hashString('x') as unknown as number;
        const b = new DomainSeparatedHasher('fnv1a-32', 'd');
        const v2 = b.hashString('x') as unknown as number;
        expect(v1).toBe(v2);
    });

    it('reset clears data but retains domain', () => {
        const dsh = new DomainSeparatedHasher('fnv1a-32', 'd');
        dsh.hashString('a');
        dsh.reset();
        const v1 = dsh.hashString('x') as unknown as number;
        const fresh = new DomainSeparatedHasher('fnv1a-32', 'd');
        const v2 = fresh.hashString('x') as unknown as number;
        expect(v1).toBe(v2);
    });

    it('throws on too-long tag', () => {
        expect(() => new DomainSeparatedHasher('fnv1a-32', 'x'.repeat(257))).toThrow();
    });

    it('hash() with bytes', () => {
        const dsh = new DomainSeparatedHasher('fnv1a-32', 'd');
        const v = dsh.hash(enc.encode('a')) as unknown as number;
        expect(typeof v).toBe('number');
    });
});

describe('createDomainSeparator', () => {
    it('returns a DomainSeparatedHasher', () => {
        const sep = createDomainSeparator('fnv1a-32', 'prefix');
        expect(sep).toBeInstanceOf(DomainSeparatedHasher);
    });

    it('produces consistent results', () => {
        const a = createDomainSeparator('fnv1a-32', 'p').hashString('hello') as unknown as number;
        const b = createDomainSeparator('fnv1a-32', 'p').hashString('hello') as unknown as number;
        expect(a).toBe(b);
    });

    it('different inputs give different output', () => {
        const sep = createDomainSeparator('fnv1a-32', 'p');
        const a = sep.hashString('a') as unknown as number;
        sep.reset();
        const b = sep.hashString('b') as unknown as number;
        expect(a).not.toBe(b);
    });
});
