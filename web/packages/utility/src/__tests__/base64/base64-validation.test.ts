import { describe, it, expect } from 'vitest';
import {
    encode,
    decode,
    decodeToString,
    encodeToBytes,
    isBase64,
    assertBase64,
    tryEncode,
    tryDecode,
    Base64Error,
    InvalidInputError,
    InvalidCharacterError,
    InvalidLengthError,
    InvalidDataError,
} from '../../base64/index';
import type { Base64String, Base64Result } from '../../base64/index';
import { enc, utf8, bytes, randBytes, toHex, ALL_BYTES, BOUNDARY_SIZES } from './base64-test-helpers';

describe('base64 · round-trip', () => {
    it('all 256 bytes survive lossless round-trip', () => {
        expect(toHex(decode(encode(ALL_BYTES)))).toBe(toHex(ALL_BYTES));
    });

    it.each(BOUNDARY_SIZES)('round-trips %i bytes losslessly', (n) => {
        const data = randBytes(n);
        expect(toHex(decode(encode(data)))).toBe(toHex(data));
    });

    it('urlsafe + unpadded round-trip', () => {
        const data = randBytes(1000);
        const encoded = encode(data, { alphabet: 'urlsafe' });
        const decoded = decode(encoded, { alphabet: 'urlsafe', padding: 'forbid' });
        expect(toHex(decoded)).toBe(toHex(data));
    });

    it('unicode string round-trip', () => {
        const text = 'çğıöşü ÇĞİÖŞÜ 🚀 こんにちは';
        expect(decodeToString(encode(text))).toBe(text);
    });

    it('1 MB random data survives round-trip', () => {
        const data = randBytes(1 << 20);
        expect(toHex(decode(encode(data)))).toBe(toHex(data));
    });
});

describe('base64 · isBase64', () => {
    it('returns true for valid inputs', () => {
        expect(isBase64('Zm9vYmFy')).toBe(true);
        expect(isBase64('Zg==')).toBe(true);
        expect(isBase64('')).toBe(true);
        expect(isBase64('Zg', { padding: 'forbid' })).toBe(true);
        expect(isBase64('--__', { alphabet: 'urlsafe' })).toBe(true);
    });

    it('returns false for invalid inputs', () => {
        expect(isBase64('A')).toBe(false);
        expect(isBase64('Zg=')).toBe(false);
        expect(isBase64('Zm9v*')).toBe(false);
        expect(isBase64('--__')).toBe(false);
        expect(isBase64('++++', { alphabet: 'urlsafe' })).toBe(false);
        expect(isBase64('Zg==', { padding: 'forbid' })).toBe(false);
        expect(isBase64('Zg', { padding: 'require' })).toBe(false);
        expect(isBase64('Zh==')).toBe(false);
        expect(isBase64(42 as unknown as string)).toBe(false);
    });

    it('narrows type at compile time', () => {
        const value: string = 'Zm9v';
        if (isBase64(value)) {
            const branded: Base64String<'standard'> = value;
            expect(branded).toBe('Zm9v');
        } else {
            expect.unreachable('expected true');
        }
    });
});

describe('base64 · assertBase64', () => {
    it('does not throw for valid input', () => {
        expect(() => assertBase64('Zm9vYmFy')).not.toThrow();
    });

    it('throws the correct error type for invalid input', () => {
        expect(() => assertBase64('A')).toThrow(InvalidLengthError);
        expect(() => assertBase64('Zm9v*')).toThrow(InvalidCharacterError);
        expect(() => assertBase64('Zh==')).toThrow(InvalidDataError);
        expect(() => assertBase64(9 as unknown as string)).toThrow(InvalidInputError);
    });
});

describe('base64 · tryEncode / tryDecode', () => {
    it('tryEncode returns ok on success', () => {
        const result = tryEncode('foo');
        expect(result.ok).toBe(true);
        if (result.ok) {
            expect(result.value).toBe('Zm9v');
        }
    });

    it('tryEncode returns err on failure', () => {
        const result = tryEncode(42 as unknown as Parameters<typeof tryEncode>[0]);
        expect(result.ok).toBe(false);
        if (!result.ok) {
            expect(result.error).toBeInstanceOf(InvalidInputError);
            expect(result.error).toBeInstanceOf(Base64Error);
        }
    });

    it('tryDecode returns ok on success', () => {
        const result = tryDecode('Zm9v');
        expect(result.ok).toBe(true);
        if (result.ok) {
            expect(utf8(result.value)).toBe('foo');
        }
    });

    it('tryDecode returns err on failure', () => {
        const result = tryDecode('A');
        expect(result.ok).toBe(false);
        if (!result.ok) {
            expect(result.error).toBeInstanceOf(InvalidLengthError);
        }
    });

    it('Base64Result discriminated union narrows at type level', () => {
        const r: Base64Result<Uint8Array, Base64Error> = tryDecode('Zg==');
        if (r.ok) {
            expect(r.value).toBeInstanceOf(Uint8Array);
        } else {
            expect(r.error.kind).toBeTypeOf('string');
        }
    });
});

describe('base64 · behavior contracts', () => {
    it('empty input produces empty output', () => {
        expect(encode('')).toBe('');
        expect(encode(new Uint8Array(0))).toBe('');
        expect(decode('').length).toBe(0);
        expect(decodeToString('')).toBe('');
        expect(encodeToBytes('').length).toBe(0);
    });

    it('output length follows the formula', () => {
        for (const n of BOUNDARY_SIZES) {
            const padded = encode(randBytes(n));
            expect(padded.length).toBe(Math.ceil(n / 3) * 4);
            const unpadded = encode(randBytes(n), { padding: 'omit' });
            expect(unpadded.length).toBe(Math.ceil((n * 4) / 3));
        }
    });

    it('encode is a pure function: same input yields same output', () => {
        const data = randBytes(777);
        expect(encode(data)).toBe(encode(data));
    });

    it('decode is a pure function: same input yields same output', () => {
        const encoded = encode(randBytes(777));
        expect(toHex(decode(encoded))).toBe(toHex(decode(encoded)));
    });

    it('does not mutate the input array', () => {
        const data = randBytes(100);
        const snapshot = data.slice();
        encode(data);
        expect(toHex(data)).toBe(toHex(snapshot));
    });
});
