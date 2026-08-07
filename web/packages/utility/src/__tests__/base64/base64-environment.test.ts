import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import {
    encode,
    decode,
    tryEncode,
    Base64Error,
    InvalidInputError,
} from '../../base64/index';
import type { Base64String, Base64Alphabet, Base64Result } from '../../base64/index';
import {
    bytes,
    randBytes,
    toHex,
    nativeBuffer,
    bufferB64,
} from './base64-test-helpers';

type Base64Module = typeof import('../../base64/index');

describe('base64 · cross-validation against reference implementations', () => {
    it('matches btoa across 500 random encode samples', () => {
        for (let i = 0; i < 500; i++) {
            const data = randBytes(1 + ((i * 37) % 300));
            let reference = '';
            const CHUNK = 0x8000;
            for (let j = 0; j < data.length; j += CHUNK) {
                reference += String.fromCharCode(...data.subarray(j, j + CHUNK));
            }
            expect(encode(data)).toBe(btoa(reference));
        }
    });

    it('matches atob across 500 random decode samples', () => {
        for (let i = 0; i < 500; i++) {
            const data = randBytes(1 + ((i * 53) % 300));
            let binary = '';
            const CHUNK = 0x8000;
            for (let j = 0; j < data.length; j += CHUNK) {
                binary += String.fromCharCode(...data.subarray(j, j + CHUNK));
            }
            const encoded = btoa(binary);
            const bin = atob(encoded);
            const expected = new Uint8Array(bin.length);
            for (let k = 0; k < bin.length; k++) expected[k] = bin.charCodeAt(k);
            expect(toHex(decode(encoded))).toBe(toHex(expected));
        }
    });

    it.runIf(nativeBuffer !== null)('matches native Node Buffer encode across 200 samples', () => {
        for (let i = 0; i < 200; i++) {
            const data = randBytes(1 + ((i * 41) % 400));
            expect(encode(data)).toBe(bufferB64(data));
        }
    });

    it.runIf(nativeBuffer !== null)('matches native Node Buffer decode across 200 samples', () => {
        for (let i = 0; i < 200; i++) {
            const data = randBytes(1 + ((i * 59) % 400));
            const encoded = bufferB64(data);
            const viaBuffer = nativeBuffer!.from(encoded, 'base64');
            expect(toHex(decode(encoded))).toBe(toHex(new Uint8Array(viaBuffer)));
        }
    });
});

describe('base64 · TextEncoder/TextDecoder fallback', () => {
    const encoderSpy = vi.spyOn(globalThis, 'TextEncoder');
    const decoderSpy = vi.spyOn(globalThis, 'TextDecoder');

    beforeEach(() => {
        vi.resetModules();
    });

    afterEach(() => {
        encoderSpy.mockRestore();
        decoderSpy.mockRestore();
    });

    it('encode works without TextEncoder', async () => {
        encoderSpy.mockImplementation(() => {
            throw new Error('TextEncoder blocked');
        });
        const fresh = (await import('../../base64/index?no-encoder')) as Base64Module;
        expect(fresh.encode('merhaba dünya')).toBe('bWVyaGFiYSBkw7xueWE=');
        expect(fresh.encode('🚀')).toBe('8J+agA==');
    });

    it('encode and decodeToString work without TextDecoder', async () => {
        decoderSpy.mockImplementation(() => {
            throw new Error('TextDecoder blocked');
        });
        const fresh = (await import('../../base64/index?no-decoder')) as Base64Module;
        expect(fresh.encode('foo')).toBe('Zm9v');
        expect(fresh.decodeToString('bWVyaGFiYSBkw7xueWE=')).toBe('merhaba dünya');
        expect(fresh.decodeToString('8J+agPCflKXwn5Kv')).toBe('🚀🔥💯');
    });
});

describe('base64 · graceful fallback when TextEncoder/TextDecoder fail', () => {
    beforeEach(() => {
        vi.resetModules();
    });

    afterEach(() => {
        vi.restoreAllMocks();
    });

    it('tryEncode succeeds via fallback when TextEncoder constructor throws', async () => {
        const encoderSpy = vi.spyOn(globalThis, 'TextEncoder');
        encoderSpy.mockImplementation(() => {
            throw new TypeError('TextEncoder unavailable');
        });
        const fresh = (await import('../../base64/index?explode-encoder')) as Base64Module;
        const result = fresh.tryEncode('foo');
        expect(result.ok).toBe(true);
        if (result.ok) {
            expect(result.value).toBe('Zm9v');
        }
        encoderSpy.mockRestore();
    });
});

describe('base64 · performance budget', () => {
    it('1 MB encode completes under 500 ms', () => {
        const data = randBytes(1 << 20);
        encode(data); // warm-up
        const t0 = performance.now();
        encode(data);
        expect(performance.now() - t0).toBeLessThan(500);
    });

    it('1.33 MB decode completes under 500 ms', () => {
        const encoded = encode(randBytes(1 << 20));
        decode(encoded); // warm-up
        const t0 = performance.now();
        decode(encoded);
        expect(performance.now() - t0).toBeLessThan(500);
    });
});

describe('base64 · type-level contracts', () => {
    it('branded string is assignable to plain string', () => {
        const branded = encode('foo');
        const plain: string = branded;
        expect(plain).toBe('Zm9v');
        expectTypeOf(branded).toMatchTypeOf<string>();
        expectTypeOf<typeof branded>().not.toEqualTypeOf<string>();
    });

    it('alphabet parameter is reflected in the return type', () => {
        const standard = encode(bytes(1));
        const urlsafe = encode(bytes(1), { alphabet: 'urlsafe' });
        expectTypeOf(standard).toEqualTypeOf<Base64String<'standard'>>();
        expectTypeOf(urlsafe).toEqualTypeOf<Base64String<'urlsafe'>>();
    });

    it('dynamic alphabet widens the return type', () => {
        const alphabet: Base64Alphabet = 'urlsafe';
        const result = encode(bytes(1), { alphabet });
        expectTypeOf(result).toEqualTypeOf<Base64String<Base64Alphabet>>();
    });

    it('Base64Result ok/err separation is visible at type level', () => {
        const ok: Base64Result<number, Base64Error> = { ok: true, value: 1 };
        const err: Base64Result<number, Base64Error> = { ok: false, error: new InvalidInputError() };
        if (ok.ok) {
            expectTypeOf(ok.value).toEqualTypeOf<number>();
        }
        if (!err.ok) {
            expectTypeOf(err.error).toEqualTypeOf<Base64Error>();
        }
    });
});
