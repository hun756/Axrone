import { Float64, UInt32, UInt64 } from '../types';

export const enum RandomEngineType {
    XOROSHIRO128_PLUS_PLUS = 'xoroshiro128++',
    PCG_XSH_RR = 'pcg-xsh-rr',
    XOSHIRO256_PLUS_PLUS = 'xoshiro256++',
    SPLITMIX64 = 'splitmix64',
    CHACHA20 = 'chacha20',
    CRYPTO = 'crypto',
}

export const enum Endianness {
    LITTLE = 'little',
    BIG = 'big',
}

export type SeedSource = number | string | Uint8Array | Int32Array | BigInt64Array | null;
export type RandomStateVector = [UInt64, UInt64, UInt64, UInt64];

export interface IRandomState {
    readonly vector: RandomStateVector;
    readonly counter: UInt64;
    readonly engine: RandomEngineType;
}

export type RandomResult<T> = readonly [T, IRandomState];

export interface IRandomEngine {
    readonly next01: () => Float64;
    readonly nextUint32: () => UInt32;
    readonly nextUint64: () => UInt64;
    readonly jumpAhead: (steps?: UInt64) => void;
    readonly getState: () => IRandomState;
    readonly setState: (state: IRandomState) => void;
    readonly clone: () => IRandomEngine;
}

export interface IDistribution<T> {
    readonly sample: (state: IRandomState) => RandomResult<T>;
}

const UINT32_MAX = 0xffffffff >>> 0;
const UINT64_MAX = 0xffffffffffffffffn;
const INV_UINT32_MAX = 1.0 / (UINT32_MAX + 1);
const INV_UINT64_MAX = 1.0 / Number(UINT64_MAX + 1n);
const PI = Math.PI;
const TWO_PI = 2.0 * PI;
const LN2 = Math.LN2;
const E = Math.E;
const SQRT_2PI = Math.sqrt(TWO_PI);

const validateProbability = (p: Float64, name = 'probability'): void => {
    if (p < 0 || p > 1 || !Number.isFinite(p)) {
        throw new RangeError(`${name} must be between 0 and 1`);
    }
};

const validatePositive = (value: Float64, name = 'value'): void => {
    if (value <= 0 || !Number.isFinite(value)) {
        throw new RangeError(`${name} must be positive`);
    }
};

const validateNonNegative = (value: number, name = 'value'): void => {
    if (value < 0 || !Number.isFinite(value)) {
        throw new RangeError(`${name} must be non-negative`);
    }
};

const validateInteger = (value: number, name = 'value'): void => {
    if (!Number.isInteger(value) || !Number.isFinite(value)) {
        throw new TypeError(`${name} must be an integer`);
    }
};

const hex = (() => {
    const lookup: string[] = [];
    for (let i = 0; i < 256; i++) {
        lookup.push((i < 16 ? '0' : '') + i.toString(16));
    }
    return lookup as readonly string[];
})();

const createSeedFromTime = (): IRandomState => {
    const now = BigInt(Date.now());
    const pid = typeof process !== 'undefined' && process.pid ? BigInt(process.pid) : 0n;
    const entropy = crypto?.getRandomValues
        ? BigInt(
              '0x' +
                  Array.from(crypto.getRandomValues(new Uint8Array(8)))
                      .map((b) => hex[b])
                      .join('')
          )
        : 0n;

    const high = now << 32n;
    const mid = pid << 16n;
    const low = entropy & 0xffffn;

    const seed = high | mid | low;

    return {
        vector: [seed, seed ^ 0xdeadbeefn, seed ^ 0x12345678n, seed ^ 0x87654321n],
        counter: 0n,
        engine: RandomEngineType.XOROSHIRO128_PLUS_PLUS,
    };
};

