import { Float64, UInt32, UInt64 } from '../../types';
import { IRandomEngine, IRandomState, RandomEngineType, SeedSource } from '../types';
import { hashSeedToState } from '../seed-utils';

// Number-based xoshiro256++ using 8×32-bit words (4×64-bit state)
// Internal operations use Number (fast), interface uses BigInt (compatibility)
export class Xoshiro256PlusPlus implements IRandomEngine {
    private s0_lo: number; private s0_hi: number;
    private s1_lo: number; private s1_hi: number;
    private s2_lo: number; private s2_hi: number;
    private s3_lo: number; private s3_hi: number;
    private counter: number;
    private readonly engineType = RandomEngineType.XOSHIRO256_PLUS_PLUS;

    constructor(seed: SeedSource = null) {
        const state = hashSeedToState(seed);
        this.s0_lo = Number(state.vector[0] & 0xffffffffn) | 0;
        this.s0_hi = Number((state.vector[0] >> 32n) & 0xffffffffn) | 0;
        this.s1_lo = Number(state.vector[1] & 0xffffffffn) | 0;
        this.s1_hi = Number((state.vector[1] >> 32n) & 0xffffffffn) | 0;
        this.s2_lo = Number(state.vector[2] & 0xffffffffn) | 0;
        this.s2_hi = Number((state.vector[2] >> 32n) & 0xffffffffn) | 0;
        this.s3_lo = Number(state.vector[3] & 0xffffffffn) | 0;
        this.s3_hi = Number((state.vector[3] >> 32n) & 0xffffffffn) | 0;
        this.counter = Number(state.counter) | 0;
        this.warmup();
    }

    public next01 = (): Float64 => {
        return this.nextUint32() * (1.0 / 4294967296.0);
    };

    public nextUint32 = (): UInt32 => {
        const [lo] = this.nextUint64Internal();
        return lo >>> 0;
    };

    public nextUint64 = (): UInt64 => {
        const [lo, hi] = this.nextUint64Internal();
        return (BigInt(hi >>> 0) << 32n) | BigInt(lo >>> 0);
    };

    private nextUint64Internal = (): [number, number] => {
        this.counter = (this.counter + 1) | 0;

        // result = rotl(s0 + s3, 23) + s0
        const [sum_lo, sum_hi] = this.add64(this.s0_lo, this.s0_hi, this.s3_lo, this.s3_hi);
        const [rotl_lo, rotl_hi] = this.rotl64(sum_lo, sum_hi, 23);
        const [result_lo, result_hi] = this.add64(rotl_lo, rotl_hi, this.s0_lo, this.s0_hi);

        // t = s1 << 17
        const [t_lo, t_hi] = this.shl64(this.s1_lo, this.s1_hi, 17);

        // s2 ^= s0; s3 ^= s1; s1 ^= s2; s0 ^= s3
        this.s2_lo = (this.s2_lo ^ this.s0_lo) | 0;
        this.s2_hi = (this.s2_hi ^ this.s0_hi) | 0;
        this.s3_lo = (this.s3_lo ^ this.s1_lo) | 0;
        this.s3_hi = (this.s3_hi ^ this.s1_hi) | 0;
        this.s1_lo = (this.s1_lo ^ this.s2_lo) | 0;
        this.s1_hi = (this.s1_hi ^ this.s2_hi) | 0;
        this.s0_lo = (this.s0_lo ^ this.s3_lo) | 0;
        this.s0_hi = (this.s0_hi ^ this.s3_hi) | 0;

        // s2 ^= t
        this.s2_lo = (this.s2_lo ^ t_lo) | 0;
        this.s2_hi = (this.s2_hi ^ t_hi) | 0;

        // s3 = rotl(s3, 45)
        [this.s3_lo, this.s3_hi] = this.rotl64(this.s3_lo, this.s3_hi, 45);

        return [result_lo, result_hi];
    };

