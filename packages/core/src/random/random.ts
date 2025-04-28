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

class PCGEngine implements IRandomEngine {
    private state: UInt64;
    private inc: UInt64;
    private counter: UInt64;
    private readonly engineType = RandomEngineType.PCG_XSH_RR;

    constructor(seed: SeedSource = null) {
        const seedState = hashSeedToState(seed);
        this.state = seedState.vector[0];
        this.inc = (seedState.vector[1] << 1n) | 1n;
        this.counter = seedState.counter;
        this.warmup();
    }

    public next01 = (): Float64 => {
        return this.nextUint32() * INV_UINT32_MAX;
    };

    public nextUint32 = (): UInt32 => {
        this.counter++;
        const oldState = this.state;

        this.state = (oldState * 6364136223846793005n + this.inc) & UINT64_MAX;

        const xorshifted = Number(((oldState >> 18n) ^ oldState) >> 27n) >>> 0;
        const rot = Number(oldState >> 59n);

        return ((xorshifted >>> rot) | (xorshifted << (-rot & 31))) >>> 0;
    };

    public nextUint64 = (): UInt64 => {
        const lo = BigInt(this.nextUint32());
        const hi = BigInt(this.nextUint32());
        return ((hi << 32n) | lo) & UINT64_MAX;
    };

    public jumpAhead = (steps: UInt64 = 1n): void => {
        if (steps <= 0n) return;

        if (steps < 16n) {
            for (let i = 0n; i < steps; i++) {
                this.nextUint32();
            }
            return;
        }

        const oldState = this.state;
        let curMult = 6364136223846793005n;
        let curPlus = this.inc;

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

        this.state = (accMult * oldState + accPlus) & UINT64_MAX;
        this.counter += steps;
    };

    public getState = (): IRandomState => {
        return {
            vector: [this.state, this.inc, 0n, 0n],
            counter: this.counter,
            engine: this.engineType,
        };
    };

    public setState = (state: IRandomState): void => {
        this.state = state.vector[0];
        this.inc = state.vector[1];
        this.counter = state.counter;
    };

    public clone = (): IRandomEngine => {
        const copy = new PCGEngine();
        copy.state = this.state;
        copy.inc = this.inc;
        copy.counter = this.counter;
        return copy;
    };

    private warmup = (): void => {
        for (let i = 0; i < 16; i++) {
            this.nextUint32();
        }
        this.counter = 0n;
    };
}

class SplitMix64Engine implements IRandomEngine {
    private state: UInt64;
    private counter: UInt64;
    private readonly engineType = RandomEngineType.SPLITMIX64;

    constructor(seed: SeedSource = null) {
        const seedState = hashSeedToState(seed);
        this.state = seedState.vector[0];
        this.counter = seedState.counter;
        this.warmup();
    }

    public next01 = (): Float64 => {
        return Number(this.nextUint64() >> 11n) * (1.0 / 9007199254740992.0);
    };

    public nextUint32 = (): UInt32 => {
        return Number(this.nextUint64() & 0xffffffffn) >>> 0;
    };

    public nextUint64 = (): UInt64 => {
        this.counter++;
        this.state = (this.state + 0x9e3779b97f4a7c15n) & UINT64_MAX;
        let z = this.state;
        z = ((z ^ (z >> 30n)) * 0xbf58476d1ce4e5b9n) & UINT64_MAX;
        z = ((z ^ (z >> 27n)) * 0x94d049bb133111ebn) & UINT64_MAX;
        return z ^ (z >> 31n);
    };

    public jumpAhead = (steps: UInt64 = 1n): void => {
        if (steps <= 0n) return;

        this.state = (this.state + steps * 0x9e3779b97f4a7c15n) & UINT64_MAX;
        this.counter += steps;
    };

    public getState = (): IRandomState => {
        return {
            vector: [this.state, 0n, 0n, 0n],
            counter: this.counter,
            engine: this.engineType,
        };
    };

    public setState = (state: IRandomState): void => {
        this.state = state.vector[0];
        this.counter = state.counter;
    };

