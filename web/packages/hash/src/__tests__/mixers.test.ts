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
} from '../hash/mixers';

describe('hash/mixers — finalizers and state mixers', () => {
    describe('fmix32', () => {
        it('produces known output for input 0', () => {
            expect(fmix32(0)).toBe(0);
        });

        it('matches MurmurHash3 finalizer reference values', () => {
            expect(fmix32(0)).toBe(0);
            expect(fmix32(0x12345678)).toBe(0x1a1733dd);
            expect(fmix32(0xdeadbeef)).toBe(0x5b9b8bb5);
            expect(fmix32(0xffffffff)).toBe(0xa4c1c4d9);
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
        it('produces different output from fmix32', () => {
            const f1 = fmix32(0x12345678);
            const f2 = fmix32Alt(0x12345678);
            expect(f2).not.toBe(f1);
        });

        it('is deterministic', () => {
            for (const v of [0, 1, 0xdeadbeef, 0xffffffff]) {
                expect(fmix32Alt(v)).toBe(fmix32Alt(v));
            }
        });
    });

    describe('avalanche32', () => {
        it('is equivalent to fmix32', () => {
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
    });

    describe('splitmix64Step', () => {
        it('advances state by golden ratio', () => {
            expect(splitmix64Step(0n)).toBe(0x9e3779b97f4a7c15n);
        });
    });

    describe('murmur3Scramble', () => {
        it('produces known output for known input', () => {
            expect(murmur3Scramble(0)).toBe(0);
            expect(murmur3Scramble(0x12345678)).toBe(0x3438b43b);
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
});
