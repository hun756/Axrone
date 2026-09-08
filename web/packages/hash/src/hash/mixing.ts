import type { BytesLike } from '../types';

/**
 * Shared FNV-1a mixing primitives.
 *
 * Centralises the FNV prime/offset constants and the core
 * multiply-and-xor step so that every algorithm that needs FNV-1a
 * byte mixing (Fnv1a32, StructState, hashCombine*, …) imports from
 * a single source of truth.
 */

export const FNV_PRIME_32 = 0x01000193;
export const FNV_OFFSET_32 = 0x811c9dc5;

/**
 * Mix a single byte into a 32-bit FNV-1a hash state.
 *
 * Equivalent to: `Math.imul(h ^ byte, FNV_PRIME_32) >>> 0`
 */
export function fnv1aMix32(h: number, byte: number): number {
    return Math.imul(h ^ (byte & 0xff), FNV_PRIME_32) >>> 0;
}

/**
 * Mix a full 32-bit value (4 bytes, little-endian order) into an FNV-1a state.
 */
export function fnv1aMixU32(h: number, value: number): number {
    h = fnv1aMix32(h, value & 0xff);
    h = fnv1aMix32(h, (value >>> 8) & 0xff);
    h = fnv1aMix32(h, (value >>> 16) & 0xff);
    h = fnv1aMix32(h, (value >>> 24) & 0xff);
    return h;
}

/**
 * Mix `length` bytes from `bytes` starting at `offset` into an FNV-1a state.
 */
export function fnv1aMixBytes32(h: number, bytes: BytesLike, offset: number, length: number): number {
    const end = offset + length;
    for (let i = offset; i < end; i++) {
        h = fnv1aMix32(h, bytes[i]!);
    }
    return h;
}