    public clone = (): IRandomEngine => {
        const copy = new SplitMix64Engine();
        copy.state = this.state;
        copy.counter = this.counter;
        return copy;
    };

    private warmup = (): void => {
        for (let i = 0; i < 8; i++) {
            this.nextUint64();
        }
        this.counter = 0n;
    };
}

class CryptoEngine implements IRandomEngine {
    private s0: UInt64;
    private s1: UInt64;
    private s2: UInt64;
    private s3: UInt64;
    private counter: UInt64;
    private readonly engineType = RandomEngineType.CRYPTO;
    private readonly buffer: Uint8Array;
    private bufferPosition: number;
    private readonly bufferSize = 1024;

    constructor() {
        this.buffer = new Uint8Array(this.bufferSize);
        this.bufferPosition = this.bufferSize;

        this.s0 = 0n;
        this.s1 = 0n;
        this.s2 = 0n;
        this.s3 = 0n;
        this.counter = 0n;

        this.refillBuffer();
    }

    public next01 = (): Float64 => {
        return this.nextUint32() * INV_UINT32_MAX;
    };

    public nextUint32 = (): UInt32 => {
        this.counter++;

        if (this.bufferPosition + 4 > this.bufferSize) {
            this.refillBuffer();
        }

        const value = new DataView(this.buffer.buffer).getUint32(this.bufferPosition, true);
        this.bufferPosition += 4;

        return value >>> 0;
    };

    public nextUint64 = (): UInt64 => {
        this.counter++;

        if (this.bufferPosition + 8 > this.bufferSize) {
            this.refillBuffer();
        }

        const view = new DataView(this.buffer.buffer);
        const lo = BigInt(view.getUint32(this.bufferPosition, true));
        const hi = BigInt(view.getUint32(this.bufferPosition + 4, true));
        this.bufferPosition += 8;

        return ((hi << 32n) | lo) & UINT64_MAX;
    };

    public jumpAhead = (steps: UInt64 = 1n): void => {
        if (steps <= 0n) return;

        if (steps < 16n) {
            for (let i = 0n; i < steps; i++) {
                this.nextUint32();
            }
            return;
        }

        this.counter += steps;

        this.bufferPosition = this.bufferSize;
    };

    public getState = (): IRandomState => {
        return {
            vector: [
                BigInt(
                    '0x' +
                        Array.from(this.buffer.slice(0, 8))
                            .map((b) => hex[b])
                            .join('')
                ),
                BigInt(
                    '0x' +
                        Array.from(this.buffer.slice(8, 16))
                            .map((b) => hex[b])
                            .join('')
                ),
                BigInt(
                    '0x' +
                        Array.from(this.buffer.slice(16, 24))
                            .map((b) => hex[b])
                            .join('')
                ),
                BigInt(
                    '0x' +
                        Array.from(this.buffer.slice(24, 32))
                            .map((b) => hex[b])
                            .join('')
                ),
            ],
            counter: this.counter,
            engine: this.engineType,
        };
    };

    public setState = (state: IRandomState): void => {
        this.counter = state.counter;
        this.refillBuffer();
    };

    public clone = (): IRandomEngine => {
        const copy = new CryptoEngine();
        copy.counter = this.counter;
        return copy;
    };

    private refillBuffer = (): void => {
        if (typeof crypto !== 'undefined' && crypto.getRandomValues) {
            crypto.getRandomValues(this.buffer);
        } else {
            const seedArray = new BigInt64Array(4);
            seedArray[0] = BigInt(Date.now()) + this.counter;
            seedArray[1] = 1n + this.counter * 2n;
            seedArray[2] = 3n + this.counter * 4n;
            seedArray[3] = 5n + this.counter * 8n;

            const fallbackEngine = new Xoshiro256PlusPlus(seedArray);

            for (let i = 0; i < this.buffer.length; i += 8) {
                const value = fallbackEngine.nextUint64();

                for (let j = 0; j < 8 && i + j < this.buffer.length; j++) {
                    this.buffer[i + j] = Number((value >> BigInt(j * 8)) & 0xffn);
                }
            }
        }

        this.bufferPosition = 0;
    };
}

