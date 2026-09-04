import { Float64, UInt32, UInt64 } from '../../types';
import { IRandomEngine, IRandomState, RandomEngineType, SeedSource } from '../types';
import { INV_UINT32_MAX } from '../constants';
import { hashSeedToState } from '../seed-utils';

// Number-based PCG-XSH-RR using 2×32-bit words for 64-bit state
// Internal operations use Number (fast), interface uses BigInt (compatibility)
export class PCGEngine implements IRandomEngine {
    private state_lo: number;
    private state_hi: number;
    private inc_lo: number;
    private inc_hi: number;
    private counter: number;
    private readonly engineType = RandomEngineType.PCG_XSH_RR;

    // PCG constant multiplier: 6364136223846793005 = 0x5851F42D4C957F2D
    private static readonly MULT_LO = 0x4C957F2D | 0;
    private static readonly MULT_HI = 0x5851F42D | 0;

    constructor(seed: SeedSource = null) {
        const seedState = hashSeedToState(seed);
        this.state_lo = Number(seedState.vector[0] & 0xffffffffn) | 0;
        this.state_hi = Number((seedState.vector[0] >> 32n) & 0xffffffffn) | 0;
        // inc = (vector[1] << 1) | 1 — must be odd
        const inc_raw_lo = Number(seedState.vector[1] & 0xffffffffn) | 0;
        const inc_raw_hi = Number((seedState.vector[1] >> 32n) & 0xffffffffn) | 0;
        // Left shift by 1: (hi << 1) | (lo >>> 31), (lo << 1)
        this.inc_lo = ((inc_raw_lo << 1) | 1) | 0;
        this.inc_hi = ((inc_raw_hi << 1) | (inc_raw_lo >>> 31)) | 0;
        this.counter = Number(seedState.counter) | 0;
        this.warmup();
    }

    public next01 = (): Float64 => {
        return this.nextUint32() * INV_UINT32_MAX;
    };

    public nextUint32 = (): UInt32 => {
        this.counter = (this.counter + 1) | 0;

        // Save old state for output
        const old_lo = this.state_lo;
        const old_hi = this.state_hi;

        // LCG step: state = state * MULT + inc
        this.stepLCG();

        // XSH-RR output from old state
        const xorshifted = (((old_hi >>> 18) | (old_lo << 14)) ^ old_hi) >>> 27;
        // Wait, that's not right. Let me redo:
        // oldState >> 18n: shift right 18 bits of 64-bit value
        // For 64-bit: (old_hi >>> 18) in hi, ((old_lo >>> 18) | (old_hi << 14)) in lo
        // Then XOR with oldState
        // Then >> 27n: shift right 27 more bits
        // Total shift: 18 + 27 = 45 bits from top

        // Actually, let me compute it properly:
        // xorshifted = ((oldState >> 18) ^ oldState) >> 27
        // This gives us a 64-45 = 19 bit value? No, >> 27 of a 64-bit gives 37 bits.
        // But we only need 32 bits for the rotation output.

        // Let me re-derive:
        // oldState >> 18: top 46 bits become bits 0-45
        // XOR with oldState: 64-bit result
        // >> 27: top 37 bits become bits 0-36
        // We need bits 0-31 (32 bits) for xorshifted
        // rot = oldState >> 59: top 5 bits

        // 64-bit shift right by 18:
        const s18_lo = ((old_lo >>> 18) | (old_hi << 14)) | 0;
        const s18_hi = (old_hi >>> 18) | 0;

        // XOR with old:
        const xor_lo = (s18_lo ^ old_lo) | 0;
        const xor_hi = (s18_hi ^ old_hi) | 0;

        // Shift right by 27 more (total 45):
        const xorshifted_full_lo = ((xor_lo >>> 27) | (xor_hi << 5)) | 0;
        // We only need low 32 bits
        const xorshifted = xorshifted_full_lo >>> 0;

        // rot = oldState >> 59: top 5 bits of 64-bit = old_hi >>> 27
        const rot = (old_hi >>> 27) & 31;

        return ((xorshifted >>> rot) | (xorshifted << ((-rot) & 31))) >>> 0;
    };

