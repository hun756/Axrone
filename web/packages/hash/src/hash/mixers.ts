import { rotl32 } from './bits';

export function fmix32(h: number): number {
    h = (h ^ (h >>> 16)) >>> 0;
    h = Math.imul(h, 0x85ebca6b) >>> 0;
    h = (h ^ (h >>> 13)) >>> 0;
    h = Math.imul(h, 0xc2b2ae35) >>> 0;
    h = (h ^ (h >>> 16)) >>> 0;
    return h >>> 0;
}

export function fmix32Alt(h: number): number {
    h = Math.imul(h ^ (h >>> 16), 0x85ebca6b) >>> 0;
    h = Math.imul(h ^ (h >>> 13), 0xc2b2ae35) >>> 0;
    h = (h ^ (h >>> 16)) >>> 0;
    return h >>> 0;
}

export function avalanche32(h: number): number {
    h = (h ^ (h >>> 16)) >>> 0;
    h = Math.imul(h, 0x85ebca6b) >>> 0;
    h = (h ^ (h >>> 13)) >>> 0;
    h = Math.imul(h, 0xc2b2ae35) >>> 0;
    h = (h ^ (h >>> 16)) >>> 0;
    return h >>> 0;
}

export function splitmix32(state: number): number {
    state = (state + 0x9e3779b9) >>> 0;
    let z = state;
    z = ((z ^ (z >>> 16)) >>> 0);
    z = Math.imul(z, 0x85ebca6b) >>> 0;
    z = ((z ^ (z >>> 13)) >>> 0);
    z = Math.imul(z, 0xc2b2ae35) >>> 0;
    z = (z ^ (z >>> 16)) >>> 0;
    return z >>> 0;
}

export function splitmix32Step(state: number): number {
    return (state + 0x9e3779b9) >>> 0;
}

export function splitmix64(state: bigint): bigint {
    state = (state + 0x9e3779b97f4a7c15n) & 0xffffffffffffffffn;
    let z = state;
    z = ((z ^ (z >> 30n)) * 0xbf58476d1ce4e5b9n) & 0xffffffffffffffffn;
    z = ((z ^ (z >> 27n)) * 0x94d049bb133111ebn) & 0xffffffffffffffffn;
    z = (z ^ (z >> 31n)) & 0xffffffffffffffffn;
    return z;
}

export function splitmix64Step(state: bigint): bigint {
    return (state + 0x9e3779b97f4a7c15n) & 0xffffffffffffffffn;
}

export function murmur3Scramble(k1: number): number {
    k1 = Math.imul(k1, 0xcc9e2d51) >>> 0;
    k1 = rotl32(k1, 15);
    k1 = Math.imul(k1, 0x1b873593) >>> 0;
    return k1 >>> 0;
}

export function xorshift32(state: number): number {
    state = (state ^ (state << 13)) >>> 0;
    state = (state ^ (state >>> 17)) >>> 0;
    state = (state ^ (state << 5)) >>> 0;
    return state >>> 0;
}

export function xorshift64(state: bigint): bigint {
    state = (state ^ (state << 13n)) & 0xffffffffffffffffn;
    state = (state ^ (state >> 17n)) & 0xffffffffffffffffn;
    state = (state ^ (state << 5n)) & 0xffffffffffffffffn;
    return state;
}

export function xoshiro128Plus(s0: number, s1: number, s2: number, s3: number): [number, number, number, number] {
    const result = (Math.imul(s0, 5) >>> 0) << 7 | ((Math.imul(s0, 5) >>> 0) >>> 25);
    const t = (s1 << 9) >>> 0;

    s2 = (s2 ^ s0) >>> 0;
    s3 = (s3 ^ s1) >>> 0;
    s1 = (s1 ^ s2) >>> 0;
    s0 = (s0 ^ s3) >>> 0;
    s2 = (s2 ^ t) >>> 0;
    s3 = rotl32(s3, 11);
    return [s0, s1, s2, s3];
}
