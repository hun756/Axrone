import type { Hash32, Hash64, HashValue } from './types';
import { asHash32 } from './types';
import { fmix32 } from './mixers';

const FNV_PRIME_32 = 0x01000193;
const FNV_OFFSET_32 = 0x811c9dc5;

export function hashCombine(a: HashValue, b: HashValue): Hash32 {
    let h: number;
    if (typeof a === 'number') h = a as unknown as number;
    else h = Number((a as bigint & 0xffffffffn) ^ ((a as bigint >> 32n) & 0xffffffffn));
    h = (h ^ (b as unknown as number & 0xff)) >>> 0;
    h = Math.imul(h, FNV_PRIME_32) >>> 0;
    return fmix32(h) as unknown as Hash32;
}

export function hashCombineOrdered(values: readonly HashValue[]): Hash32 {
    let h = FNV_OFFSET_32;
    for (const v of values) {
        if (typeof v === 'number') {
            const num = v as unknown as number;
            h = Math.imul(h ^ (num & 0xff), FNV_PRIME_32) >>> 0;
            h = Math.imul(h ^ ((num >>> 8) & 0xff), FNV_PRIME_32) >>> 0;
            h = Math.imul(h ^ ((num >>> 16) & 0xff), FNV_PRIME_32) >>> 0;
            h = Math.imul(h ^ ((num >>> 24) & 0xff), FNV_PRIME_32) >>> 0;
        } else {
            let big = v as bigint;
            for (let i = 0; i < 8; i++) {
                h = Math.imul(h ^ Number(big & 0xffn), FNV_PRIME_32) >>> 0;
                big >>= 8n;
            }
        }
    }
    return fmix32(h) as unknown as Hash32;
}

export function hashCombineStrings(seed: number, ...strings: readonly string[]): Hash32 {
    let h = seed ^ FNV_OFFSET_32;
    for (const s of strings) {
        for (let i = 0; i < s.length; i++) {
            const c = s.charCodeAt(i);
            h = Math.imul(h ^ (c & 0xff), FNV_PRIME_32) >>> 0;
            h = Math.imul(h ^ ((c >>> 8) & 0xff), FNV_PRIME_32) >>> 0;
        }
        h = Math.imul(h, FNV_PRIME_32) >>> 0;
    }
    return fmix32(h) as unknown as Hash32;
}

export function hashCombineBooleans(seed: number, ...values: readonly boolean[]): Hash32 {
    let h = seed ^ FNV_OFFSET_32;
    for (const v of values) {
        h = Math.imul(h ^ (v ? 1 : 0), FNV_PRIME_32) >>> 0;
    }
    return fmix32(h) as unknown as Hash32;
}

export function hashCombineNumbers(seed: number, ...values: readonly number[]): Hash32 {
    let h = seed ^ FNV_OFFSET_32;
    for (const v of values) {
        const buf = new Float64Array(1);
        const ibuf = new Int32Array(buf.buffer);
        buf[0] = v;
        h = Math.imul(h ^ (ibuf[0]! & 0xff), FNV_PRIME_32) >>> 0;
        h = Math.imul(h ^ ((ibuf[0]! >>> 8) & 0xff), FNV_PRIME_32) >>> 0;
        h = Math.imul(h ^ ((ibuf[0]! >>> 16) & 0xff), FNV_PRIME_32) >>> 0;
        h = Math.imul(h ^ ((ibuf[0]! >>> 24) & 0xff), FNV_PRIME_32) >>> 0;
        h = Math.imul(h ^ (ibuf[1]! & 0xff), FNV_PRIME_32) >>> 0;
        h = Math.imul(h ^ ((ibuf[1]! >>> 8) & 0xff), FNV_PRIME_32) >>> 0;
        h = Math.imul(h ^ ((ibuf[1]! >>> 16) & 0xff), FNV_PRIME_32) >>> 0;
        h = Math.imul(h ^ ((ibuf[1]! >>> 24) & 0xff), FNV_PRIME_32) >>> 0;
    }
    return fmix32(h) as unknown as Hash32;
}

export const HashCombine = {
    combine: hashCombine,
    combineOrdered: hashCombineOrdered,
    combineStrings: hashCombineStrings,
    combineBooleans: hashCombineBooleans,
    combineNumbers: hashCombineNumbers,
} as const;