    public jumpAhead = (steps: UInt64 = 1n): void => {
        if (steps <= 0n) return;

        if (Number(steps) < 0x10000) {
            const n = Number(steps);
            for (let i = 0; i < n; i++) {
                this.nextUint64Internal();
            }
            return;
        }

        // For large steps, use the canonical jump polynomial (2^128 steps) repeatedly
        const JUMP = [
            0x180ec6d33cfd0aban, 0xd5a61266f0c9392cn,
            0xa9582618e03fc9aan, 0x39abdc4529b1661cn,
        ];
        const jumpsNeeded = Number(steps >> 128n);
        const remainder = Number(steps & ((1n << 128n) - 1n));

        for (let j = 0; j < jumpsNeeded; j++) {
            let js0_lo = 0, js0_hi = 0, js1_lo = 0, js1_hi = 0;
            let js2_lo = 0, js2_hi = 0, js3_lo = 0, js3_hi = 0;
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
                        js2_lo = (js2_lo ^ this.s2_lo) | 0;
                        js2_hi = (js2_hi ^ this.s2_hi) | 0;
                        js3_lo = (js3_lo ^ this.s3_lo) | 0;
                        js3_hi = (js3_hi ^ this.s3_hi) | 0;
                    }
                    this.nextUint64Internal();
                }
            }
            this.s0_lo = js0_lo; this.s0_hi = js0_hi;
            this.s1_lo = js1_lo; this.s1_hi = js1_hi;
            this.s2_lo = js2_lo; this.s2_hi = js2_hi;
            this.s3_lo = js3_lo; this.s3_hi = js3_hi;
        }

        for (let i = 0; i < remainder; i++) {
            this.nextUint64Internal();
        }
    };

    public getState = (): IRandomState => {
        return {
            vector: [
                (BigInt(this.s0_hi) << 32n) | BigInt(this.s0_lo >>> 0),
                (BigInt(this.s1_hi) << 32n) | BigInt(this.s1_lo >>> 0),
                (BigInt(this.s2_hi) << 32n) | BigInt(this.s2_lo >>> 0),
                (BigInt(this.s3_hi) << 32n) | BigInt(this.s3_lo >>> 0),
                0n, 0n,
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
        this.s2_lo = Number(state.vector[2] & 0xffffffffn) | 0;
        this.s2_hi = Number((state.vector[2] >> 32n) & 0xffffffffn) | 0;
        this.s3_lo = Number(state.vector[3] & 0xffffffffn) | 0;
        this.s3_hi = Number((state.vector[3] >> 32n) & 0xffffffffn) | 0;
        this.counter = Number(state.counter) | 0;
    };

    public clone = (): IRandomEngine => {
        const copy = new Xoshiro256PlusPlus();
        copy.s0_lo = this.s0_lo; copy.s0_hi = this.s0_hi;
        copy.s1_lo = this.s1_lo; copy.s1_hi = this.s1_hi;
        copy.s2_lo = this.s2_lo; copy.s2_hi = this.s2_hi;
        copy.s3_lo = this.s3_lo; copy.s3_hi = this.s3_hi;
        copy.counter = this.counter;
        return copy;
    };

    private rotl64 = (lo: number, hi: number, k: number): [number, number] => {
        if (k === 0) return [lo, hi];
        if (k < 32) {
            return [((lo << k) | (hi >>> (32 - k))) | 0, ((hi << k) | (lo >>> (32 - k))) | 0];
        } else {
            const k2 = k - 32;
            return [((hi << k2) | (lo >>> (32 - k2))) | 0, ((lo << k2) | (hi >>> (32 - k2))) | 0];
        }
    };

    private shl64 = (lo: number, hi: number, k: number): [number, number] => {
        if (k === 0) return [lo, hi];
        if (k < 32) {
            return [(lo << k) | 0, ((hi << k) | (lo >>> (32 - k))) | 0];
        } else {
            return [0, (lo << (k - 32)) | 0];
        }
    };

    private add64 = (lo1: number, hi1: number, lo2: number, hi2: number): [number, number] => {
        const new_lo = (lo1 + lo2) | 0;
        const carry = (new_lo >>> 0) < (lo1 >>> 0) ? 1 : 0;
        return [new_lo, (hi1 + hi2 + carry) | 0];
    };

    private warmup = (): void => {
        for (let i = 0; i < 32; i++) {
            this.nextUint64Internal();
        }
        this.counter = 0;
    };
}