class ChaCha20Engine implements IRandomEngine {
    private state = new Uint32Array(16);
    private buffer = new Uint32Array(16);
    private index = 16;
    private counter: UInt64;
    private readonly engineType = RandomEngineType.CHACHA20;

    constructor(seed: SeedSource = null) {
        this.counter = 0n;
        this.initializeState(seed);
    }

    public next01 = (): Float64 => {
        return this.nextUint32() * INV_UINT32_MAX;
    };

    public nextUint32 = (): UInt32 => {
        this.counter++;

        if (this.index >= 16) {
            this.generateBlock();
            this.index = 0;
        }

        return this.buffer[this.index++] >>> 0;
    };

    public nextUint64 = (): UInt64 => {
        const lo = BigInt(this.nextUint32());
        const hi = BigInt(this.nextUint32());
        return ((hi << 32n) | lo) & UINT64_MAX;
    };

    public jumpAhead = (steps: UInt64 = 1n): void => {
        if (steps <= 0n) return;

        if (steps < 16n) {
            for (let i = 0n; i < steps; i++) {
                this.nextUint32();
            }
            return;
        }

        const remainingInCurrentBlock = BigInt(16 - this.index);
        const stepsAfterCurrentBlock =
            steps > remainingInCurrentBlock ? steps - remainingInCurrentBlock : 0n;
        const fullBlocksToSkip = stepsAfterCurrentBlock >> 4n;
        const remainingSteps = stepsAfterCurrentBlock & 0xfn;

        if (fullBlocksToSkip > 0n) {
            const currentBlockCount = this.state[12] + (this.state[13] << 32);
            const newBlockCount = BigInt(currentBlockCount) + fullBlocksToSkip;

            this.state[12] = Number(newBlockCount & 0xffffffffn);
            this.state[13] = Number((newBlockCount >> 32n) & 0xffffffffn);

            this.index = 16;
        }

        for (let i = 0n; i < remainingSteps; i++) {
            this.nextUint32();
        }

        this.counter += steps;
    };

    public getState = (): IRandomState => {
        return {
            vector: [
                BigInt(this.state[0]) | (BigInt(this.state[1]) << 32n),
                BigInt(this.state[2]) | (BigInt(this.state[3]) << 32n),
                BigInt(this.state[4]) | (BigInt(this.state[5]) << 32n),
                BigInt(this.state[6]) | (BigInt(this.state[7]) << 32n),
            ],
            counter: this.counter,
            engine: this.engineType,
        };
    };

    public setState = (state: IRandomState): void => {
        this.state[0] = Number(state.vector[0] & 0xffffffffn);
        this.state[1] = Number((state.vector[0] >> 32n) & 0xffffffffn);
        this.state[2] = Number(state.vector[1] & 0xffffffffn);
        this.state[3] = Number((state.vector[1] >> 32n) & 0xffffffffn);
        this.state[4] = Number(state.vector[2] & 0xffffffffn);
        this.state[5] = Number((state.vector[2] >> 32n) & 0xffffffffn);
        this.state[6] = Number(state.vector[3] & 0xffffffffn);
        this.state[7] = Number((state.vector[3] >> 32n) & 0xffffffffn);

        this.counter = state.counter;
        this.index = 16;
    };

    public clone = (): IRandomEngine => {
        const copy = new ChaCha20Engine();

        for (let i = 0; i < 16; i++) {
            copy.state[i] = this.state[i];
        }

        for (let i = 0; i < 16; i++) {
            copy.buffer[i] = this.buffer[i];
        }

        copy.index = this.index;
        copy.counter = this.counter;

        return copy;
    };

