import type {
    Base64Alphabet,
    Base64String,
    BinaryInput,
    PaddingPolicy,
    PaddingVerificationPolicy,
    EncodingOptions,
    DecodingOptions,
    ValidationOptions,
    Base64Result,
} from './types';
import {
    Base64Error,
    InvalidInputError,
    InvalidCharacterError,
    InvalidLengthError,
    InvalidPaddingError,
    InvalidDataError,
    UnexpectedError,
} from './errors';

type TextEncoderLike = {
    encode(input?: string): Uint8Array;
};

type TextDecoderLike = {
    decode(input?: Uint8Array): string;
};

type Base64Global = {
    TextEncoder?: new () => TextEncoderLike;
    TextDecoder?: new (label?: string, options?: { fatal?: boolean }) => TextDecoderLike;
};

const globalObject = (typeof globalThis === 'undefined' ? {} : globalThis) as Base64Global;

const STANDARD_ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';
const URLSAFE_ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_';
const INVALID = 255;
const PADDING = 254;
const PADDING_CODE = 61;
const EMPTY_BYTES = new Uint8Array(0);

type ReadonlyUint8Array = Readonly<Uint8Array>;

interface AlphabetTables {
    readonly encode: ReadonlyUint8Array;
    readonly decode: ReadonlyUint8Array;
}

interface Base64Analysis {
    readonly dataChars: number;
    readonly paddingChars: number;
    readonly outputLength: number;
    readonly fullChars: number;
    readonly remainder: number;
}

let standardTables: AlphabetTables | undefined;
let urlsafeTables: AlphabetTables | undefined;
let textEncoder: TextEncoderLike | undefined;
let asciiDecoder: TextDecoderLike | undefined;
let utf8Decoder: TextDecoderLike | undefined;

const objectToString = Object.prototype.toString;

function isUint8Array(value: unknown): value is Uint8Array {
    return objectToString.call(value) === '[object Uint8Array]';
}

function isArrayBuffer(value: unknown): value is ArrayBuffer {
    return objectToString.call(value) === '[object ArrayBuffer]';
}

function fallbackUtf8Encode(input: string = ''): Uint8Array {
    let byteLength = 0;

    for (let i = 0; i < input.length; i += 1) {
        const code = input.charCodeAt(i);

        if (code <= 0x7f) {
            byteLength += 1;
        } else if (code <= 0x7ff) {
            byteLength += 2;
        } else if (code >= 0xd800 && code <= 0xdbff) {
            const next = input.charCodeAt(i + 1);

            if (next >= 0xdc00 && next <= 0xdfff) {
                byteLength += 4;
                i += 1;
            } else {
                byteLength += 3;
            }
        } else if (code >= 0xdc00 && code <= 0xdfff) {
            byteLength += 3;
        } else {
            byteLength += 3;
        }
    }

    const bytes = new Uint8Array(byteLength);
    let position = 0;

    for (let i = 0; i < input.length; i += 1) {
        const code = input.charCodeAt(i);

        if (code <= 0x7f) {
            bytes[position] = code;
            position += 1;
        } else if (code <= 0x7ff) {
            bytes[position] = 0xc0 | (code >> 6);
            bytes[position + 1] = 0x80 | (code & 0x3f);
            position += 2;
        } else if (code >= 0xd800 && code <= 0xdbff) {
            const next = input.charCodeAt(i + 1);

            if (next >= 0xdc00 && next <= 0xdfff) {
                const codePoint = 0x10000 + ((code - 0xd800) << 10) + (next - 0xdc00);

                bytes[position] = 0xf0 | (codePoint >> 18);
                bytes[position + 1] = 0x80 | ((codePoint >> 12) & 0x3f);
                bytes[position + 2] = 0x80 | ((codePoint >> 6) & 0x3f);
                bytes[position + 3] = 0x80 | (codePoint & 0x3f);
                position += 4;
                i += 1;
            } else {
                bytes[position] = 0xef;
                bytes[position + 1] = 0xbf;
                bytes[position + 2] = 0xbd;
                position += 3;
            }
        } else if (code >= 0xdc00 && code <= 0xdfff) {
            bytes[position] = 0xef;
            bytes[position + 1] = 0xbf;
            bytes[position + 2] = 0xbd;
            position += 3;
        } else {
            bytes[position] = 0xe0 | (code >> 12);
            bytes[position + 1] = 0x80 | ((code >> 6) & 0x3f);
            bytes[position + 2] = 0x80 | (code & 0x3f);
            position += 3;
        }
    }

    return bytes;
}

