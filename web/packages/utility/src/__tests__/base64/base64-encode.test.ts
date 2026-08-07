import { describe, it, expect } from 'vitest';
import { encode, encodeToBytes, InvalidInputError } from '../../base64/index';
import type { BinaryInput } from '../../base64/index';
import { enc, bytes, randBytes, RFC_CASES, UNICODE_CASES, STD } from './base64-test-helpers';

describe('base64 · encode', () => {
    describe('RFC 4648 vectors', () => {
        it.each(RFC_CASES)('encode(%j) === %j', (input, expected) => {
            expect(encode(enc(input))).toBe(expected);
        });
    });

    describe('unicode', () => {
        it.each(UNICODE_CASES)('encode(%j) === %j', (input, expected) => {
            expect(encode(input)).toBe(expected);
        });
    });

    describe('alphabet completeness', () => {
        it('produces all 64 standard alphabet characters in order', () => {
            const input = bytes(
                0x00, 0x10, 0x83, 0x10, 0x51, 0x87, 0x20, 0x92, 0x8b, 0x30, 0xd3, 0x8f,
                0x41, 0x14, 0x93, 0x51, 0x55, 0x97, 0x61, 0x96, 0x9b, 0x71, 0xd7, 0x9f,
                0x82, 0x18, 0xa3, 0x92, 0x59, 0xa7, 0xa2, 0x9a, 0xab, 0xb2, 0xdb, 0xaf,
                0xc3, 0x1c, 0xb3, 0xd3, 0x5d, 0xb7, 0xe3, 0x9e, 0xbb, 0xf3, 0xdf, 0xbf,
            );
            expect(encode(input)).toBe(STD);
        });

        it('urlsafe alphabet produces - and _ instead of + and /', () => {
            const input = bytes(0xfb, 0xef, 0xbe);
            expect(encode(input, { alphabet: 'standard' })).toBe('++++');
            expect(encode(input, { alphabet: 'urlsafe' })).toBe('----');

            const input2 = bytes(0xfb, 0xff, 0xbf);
            expect(encode(input2, { alphabet: 'standard' })).toBe('+/+/');
            expect(encode(input2, { alphabet: 'urlsafe' })).toBe('-_-_');
        });
    });

    describe('padding policy', () => {
        it('standard default includes padding', () => {
            expect(encode(bytes(1))).toBe('AQ==');
            expect(encode(bytes(1, 2))).toBe('AQI=');
            expect(encode(bytes(1, 2, 3))).toBe('AQID');
        });

        it('urlsafe default omits padding', () => {
            expect(encode(bytes(1), { alphabet: 'urlsafe' })).toBe('AQ');
            expect(encode(bytes(1, 2), { alphabet: 'urlsafe' })).toBe('AQI');
            expect(encode(bytes(1, 2, 3), { alphabet: 'urlsafe' })).toBe('AQID');
        });

        it('explicit policy overrides default', () => {
            expect(encode(bytes(1), { padding: 'omit' })).toBe('AQ');
            expect(encode(bytes(1, 2), { padding: 'omit' })).toBe('AQI');
            expect(encode(bytes(1), { alphabet: 'urlsafe', padding: 'include' })).toBe('AQ==');
            expect(encode(bytes(1, 2), { alphabet: 'urlsafe', padding: 'include' })).toBe('AQI=');
        });
    });

    describe('input types', () => {
        it('Uint8Array, ArrayBuffer, DataView and TypedArray views produce the same output', () => {
            const source = randBytes(300);
            const expected = encode(source);
            const ab = source.buffer.slice(source.byteOffset, source.byteOffset + source.byteLength);
            expect(encode(ab)).toBe(expected);
            expect(encode(new DataView(ab))).toBe(expected);
            expect(encode(new Uint16Array(ab))).toBe(expected);
            expect(encode(new Uint8Array(ab, 10, 100))).toBe(encode(source.subarray(10, 110)));
        });

        it('subarray view encodes only its own range', () => {
            const source = bytes(9, 9, 1, 2, 3, 9, 9);
            expect(encode(new Uint8Array(source.buffer, 2, 3))).toBe('AQID');
        });

        it('string input is encoded as UTF-8', () => {
            expect(encode('foo')).toBe('Zm9v');
        });

        it('invalid input type throws InvalidInputError', () => {
            expect(() => encode(42 as unknown as BinaryInput)).toThrow(InvalidInputError);
            expect(() => encode(null as unknown as BinaryInput)).toThrow(InvalidInputError);
            expect(() => encode(undefined as unknown as BinaryInput)).toThrow(InvalidInputError);
            expect(() => encode({} as unknown as BinaryInput)).toThrow(InvalidInputError);
        });
    });
});

describe('base64 · encodeToBytes', () => {
    it('returns an ASCII byte array', () => {
        const out = encodeToBytes('foo');
        expect(out).toBeInstanceOf(Uint8Array);
        expect(Array.from(out)).toEqual([90, 109, 57, 118]);
    });

    it('returns an empty array for empty input', () => {
        expect(encodeToBytes('').length).toBe(0);
    });

    it('output is always valid ASCII', () => {
        const out = encodeToBytes(randBytes(500));
        for (const b of out) {
            expect(b).toBeLessThanOrEqual(127);
        }
    });
});