    private initializeState = (seed: SeedSource): void => {
        this.state[0] = 0x61707865;
        this.state[1] = 0x3320646e;
        this.state[2] = 0x79622d32;
        this.state[3] = 0x6b206574;

        if (seed === null) {
            if (typeof crypto !== 'undefined' && crypto.getRandomValues) {
                const randomBytes = new Uint8Array(32);
                crypto.getRandomValues(randomBytes);

                // Key: 8 words (32 bytes)
                for (let i = 0; i < 8; i++) {
                    const idx = i * 4;
                    this.state[4 + i] =
                        ((randomBytes[idx] << 0) |
                            (randomBytes[idx + 1] << 8) |
                            (randomBytes[idx + 2] << 16) |
                            (randomBytes[idx + 3] << 24)) >>>
                        0;
                }

                // Nonce and counter: 4 words (16 bytes)
                for (let i = 0; i < 4; i++) {
                    const idx = 32 + i * 4;
                    this.state[12 + i] =
                        ((randomBytes[idx % randomBytes.length] << 0) |
                            (randomBytes[(idx + 1) % randomBytes.length] << 8) |
                            (randomBytes[(idx + 2) % randomBytes.length] << 16) |
                            (randomBytes[(idx + 3) % randomBytes.length] << 24)) >>>
                        0;
                }
            } else {
                // Fallback for environments without crypto
                const now = Date.now();
                this.state[4] = now & 0xffffffff;
                this.state[5] = (now >> 16) & 0xffffffff;
                this.state[6] = 0x6a09e667;
                this.state[7] = 0xbb67ae85;
                this.state[8] = 0x3c6ef372;
                this.state[9] = 0xa54ff53a;
                this.state[10] = 0x510e527f;
                this.state[11] = 0x9b05688c;
                this.state[12] = 0; // Counter lo
                this.state[13] = 0; // Counter hi
                this.state[14] = 0x1f83d9ab; // Nonce
                this.state[15] = 0x5be0cd19; // Nonce
            }
        } else {
            const seedState = hashSeedToState(seed);

            // Key: 8 words (32 bytes)
            this.state[4] = Number(seedState.vector[0] & 0xffffffffn);
            this.state[5] = Number((seedState.vector[0] >> 32n) & 0xffffffffn);
            this.state[6] = Number(seedState.vector[1] & 0xffffffffn);
            this.state[7] = Number((seedState.vector[1] >> 32n) & 0xffffffffn);
            this.state[8] = Number(seedState.vector[2] & 0xffffffffn);
            this.state[9] = Number((seedState.vector[2] >> 32n) & 0xffffffffn);
            this.state[10] = Number(seedState.vector[3] & 0xffffffffn);
            this.state[11] = Number((seedState.vector[3] >> 32n) & 0xffffffffn);

            // Counter and nonce: 4 words (16 bytes)
            this.state[12] = 0; // Counter lo
            this.state[13] = 0; // Counter hi
            this.state[14] = Number((seedState.vector[0] ^ seedState.vector[2]) & 0xffffffffn); // Nonce
            this.state[15] = Number((seedState.vector[1] ^ seedState.vector[3]) & 0xffffffffn); // Nonce
        }
    };

    private generateBlock = (): void => {
        for (let i = 0; i < 16; i++) {
            this.buffer[i] = this.state[i];
        }

        // Apply ChaCha20 rounds
        for (let i = 0; i < 10; i++) {
            this.quarterRound(0, 4, 8, 12);
            this.quarterRound(1, 5, 9, 13);
            this.quarterRound(2, 6, 10, 14);
            this.quarterRound(3, 7, 11, 15);
            this.quarterRound(0, 5, 10, 15);
            this.quarterRound(1, 6, 11, 12);
            this.quarterRound(2, 7, 8, 13);
            this.quarterRound(3, 4, 9, 14);
        }

        for (let i = 0; i < 16; i++) {
            this.buffer[i] = (this.buffer[i] + this.state[i]) >>> 0;
        }

        const carry = (this.state[12] + 1) >>> 0 < this.state[12] ? 1 : 0;
        this.state[12] = (this.state[12] + 1) >>> 0;
        this.state[13] = (this.state[13] + carry) >>> 0;
    };