const hashSeedToState = (seed: SeedSource): IRandomState => {
    if (seed === null) {
        return createSeedFromTime();
    }

    let s0 = 0x6a09e667f3bcc908n;
    let s1 = 0xbb67ae8584caa73bn;
    let s2 = 0x3c6ef372fe94f82bn;
    let s3 = 0xa54ff53a5f1d36f1n;

    const mix = (): void => {
        s0 = (s0 ^ s1 ^ s2 ^ s3) & UINT64_MAX;
        s1 = ((s1 << 11n) | (s1 >> 53n)) & UINT64_MAX;
        s2 = ((s2 << 23n) | (s2 >> 41n)) & UINT64_MAX;
        s3 = ((s3 << 7n) | (s3 >> 57n)) & UINT64_MAX;

        const t = (s1 << 29n) & UINT64_MAX;

        s2 ^= s0;
        s3 ^= s1;
        s1 ^= s2;
        s0 ^= s3;

        s2 ^= t;
        s3 = ((s3 << 25n) | (s3 >> 39n)) & UINT64_MAX;
    };

    if (typeof seed === 'string') {
        const encoder = new TextEncoder();
        const data = encoder.encode(seed);

        for (let i = 0; i < data.length; i += 32) {
            const chunk = data.slice(i, Math.min(i + 32, data.length));
            let a = 0n,
                b = 0n,
                c = 0n,
                d = 0n;

            for (let j = 0; j < chunk.length; j++) {
                const bitShift = BigInt(j & 7) << 3n;
                const val = BigInt(chunk[j]);

                if (j < 8) a |= val << bitShift;
                else if (j < 16) b |= val << bitShift;
                else if (j < 24) c |= val << bitShift;
                else d |= val << bitShift;
            }

            s0 ^= a;
            s1 ^= b;
            s2 ^= c;
            s3 ^= d;

            mix();
        }
    } else if (typeof seed === 'number') {
        const val = BigInt(seed);
        s0 ^= val;
        s1 ^= val ^ 0x5555555555555555n;
        mix();
    } else if (seed instanceof Uint8Array) {
        for (let i = 0; i < seed.length; i += 32) {
            const chunk = seed.slice(i, Math.min(i + 32, seed.length));
            let a = 0n,
                b = 0n,
                c = 0n,
                d = 0n;

            for (let j = 0; j < chunk.length; j++) {
                const bitShift = BigInt(j & 7) << 3n;
                const val = BigInt(chunk[j]);

                if (j < 8) a |= val << bitShift;
                else if (j < 16) b |= val << bitShift;
                else if (j < 24) c |= val << bitShift;
                else d |= val << bitShift;
            }

            s0 ^= a;
            s1 ^= b;
            s2 ^= c;
            s3 ^= d;

            mix();
        }
    } else if (seed instanceof Int32Array) {
        for (let i = 0; i < seed.length; i += 8) {
            let a = 0n,
                b = 0n,
                c = 0n,
                d = 0n;

            for (let j = 0; j < 8 && i + j < seed.length; j++) {
                const bitShift = BigInt(j) << 5n;
                const val = BigInt(seed[i + j]) & 0xffffffffn;

                if (j < 2) a |= val << bitShift;
                else if (j < 4) b |= val << bitShift;
                else if (j < 6) c |= val << bitShift;
                else d |= val << bitShift;
            }

            s0 ^= a;
            s1 ^= b;
            s2 ^= c;
            s3 ^= d;

            mix();
        }
    } else if (seed instanceof BigInt64Array) {
        for (let i = 0; i < seed.length; i += 4) {
            const a = i < seed.length ? BigInt(seed[i]) & UINT64_MAX : 0n;
            const b = i + 1 < seed.length ? BigInt(seed[i + 1]) & UINT64_MAX : 0n;
            const c = i + 2 < seed.length ? BigInt(seed[i + 2]) & UINT64_MAX : 0n;
            const d = i + 3 < seed.length ? BigInt(seed[i + 3]) & UINT64_MAX : 0n;

            s0 ^= a;
            s1 ^= b;
            s2 ^= c;
            s3 ^= d;

            mix();
        }
    }

    for (let i = 0; i < 16; i++) {
        mix();
    }

    if (s0 === 0n && s1 === 0n && s2 === 0n && s3 === 0n) {
        s0 = 0x6a09e667f3bcc908n;
        s1 = 0xbb67ae8584caa73bn;
        s2 = 0x3c6ef372fe94f82bn;
        s3 = 0xa54ff53a5f1d36f1n;
    }

    return {
        vector: [s0, s1, s2, s3],
        counter: 0n,
        engine: RandomEngineType.XOROSHIRO128_PLUS_PLUS,
    };
};

class Xoroshiro128PlusPlus implements IRandomEngine {
    private s0: UInt64;
    private s1: UInt64;
    private counter: UInt64;
    private readonly engineType = RandomEngineType.XOROSHIRO128_PLUS_PLUS;

    constructor(seed: SeedSource = null) {
        const state = hashSeedToState(seed);
        this.s0 = state.vector[0];
        this.s1 = state.vector[1];
        this.counter = state.counter;
        this.warmup();
    }

    public next01 = (): Float64 => {
        const result = Number(this.nextUint64() >> 11n) * (1.0 / 9007199254740992.0);
        return result;
    };

    public nextUint32 = (): UInt32 => {
        return Number(this.nextUint64() & 0xffffffffn) >>> 0;
    };

    public nextUint64 = (): UInt64 => {
        this.counter++;

        const result = (this.rotl(this.s0 + this.s1, 17n) + this.s0) & UINT64_MAX;

        const t = (this.s1 << 41n) & UINT64_MAX;

        this.s1 ^= this.s0;
        this.s0 = this.rotl(this.s0, 49n) ^ this.s1 ^ (this.s1 << 21n);
        this.s1 = this.rotl(this.s1, 28n);

        return result;
    };