function fallbackUtf8Decode(bytes: Uint8Array = EMPTY_BYTES): string {
    const limit = bytes.length;
    let result = '';
    let part = '';
    let partLength = 0;
    let i = 0;

    while (i < limit) {
        const b0 = bytes[i];
        let code: number;

        if (b0 <= 0x7f) {
            code = b0;
            i += 1;
        } else if (b0 >= 0xc2 && b0 <= 0xdf) {
            if (i + 1 >= limit) {
                throw new InvalidDataError('Invalid UTF-8 sequence.');
            }

            const b1 = bytes[i + 1];

            if ((b1 & 0xc0) !== 0x80) {
                throw new InvalidDataError('Invalid UTF-8 continuation byte.');
            }

            code = ((b0 & 0x1f) << 6) | (b1 & 0x3f);

            if (code < 0x80) {
                throw new InvalidDataError('Overlong UTF-8 sequence.');
            }

            i += 2;
        } else if (b0 >= 0xe0 && b0 <= 0xef) {
            if (i + 2 >= limit) {
                throw new InvalidDataError('Invalid UTF-8 sequence.');
            }

            const b1 = bytes[i + 1];
            const b2 = bytes[i + 2];

            if ((b1 & 0xc0) !== 0x80 || (b2 & 0xc0) !== 0x80) {
                throw new InvalidDataError('Invalid UTF-8 continuation byte.');
            }

            if (b0 === 0xe0 && b1 < 0xa0) {
                throw new InvalidDataError('Overlong UTF-8 sequence.');
            }

            if (b0 === 0xed && b1 > 0x9f) {
                throw new InvalidDataError('UTF-8 surrogate code point.');
            }

            code = ((b0 & 0x0f) << 12) | ((b1 & 0x3f) << 6) | (b2 & 0x3f);

            if (code < 0x800 || (code >= 0xd800 && code <= 0xdfff)) {
                throw new InvalidDataError('Invalid UTF-8 code point.');
            }

            i += 3;
        } else if (b0 >= 0xf0 && b0 <= 0xf4) {
            if (i + 3 >= limit) {
                throw new InvalidDataError('Invalid UTF-8 sequence.');
            }

            const b1 = bytes[i + 1];
            const b2 = bytes[i + 2];
            const b3 = bytes[i + 3];

            if ((b1 & 0xc0) !== 0x80 || (b2 & 0xc0) !== 0x80 || (b3 & 0xc0) !== 0x80) {
                throw new InvalidDataError('Invalid UTF-8 continuation byte.');
            }

            if (b0 === 0xf0 && b1 < 0x90) {
                throw new InvalidDataError('Overlong UTF-8 sequence.');
            }

            if (b0 === 0xf4 && b1 > 0x8f) {
                throw new InvalidDataError('UTF-8 code point out of range.');
            }

            code = ((b0 & 0x07) << 18) | ((b1 & 0x3f) << 12) | ((b2 & 0x3f) << 6) | (b3 & 0x3f);

            if (code < 0x10000 || code > 0x10ffff) {
                throw new InvalidDataError('Invalid UTF-8 code point.');
            }

            i += 4;
        } else {
            throw new InvalidDataError('Invalid UTF-8 byte.');
        }

        if (code <= 0xffff) {
            part += String.fromCharCode(code);
            partLength += 1;
        } else {
            const offset = code - 0x10000;
            part += String.fromCharCode(0xd800 + (offset >> 10), 0xdc00 + (offset & 0x3ff));
            partLength += 2;
        }

        if (partLength >= 8192) {
            result += part;
            part = '';
            partLength = 0;
        }
    }

    return result + part;
}

function asciiBytesToString(bytes: Uint8Array = EMPTY_BYTES): string {
    const length = bytes.length;

    if (length === 0) {
        return '';
    }

    const chunkSize = 0x2000;
    let result = '';

    for (let start = 0; start < length; start += chunkSize) {
        const end = Math.min(start + chunkSize, length);
        const chunk = new Array<number>(end - start);

        for (let i = start, j = 0; i < end; i += 1, j += 1) {
            chunk[j] = bytes[i];
        }

        result += String.fromCharCode(...chunk);
    }

    return result;
}

