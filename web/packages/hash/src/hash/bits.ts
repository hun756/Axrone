import type { BytesLike } from '../../types';

const F32_BUF = new Float32Array(1);
const I32_BUF = new Int32Array(F32_BUF.buffer);

const F64_BUF = new Float64Array(1);
const I64_BUF = new Int32Array(F64_BUF.buffer);

export function float32ToBits(v: number): number {
    F32_BUF[0] = v;
    return I32_BUF[0]!;
}

export function bitsToFloat32(v: number): number {
    I32_BUF[0] = v;
    return F32_BUF[0]!;
}

export function float64ToBitsPair(v: number): [number, number] {
    F64_BUF[0] = v;
    return [I64_BUF[0]!, I64_BUF[1]!];
}

export function bitsPairToFloat64(lo: number, hi: number): number {
    I64_BUF[0] = lo;
    I64_BUF[1] = hi;
    return F64_BUF[0]!;
}

export function rotl32(x: number, r: number): number {
    return ((x << r) | (x >>> (32 - r))) >>> 0;
}

export function rotr32(x: number, r: number): number {
    return ((x >>> r) | (x << (32 - r))) >>> 0;
}

export function rotl64(x: bigint, r: bigint): bigint {
    const rMask = r & 63n;
    return ((x << rMask) | (x >> (64n - rMask))) & 0xffffffffffffffffn;
}

export function rotr64(x: bigint, r: bigint): bigint {
    const rMask = r & 63n;
    return ((x >> rMask) | (x << (64n - rMask))) & 0xffffffffffffffffn;
}

export function readU32LE(bytes: BytesLike, offset: number): number {
    return (
        ((bytes[offset]! & 0xff) |
            ((bytes[offset + 1]! & 0xff) << 8) |
            ((bytes[offset + 2]! & 0xff) << 16) |
            ((bytes[offset + 3]! & 0xff) << 24)) >>>
        0
    );
}

export function readU32BE(bytes: BytesLike, offset: number): number {
    return (
        (((bytes[offset]! & 0xff) << 24) |
            ((bytes[offset + 1]! & 0xff) << 16) |
            ((bytes[offset + 2]! & 0xff) << 8) |
            (bytes[offset + 3]! & 0xff)) >>>
            0
    );
}

export function writeU32LE(value: number, out: Uint8Array, offset: number): void {
    out[offset] = value & 0xff;
    out[offset + 1] = (value >>> 8) & 0xff;
    out[offset + 2] = (value >>> 16) & 0xff;
    out[offset + 3] = (value >>> 24) & 0xff;
}

export function writeU32BE(value: number, out: Uint8Array, offset: number): void {
    out[offset] = (value >>> 24) & 0xff;
    out[offset + 1] = (value >>> 16) & 0xff;
    out[offset + 2] = (value >>> 8) & 0xff;
    out[offset + 3] = value & 0xff;
}

export function readU64LE(bytes: BytesLike, offset: number): bigint {
    return (
        BigInt(readU32LE(bytes, offset)) |
        (BigInt(readU32LE(bytes, offset + 4)) << 32n)
    );
}

export function readU64BE(bytes: BytesLike, offset: number): bigint {
    return (
        (BigInt(readU32BE(bytes, offset)) << 32n) |
        BigInt(readU32BE(bytes, offset + 4))
    );
}

export function writeU64LE(value: bigint, out: Uint8Array, offset: number): void {
    writeU32LE(Number(value & 0xffffffffn), out, offset);
    writeU32LE(Number((value >> 32n) & 0xffffffffn), out, offset + 4);
}

export function writeU64BE(value: bigint, out: Uint8Array, offset: number): void {
    writeU32BE(Number((value >> 32n) & 0xffffffffn), out, offset);
    writeU32BE(Number(value & 0xffffffffn), out, offset + 4);
}

export function readU16LE(bytes: BytesLike, offset: number): number {
    return ((bytes[offset]! & 0xff) | ((bytes[offset + 1]! & 0xff) << 8)) & 0xffff;
}

export function readU16BE(bytes: BytesLike, offset: number): number {
    return (((bytes[offset]! & 0xff) << 8) | (bytes[offset + 1]! & 0xff)) & 0xffff;
}

export function writeU16LE(value: number, out: Uint8Array, offset: number): void {
    out[offset] = value & 0xff;
    out[offset + 1] = (value >>> 8) & 0xff;
}

export function writeU16BE(value: number, out: Uint8Array, offset: number): void {
    out[offset] = (value >>> 8) & 0xff;
    out[offset + 1] = value & 0xff;
}
