import { Float64, UInt32, UInt64 } from '../../types';
import { IRandomEngine, IRandomState, RandomEngineType, SeedSource } from '../types';
import { hashSeedToState } from '../seed-utils';

// Number-based xoroshiro128++ using 4×32-bit words (2×64-bit state)
// Internal operations use Number (fast), interface uses BigInt (compatibility)
export class Xoroshiro128PlusPlus implements IRandomEngine {
    private s0_lo: number;
    private s0_hi: number;
    private s1_lo: number;
    private s1_hi: number;
    private counter: number;
    private readonly engineType = RandomEngineType.XOROSHIRO128_PLUS_PLUS;

    constructor(seed: SeedSource = null) {
        const state = hashSeedToState(seed);
        // Split 64-bit BigInts into 2×32-bit words
        this.s0_lo = Number(state.vector[0] & 0xffffffffn) | 0;
        this.s0_hi = Number((state.vector[0] >> 32n) & 0xffffffffn) | 0;
        this.s1_lo = Number(state.vector[1] & 0xffffffffn) | 0;
        this.s1_hi = Number((state.vector[1] >> 32n) & 0xffffffffn) | 0;
        this.counter = Number(state.counter) | 0;
        this.warmup();
    }

    public next01 = (): Float64 => {
        // Use 32-bit draw for speed (53-bit precision not needed for most uses)
        return this.nextUint32() * (1.0 / 4294967296.0);
    };

    public nextUint32 = (): UInt32 => {
        // Draw 64-bit, return low 32 bits
        const [lo, hi] = this.nextUint64Internal();
        return lo >>> 0;
    };

    public nextUint64 = (): UInt64 => {
        const [lo, hi] = this.nextUint64Internal();
        return (BigInt(hi >>> 0) << 32n) | BigInt(lo >>> 0);
    };

    private nextUint64Internal = (): [number, number] => {
        this.counter = (this.counter + 1) | 0;

        // result = rotl(s0 + s1, 17) + s0
        const [sum_lo, sum_hi] = this.add64(this.s0_lo, this.s0_hi, this.s1_lo, this.s1_hi);
        const [result_lo, result_hi] = this.add64(
            ...this.rotl64(sum_lo, sum_hi, 17),
            this.s0_lo,
            this.s0_hi
        );

        // s1 ^= s0
        this.s1_lo = (this.s1_lo ^ this.s0_lo) | 0;
        this.s1_hi = (this.s1_hi ^ this.s0_hi) | 0;

        // s0 = rotl(s0, 49) ^ s1 ^ (s1 << 21)
        const [rotl_s0_lo, rotl_s0_hi] = this.rotl64(this.s0_lo, this.s0_hi, 49);
        const [shift_s1_lo, shift_s1_hi] = this.shl64(this.s1_lo, this.s1_hi, 21);
        this.s0_lo = (rotl_s0_lo ^ this.s1_lo ^ shift_s1_lo) | 0;
        this.s0_hi = (rotl_s0_hi ^ this.s1_hi ^ shift_s1_hi) | 0;

        // s1 = rotl(s1, 28)
        [this.s1_lo, this.s1_hi] = this.rotl64(this.s1_lo, this.s1_hi, 28);

        return [result_lo, result_hi];
    };

