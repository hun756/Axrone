import { describe, expect, it } from 'vitest';
import {
    fmix32,
    fmix32Alt,
    avalanche32,
    splitmix32,
    splitmix32Step,
    splitmix64,
    splitmix64Step,
    murmur3Scramble,
    xorshift32,
    xorshift64,
    xoshiro128Plus,
} from '../hash/mixers';

const U32 = (n: number): number => n >>> 0;

describe('hash/mixers — finalizers and state mixers', () => {
    describe('fmix32', () => {
        it('produces 0 for input 0', () => {
            expect(fmix32(0)).toBe(0);
        });

        it('produces known outputs for various inputs', () => {
            expect(U32(fmix32(0x12345678))).toBe(0xe37cd1bc);
            expect(U32(fmix32(0xdeadbeef))).toBe(0x0de5c6a9);
            expect(U32(fmix32(0xffffffff))).toBe(0x81f16f39);
        });

        it('is deterministic', () => {
            const values = [0, 1, 0x100, 0xdeadbeef, 0xffffffff];
            for (const v of values) {
                expect(fmix32(v)).toBe(fmix32(v));
            }
        });

        it('returns 32-bit unsigned', () => {
            for (let i = 0; i < 1000; i++) {
                const result = fmix32(Math.floor(Math.random() * 0xffffffff));
                expect(result).toBeGreaterThanOrEqual(0);
                expect(result).toBeLessThanOrEqual(0xffffffff);
                expect(Number.isInteger(result)).toBe(true);
            }
        });
    });

    describe('fmix32Alt', () => {
        it('produces known output for input 0', () => {
            expect(fmix32Alt(0)).toBe(0);
        });

        it('produces deterministic output', () => {
            for (const v of [0, 1, 0xdeadbeef, 0xffffffff]) {
                expect(fmix32Alt(v)).toBe(fmix32Alt(v));
            }
        });

        it('returns 32-bit unsigned', () => {
            for (let i = 0; i < 1000; i++) {
                const result = fmix32Alt(Math.floor(Math.random() * 0xffffffff));
                expect(result).toBeGreaterThanOrEqual(0);
                expect(result).toBeLessThanOrEqual(0xffffffff);
            }
        });
    });

    describe('avalanche32', () => {
        it('matches fmix32', () => {
            for (const v of [0, 1, 0x100, 0xdeadbeef, 0xffffffff]) {
                expect(avalanche32(v)).toBe(fmix32(v));
            }
        });
    });

    describe('splitmix32', () => {
        it('produces different outputs for sequential inputs', () => {
            const out1 = splitmix32(0);
            const out2 = splitmix32(1);
            const out3 = splitmix32(out1);
            expect(out1).not.toBe(out2);
            expect(out3).not.toBe(out1);
            expect(out3).not.toBe(out2);
        });

        it('is deterministic', () => {
            expect(splitmix32(42)).toBe(splitmix32(42));
        });

        it('returns 32-bit unsigned', () => {
            for (let i = 0; i < 1000; i++) {
                const result = splitmix32(i);
                expect(result).toBeGreaterThanOrEqual(0);
                expect(result).toBeLessThanOrEqual(0xffffffff);
            }
        });

        it('produces well-distributed values', () => {
            const seen = new Set<number>();
            for (let i = 0; i < 1000; i++) {
                seen.add(splitmix32(i));
            }
            expect(seen.size).toBeGreaterThan(990);
        });
    });

    describe('splitmix32Step', () => {
        it('advances state by golden ratio constant', () => {
            expect(splitmix32Step(0)).toBe(0x9e3779b9);
            expect(splitmix32Step(0x9e3779b9)).toBe(0x3c6ef372);
        });
    });

    describe('splitmix64', () => {
        it('produces different outputs for sequential inputs', () => {
            const a = splitmix64(0n);
            const b = splitmix64(1n);
            const c = splitmix64(a);
            expect(a).not.toBe(b);
            expect(c).not.toBe(a);
            expect(c).not.toBe(b);
        });

        it('returns 64-bit unsigned', () => {
            for (let i = 0n; i < 100n; i++) {
                const result = splitmix64(i * 0x9e3779b97f4a7c15n);
                expect(result).toBeGreaterThanOrEqual(0n);
                expect(result).toBeLessThanOrEqual(0xffffffffffffffffn);
            }
        });

        it('produces well-distributed values', () => {
            const seen = new Set<bigint>();
            for (let i = 0n; i < 1000n; i++) {
                seen.add(splitmix64(i * 0x9e3779b97f4a7c15n));
            }
            expect(seen.size).toBe(1000);
        });
    });

    describe('splitmix64Step', () => {
        it('advances state by golden ratio', () => {
            expect(splitmix64Step(0n)).toBe(0x9e3779b97f4a7c15n);
        });
    });

    describe('murmur3Scramble', () => {
        it('produces known output for known input', () => {
            expect(murmur3Scramble(0)).toBe(0);
            expect(U32(murmur3Scramble(0x12345678))).toBe(0x6e163dcb);
        });

        it('returns 32-bit unsigned', () => {
            for (let i = 0; i < 100; i++) {
                const result = murmur3Scramble(i);
                expect(result).toBeGreaterThanOrEqual(0);
                expect(result).toBeLessThanOrEqual(0xffffffff);
            }
        });
    });

    describe('xorshift32', () => {
        it('produces different outputs for sequential inputs', () => {
            const a = xorshift32(1);
            const b = xorshift32(a);
            expect(a).not.toBe(b);
            expect(b).not.toBe(0);
        });

        it('returns 32-bit unsigned', () => {
            for (let i = 0; i < 100; i++) {
                const result = xorshift32(i);
                expect(result).toBeGreaterThanOrEqual(0);
                expect(result).toBeLessThanOrEqual(0xffffffff);
            }
        });
    });

    describe('xorshift64', () => {
        it('produces different outputs for sequential inputs', () => {
            const a = xorshift64(1n);
            const b = xorshift64(a);
            expect(a).not.toBe(b);
        });

        it('returns 64-bit unsigned', () => {
            for (let i = 0n; i < 100n; i++) {
                const result = xorshift64(i);
                expect(result).toBeGreaterThanOrEqual(0n);
                expect(result).toBeLessThanOrEqual(0xffffffffffffffffn);
            }
        });
    });

    describe('xoshiro128Plus', () => {
        it('returns a 4-tuple of numbers', () => {
            const result = xoshiro128Plus(1, 2, 3, 4);
            expect(result).toHaveLength(4);
            for (const v of result) {
                expect(typeof v).toBe('number');
            }
        });

        it('is deterministic', () => {
            const a = xoshiro128Plus(0x12345678, 0x9abcdef0, 0xdeadbeef, 0xcafebabe);
            const b = xoshiro128Plus(0x12345678, 0x9abcdef0, 0xdeadbeef, 0xcafebabe);
            expect(a).toEqual(b);
        });

        it('advances state (different outputs on repeated calls)', () => {
            let s = xoshiro128Plus(1, 2, 3, 4);
            const seen = new Set<string>();
            seen.add(s.join(','));
            for (let i = 0; i < 10; i++) {
                s = xoshiro128Plus(s[0], s[1], s[2], s[3]);
                seen.add(s.join(','));
            }
            // All 11 states should be unique
            expect(seen.size).toBe(11);
        });

        it('returns 32-bit unsigned values', () => {
            const [s0, s1, s2, s3] = xoshiro128Plus(100, 200, 300, 400);
            for (const v of [s0, s1, s2, s3]) {
                expect(v).toBeGreaterThanOrEqual(0);
                expect(v).toBeLessThanOrEqual(0xffffffff);
            }
        });

        it('different initial states produce different outputs', () => {
            const a = xoshiro128Plus(1, 2, 3, 4);
            const b = xoshiro128Plus(5, 6, 7, 8);
            expect(a).not.toEqual(b);
        });
    });
});
