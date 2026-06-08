import { describe, expect, it } from 'vitest';
import { DomainSeparatedHasher, createDomainSeparator } from '../../hash/domain';
import { Fnv1a32 } from '../../hash/algorithms';

describe('DomainSeparatedHasher', () => {
    it('prefixes domain string', () => {
        const dsh = new DomainSeparatedHasher('my-domain', Fnv1a32);
        dsh.updateString('data');
        const v1 = dsh.digest() as unknown as number;

        const bare = new Fnv1a32();
        bare.updateString('my-domaindata');
        expect(v1).toBe(bare.digest());
    });

    it('different domains give different output', () => {
        const a = new DomainSeparatedHasher('domain-a', Fnv1a32);
        a.updateString('data');
        const b = new DomainSeparatedHasher('domain-b', Fnv1a32);
        b.updateString('data');
        expect(a.digest()).not.toBe(b.digest());
    });

    it('same domain same data gives same output', () => {
        const a = new DomainSeparatedHasher('d', Fnv1a32);
        a.updateString('x');
        const b = new DomainSeparatedHasher('d', Fnv1a32);
        b.updateString('x');
        expect(a.digest()).toBe(b.digest());
    });

    it('reset clears state but retains domain', () => {
        const dsh = new DomainSeparatedHasher('d', Fnv1a32);
        dsh.updateString('a');
        dsh.reset();
        dsh.updateString('x');
        const v1 = dsh.digest();
        const fresh = new DomainSeparatedHasher('d', Fnv1a32);
        fresh.updateString('x');
        expect(v1).toBe(fresh.digest());
    });

    it('updateHashable works', () => {
        const dsh = new DomainSeparatedHasher('d', Fnv1a32);
        dsh.updateHashable({
            hashInto(h: any) {
                h.updateString('nested');
            }
        });
        expect(dsh.digest()).toBeGreaterThanOrEqual(0);
    });
});

describe('createDomainSeparator', () => {
    it('returns a function that hashes with prefix', () => {
        const sep = createDomainSeparator('prefix', Fnv1a32);
        const a = sep('hello');
        const b = sep('hello');
        expect(a).toBe(b);
    });

    it('different inputs give different output', () => {
        const sep = createDomainSeparator('prefix', Fnv1a32);
        expect(sep('a')).not.toBe(sep('b'));
    });
});