    private quarterRound = (a: number, b: number, c: number, d: number): void => {
        this.buffer[a] = (this.buffer[a] + this.buffer[b]) >>> 0;
        this.buffer[d] = this.rotl32(this.buffer[d] ^ this.buffer[a], 16);

        this.buffer[c] = (this.buffer[c] + this.buffer[d]) >>> 0;
        this.buffer[b] = this.rotl32(this.buffer[b] ^ this.buffer[c], 12);

        this.buffer[a] = (this.buffer[a] + this.buffer[b]) >>> 0;
        this.buffer[d] = this.rotl32(this.buffer[d] ^ this.buffer[a], 8);

        this.buffer[c] = (this.buffer[c] + this.buffer[d]) >>> 0;
        this.buffer[b] = this.rotl32(this.buffer[b] ^ this.buffer[c], 7);
    };

    private rotl32 = (x: number, n: number): number => {
        return ((x << n) | (x >>> (32 - n))) >>> 0;
    };
}

export const createEngineFactory = (
    engineType: RandomEngineType
): ((seed?: SeedSource) => IRandomEngine) => {
    switch (engineType) {
        case RandomEngineType.XOROSHIRO128_PLUS_PLUS:
            return (seed?: SeedSource) => new Xoroshiro128PlusPlus(seed);
        case RandomEngineType.PCG_XSH_RR:
            return (seed?: SeedSource) => new PCGEngine(seed);
        case RandomEngineType.XOSHIRO256_PLUS_PLUS:
            return (seed?: SeedSource) => new Xoshiro256PlusPlus(seed);
        case RandomEngineType.SPLITMIX64:
            return (seed?: SeedSource) => new SplitMix64Engine(seed);
        case RandomEngineType.CHACHA20:
            return (seed?: SeedSource) => new ChaCha20Engine(seed);
        case RandomEngineType.CRYPTO:
            return () => new CryptoEngine();
        default:
            return (seed?: SeedSource) => new Xoroshiro128PlusPlus(seed);
    }
};

export class UniformDistribution implements IDistribution<number> {
    constructor(
        private readonly min: number = 0,
        private readonly max: number = 1
    ) {
        if (!Number.isFinite(min) || !Number.isFinite(max)) {
            throw new RangeError('Bounds must be finite numbers');
        }

        if (min > max) {
            throw new RangeError('Min must be less than or equal to max');
        }
    }

    public sample = (state: IRandomState): RandomResult<number> => {
        const engine = createEngineFactory(state.engine)();
        engine.setState(state);

        const value = this.min + (this.max - this.min) * engine.next01();

        return [value, engine.getState()];
    };
}

export class IntegerDistribution implements IDistribution<number> {
    constructor(
        private readonly min: number,
        private readonly max: number
    ) {
        validateInteger(min, 'min');
        validateInteger(max, 'max');

        if (min > max) {
            throw new RangeError('Min must be less than or equal to max');
        }
    }

    public sample = (state: IRandomState): RandomResult<number> => {
        const engine = createEngineFactory(state.engine)();
        engine.setState(state);

        let value: number;
        const range = this.max - this.min + 1;

        if (range <= 0) {
            throw new RangeError('Range is too large and would cause integer overflow');
        }

        if (range <= 0x100000000) {
            value = this.min + Math.floor(range * engine.next01());
        } else {
            const limit = Math.floor(0x100000000 * Math.floor(range / 0x100000000));
            let x: number;

            do {
                x = engine.nextUint32() * 0x100000000 + engine.nextUint32();
            } while (x >= limit);

            value = this.min + (x % range);
        }

        return [value, engine.getState()];
    };
}

export class NormalDistribution implements IDistribution<number> {
    constructor(
        private readonly mean: number = 0,
        private readonly stdDev: number = 1
    ) {
        if (!Number.isFinite(mean) || !Number.isFinite(stdDev)) {
            throw new RangeError('Parameters must be finite numbers');
        }

        if (stdDev <= 0) {
            throw new RangeError('Standard deviation must be positive');
        }
    }

