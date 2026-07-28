import { TerrainError, TerrainErrorCode } from '../errors';

/**
 * Environment-agnostic base64 helpers shared by the terrain persistence
 * codecs (heights, splat weights). Implemented locally so the package works
 * identically in DOM, worker, and Node contexts without `atob`/`btoa`.
 */
const BASE64_ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';

const BASE64_LOOKUP: Int16Array = (() => {
    const lookup = new Int16Array(128).fill(-1);
    for (let index = 0; index < BASE64_ALPHABET.length; index += 1) {
        lookup[BASE64_ALPHABET.charCodeAt(index)] = index;
    }
    return lookup;
})();

export const encodeBytesToBase64 = (bytes: Uint8Array): string => {
    let output = '';
    for (let index = 0; index < bytes.length; index += 3) {
        const first = bytes[index]!;
        const second = index + 1 < bytes.length ? bytes[index + 1]! : 0;
        const third = index + 2 < bytes.length ? bytes[index + 2]! : 0;

        output += BASE64_ALPHABET[first >> 2];
        output += BASE64_ALPHABET[((first & 0x03) << 4) | (second >> 4)];
        output += index + 1 < bytes.length ? BASE64_ALPHABET[((second & 0x0f) << 2) | (third >> 6)] : '=';
        output += index + 2 < bytes.length ? BASE64_ALPHABET[third & 0x3f] : '=';
    }

    return output;
};

export const decodeBase64ToBytes = (value: string): Uint8Array => {
    const sanitized = value.replace(/=+$/u, '');
    const byteLength = Math.floor((sanitized.length * 3) / 4);
    const bytes = new Uint8Array(byteLength);

    let byteIndex = 0;
    let buffer = 0;
    let bitsCollected = 0;

    for (let index = 0; index < sanitized.length; index += 1) {
        const code = sanitized.charCodeAt(index);
        const sextet = code < 128 ? BASE64_LOOKUP[code]! : -1;
        if (sextet < 0) {
            throw new TerrainError(
                `Payload contains an invalid base64 character at index ${index}.`,
                TerrainErrorCode.SOURCE_DECODE_FAILED,
                { index }
            );
        }

        buffer = (buffer << 6) | sextet;
        bitsCollected += 6;
        if (bitsCollected >= 8) {
            bitsCollected -= 8;
            bytes[byteIndex] = (buffer >> bitsCollected) & 0xff;
            byteIndex += 1;
        }
    }

    return bytes;
};
