/**
 * Shared test constants and utilities for the Base64 module test suite.
 */

export const STD = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';
export const URL = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_';

const T = new TextEncoder();
const D = new TextDecoder();

export const enc = (s: string): Uint8Array => T.encode(s);
export const utf8 = (b: Uint8Array): string => D.decode(b);
export const bytes = (...v: number[]): Uint8Array => Uint8Array.from(v);

export const concatBytes = (...parts: Uint8Array[]): Uint8Array => {
    const total = parts.reduce((n, p) => n + p.length, 0);
    const out = new Uint8Array(total);
    let offset = 0;
    for (const p of parts) {
        out.set(p, offset);
        offset += p.length;
    }
    return out;
};

export const randBytes = (n: number): Uint8Array => {
    const out = new Uint8Array(n);
    for (let i = 0; i < n; i += 65536) {
        crypto.getRandomValues(out.subarray(i, Math.min(i + 65536, n)));
    }
    return out;
};

export const toHex = (b: Uint8Array): string =>
    Array.from(b, (v) => v.toString(16).padStart(2, '0')).join('');

export const RFC_CASES: ReadonlyArray<readonly [string, string]> = [
    ['', ''],
    ['f', 'Zg=='],
    ['fo', 'Zm8='],
    ['foo', 'Zm9v'],
    ['foob', 'Zm9vYg=='],
    ['fooba', 'Zm9vYmE='],
    ['foobar', 'Zm9vYmFy'],
];

export const UNICODE_CASES: ReadonlyArray<readonly [string, string]> = [
    ['merhaba dünya', 'bWVyaGFiYSBkw7xueWE='],
    ['çğıöşü ÇĞİÖŞÜ', 'w6fEn8Sxw7bFn8O8IMOHxJ7EsMOWxZ7DnA=='],
    ['こんにちは世界', '44GT44KT44Gr44Gh44Gv5LiW55WM'],
    ['🚀🔥💯', '8J+agPCflKXwn5Kv'],
    ['émoji 👨‍👩‍👧‍👦 test', 'w6ltb2ppIPCfkajigI3wn5Gp4oCN8J+Rp+KAjfCfkaYgdGVzdA=='],
    ['\x00\x01\x02}', 'AAECfQ=='],
    ['line1\nline2\ttab', 'bGluZTEKbGluZTIJdGFi'],
];

export const ALL_BYTES = (() => {
    const a = new Uint8Array(256);
    for (let i = 0; i < 256; i++) a[i] = i;
    return a;
})();

export const BOUNDARY_SIZES: readonly number[] = [
    0, 1, 2, 3, 4, 5, 6, 7, 8, 15, 16, 17, 63, 64, 65, 255, 256, 257, 1023, 1024, 4096,
];

/** Detects whether the runtime provides a native Node.js Buffer. */
export const detectNativeBuffer = (): { from(...args: unknown[]): Uint8Array } | null => {
    try {
        // eslint-disable-next-line @typescript-eslint/no-require-imports
        const mod = require('node:buffer') as { Buffer?: { from(...args: unknown[]): Uint8Array } };
        return mod.Buffer ?? null;
    } catch {
        return null;
    }
};

export const nativeBuffer = detectNativeBuffer();

export const bufferB64 = (b: Uint8Array): string =>
    nativeBuffer === null
        ? ''
        : (nativeBuffer.from(b) as unknown as { toString(e: string): string }).toString('base64');
