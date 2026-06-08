import { describe, expect, it } from 'vitest';
import { Fnv1a32, Murmur3_32, XxHash32, Cyrb53, Djb2, Crc32, Crc32c, Fnv1a64, XxHash64, Murmur2_64, SipHash2_4 } from '../hash/algorithms';

const enc = new TextEncoder();

function captureOutputs(): void {
    describe('ACTUAL OUTPUTS (for known vectors reference)', () => {
        it('captures all algorithm outputs', () => {
            const tests: { name: string; input: string; algorithm: string; output: string }[] = [];

            for (const [name, factory] of [
                ['Fnv1a32-ASCII', () => {
                    const h = new Fnv1a32();
                    h.updateBytes(enc.encode('hello'));
                    return h.digest() as unknown as number;
                }],
                ['Fnv1a32-UTF16', () => {
                    const h = new Fnv1a32();
                    h.updateString('hello');
                    return h.digest() as unknown as number;
                }],
                ['Murmur3_32', () => {
                    const h = new Murmur3_32();
                    h.updateBytes(enc.encode('hello'));
                    return h.digest() as unknown as number;
                }],
                ['XxHash32', () => {
                    const h = new XxHash32();
                    h.updateBytes(enc.encode('hello'));
                    return h.digest() as unknown as number;
                }],
                ['Cyrb53', () => {
                    const h = new Cyrb53();
                    h.updateBytes(enc.encode('hello'));
                    return (h.digest() as unknown as bigint).toString(16);
                }],
                ['Djb2', () => {
                    const h = new Djb2();
                    h.updateBytes(enc.encode('hello'));
                    return h.digest() as unknown as number;
                }],
                ['Crc32', () => {
                    const h = new Crc32();
                    h.updateBytes(enc.encode('hello'));
                    return h.digest() as unknown as number;
                }],
                ['Crc32c', () => {
                    const h = new Crc32c();
                    h.updateBytes(enc.encode('hello'));
                    return h.digest() as unknown as number;
                }],
                ['Fnv1a64', () => {
                    const h = new Fnv1a64();
                    h.updateBytes(enc.encode('hello'));
                    return (h.digest() as unknown as bigint).toString(16);
                }],
                ['XxHash64', () => {
                    const h = new XxHash64();
                    h.updateBytes(enc.encode('hello'));
                    return (h.digest() as unknown as bigint).toString(16);
                }],
                ['Murmur2_64', () => {
                    const h = new Murmur2_64();
                    h.updateBytes(enc.encode('hello'));
                    return (h.digest() as unknown as bigint).toString(16);
                }],
                ['SipHash2_4', () => {
                    const key = new Uint8Array(16);
                    const h = new SipHash2_4(key);
                    h.updateBytes(enc.encode('hello'));
                    return (h.digest() as unknown as bigint).toString(16);
                }],
            ] as const) {
                const out = factory();
                tests.push({ name, input: 'hello', algorithm: name.split('-')[0]!, output: typeof out === 'string' ? out : '0x' + (out as number).toString(16) });
            }
            console.log('\n=== Hash output values for "hello" ===');
            for (const t of tests) {
                console.log(`${t.name}: ${t.output}`);
            }
            expect(tests.length).toBe(12);
        });
    });
}

captureOutputs();