function getTextEncoder(): TextEncoderLike {
    if (textEncoder === undefined) {
        const Impl = globalObject.TextEncoder;

        if (Impl === undefined) {
            textEncoder = { encode: fallbackUtf8Encode };
        } else {
            try {
                textEncoder = new Impl();
            } catch {
                textEncoder = { encode: fallbackUtf8Encode };
            }
        }
    }

    return textEncoder;
}

function getAsciiDecoder(): TextDecoderLike {
    if (asciiDecoder === undefined) {
        const Impl = globalObject.TextDecoder;

        if (Impl === undefined) {
            asciiDecoder = { decode: asciiBytesToString };
        } else {
            try {
                asciiDecoder = new Impl('utf-8');
            } catch {
                asciiDecoder = { decode: asciiBytesToString };
            }
        }
    }

    return asciiDecoder;
}

function getUtf8Decoder(): TextDecoderLike {
    if (utf8Decoder === undefined) {
        const Impl = globalObject.TextDecoder;

        if (Impl === undefined) {
            utf8Decoder = { decode: fallbackUtf8Decode };
        } else {
            try {
                utf8Decoder = new Impl('utf-8', { fatal: true });
            } catch {
                utf8Decoder = { decode: fallbackUtf8Decode };
            }
        }
    }

    return utf8Decoder;
}

function createTables(alphabet: string): AlphabetTables {
    const encode = new Uint8Array(64);
    const decode = new Uint8Array(256);

    decode.fill(INVALID);

    for (let i = 0; i < 64; i += 1) {
        const code = alphabet.charCodeAt(i);
        encode[i] = code;
        decode[code] = i;
    }

    decode[PADDING_CODE] = PADDING;

    return { encode, decode };
}

function getTables(alphabet: Base64Alphabet): AlphabetTables {
    if (alphabet === 'urlsafe') {
        if (urlsafeTables === undefined) {
            urlsafeTables = createTables(URLSAFE_ALPHABET);
        }

        return urlsafeTables;
    }

    if (standardTables === undefined) {
        standardTables = createTables(STANDARD_ALPHABET);
    }

    return standardTables;
}

function resolveAlphabet(alphabet: Base64Alphabet | undefined): Base64Alphabet {
    if (alphabet === 'urlsafe') {
        return 'urlsafe';
    }

    if (alphabet === undefined || alphabet === 'standard') {
        return 'standard';
    }

    throw new InvalidInputError(`Unsupported Base64 alphabet: ${String(alphabet)}.`);
}

function resolveEncodingPadding(padding: PaddingPolicy | undefined, alphabet: Base64Alphabet): boolean {
    if (padding === 'include') {
        return true;
    }

    if (padding === 'omit') {
        return false;
    }

    return alphabet === 'standard';
}

function resolveVerificationPadding(padding: PaddingVerificationPolicy | undefined): PaddingVerificationPolicy {
    if (padding === 'require' || padding === 'optional' || padding === 'forbid') {
        return padding;
    }

    return 'optional';
}

function encodedLength(byteLength: number, includePadding: boolean): number {
    if (byteLength === 0) {
        return 0;
    }

    const complete = Math.floor(byteLength / 3);
    const remainder = byteLength - complete * 3;

    if (includePadding) {
        return (complete + (remainder === 0 ? 0 : 1)) * 4;
    }

    return complete * 4 + (remainder === 0 ? 0 : remainder + 1);
}

function toBytes(input: BinaryInput): Uint8Array {
    if (typeof input === 'string') {
        return getTextEncoder().encode(input);
    }

    if (isUint8Array(input)) {
        return input;
    }

    if (isArrayBuffer(input)) {
        return new Uint8Array(input);
    }

    if (ArrayBuffer.isView(input)) {
        return new Uint8Array(input.buffer, input.byteOffset, input.byteLength);
    }

    throw new InvalidInputError('Input must be a string, ArrayBuffer, ArrayBufferView, or Uint8Array.');
}