    public sample = (state: IRandomState): RandomResult<number> => {
        const engine = createEngineFactory(state.engine)();
        engine.setState(state);

        // Box-Muller transform
        // TODO: apply our box muller library here
        const u1 = engine.next01();
        const u2 = engine.next01();

        const r = Math.sqrt(-2.0 * Math.log(Math.max(u1, Number.EPSILON)));
        const theta = TWO_PI * u2;

        const standardNormal = r * Math.cos(theta);

        return [this.mean + this.stdDev * standardNormal, engine.getState()];
    };
}

export class ExponentialDistribution implements IDistribution<number> {
    constructor(private readonly lambda: number = 1) {
        validatePositive(lambda, 'lambda');
    }

    public sample = (state: IRandomState): RandomResult<number> => {
        const engine = createEngineFactory(state.engine)();
        engine.setState(state);
        const u = engine.next01();
        const value = -Math.log(1 - u) / this.lambda;

        return [value, engine.getState()];
    };
}

export class PoissonDistribution implements IDistribution<number> {
    constructor(private readonly lambda: number) {
        validatePositive(lambda, 'lambda');
    }

    public sample = (state: IRandomState): RandomResult<number> => {
        const engine = createEngineFactory(state.engine)();
        engine.setState(state);

        if (this.lambda < 10) {
            // For small lambda, use Knuth's algorithm
            const L = Math.exp(-this.lambda);
            let k = 0;
            let p = 1;

            do {
                k++;
                p *= engine.next01();
            } while (p > L);

            return [k - 1, engine.getState()];
        } else {
            // For larger lambda, use the "rejection method" algorithm
            const c = 0.767 - 3.36 / this.lambda;
            const beta = PI / Math.sqrt(3.0 * this.lambda);
            const alpha = beta * this.lambda;
            const k = Math.log(c) - this.lambda - Math.log(beta);

            while (true) {
                const u = engine.next01();
                const x = (alpha - Math.log((1.0 - u) / u)) / beta;
                const n = Math.floor(x + 0.5);

                if (n < 0) continue;

                const v = engine.next01();
                const y = alpha - beta * x;
                const lhs = y + Math.log(v / Math.pow(1.0 + Math.exp(y), 2));
                const rhs = k + n * Math.log(this.lambda) - Math.log(factorial(n));

                if (lhs <= rhs) {
                    return [n, engine.getState()];
                }
            }
        }
    };
}

export class BernoulliDistribution implements IDistribution<boolean> {
    constructor(private readonly p: number = 0.5) {
        validateProbability(p, 'p');
    }

    public sample = (state: IRandomState): RandomResult<boolean> => {
        const engine = createEngineFactory(state.engine)();
        engine.setState(state);

        const value = engine.next01() < this.p;

        return [value, engine.getState()];
    };
}

export class BinomialDistribution implements IDistribution<number> {
    constructor(
        private readonly n: number,
        private readonly p: number
    ) {
        validateNonNegative(n, 'n');
        validateInteger(n, 'n');
        validateProbability(p, 'p');
    }

    public sample = (state: IRandomState): RandomResult<number> => {
        const engine = createEngineFactory(state.engine)();
        engine.setState(state);

        if (this.n === 0 || this.p === 0) return [0, engine.getState()];
        if (this.p === 1) return [this.n, engine.getState()];

        if (this.n < 100) {
            let successes = 0;

            for (let i = 0; i < this.n; i++) {
                if (engine.next01() < this.p) {
                    successes++;
                }
            }

            return [successes, engine.getState()];
        }

        const mean = this.n * this.p;
        const stdDev = Math.sqrt(this.n * this.p * (1 - this.p));

        const normalSample = new NormalDistribution(mean, stdDev).sample(engine.getState());
        engine.setState(normalSample[1]);

        const value = Math.max(0, Math.min(this.n, Math.round(normalSample[0])));

        return [value, engine.getState()];
    };
}

