import { describe, expect, it } from 'vitest';
import { Fnv1a32, XxHash32, Murmur3_32, Djb2, Cyrb53 } from '../../hash/algorithms';

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
    it('XxHash32 distributes into 16 buckets (smoke test)', () => {
        const buckets = new Array(16).fill(0);
        for (let i = 0; i < 16000; i++) {
            const h = new XxHash32();
            h.updateBytes(enc.encode(`key-${i}`));
            buckets[(h.digest() as unknown as number) % 16]++;
        }
        // Each bucket should have ~1000 entries
        const max = Math.max(...buckets);
        const min = Math.min(...buckets);
        // Loose check: no bucket is wildly under/over-represented
        expect(min).toBeGreaterThan(700);
        expect(max).toBeLessThan(1300);
    });

    it('Murmur3_32 distributes into 256 buckets', () => {
        const buckets = new Array(256).fill(0);
        for (let i = 0; i < 25600; i++) {
            const h = new Murmur3_32();
            h.updateBytes(enc.encode(`k-${i}`));
            buckets[(h.digest() as unknown as number) & 0xff]++;
        }
        const max = Math.max(...buckets);
        const min = Math.min(...buckets);
        expect(min).toBeGreaterThan(60);
        expect(max).toBeLessThan(160);
    });
});

describe('collision probability', () => {
    it('low collision rate for sequential keys (Fnv1a32)', () => {
        const seen = new Set<number>();
        let collisions = 0;
        for (let i = 0; i < 10000; i++) {
            const h = new Fnv1a32();
            h.updateBytes(enc.encode(`k-${i}`));
            const v = h.digest() as unknown as number;
            if (seen.has(v)) collisions++;
            seen.add(v);
        }
        // For 10k items in 32-bit space, expected collisions ~ 0.01
        expect(collisions).toBeLessThan(50); // very loose
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
