import { describe, expect, it } from 'vitest';
import { Fnv1a32, XxHash32, Murmur3_32, Djb2 } from '../hash/algorithms';

const enc = new TextEncoder();

function popcount32(x: number): number {
    let c = 0;
    let v = x >>> 0;
    while (v) { c += v & 1; v >>>= 1; }
    return c;
}

function hammingDistance(a: number, b: number): number {
    return popcount32((a ^ b) >>> 0);
}

describe('avalanche effect', () => {
    const algorithms: Array<[string, (s: string) => number]> = [
        ['Fnv1a32', (s) => { const h = new Fnv1a32(); h.updateString(s); return h.digest() as unknown as number; }],
        ['XxHash32', (s) => { const h = new XxHash32(); h.updateString(s); return h.digest() as unknown as number; }],
        ['Murmur3_32', (s) => { const h = new Murmur3_32(); h.updateString(s); return h.digest() as unknown as number; }],
        ['Djb2', (s) => { const h = new Djb2(); h.updateString(s); return h.digest(); }],
    ];

    for (const [name, fn] of algorithms) {
        it(`${name}: single byte change flips ~50% of bits`, () => {
            const samples = 100;
            const distances: number[] = [];
            for (let i = 0; i < samples; i++) {
                const base = `sample-${i}-data`;
                const changed = `sample-${i}-datb`; // last char changed
                distances.push(hammingDistance(fn(base), fn(changed)));
            }
            const avg = distances.reduce((a, b) => a + b, 0) / samples;
            // Avalanche ideal: 16 bits for 32-bit hashes
            expect(avg).toBeGreaterThan(8);
            expect(avg).toBeLessThan(24);
        });
    }
});

describe('distribution (chi-square)', () => {
    it('XxHash32 does not collide on small set', () => {
        const seen = new Set<number>();
        for (let i = 0; i < 100; i++) {
            const h = new XxHash32();
            h.updateBytes(enc.encode(`unique-${i}-hash-input`));
            const v = (h.digest() as unknown as number) >>> 0;
            expect(seen.has(v)).toBe(false);
            seen.add(v);
        }
    });

    it('Murmur3_32 distributes into 256 buckets (small sample)', () => {
        const buckets = new Array(256).fill(0);
        for (let i = 0; i < 5000; i++) {
            const h = new Murmur3_32();
            h.updateBytes(enc.encode(`kk-${i}-xyz`));
            buckets[((h.digest() as unknown as number) >>> 0) & 0xff]++;
        }
        const max = Math.max(...buckets);
        const min = Math.min(...buckets);
        expect(min).toBeGreaterThan(8);
        expect(max).toBeLessThan(50);
    });
});

describe('collision probability', () => {
    it('low collision rate for sequential keys (Fnv1a32)', () => {
        const seen = new Set<number>();
        let collisions = 0;
        for (let i = 0; i < 1000; i++) {
            const h = new Fnv1a32();
            h.updateBytes(enc.encode(`unique-prefix-${i}-data`));
            const v = h.digest() as unknown as number;
            if (seen.has(v)) collisions++;
            seen.add(v);
        }
        expect(collisions).toBeLessThan(5);
    });
});

describe('endianness and byte ordering', () => {
    it('Fnv1a32 processes bytes in order', () => {
        const a = new Fnv1a32();
        a.updateBytes(enc.encode('ab'));
        const b = new Fnv1a32();
        b.updateBytes(enc.encode('ba'));
        expect(a.digest()).not.toBe(b.digest());
    });
});