    public jumpAhead = (steps: UInt64 = 1n): void => {
        if (steps <= 0n) return;

        if (steps < 16n) {
            for (let i = 0n; i < steps; i++) {
                this.nextUint64();
            }
            return;
        }

        const JUMP = [0xdf900294d8f554a5n, 0x170865df4b3201fcn];

        let js0 = 0n;
        let js1 = 0n;

        for (const jump of JUMP) {
            for (let b = 0n; b < 64n; b++) {
                if ((jump & (1n << b)) !== 0n) {
                    js0 ^= this.s0;
                    js1 ^= this.s1;
                }
                this.nextUint64();
            }
        }

        this.s0 = js0;
        this.s1 = js1;
        this.counter += steps - 1n;
    };

    public getState = (): IRandomState => {
        return {
            vector: [this.s0, this.s1, 0n, 0n],
            counter: this.counter,
            engine: this.engineType,
        };
    };

    public setState = (state: IRandomState): void => {
        this.s0 = state.vector[0];
        this.s1 = state.vector[1];
        this.counter = state.counter;
    };

    public clone = (): IRandomEngine => {
        const copy = new Xoroshiro128PlusPlus();
        copy.s0 = this.s0;
        copy.s1 = this.s1;
        copy.counter = this.counter;
        return copy;
    };

    private rotl = (x: UInt64, k: UInt64): UInt64 => {
        return ((x << k) | (x >> (64n - k))) & UINT64_MAX;
    };

    private warmup = (): void => {
        for (let i = 0; i < 32; i++) {
            this.nextUint64();
        }
        this.counter = 0n;
    };
}

class Xoshiro256PlusPlus implements IRandomEngine {
    private s0: UInt64;
    private s1: UInt64;
    private s2: UInt64;
    private s3: UInt64;
    private counter: UInt64;
    private readonly engineType = RandomEngineType.XOSHIRO256_PLUS_PLUS;

    constructor(seed: SeedSource = null) {
        const state = hashSeedToState(seed);
        this.s0 = state.vector[0];
        this.s1 = state.vector[1];
        this.s2 = state.vector[2];
        this.s3 = state.vector[3];
        this.counter = state.counter;
        this.warmup();
    }

    public next01 = (): Float64 => {
        const result = Number(this.nextUint64() >> 11n) * (1.0 / 9007199254740992.0);
        return result;
    };

    public nextUint32 = (): UInt32 => {
        return Number(this.nextUint64() & 0xffffffffn) >>> 0;
    };

    public nextUint64 = (): UInt64 => {
        this.counter++;

        const result = (this.rotl(this.s0 + this.s3, 23n) + this.s0) & UINT64_MAX;

        const t = (this.s1 << 17n) & UINT64_MAX;

        this.s2 ^= this.s0;
        this.s3 ^= this.s1;
        this.s1 ^= this.s2;
        this.s0 ^= this.s3;

        this.s2 ^= t;
        this.s3 = this.rotl(this.s3, 45n);

        return result;
    };

    public jumpAhead = (steps: UInt64 = 1n): void => {
        if (steps <= 0n) return;

        if (steps < 16n) {
            for (let i = 0n; i < steps; i++) {
                this.nextUint64();
            }
            return;
        }

        const JUMP = [
            0x180ec6d33cfd0aban,
            0xd5a61266f0c9392cn,
            0xa9582618e03fc9aan,
            0x39abdc4529b1661cn,
        ];

        let s0 = 0n;
        let s1 = 0n;
        let s2 = 0n;
        let s3 = 0n;

        for (const jump of JUMP) {
            for (let b = 0n; b < 64n; b++) {
                if ((jump & (1n << b)) !== 0n) {
                    s0 ^= this.s0;
                    s1 ^= this.s1;
                    s2 ^= this.s2;
                    s3 ^= this.s3;
                }
                this.nextUint64();
            }
        }

        this.s0 = s0;
        this.s1 = s1;
        this.s2 = s2;
        this.s3 = s3;
        this.counter += steps - 1n;
    };

    public getState = (): IRandomState => {
        return {
            vector: [this.s0, this.s1, this.s2, this.s3],
            counter: this.counter,
            engine: this.engineType,
        };
    };

    public setState = (state: IRandomState): void => {
        this.s0 = state.vector[0];
        this.s1 = state.vector[1];
        this.s2 = state.vector[2];
        this.s3 = state.vector[3];
        this.counter = state.counter;
    };

    public clone = (): IRandomEngine => {
        const copy = new Xoshiro256PlusPlus();
        copy.s0 = this.s0;
        copy.s1 = this.s1;
        copy.s2 = this.s2;
        copy.s3 = this.s3;
        copy.counter = this.counter;
        return copy;
    };

    private rotl = (x: UInt64, k: UInt64): UInt64 => {
        return ((x << k) | (x >> (64n - k))) & UINT64_MAX;
    };

    private warmup = (): void => {
        for (let i = 0; i < 32; i++) {
            this.nextUint64();
        }
        this.counter = 0n;
    };
}