    public nextUint64 = (): UInt64 => {
        const lo = BigInt(this.nextUint32());
        const hi = BigInt(this.nextUint32());
        return (hi << 32n) | lo;
    };

    private stepLCG = (): void => {
        // state = state * MULT + inc (64-bit arithmetic with 32-bit words)
        const s_lo = this.state_lo;
        const s_hi = this.state_hi;
        const m_lo = PCGEngine.MULT_LO;
        const m_hi = PCGEngine.MULT_HI;

        // Multiply: only need low 64 bits
        const a = s_lo >>> 0;
        const b = s_hi >>> 0;
        const c = m_lo >>> 0;
        const d = m_hi >>> 0;

        const ac = a * c;
        const bc = b * c;
        const ad = a * d;

        const mid = (ac >>> 0) + (bc & 0xffffffff) + (ad & 0xffffffff);
        let new_lo = mid | 0;
        let new_hi = ((ac / 0x100000000) + (bc >>> 0) + (ad >>> 0) + (mid / 0x100000000)) | 0;

        // Add inc
        const sum_lo = (new_lo + this.inc_lo) | 0;
        const carry = (sum_lo >>> 0) < (new_lo >>> 0) ? 1 : 0;
        const sum_hi = (new_hi + this.inc_hi + carry) | 0;

        this.state_lo = sum_lo;
        this.state_hi = sum_hi;
    };

    public jumpAhead = (steps: UInt64 = 1n): void => {
        if (steps <= 0n) return;

        if (Number(steps) < 16) {
            const n = Number(steps);
            for (let i = 0; i < n; i++) {
                this.nextUint32();
            }
            return;
        }

        // O(log n) LCG jump: compute MULT^steps and apply
        // This is complex with Number-based arithmetic — use BigInt for jump only
        const oldState = (BigInt(this.state_hi) << 32n) | BigInt(this.state_lo >>> 0);
        const oldInc = (BigInt(this.inc_hi) << 32n) | BigInt(this.inc_lo >>> 0);
        const UINT64_MAX = 0xffffffffffffffffn;

        let curMult = 6364136223846793005n;
        let curPlus = oldInc;
        let accMult = 1n;
        let accPlus = 0n;
        let ssteps = steps;

        while (ssteps > 0n) {
            if (ssteps & 1n) {
                accMult = (accMult * curMult) & UINT64_MAX;
                accPlus = (accPlus * curMult + curPlus) & UINT64_MAX;
            }
            curPlus = ((curMult + 1n) * curPlus) & UINT64_MAX;
            curMult = (curMult * curMult) & UINT64_MAX;
            ssteps >>= 1n;
        }

        const newState = (accMult * oldState + accPlus) & UINT64_MAX;
        this.state_lo = Number(newState & 0xffffffffn) | 0;
        this.state_hi = Number((newState >> 32n) & 0xffffffffn) | 0;
        this.counter += Number(steps);
    };

    public getState = (): IRandomState => {
        return {
            vector: [
                (BigInt(this.state_hi) << 32n) | BigInt(this.state_lo >>> 0),
                (BigInt(this.inc_hi) << 32n) | BigInt(this.inc_lo >>> 0),
                0n, 0n, 0n, 0n,
            ],
            counter: BigInt(this.counter),
            engine: this.engineType,
        };
    };

    public setState = (state: IRandomState): void => {
        this.state_lo = Number(state.vector[0] & 0xffffffffn) | 0;
        this.state_hi = Number((state.vector[0] >> 32n) & 0xffffffffn) | 0;
        this.inc_lo = Number(state.vector[1] & 0xffffffffn) | 0;
        this.inc_hi = Number((state.vector[1] >> 32n) & 0xffffffffn) | 0;
        this.counter = Number(state.counter) | 0;
    };

    public clone = (): IRandomEngine => {
        const copy = new PCGEngine();
        copy.state_lo = this.state_lo;
        copy.state_hi = this.state_hi;
        copy.inc_lo = this.inc_lo;
        copy.inc_hi = this.inc_hi;
        copy.counter = this.counter;
        return copy;
    };

    private warmup = (): void => {
        for (let i = 0; i < 16; i++) {
            this.nextUint32();
        }
        this.counter = 0;
    };
}
