import type { Hash32, Hash64, HashValue } from './types';
import { asHash32 } from './types';
import { fmix32 } from './mixers';
import { fnv1aMix32, fnv1aMixU32, FNV_PRIME_32, FNV_OFFSET_32 } from './mixing';

export function hashCombine(a: HashValue, b: HashValue): Hash32 {
    let h: number;
    if (typeof a === 'number') h = a as unknown as number;
    else h = Number((a as bigint & 0xffffffffn) ^ ((a as bigint >> 32n) & 0xffffffffn));
    h = (h ^ (b as unknown as number)) >>> 0;
    h = Math.imul(h, FNV_PRIME_32) >>> 0;
    return fmix32(h) as unknown as Hash32;
}

export function hashCombineOrdered(values: readonly HashValue[]): Hash32 {
    let h = FNV_OFFSET_32;
    for (const v of values) {
        if (typeof v === 'number') {
            h = fnv1aMixU32(h, v as unknown as number);
        } else {
            let big = v as bigint;
            for (let i = 0; i < 8; i++) {
                h = fnv1aMix32(h, Number(big & 0xffn));
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
            h = fnv1aMix32(h, c & 0xff);
            h = fnv1aMix32(h, (c >>> 8) & 0xff);
        }
        h = Math.imul(h, FNV_PRIME_32) >>> 0;
    }
    return fmix32(h) as unknown as Hash32;
}

export function hashCombineBooleans(seed: number, ...values: readonly boolean[]): Hash32 {
    let h = seed ^ FNV_OFFSET_32;
    for (const v of values) {
        h = fnv1aMix32(h, v ? 1 : 0);
    }
    return fmix32(h) as unknown as Hash32;
}

const COMBINE_F64_BUF = new Float64Array(1);
const COMBINE_I32_BUF = new Int32Array(COMBINE_F64_BUF.buffer);

export function hashCombineNumbers(seed: number, ...values: readonly number[]): Hash32 {
    let h = seed ^ FNV_OFFSET_32;
    for (const v of values) {
        COMBINE_F64_BUF[0] = v;
        h = fnv1aMix32(h, COMBINE_I32_BUF[0]! & 0xff);
        h = fnv1aMix32(h, (COMBINE_I32_BUF[0]! >>> 8) & 0xff);
        h = fnv1aMix32(h, (COMBINE_I32_BUF[0]! >>> 16) & 0xff);
        h = fnv1aMix32(h, (COMBINE_I32_BUF[0]! >>> 24) & 0xff);
        h = fnv1aMix32(h, COMBINE_I32_BUF[1]! & 0xff);
        h = fnv1aMix32(h, (COMBINE_I32_BUF[1]! >>> 8) & 0xff);
        h = fnv1aMix32(h, (COMBINE_I32_BUF[1]! >>> 16) & 0xff);
        h = fnv1aMix32(h, (COMBINE_I32_BUF[1]! >>> 24) & 0xff);
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
