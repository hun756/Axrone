import type { Hash32, Hash64, Hash128, Hash256, Hash512, HashValue } from './types';
import { asHash32, asHash64, asHash128 } from './types';
import { HashSerializationError, HashDeserializationError } from './errors';

const HEX_CHARS = '0123456789abcdef';
const HEX_REV: number[] = new Array(256).fill(-1);
for (let i = 0; i < 10; i++) HEX_REV[0x30 + i] = i;
for (let i = 0; i < 6; i++) {
    HEX_REV[0x41 + i] = i + 10;
    HEX_REV[0x61 + i] = i + 10;
}

export function hash32ToHex(value: Hash32, uppercase: boolean = false): string {
    let n = (value as number) >>> 0;
    let out = '';
    for (let i = 0; i < 8; i++) {
        out = HEX_CHARS[n & 0xf] + out;
        n >>>= 4;
    }
    return uppercase ? out.toUpperCase() : out;
}

export function hash64ToHex(value: Hash64, uppercase: boolean = false): string {
    let n = value as unknown as bigint;
    let out = '';
    for (let i = 0; i < 16; i++) {
        out = HEX_CHARS[Number(n & 0xfn)] + out;
        n >>= 4n;
    }
    return uppercase ? out.toUpperCase() : out;
}

export function hash128ToHex(value: Hash128, uppercase: boolean = false): string {
    let n = value as unknown as bigint;
    let out = '';
    for (let i = 0; i < 32; i++) {
        out = HEX_CHARS[Number(n & 0xfn)] + out;
        n >>= 4n;
    }
    return uppercase ? out.toUpperCase() : out;
}

export function hash256ToHex(value: Hash256, uppercase: boolean = false): string {
    let n = value as unknown as bigint;
    let out = '';
    for (let i = 0; i < 64; i++) {
        out = HEX_CHARS[Number(n & 0xfn)] + out;
        n >>= 4n;
    }
    return uppercase ? out.toUpperCase() : out;
}

export function hash512ToHex(value: Hash512, uppercase: boolean = false): string {
    let n = value as unknown as bigint;
    let out = '';
    for (let i = 0; i < 128; i++) {
        out = HEX_CHARS[Number(n & 0xfn)] + out;
        n >>= 4n;
    }
    return uppercase ? out.toUpperCase() : out;
}

export function hexToHash32(input: string): Hash32 {
    if (input.length !== 8) {
        throw new HashDeserializationError(`hexToHash32: input must be 8 hex chars, got ${input.length}`);
    }
    let h = 0;
    for (let i = 0; i < 8; i++) {
        const c = input.charCodeAt(i);
        const v = HEX_REV[c];
        if (v === undefined || v === -1) {
            throw new HashDeserializationError(`hexToHash32: invalid hex char "${input[i]}"`);
        }
        h = (h << 4) | v;
    }
    return asHash32(h);
}

export function hexToHash64(input: string): Hash64 {
    if (input.length !== 16) {
        throw new HashDeserializationError(`hexToHash64: input must be 16 hex chars, got ${input.length}`);
    }
    let h = 0n;
    for (let i = 0; i < 16; i++) {
        const c = input.charCodeAt(i);
        const v = HEX_REV[c];
        if (v === undefined || v === -1) {
            throw new HashDeserializationError(`hexToHash64: invalid hex char "${input[i]}"`);
        }
        h = (h << 4n) | BigInt(v);
    }
    return asHash64(h);
}

export function hexToHash128(input: string): Hash128 {
    if (input.length !== 32) {
        throw new HashDeserializationError(`hexToHash128: input must be 32 hex chars, got ${input.length}`);
    }
    let h = 0n;
    for (let i = 0; i < 32; i++) {
        const c = input.charCodeAt(i);
        const v = HEX_REV[c];
        if (v === undefined || v === -1) {
            throw new HashDeserializationError(`hexToHash128: invalid hex char "${input[i]}"`);
        }
        h = (h << 4n) | BigInt(v);
    }
    return asHash128(h);
}

const B64_CHARS = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';
const B64_REV: number[] = new Array(256).fill(-1);
for (let i = 0; i < B64_CHARS.length; i++) B64_REV[B64_CHARS.charCodeAt(i)] = i;

function _btoa(s: string): string {
    if (typeof btoa !== 'undefined') return btoa(s);
    return Buffer.from(s, 'binary').toString('base64');
}

function _atob(s: string): string {
    if (typeof atob !== 'undefined') return atob(s);
    return Buffer.from(s, 'base64').toString('binary');
}

export function bytesToBase64(bytes: Uint8Array): string {
    let binary = '';
    for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]!);
    return _btoa(binary);
}

export function base64ToBytes(input: string): Uint8Array {
    const binary = _atob(input);
    const out = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) out[i] = binary.charCodeAt(i);
    return out;
}

export function hash32ToBase64(value: Hash32): string {
    const out = new Uint8Array(4);
    new DataView(out.buffer).setUint32(0, value as number, true);
    return bytesToBase64(out);
}

export function hash64ToBase64(value: Hash64): string {
    const out = new Uint8Array(8);
    let n = value as unknown as bigint;
    for (let i = 0; i < 8; i++) {
        out[i] = Number(n & 0xffn);
        n >>= 8n;
    }
    return bytesToBase64(out);
}

export function base64ToHash32(input: string): Hash32 {
    const bytes = base64ToBytes(input);
    if (bytes.length !== 4) {
        throw new HashDeserializationError(`base64ToHash32: expected 4 bytes, got ${bytes.length}`);
    }
    return asHash32(new DataView(bytes.buffer, bytes.byteOffset, 4).getUint32(0, true));
}

export function base64ToHash64(input: string): Hash64 {
    const bytes = base64ToBytes(input);
    if (bytes.length !== 8) {
        throw new HashDeserializationError(`base64ToHash64: expected 8 bytes, got ${bytes.length}`);
    }
    let h = 0n;
    for (let i = 7; i >= 0; i--) h = (h << 8n) | BigInt(bytes[i]!);
    return asHash64(h);
}

export function hashToBytes(value: HashValue): Uint8Array {
    if (typeof value === 'number') {
        const out = new Uint8Array(4);
        new DataView(out.buffer).setUint32(0, value >>> 0, true);
        return out;
    }
    let n = value as unknown as bigint;
    const size = Math.ceil(Math.max(0, Number(n.toString(2).length)) / 8);
    const out = new Uint8Array(8);
    for (let i = 0; i < 8; i++) {
        out[i] = Number(n & 0xffn);
        n >>= 8n;
    }
    return out;
}

export function bytesToHash32(bytes: Uint8Array, offset: number = 0): Hash32 {
    if (bytes.length - offset < 4) {
        throw new HashDeserializationError(`bytesToHash32: need 4 bytes, got ${bytes.length - offset}`);
    }
    return asHash32(new DataView(bytes.buffer, bytes.byteOffset + offset, 4).getUint32(0, true));
}

export function bytesToHash64(bytes: Uint8Array, offset: number = 0): Hash64 {
    if (bytes.length - offset < 8) {
        throw new HashDeserializationError(`bytesToHash64: need 8 bytes, got ${bytes.length - offset}`);
    }
    let h = 0n;
    for (let i = 7; i >= 0; i--) h = (h << 8n) | BigInt(bytes[offset + i]!);
    return asHash64(h);
}

export function toBase64Url(bytes: Uint8Array): string {
    return bytesToBase64(bytes).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

export function fromBase64Url(input: string): Uint8Array {
    let s = input.replace(/-/g, '+').replace(/_/g, '/');
    while (s.length % 4) s += '=';
    return base64ToBytes(s);
}
