import { describe, it, expect } from 'vitest';
import {
    decode,
    decodeToString,
    InvalidCharacterError,
    InvalidLengthError,
    InvalidPaddingError,
    InvalidDataError,
    InvalidInputError,
} from '../../base64/index';
import { utf8, toHex, bytes, RFC_CASES } from './base64-test-helpers';

describe('base64 · decode', () => {
    describe('RFC 4648 vectors', () => {
        it.each(RFC_CASES)('decode(%j) === %j bytes', (plain, b64) => {
            expect(utf8(decode(b64))).toBe(plain);
        });
    });

    describe('padding modes', () => {
        it('default: accepts both padded and unpadded input', () => {
            expect(utf8(decode('Zg=='))).toBe('f');
            expect(utf8(decode('Zg'))).toBe('f');
            expect(utf8(decode('Zm8='))).toBe('fo');
            expect(utf8(decode('Zm8'))).toBe('fo');
        });

        it('require: rejects missing padding', () => {
            expect(utf8(decode('Zg==', { padding: 'require' }))).toBe('f');
            expect(utf8(decode('Zm9v', { padding: 'require' }))).toBe('foo');
            expect(() => decode('Zg', { padding: 'require' })).toThrow(InvalidPaddingError);
            expect(() => decode('Zm8', { padding: 'require' })).toThrow(InvalidPaddingError);
        });

        it('forbid: rejects padding', () => {
            expect(utf8(decode('Zg', { padding: 'forbid' }))).toBe('f');
            expect(() => decode('Zg==', { padding: 'forbid' })).toThrow(InvalidPaddingError);
            expect(() => decode('Zm8=', { padding: 'forbid' })).toThrow(InvalidPaddingError);
        });
    });

    describe('alphabet selection', () => {
        it('standard mode rejects urlsafe characters', () => {
            expect(() => decode('--__')).toThrow(InvalidCharacterError);
        });

        it('urlsafe mode rejects standard characters', () => {
            expect(() => decode('++++', { alphabet: 'urlsafe' })).toThrow(InvalidCharacterError);
        });

        it('decodes urlsafe input correctly', () => {
            expect(toHex(decode('--__', { alphabet: 'urlsafe' }))).toBe('fbefff');
        });
    });

    describe('error inputs', () => {
        it('reports character and index for invalid character', () => {
            try {
                decode('Zm9v*');
                expect.unreachable('error was expected');
            } catch (error) {
                expect(error).toBeInstanceOf(InvalidCharacterError);
                const e = error as InstanceType<typeof InvalidCharacterError>;
                expect(e.character).toBe('*');
                expect(e.index).toBe(4);
                expect(e.alphabet).toBe('standard');
            }
        });

        it.each(['A', 'AAAAA', 'A=', 'AA=AA', 'ABCDEF'])('rejects invalid length: %j', (input) => {
            expect(() => decode(input)).toThrowError();
        });

        it('single character throws InvalidLengthError', () => {
            expect(() => decode('A')).toThrow(InvalidLengthError);
        });

        it('wrong padding count throws InvalidPaddingError', () => {
            expect(() => decode('Zg=')).toThrow(InvalidPaddingError);
            expect(() => decode('Zg===')).toThrow(InvalidPaddingError);
            expect(() => decode('Zm9v=')).toThrow(InvalidPaddingError);
        });

        it('data after padding throws InvalidPaddingError', () => {
            expect(() => decode('Zg==Zg==')).toThrow(InvalidPaddingError);
            expect(() => decode('Zg==A')).toThrow(InvalidPaddingError);
        });

        it('whitespace characters are rejected', () => {
            expect(() => decode('Zm9v ')).toThrow(InvalidCharacterError);
            expect(() => decode('Zm9v\n')).toThrow(InvalidCharacterError);
            expect(() => decode(' Zm9v')).toThrow(InvalidCharacterError);
        });

        it('non-string input throws InvalidInputError', () => {
            expect(() => decode(123 as unknown as string)).toThrow(InvalidInputError);
            expect(() => decode(null as unknown as string)).toThrow(InvalidInputError);
        });

        it('unicode characters are rejected', () => {
            expect(() => decode('Zm9v\u{1F600}')).toThrow(InvalidCharacterError);
        });
    });
});

describe('base64 · decodeToString', () => {
    it('decodes base64 to UTF-8 string', () => {
        expect(decodeToString('bWVyaGFiYSBkw7xueWE=')).toBe('merhaba dünya');
        expect(decodeToString('8J+agPCflKXwn5Kv')).toBe('🚀🔥💯');
    });

    it('throws InvalidDataError for invalid UTF-8 bytes', () => {
        expect(() => decodeToString('/w==')).toThrow(InvalidDataError);
        expect(() => decodeToString('gA==')).toThrow(InvalidDataError);
    });

    it('returns empty string for empty input', () => {
        expect(decodeToString('')).toBe('');
    });
});
