/**
 * Shared hex conversion utilities.
 *
 * Every algorithm's `digestHex()` and the serialisation helpers were
 * re-implementing the same nibble loop.  These three functions cover
 * all cases:
 *
 *  - `u32ToHex`    — 32-bit number → 8 hex chars
 *  - `bigIntToHex`  — arbitrary-precision bigint → fixed-width hex
 *  - `bytesToHex`   — Uint8Array → hex string (used by SHA / WebCrypto)
 */

const HEX_CHARS = '0123456789abcdef';

/**
 * Convert an unsigned 32-bit integer to an 8-character lowercase hex string.
 * Pass `uppercase = true` for A-F.
 */
export function u32ToHex(value: number, uppercase: boolean = false): string {
    let n = value >>> 0;
    let out = '';
    for (let i = 0; i < 8; i++) {
        out = HEX_CHARS[n & 0xf] + out;
        n >>>= 4;
    }
    return uppercase ? out.toUpperCase() : out;
}

/**
 * Convert a bigint to a fixed-width lowercase hex string.
 *
 * `nibbles` is the total number of hex characters in the output
 * (e.g. 16 for 64-bit, 32 for 128-bit, 64 for 256-bit, 128 for 512-bit).
 */
export function bigIntToHex(value: bigint, nibbles: number, uppercase: boolean = false): string {
    let n = value;
    let out = '';
    for (let i = 0; i < nibbles; i++) {
        out = HEX_CHARS[Number(n & 0xfn)] + out;
        n >>= 4n;
    }
    return uppercase ? out.toUpperCase() : out;
}

/**
 * Convert a byte array to a lowercase hex string.
 * Used by SHA / WebCrypto digests that produce Uint8Array results.
 */
export function bytesToHex(bytes: Uint8Array, uppercase: boolean = false): string {
    let s = '';
    for (let i = 0; i < bytes.length; i++) {
        const b = bytes[i]!;
        s += HEX_CHARS[(b >>> 4) & 0xf] + HEX_CHARS[b & 0xf];
    }
    return uppercase ? s.toUpperCase() : s;
}