function analyzeBase64(
    input: string,
    alphabet: Base64Alphabet,
    paddingPolicy: PaddingVerificationPolicy,
): Base64Analysis {
    const length = input.length;

    if (length === 0) {
        return { dataChars: 0, paddingChars: 0, outputLength: 0, fullChars: 0, remainder: 0 };
    }

    const table = getTables(alphabet).decode;
    let dataChars = 0;
    let paddingChars = 0;
    let seenPadding = false;

    for (let i = 0; i < length; i += 1) {
        const code = input.charCodeAt(i);
        const value = code <= 255 ? table[code] : INVALID;

        if (value === INVALID) {
            throw new InvalidCharacterError(input.charAt(i), i, alphabet);
        }

        if (value === PADDING) {
            seenPadding = true;
            paddingChars += 1;
            continue;
        }

        if (seenPadding) {
            throw new InvalidPaddingError(i, 'Base64 padding must appear only at the end of input.');
        }

        dataChars += 1;
    }

    const remainder = dataChars % 4;

    if (remainder === 1) {
        throw new InvalidLengthError(length);
    }

    const expectedPadding = remainder === 2 ? 2 : remainder === 3 ? 1 : 0;

    if (paddingChars > 0) {
        if (paddingPolicy === 'forbid') {
            throw new InvalidPaddingError(undefined, 'Base64 padding is forbidden.');
        }

        if (paddingChars !== expectedPadding) {
            throw new InvalidPaddingError(undefined, 'Base64 padding length is invalid.');
        }
    } else if (paddingPolicy === 'require' && expectedPadding !== 0) {
        throw new InvalidPaddingError(undefined, 'Base64 padding is required.');
    }

    const fullGroups = Math.floor(dataChars / 4);
    const fullChars = fullGroups * 4;
    const outputLength = fullGroups * 3 + (remainder === 2 ? 1 : remainder === 3 ? 2 : 0);

    return { dataChars, paddingChars, outputLength, fullChars, remainder };
}

function assertCanonicalTrailingBits(input: string, analysis: Base64Analysis, table: ReadonlyUint8Array): void {
    if (analysis.remainder === 2) {
        const b = table[input.charCodeAt(analysis.fullChars + 1)];

        if ((b & 15) !== 0) {
            throw new InvalidDataError('Base64 input contains non-canonical trailing bits.');
        }
    } else if (analysis.remainder === 3) {
        const c = table[input.charCodeAt(analysis.fullChars + 2)];

        if ((c & 3) !== 0) {
            throw new InvalidDataError('Base64 input contains non-canonical trailing bits.');
        }
    }
}

function normalizeError(error: unknown): Base64Error {
    if (error instanceof Base64Error) {
        return error;
    }

    if (error instanceof Error) {
        return new UnexpectedError(error.message, { cause: error });
    }

    return new UnexpectedError('An unexpected error occurred.', { cause: error });
}

export function encodeToBytes<A extends Base64Alphabet = 'standard'>(
    input: BinaryInput,
    options?: EncodingOptions<A>,
): Uint8Array {
    const alphabet = resolveAlphabet(options?.alphabet);
    const includePadding = resolveEncodingPadding(options?.padding, alphabet);
    const bytes = toBytes(input);
    const byteLength = bytes.length;
    const outputLength = encodedLength(byteLength, includePadding);

    if (outputLength === 0) {
        return EMPTY_BYTES;
    }

    const tables = getTables(alphabet);
    const encodeTable = tables.encode;
    const output = new Uint8Array(outputLength);
    const remainder = byteLength % 3;
    const limit = byteLength - remainder;

    let i = 0;
    let j = 0;

    for (; i < limit; i += 3) {
        const b0 = bytes[i];
        const b1 = bytes[i + 1];
        const b2 = bytes[i + 2];

        output[j] = encodeTable[b0 >> 2];
        output[j + 1] = encodeTable[((b0 & 3) << 4) | (b1 >> 4)];
        output[j + 2] = encodeTable[((b1 & 15) << 2) | (b2 >> 6)];
        output[j + 3] = encodeTable[b2 & 63];
        j += 4;
    }

    if (remainder === 1) {
        const b0 = bytes[i];

        output[j] = encodeTable[b0 >> 2];
        output[j + 1] = encodeTable[(b0 & 3) << 4];
        j += 2;

        if (includePadding) {
            output[j] = PADDING_CODE;
            output[j + 1] = PADDING_CODE;
        }
    } else if (remainder === 2) {
        const b0 = bytes[i];
        const b1 = bytes[i + 1];

        output[j] = encodeTable[b0 >> 2];
        output[j + 1] = encodeTable[((b0 & 3) << 4) | (b1 >> 4)];
        output[j + 2] = encodeTable[(b1 & 15) << 2];
        j += 3;

        if (includePadding) {
            output[j] = PADDING_CODE;
        }
    }

    return output;
}

