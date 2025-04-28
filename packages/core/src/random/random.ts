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

