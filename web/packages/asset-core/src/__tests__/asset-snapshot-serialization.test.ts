import { describe, expect, it } from 'vitest';
import {
    isAssetJsonValue,
    isTypedArrayView,
    getBytes,
    isAssetBinaryValue,
    isAssetDatabaseSnapshot,
    serializeAssetSnapshotData,
    deserializeAssetSnapshotData,
    type AssetSnapshotSerializationContext,
    type AssetSchema,
    type AssetJsonValue,
    type AssetBinaryCodec,
} from '@axrone/asset-core';
import {
    isAssetJsonValue as isAssetJsonValueInternal,
    isTypedArrayView as isTypedArrayViewInternal,
    getBytes as getBytesInternal,
    isAssetBinaryValue as isAssetBinaryValueInternal,
    isAssetDatabaseSnapshot as isAssetDatabaseSnapshotInternal,
    serializeAssetSnapshotData as serializeAssetSnapshotDataInternal,
    deserializeAssetSnapshotData as deserializeAssetSnapshotDataInternal,
} from '../internal/snapshot-serialization';

interface TestSchema extends AssetSchema {
    text: string;
    json: { value: number };
    bytes: Uint8Array;
}

describe('Snapshot Serialization', () => {
    describe('isAssetJsonValue', () => {
        it('returns true for null', () => {
            expect(isAssetJsonValueInternal(null)).toBe(true);
        });

        it('returns true for string', () => {
            expect(isAssetJsonValueInternal('test')).toBe(true);
        });

        it('returns true for number', () => {
            expect(isAssetJsonValueInternal(42)).toBe(true);
        });

        it('returns true for boolean', () => {
            expect(isAssetJsonValueInternal(true)).toBe(true);
            expect(isAssetJsonValueInternal(false)).toBe(true);
        });

        it('returns true for nested arrays', () => {
            expect(isAssetJsonValueInternal([1, 'test', [true, null]])).toBe(true);
        });

        it('returns true for nested objects', () => {
            expect(isAssetJsonValueInternal({ a: 1, b: { c: 'test' } })).toBe(true);
        });

        it('returns false for undefined', () => {
            expect(isAssetJsonValueInternal(undefined)).toBe(false);
        });

        it('returns false for Date', () => {
            expect(isAssetJsonValueInternal(new Date())).toBe(false);
        });

        it('returns false for Uint8Array', () => {
            expect(isAssetJsonValueInternal(new Uint8Array([1, 2, 3]))).toBe(false);
        });

        it('returns false for function', () => {
            expect(isAssetJsonValueInternal(() => {})).toBe(false);
        });
    });

    describe('isTypedArrayView', () => {
        it('returns true for Uint8Array', () => {
            expect(isTypedArrayViewInternal(new Uint8Array([1, 2, 3]))).toBe(true);
        });

        it('returns true for Int32Array', () => {
            expect(isTypedArrayViewInternal(new Int32Array([1, 2, 3]))).toBe(true);
        });

        it('returns true for DataView', () => {
            const buffer = new ArrayBuffer(16);
            expect(isTypedArrayViewInternal(new DataView(buffer))).toBe(true);
        });

        it('returns false for ArrayBuffer', () => {
            expect(isTypedArrayViewInternal(new ArrayBuffer(16))).toBe(false);
        });

        it('returns false for plain objects', () => {
            expect(isTypedArrayViewInternal({})).toBe(false);
            expect(isTypedArrayViewInternal(null)).toBe(false);
        });
    });

    describe('getBytes', () => {
        it('returns Uint8Array as-is', () => {
            const bytes = new Uint8Array([1, 2, 3]);
            expect(getBytesInternal(bytes)).toBe(bytes);
        });

        it('wraps ArrayBuffer in Uint8Array', () => {
            const buffer = new Uint8Array([1, 2, 3]).buffer;
            const result = getBytesInternal(buffer);
            expect(result).toBeInstanceOf(Uint8Array);
            expect(Array.from(result)).toEqual([1, 2, 3]);
        });

        it('handles ArrayBufferView with offset and length', () => {
            const buffer = new Uint8Array([1, 2, 3, 4, 5]).buffer;
            const view = new Uint8Array(buffer, 1, 3);
            const result = getBytesInternal(view);
            expect(Array.from(result)).toEqual([2, 3, 4]);
        });
    });

    describe('isAssetBinaryValue', () => {
        it('returns true for valid inline binary', () => {
            expect(
                isAssetBinaryValueInternal({
                    __asset: 'axrone.binary',
                    storage: 'inline',
                    encoding: 'base64',
                    data: 'AQID',
                    byteLength: 3,
                })
            ).toBe(true);
        });

        it('returns true for valid external binary', () => {
            expect(
                isAssetBinaryValueInternal({
                    __asset: 'axrone.binary',
                    storage: 'external',
                    storageKey: 'test-key',
                    byteLength: 100,
                })
            ).toBe(true);
        });

        it('returns false for missing __asset field', () => {
            expect(
                isAssetBinaryValueInternal({
                    storage: 'inline',
                    encoding: 'base64',
                    data: 'AQID',
                    byteLength: 3,
                })
            ).toBe(false);
        });

        it('returns false for wrong __asset value', () => {
            expect(
                isAssetBinaryValueInternal({
                    __asset: 'wrong',
                    storage: 'inline',
                    encoding: 'base64',
                    data: 'AQID',
                    byteLength: 3,
                })
            ).toBe(false);
        });

        it('returns false for negative byteLength', () => {
            expect(
                isAssetBinaryValueInternal({
                    __asset: 'axrone.binary',
                    storage: 'inline',
                    encoding: 'base64',
                    data: 'AQID',
                    byteLength: -1,
                })
            ).toBe(false);
        });

        it('returns false for non-integer byteLength', () => {
            expect(
                isAssetBinaryValueInternal({
                    __asset: 'axrone.binary',
                    storage: 'inline',
                    encoding: 'base64',
                    data: 'AQID',
                    byteLength: 3.5,
                })
            ).toBe(false);
        });
    });

    describe('isAssetDatabaseSnapshot', () => {
        it('returns true for version 4', () => {
            expect(
                isAssetDatabaseSnapshotInternal({
                    version: 4,
                    assets: [],
                })
            ).toBe(true);
        });

        it('returns true for legacy versions 1, 2, 3', () => {
            expect(
                isAssetDatabaseSnapshotInternal({
                    version: 1,
                    assets: [],
                })
            ).toBe(true);
            expect(
                isAssetDatabaseSnapshotInternal({
                    version: 2,
                    assets: [],
                })
            ).toBe(true);
            expect(
                isAssetDatabaseSnapshotInternal({
                    version: 3,
                    assets: [],
                })
            ).toBe(true);
        });

        it('returns false for invalid version', () => {
            expect(
                isAssetDatabaseSnapshotInternal({
                    version: 99,
                    assets: [],
                })
            ).toBe(false);
        });

        it('returns false for missing assets array', () => {
            expect(
                isAssetDatabaseSnapshotInternal({
                    version: 4,
                })
            ).toBe(false);
        });

        it('returns false for null', () => {
            expect(isAssetDatabaseSnapshotInternal(null)).toBe(false);
        });
    });

    describe('serializeAssetSnapshotData', () => {
        const createContext = (
            overrides: Partial<AssetSnapshotSerializationContext<TestSchema>> = {}
        ): AssetSnapshotSerializationContext<TestSchema> => ({
            locale: 'en-US',
            codecs: {},
            binary: { mode: 'inline', inlineThresholdBytes: 1024 },
            ...overrides,
        });

        const createDescriptor = (data: any) => ({
            kind: 'text' as const,
            id: 'test-id',
            key: 'test-key' as any,
            data,
            revision: 1,
            fingerprint: 'fp:test',
            metadata: {
                tags: [] as string[],
                properties: {} as Record<string, AssetJsonValue>,
            },
        });

        it('serializes JSON data without codec', () => {
            const context = createContext();
            const descriptor = createDescriptor({ value: 42 });
            descriptor.kind = 'json';

            const result = serializeAssetSnapshotDataInternal(descriptor, context);
            expect(result).toEqual({ value: 42 });
        });

        it('uses JSON codec when available', () => {
            const codec = {
                format: 'json' as const,
                serialize: (data: { value: number }) => ({ serialized: data.value }),
                deserialize: (data: any) => ({ value: data.serialized }),
            };

            const context = createContext({ codecs: { json: codec } });
            const descriptor = createDescriptor({ value: 42 });
            descriptor.kind = 'json';

            const result = serializeAssetSnapshotDataInternal(descriptor, context);
            expect(result).toEqual({ serialized: 42 });
        });

        it('serializes binary data inline when small', () => {
            const context = createContext();
            const descriptor = createDescriptor(new Uint8Array([1, 2, 3]));
            descriptor.kind = 'bytes';

            const result = serializeAssetSnapshotDataInternal(descriptor, context);
            expect(result).toEqual({
                __asset: 'axrone.binary',
                storage: 'inline',
                encoding: 'base64',
                data: 'AQID',
                byteLength: 3,
            });
        });

        it('uses binary codec when available', () => {
            const codec: AssetBinaryCodec<Uint8Array> = {
                format: 'binary',
                serialize: (data) => data,
                deserialize: (data) => data,
            };

            const context = createContext({ codecs: { bytes: codec } });
            const descriptor = createDescriptor(new Uint8Array([1, 2, 3]));
            descriptor.kind = 'bytes';

            const result = serializeAssetSnapshotDataInternal(descriptor, context);
            expect(result).toEqual({
                __asset: 'axrone.binary',
                storage: 'inline',
                encoding: 'base64',
                data: 'AQID',
                byteLength: 3,
            });
        });
    });

    describe('deserializeAssetSnapshotData', () => {
        const createContext = (
            overrides: Partial<AssetSnapshotSerializationContext<TestSchema>> = {}
        ): AssetSnapshotSerializationContext<TestSchema> => ({
            locale: 'en-US',
            codecs: {},
            binary: { mode: 'inline', inlineThresholdBytes: 1024 },
            ...overrides,
        });

        const createEntry = (data: any) => ({
            kind: 'text' as const,
            id: 'test-id',
            key: 'test-key',
            aliases: [] as string[],
            name: 'Test',
            revision: 1,
            fingerprint: 'fp:test',
            createdAtEpochMs: 1000,
            updatedAtEpochMs: 2000,
            metadata: {
                tags: [] as string[],
                properties: {} as Record<string, AssetJsonValue>,
            },
            dependencyIds: [] as string[],
            data,
        });

        const createDescriptor = () => ({
            kind: 'text' as const,
            id: 'test-id',
            key: 'test-key' as any,
            revision: 1,
            fingerprint: 'fp:test',
            metadata: {
                tags: [] as string[],
                properties: {} as Record<string, AssetJsonValue>,
            },
        });

        it('deserializes JSON data without codec', () => {
            const context = createContext();
            const entry = createEntry({ value: 42 });
            entry.kind = 'json';
            const descriptor = createDescriptor();
            descriptor.kind = 'json';

            const result = deserializeAssetSnapshotDataInternal(entry, 'json', descriptor, context);
            expect(result).toEqual({ value: 42 });
        });

        it('uses JSON codec when available', () => {
            const codec = {
                format: 'json' as const,
                serialize: (data: { value: number }) => ({ serialized: data.value }),
                deserialize: (data: any) => ({ value: data.serialized }),
            };

            const context = createContext({ codecs: { json: codec } });
            const entry = createEntry({ serialized: 42 });
            entry.kind = 'json';
            const descriptor = createDescriptor();
            descriptor.kind = 'json';

            const result = deserializeAssetSnapshotDataInternal(entry, 'json', descriptor, context);
            expect(result).toEqual({ value: 42 });
        });

        it('deserializes inline binary without codec', () => {
            const context = createContext();
            const entry = createEntry({
                __asset: 'axrone.binary',
                storage: 'inline',
                encoding: 'base64',
                data: 'AQID',
                byteLength: 3,
            });
            entry.kind = 'bytes';
            const descriptor = createDescriptor();
            descriptor.kind = 'bytes';

            const result = deserializeAssetSnapshotDataInternal(
                entry,
                'bytes',
                descriptor,
                context
            );
            expect(result).toBeInstanceOf(Uint8Array);
            expect(Array.from(result as Uint8Array)).toEqual([1, 2, 3]);
        });

        it('uses binary codec when available', () => {
            const codec: AssetBinaryCodec<Uint8Array> = {
                format: 'binary',
                serialize: (data) => data,
                deserialize: (data) => data,
            };

            const context = createContext({ codecs: { bytes: codec } });
            const entry = createEntry({
                __asset: 'axrone.binary',
                storage: 'inline',
                encoding: 'base64',
                data: 'AQID',
                byteLength: 3,
            });
            entry.kind = 'bytes';
            const descriptor = createDescriptor();
            descriptor.kind = 'bytes';

            const result = deserializeAssetSnapshotDataInternal(
                entry,
                'bytes',
                descriptor,
                context
            );
            expect(result).toBeInstanceOf(Uint8Array);
            expect(Array.from(result as Uint8Array)).toEqual([1, 2, 3]);
        });
    });
});
