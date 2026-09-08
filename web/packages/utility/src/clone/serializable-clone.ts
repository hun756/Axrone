import type { TypedArray } from '../types';
import { isRecord } from '../object';

export interface CloneSerializableOptions {
    readonly freeze?: boolean;
}

const cloneArrayBufferView = <TValue extends ArrayBufferView>(value: TValue): TValue => {
    if (value instanceof DataView) {
        const clonedBytes = new Uint8Array(value.byteLength);
        clonedBytes.set(new Uint8Array(value.buffer, value.byteOffset, value.byteLength));
        return new DataView(clonedBytes.buffer) as unknown as TValue;
    }

    const typedArray = value as unknown as TypedArray;
    return typedArray.slice() as unknown as TValue;
};

const maybeFreeze = <TValue>(value: TValue, freeze: boolean): TValue =>
    (freeze ? Object.freeze(value) : value);

const cloneSerializableInternal = (value: unknown, freeze: boolean): unknown => {
    if (Array.isArray(value)) {
        return maybeFreeze(value.map((entry) => cloneSerializableInternal(entry, freeze)), freeze);
    }

    if (ArrayBuffer.isView(value)) {
        return maybeFreeze(cloneArrayBufferView(value), freeze);
    }

    if (value instanceof ArrayBuffer) {
        return maybeFreeze(value.slice(0), freeze);
    }

    // Non-JSON-serializable types: return as-is (not cloned) to prevent silent corruption
    if (value instanceof Date || value instanceof Map || value instanceof Set) {
        return value;
    }

    if (!isRecord(value)) {
        return value;
    }

    const cloned: Record<string, unknown> = {};
    for (const [key, entry] of Object.entries(value)) {
        cloned[key] = cloneSerializableInternal(entry, freeze);
    }

    return maybeFreeze(cloned, freeze);
};

export const cloneSerializable = <TValue>(
    value: TValue,
    options: CloneSerializableOptions = {}
): TValue => cloneSerializableInternal(value, options.freeze ?? false) as TValue;