export class GeometricDistribution implements IDistribution<number> {
    constructor(private readonly p: number) {
        validateProbability(p, 'p');

        if (p === 0) {
            throw new RangeError('p must be greater than 0');
        }
    }

    public sample = (state: IRandomState): RandomResult<number> => {
        const engine = createEngineFactory(state.engine)();
        engine.setState(state);

        const u = engine.next01();

        const value = Math.floor(Math.log1p(-u) / Math.log1p(-this.p));

        return [value, engine.getState()];
    };
}

// utility functions
const factorial = (() => {
    const cache = new Map<number, number>();

    return (n: number): number => {
        if (n < 0) {
            throw new RangeError('Factorial not defined for negative numbers');
        }

        if (n < 2) return 1;

        if (cache.has(n)) {
            return cache.get(n)!;
        }

        if (n > 170) {
            return Infinity;
        }

        let result = n;
        for (let i = n - 1; i > 1; i--) {
            result *= i;
        }

        cache.set(n, result);
        return result;
    };
})();

// ---

export interface IRandomSequence<T> {
    readonly next: () => T;
    readonly take: (count: number) => T[];
    readonly skip: (count: number) => void;
    readonly map: <U>(fn: (value: T) => U) => IRandomSequence<U>;
    readonly filter: (predicate: (value: T) => boolean) => IRandomSequence<T>;
}

export interface IRandomAPI {
    readonly float: () => number;
    readonly floatBetween: (min: number, max: number) => number;
    readonly int: (min: number, max: number) => number;
    readonly boolean: (probability?: number) => boolean;
    readonly pick: <T>(array: ReadonlyArray<T>) => T;
    readonly weighted: <T>(items: ReadonlyArray<[T, number]>) => T;
    readonly shuffle: <T>(array: ReadonlyArray<T>) => T[];
    readonly sample: <T>(array: ReadonlyArray<T>, count: number) => T[];
    readonly uuid: () => string;
    readonly bytes: (length: number) => Uint8Array;
    readonly string: (length: number, charset?: string) => string;
    readonly sequence: <T>(generator: () => T) => IRandomSequence<T>;
    readonly normal: (mean?: number, stdDev?: number) => number;
    readonly exponential: (lambda?: number) => number;
    readonly poisson: (lambda: number) => number;
    readonly bernoulli: (p?: number) => boolean;
    readonly binomial: (n: number, p: number) => number;
    readonly geometric: (p: number) => number;
    readonly distribution: <T>(distribution: IDistribution<T>) => T;
    readonly setSeed: (seed: SeedSource) => void;
    readonly getEngine: () => IRandomEngine;
    readonly setEngine: (engineType: RandomEngineType) => void;
    readonly getState: () => IRandomState;
    readonly setState: (state: IRandomState) => void;
    readonly fork: () => IRandomAPI;
}

class RandomSequence<T> implements IRandomSequence<T> {
    constructor(
        private readonly generator: () => T,
        private readonly random: Random
    ) {}

    public next = (): T => {
        return this.generator();
    };

    public take = (count: number): T[] => {
        validateNonNegative(count, 'count');
        validateInteger(count, 'count');

        const result: T[] = [];
        for (let i = 0; i < count; i++) {
            result.push(this.generator());
        }
        return result;
    };

    public skip = (count: number): void => {
        validateNonNegative(count, 'count');
        validateInteger(count, 'count');

        for (let i = 0; i < count; i++) {
            this.generator();
        }
    };

    public map = <U>(fn: (value: T) => U): IRandomSequence<U> => {
        return new RandomSequence<U>(() => fn(this.generator()), this.random);
    };

    public filter = (predicate: (value: T) => boolean): IRandomSequence<T> => {
        return new RandomSequence<T>(() => {
            let value: T;
            do {
                value = this.generator();
            } while (!predicate(value));
            return value;
        }, this.random);
    };
}

class Random implements IRandomAPI {
    // ...
}
