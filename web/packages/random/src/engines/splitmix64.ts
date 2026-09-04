import { Float64, UInt32, UInt64 } from '../../types';
import { IRandomEngine, IRandomState, RandomEngineType, SeedSource } from '../types';
import { hashSeedToState } from '../seed-utils';

// Number-based SplitMix64 using 2×32-bit words (1×64-bit state)
// Internal operations use Number (fast), interface uses BigInt (compatibility)
export class SplitMix64Engine implements IRandomEngine {
    private state_lo: number;
    private state_hi: number;
    private counter: number;
    private readonly engineType = RandomEngineType.SPLITMIX64;

    constructor(seed: SeedSource = null) {
        const seedState = hashSeedToState(seed);
        this.state_lo = Number(seedState.vector[0] & 0xffffffffn) | 0;
        this.state_hi = Number((seedState.vector[0] >> 32n) & 0xffffffffn) | 0;
        this.counter = Number(seedState.counter) | 0;
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
        return (BigInt(hi) << 32n) | BigInt(lo >>> 0);
    };

    private nextUint64Internal = (): [number, number] => {
        this.counter = (this.counter + 1) | 0;

        // state += 0x9e3779b97f4a7c15
        const [new_lo, new_hi] = this.add64(
            this.state_lo, this.state_hi,
            0x7f4a7c15 | 0, 0x9e3779b9 | 0
        );
        this.state_lo = new_lo;
        this.state_hi = new_hi;

        // z = state
        let z_lo = this.state_lo;
        let z_hi = this.state_hi;

        // z = ((z ^ (z >> 30)) * 0xbf58476d1ce4e5b9) & UINT64_MAX
        const [shifted_lo, shifted_hi] = this.shr64(z_lo, z_hi, 30);
        z_lo = (z_lo ^ shifted_lo) | 0;
        z_hi = (z_hi ^ shifted_hi) | 0;
        [z_lo, z_hi] = this.mul64(z_lo, z_hi, 0x1ce4e5b9 | 0, 0xbf58476d | 0);

        // z = ((z ^ (z >> 27)) * 0x94d049bb133111eb) & UINT64_MAX
        const [shifted2_lo, shifted2_hi] = this.shr64(z_lo, z_hi, 27);
        z_lo = (z_lo ^ shifted2_lo) | 0;
        z_hi = (z_hi ^ shifted2_hi) | 0;
        [z_lo, z_hi] = this.mul64(z_lo, z_hi, 0x133111eb | 0, 0x94d049bb | 0);

        // return z ^ (z >> 31)
        const [final_lo, final_hi] = this.shr64(z_lo, z_hi, 31);
        return [(z_lo ^ final_lo) | 0, (z_hi ^ final_hi) | 0];
    };

    public jumpAhead = (steps: UInt64 = 1n): void => {
        if (steps <= 0n) return;

        // SplitMix64 jump is O(1): state += steps * 0x9e3779b97f4a7c15
        const steps_lo = Number(steps & 0xffffffffn) | 0;
        const steps_hi = Number((steps >> 32n) & 0xffffffffn) | 0;
        
        // Multiply steps by constant
        const [prod_lo, prod_hi] = this.mul64(steps_lo, steps_hi, 0x7f4a7c15 | 0, 0x9e3779b9 | 0);
        
        // Add to state
        const [new_lo, new_hi] = this.add64(this.state_lo, this.state_hi, prod_lo, prod_hi);
        this.state_lo = new_lo;
        this.state_hi = new_hi;
        
        this.counter += Number(steps);
    };

    public getState = (): IRandomState => {
        return {
            vector: [
                (BigInt(this.state_hi) << 32n) | BigInt(this.state_lo >>> 0),
                0n, 0n, 0n, 0n, 0n,
            ],
            counter: BigInt(this.counter),
            engine: this.engineType,
        };
    };

    public setState = (state: IRandomState): void => {
        this.state_lo = Number(state.vector[0] & 0xffffffffn) | 0;
        this.state_hi = Number((state.vector[0] >> 32n) & 0xffffffffn) | 0;
        this.counter = Number(state.counter) | 0;
    };

    public clone = (): IRandomEngine => {
        const copy = new SplitMix64Engine();
        copy.state_lo = this.state_lo;
        copy.state_hi = this.state_hi;
        copy.counter = this.counter;
        return copy;
    };

    // 64-bit arithmetic helpers using 2×32-bit words
    private add64 = (lo1: number, hi1: number, lo2: number, hi2: number): [number, number] => {
        const new_lo = (lo1 + lo2) | 0;
        const carry = (new_lo >>> 0) < (lo1 >>> 0) ? 1 : 0;
        const new_hi = (hi1 + hi2 + carry) | 0;
        return [new_lo, new_hi];
    };

    private shr64 = (lo: number, hi: number, k: number): [number, number] => {
        if (k === 0) return [lo, hi];
        if (k < 32) {
            const new_lo = ((lo >>> k) | (hi << (32 - k))) | 0;
            const new_hi = (hi >>> k) | 0;
            return [new_lo, new_hi];
        } else {
            return [(hi >>> (k - 32)) | 0, 0];
        }
    };

    private mul64 = (lo1: number, hi1: number, lo2: number, hi2: number): [number, number] => {
        // 64-bit multiplication using 32-bit chunks (only low 64 bits needed)
        const a = lo1 >>> 0;
        const b = hi1 >>> 0;
        const c = lo2 >>> 0;
        const d = hi2 >>> 0;

        const ac = a * c;
        const bc = b * c;
        const ad = a * d;

        const mid = (ac >>> 0) + (bc & 0xffffffff) + (ad & 0xffffffff);
        const result_lo = mid | 0;
        const result_hi = ((ac / 0x100000000) + (bc >>> 0) + (ad >>> 0) + (mid / 0x100000000)) | 0;

        return [result_lo, result_hi];
    };

    private warmup = (): void => {
        for (let i = 0; i < 8; i++) {
            this.nextUint64Internal();
        }
        this.counter = 0;
    };
}