export function encode<A extends Base64Alphabet = 'standard'>(
    input: BinaryInput,
    options?: EncodingOptions<A>,
): Base64String<A> {
    const bytes = encodeToBytes(input, options);

    if (bytes.length === 0) {
        return '' as Base64String<A>;
    }

    return getAsciiDecoder().decode(bytes) as Base64String<A>;
}

export function decode<A extends Base64Alphabet = Base64Alphabet>(
    input: string,
    options?: DecodingOptions<A>,
): Uint8Array {
    if (typeof input !== 'string') {
        throw new InvalidInputError('Input must be a string.');
    }

    const alphabet = resolveAlphabet(options?.alphabet);
    const paddingPolicy = resolveVerificationPadding(options?.padding);
    const analysis = analyzeBase64(input, alphabet, paddingPolicy);

    if (analysis.outputLength === 0) {
        return EMPTY_BYTES;
    }

    const table = getTables(alphabet).decode;

    assertCanonicalTrailingBits(input, analysis, table);

    const output = new Uint8Array(analysis.outputLength);
    let outIndex = 0;

    for (let i = 0; i < analysis.fullChars; i += 4) {
        const a = table[input.charCodeAt(i)];
        const b = table[input.charCodeAt(i + 1)];
        const c = table[input.charCodeAt(i + 2)];
        const d = table[input.charCodeAt(i + 3)];

        output[outIndex] = (a << 2) | (b >> 4);
        output[outIndex + 1] = ((b & 15) << 4) | (c >> 2);
        output[outIndex + 2] = ((c & 3) << 6) | d;
        outIndex += 3;
    }

    if (analysis.remainder === 2) {
        const i = analysis.fullChars;
        const a = table[input.charCodeAt(i)];
        const b = table[input.charCodeAt(i + 1)];

        output[outIndex] = (a << 2) | (b >> 4);
    } else if (analysis.remainder === 3) {
        const i = analysis.fullChars;
        const a = table[input.charCodeAt(i)];
        const b = table[input.charCodeAt(i + 1)];
        const c = table[input.charCodeAt(i + 2)];

        output[outIndex] = (a << 2) | (b >> 4);
        output[outIndex + 1] = ((b & 15) << 4) | (c >> 2);
    }

    return output;
}

export function decodeToString<A extends Base64Alphabet = Base64Alphabet>(
    input: string,
    options?: DecodingOptions<A>,
): string {
    const bytes = decode(input, options);

    if (bytes.length === 0) {
        return '';
    }

    const decoder = getUtf8Decoder();

    try {
        return decoder.decode(bytes);
    } catch (error) {
        utf8Decoder = undefined;

        if (error instanceof InvalidDataError) {
            throw error;
        }

        throw new InvalidDataError('Decoded bytes are not valid UTF-8.', { cause: error });
    }
}

export function assertBase64<A extends Base64Alphabet = 'standard'>(
    input: string,
    options?: ValidationOptions<A>,
): asserts input is Base64String<A> {
    if (typeof input !== 'string') {
        throw new InvalidInputError('Input must be a string.');
    }

    const alphabet = resolveAlphabet(options?.alphabet);
    const paddingPolicy = resolveVerificationPadding(options?.padding);
    const analysis = analyzeBase64(input, alphabet, paddingPolicy);

    if (analysis.outputLength === 0) {
        return;
    }

    assertCanonicalTrailingBits(input, analysis, getTables(alphabet).decode);
}

export function isBase64<A extends Base64Alphabet = 'standard'>(
    input: string,
    options?: ValidationOptions<A>,
): input is Base64String<A> {
    try {
        assertBase64(input, options);
        return true;
    } catch {
        return false;
    }
}

export function tryEncode<A extends Base64Alphabet = 'standard'>(
    input: BinaryInput,
    options?: EncodingOptions<A>,
): Base64Result<Base64String<A>, Base64Error> {
    try {
        return { ok: true, value: encode(input, options) };
    } catch (error) {
        return { ok: false, error: normalizeError(error) };
    }
}

export function tryDecode<A extends Base64Alphabet = Base64Alphabet>(
    input: string,
    options?: DecodingOptions<A>,
): Base64Result<Uint8Array, Base64Error> {
    try {
        return { ok: true, value: decode(input, options) };
    } catch (error) {
        return { ok: false, error: normalizeError(error) };
    }
}
