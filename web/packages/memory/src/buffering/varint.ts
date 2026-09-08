/**
 * VarInt encoding/decoding and JSON serialization helpers for ByteBuffer.
 *
 * These are extracted as standalone functions that operate on ByteBuffer instances
 * via their public API, keeping the core ByteBuffer class focused on typed read/write.
 *
 * @internal
 */

import type { ByteBuffer } from './byte-buffer-core';
import { BufferUnderflowError } from './errors';

/**
 * Write a variable-length unsigned 32-bit integer to the buffer.
 * Uses 7 bits per byte with the high bit as a continuation flag.
 */
export function writeVarInt(buffer: ByteBuffer, value: number): ByteBuffer {
    let temp = value >>> 0;

    do {
        let b = temp & 0x7f;
        temp >>>= 7;
        if (temp !== 0) {
            b |= 0x80;
        }
        buffer.putUint8(b);
    } while (temp !== 0);

    return buffer;
}

/**
 * Read a variable-length unsigned 32-bit integer from the buffer.
 */
export function readVarInt(buffer: ByteBuffer): number {
    let result = 0;
    let shift = 0;
    let b: number;

    do {
        if (shift >= 32) {
            throw new BufferUnderflowError('VarInt is too big');
        }

        b = buffer.getUint8();
        result |= (b & 0x7f) << shift;
        shift += 7;
    } while ((b & 0x80) !== 0);

    return result >>> 0;
}

/**
 * Write a JSON-serialized value to the buffer as a length-prefixed UTF-8 string.
 */
export function writeJson<T>(buffer: ByteBuffer, value: T): ByteBuffer {
    return buffer.putString(JSON.stringify(value));
}

/**
 * Read a JSON-serialized value from the buffer.
 */
export function readJson<T>(buffer: ByteBuffer): T {
    return JSON.parse(buffer.getString());
}
