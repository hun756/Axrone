import { describe, expect, it } from 'vitest';
import {
    uniqueStrings,
    normalizeMetadata,
    stableStringify,
    computeFingerprint,
    inferAssetName,
    StoredAsset,
} from '../internal/stored-asset';
import { asAssetId, asAssetKey, asAssetFingerprint, asAssetRevision } from '../reference';

describe('Stored Asset Utilities', () => {
    describe('uniqueStrings', () => {
        it('returns frozen empty array for empty input', () => {
            const result = uniqueStrings([]);
            expect(result).toEqual([]);
            expect(Object.isFrozen(result)).toBe(true);
        });

        it('removes duplicates', () => {
            const result = uniqueStrings(['a', 'b', 'a', 'c', 'b']);
            expect(result).toEqual(['a', 'b', 'c']);
        });

        it('skips whitespace-only entries', () => {
            const result = uniqueStrings(['a', '  ', 'b', '\t', 'c']);
            expect(result).toEqual(['a', 'b', 'c']);
        });

        it('trims entries', () => {
            const result = uniqueStrings(['  a  ', '  b  ']);
            expect(result).toEqual(['a', 'b']);
        });

        it('returns frozen array', () => {
            const result = uniqueStrings(['a', 'b']);
            expect(Object.isFrozen(result)).toBe(true);
        });
    });

    describe('normalizeMetadata', () => {
        it('returns defaults for undefined input', () => {
            const result = normalizeMetadata(undefined);
            expect(result).toEqual({
                uri: undefined,
                mimeType: undefined,
                locale: undefined,
                tags: [],
                properties: {},
            });
            expect(Object.isFrozen(result)).toBe(true);
        });

        it('normalizes URI', () => {
            const result = normalizeMetadata({ uri: 'assets\\file.txt' });
            expect(result.uri).toBe('assets/file.txt');
        });

        it('trims mimeType', () => {
            const result = normalizeMetadata({ mimeType: '  text/plain  ' });
            expect(result.mimeType).toBe('text/plain');
        });

        it('returns undefined for empty mimeType', () => {
            const result = normalizeMetadata({ mimeType: '   ' });
            expect(result.mimeType).toBeUndefined();
        });

        it('canonicalizes locale', () => {
            const result = normalizeMetadata({ locale: 'en-us' });
            expect(result.locale).toBe('en-US');
        });

        it('deduplicates tags', () => {
            const result = normalizeMetadata({ tags: ['ui', 'hero', 'ui'] });
            expect(result.tags).toEqual(['ui', 'hero']);
        });

        it('sorts property keys', () => {
            const result = normalizeMetadata({
                properties: { z: 'last', a: 'first', m: 'middle' },
            });
            expect(Object.keys(result.properties)).toEqual(['a', 'm', 'z']);
        });
    });

    describe('stableStringify', () => {
        it('handles null', () => {
            expect(stableStringify(null)).toBe('null');
        });

        it('handles strings', () => {
            expect(stableStringify('test')).toBe('"test"');
        });

        it('handles numbers', () => {
            expect(stableStringify(42)).toBe('42');
        });

        it('handles NaN', () => {
            expect(stableStringify(NaN)).toBe('"NaN"');
        });

        it('handles booleans', () => {
            expect(stableStringify(true)).toBe('true');
            expect(stableStringify(false)).toBe('false');
        });

        it('handles undefined', () => {
            expect(stableStringify(undefined)).toBe('"undefined"');
        });

        it('handles bigint', () => {
            expect(stableStringify(BigInt(123))).toBe('"123n"');
        });

        it('handles symbol', () => {
            expect(stableStringify(Symbol('test'))).toBe('"Symbol(test)"');
        });

        it('handles named function', () => {
            function myFunc() {}
            expect(stableStringify(myFunc)).toBe('"myFunc"');
        });

        it('handles anonymous function', () => {
            expect(stableStringify(() => {})).toContain('anonymous');
        });

        it('handles Date', () => {
            const date = new Date('2024-01-01T00:00:00Z');
            expect(stableStringify(date)).toBe('"2024-01-01T00:00:00.000Z"');
        });

        it('handles ArrayBuffer', () => {
            const buffer = new Uint8Array([1, 2, 3]).buffer;
            const result = stableStringify(buffer);
            expect(result).toContain('01');
            expect(result).toContain('02');
            expect(result).toContain('03');
        });

        it('handles Uint8Array', () => {
            const bytes = new Uint8Array([10, 20, 30]);
            const result = stableStringify(bytes);
            expect(result).toContain('0a');
            expect(result).toContain('14');
            expect(result).toContain('1e');
        });

        it('handles arrays', () => {
            expect(stableStringify([1, 2, 3])).toBe('[1,2,3]');
        });

        it('sorts object keys', () => {
            const result = stableStringify({ z: 1, a: 2, m: 3 });
            expect(result).toBe('{"a":2,"m":3,"z":1}');
        });

        it('handles nested objects', () => {
            const result = stableStringify({ b: { d: 4, c: 3 }, a: 1 });
            expect(result).toBe('{"a":1,"b":{"c":3,"d":4}}');
        });

        it('handles circular references', () => {
            const obj: any = { a: 1 };
            obj.self = obj;
            const result = stableStringify(obj);
            expect(result).toContain('[Circular]');
        });

        it('invokes toJSON', () => {
            const obj = {
                toJSON: () => ({ serialized: true }),
            };
            expect(stableStringify(obj)).toBe('{"serialized":true}');
        });
    });

    describe('computeFingerprint', () => {
        it('returns deterministic output for same inputs', () => {
            const metadata = normalizeMetadata({ uri: 'test.txt' });
            const fp1 = computeFingerprint('text', asAssetKey('test'), 'data', metadata);
            const fp2 = computeFingerprint('text', asAssetKey('test'), 'data', metadata);
            expect(fp1).toBe(fp2);
        });

        it('returns different output for different kind', () => {
            const metadata = normalizeMetadata();
            const fp1 = computeFingerprint('text', asAssetKey('test'), 'data', metadata);
            const fp2 = computeFingerprint('mesh', asAssetKey('test'), 'data', metadata);
            expect(fp1).not.toBe(fp2);
        });

        it('returns different output for different key', () => {
            const metadata = normalizeMetadata();
            const fp1 = computeFingerprint('text', asAssetKey('test1'), 'data', metadata);
            const fp2 = computeFingerprint('text', asAssetKey('test2'), 'data', metadata);
            expect(fp1).not.toBe(fp2);
        });

        it('returns different output for different data', () => {
            const metadata = normalizeMetadata();
            const fp1 = computeFingerprint('text', asAssetKey('test'), 'data1', metadata);
            const fp2 = computeFingerprint('text', asAssetKey('test'), 'data2', metadata);
            expect(fp1).not.toBe(fp2);
        });

        it('returns different output for different metadata', () => {
            const metadata1 = normalizeMetadata({ uri: 'test1.txt' });
            const metadata2 = normalizeMetadata({ uri: 'test2.txt' });
            const fp1 = computeFingerprint('text', asAssetKey('test'), 'data', metadata1);
            const fp2 = computeFingerprint('text', asAssetKey('test'), 'data', metadata2);
            expect(fp1).not.toBe(fp2);
        });

        it('returns fingerprint with fp: prefix', () => {
            const metadata = normalizeMetadata();
            const fp = computeFingerprint('text', asAssetKey('test'), 'data', metadata);
            expect(fp).toMatch(/^fp:/);
        });
    });

    describe('inferAssetName', () => {
        it('uses explicit name when provided', () => {
            expect(inferAssetName('text', asAssetKey('test'), 'Custom Name')).toBe('Custom Name');
        });

        it('trims explicit name', () => {
            expect(inferAssetName('text', asAssetKey('test'), '  Custom Name  ')).toBe(
                'Custom Name'
            );
        });

        it('falls back to last path segment', () => {
            expect(inferAssetName('text', asAssetKey('assets/folder/file.txt'))).toBe('file.txt');
        });

        it('removes fragment from key', () => {
            expect(inferAssetName('text', asAssetKey('assets/file.txt#section'))).toBe(
                'file.txt'
            );
        });

        it('falls back to kind for bare keys', () => {
            expect(inferAssetName('texture', asAssetKey('texture'))).toBe('texture');
        });
    });

    describe('StoredAsset', () => {
        const createTestAsset = () =>
            new StoredAsset({
                id: 'test-id',
                kind: 'text',
                key: asAssetKey('test-key'),
                aliases: [asAssetKey('alias-1')],
                name: 'Test Asset',
                data: 'test data',
                revision: 1,
                fingerprint: 'fp:test',
                createdAtEpochMs: 1000,
                updatedAtEpochMs: 2000,
                metadata: normalizeMetadata({ uri: 'test.txt' }),
                dependencyIds: ['dep-1', 'dep-2'],
            });

        it('creates asset with correct properties', () => {
            const asset = createTestAsset();
            expect(asset.id).toBe('test-id');
            expect(asset.kind).toBe('text');
            expect(asset.key).toBe('test-key');
            expect(asset.aliases).toEqual(['alias-1']);
            expect(asset.name).toBe('Test Asset');
            expect(asset.data).toBe('test data');
            expect(asset.revision).toBe(1);
            expect(asset.fingerprint).toBe('fp:test');
            expect(asset.createdAtEpochMs).toBe(1000);
            expect(asset.updatedAtEpochMs).toBe(2000);
            expect(asset.dependencyIds).toEqual(['dep-1', 'dep-2']);
        });

        it('lazily creates reference', () => {
            const asset = createTestAsset();
            const ref1 = asset.reference;
            const ref2 = asset.reference;
            expect(ref1).toBe(ref2);
            expect(ref1.kind).toBe('text');
            expect(ref1.id).toBe('test-id');
            expect(ref1.token).toBe('asset:text:test-id');
        });

        it('lazily creates versioned reference', () => {
            const asset = createTestAsset();
            const ref1 = asset.versionedReference;
            const ref2 = asset.versionedReference;
            expect(ref1).toBe(ref2);
            expect(ref1.revision).toBe(1);
            expect(ref1.versionedToken).toBe('asset:text:test-id@1');
        });

        it('toRecord returns frozen record', () => {
            const asset = createTestAsset();
            const record1 = asset.toRecord();
            const record2 = asset.toRecord();
            expect(record1).toBe(record2);
            expect(Object.isFrozen(record1)).toBe(true);
            expect(record1.kind).toBe('text');
            expect(record1.id).toBe('test-id');
            expect(record1.reference).toBe(asset.reference);
            expect(record1.versionedReference).toBe(asset.versionedReference);
        });
    });
});