    public jumpAhead = (steps: UInt64 = 1n): void => {
        if (steps <= 0n) return;

        // For small steps, loop directly
        if (steps < 0x10000n) {
            const n = Number(steps);
            for (let i = 0; i < n; i++) {
                this.nextUint64Internal();
            }
            return;
        }

        // For large steps, use the canonical jump polynomial (2^64 steps) repeatedly
        const JUMP = [0x2bd7a6a6a6e99c2ddcn, 0x0992ccaf6a6fca05n];
        const jumpsNeeded = Number(steps >> 64n);
        const remainder = Number(steps & ((1n << 64n) - 1n));

        for (let j = 0; j < jumpsNeeded; j++) {
            let js0_lo = 0, js0_hi = 0, js1_lo = 0, js1_hi = 0;
            for (const jump of JUMP) {
                const jump_lo = Number(jump & 0xffffffffn) | 0;
                const jump_hi = Number((jump >> 32n) & 0xffffffffn) | 0;
                for (let b = 0; b < 64; b++) {
                    const bit = b < 32
                        ? (jump_lo >>> b) & 1
                        : (jump_hi >>> (b - 32)) & 1;
                    if (bit) {
                        js0_lo = (js0_lo ^ this.s0_lo) | 0;
                        js0_hi = (js0_hi ^ this.s0_hi) | 0;
                        js1_lo = (js1_lo ^ this.s1_lo) | 0;
                        js1_hi = (js1_hi ^ this.s1_hi) | 0;
                    }
                    this.nextUint64Internal();
                }
            }
            this.s0_lo = js0_lo;
            this.s0_hi = js0_hi;
            this.s1_lo = js1_lo;
            this.s1_hi = js1_hi;
        }

        // Handle remainder
        for (let i = 0; i < remainder; i++) {
            this.nextUint64Internal();
        }
    };

    public getState = (): IRandomState => {
        return {
            vector: [
                (BigInt(this.s0_hi) << 32n) | BigInt(this.s0_lo >>> 0),
                (BigInt(this.s1_hi) << 32n) | BigInt(this.s1_lo >>> 0),
                0n, 0n, 0n, 0n,
            ],
            counter: BigInt(this.counter),
            engine: this.engineType,
        };
    };

    public setState = (state: IRandomState): void => {
        this.s0_lo = Number(state.vector[0] & 0xffffffffn) | 0;
        this.s0_hi = Number((state.vector[0] >> 32n) & 0xffffffffn) | 0;
        this.s1_lo = Number(state.vector[1] & 0xffffffffn) | 0;
        this.s1_hi = Number((state.vector[1] >> 32n) & 0xffffffffn) | 0;
        this.counter = Number(state.counter) | 0;
    };

    public clone = (): IRandomEngine => {
        const copy = new Xoroshiro128PlusPlus();
        copy.s0_lo = this.s0_lo;
        copy.s0_hi = this.s0_hi;
        copy.s1_lo = this.s1_lo;
        copy.s1_hi = this.s1_hi;
        copy.counter = this.counter;
        return copy;
    };

    private rotl64 = (lo: number, hi: number, k: number): [number, number] => {
        if (k === 0) return [lo, hi];
        if (k < 32) {
            const new_lo = ((lo << k) | (hi >>> (32 - k))) | 0;
            const new_hi = ((hi << k) | (lo >>> (32 - k))) | 0;
            return [new_lo, new_hi];
        } else {
            const k2 = k - 32;
            const new_lo = ((hi << k2) | (lo >>> (32 - k2))) | 0;
            const new_hi = ((lo << k2) | (hi >>> (32 - k2))) | 0;
            return [new_lo, new_hi];
        }
    };

    private shl64 = (lo: number, hi: number, k: number): [number, number] => {
        if (k === 0) return [lo, hi];
        if (k < 32) {
            const new_hi = ((hi << k) | (lo >>> (32 - k))) | 0;
            const new_lo = (lo << k) | 0;
            return [new_lo, new_hi];
        } else {
            return [(lo << (k - 32)) | 0, 0];
        }
    };

    private add64 = (lo1: number, hi1: number, lo2: number, hi2: number): [number, number] => {
        const new_lo = (lo1 + lo2) | 0;
        const carry = (new_lo >>> 0) < (lo1 >>> 0) ? 1 : 0;
        const new_hi = (hi1 + hi2 + carry) | 0;
        return [new_lo, new_hi];
    };

    private warmup = (): void => {
        for (let i = 0; i < 32; i++) {
            this.nextUint64Internal();
        }
        this.counter = 0;
    };
}
