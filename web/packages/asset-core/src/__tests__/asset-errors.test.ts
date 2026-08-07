import { describe, expect, it } from 'vitest';
import {
    DEFAULT_ASSET_MESSAGE_RESOLVER,
    resolveAssetMessage,
    AssetError,
    AssetConfigurationError,
    AssetConflictError,
    AssetDisposedError,
    AssetImportError,
    AssetImporterNotFoundError,
    AssetReferenceError,
    AssetDependencyError,
    AssetSnapshotError,
    AssetLifecycleError,
} from '@axrone/asset-core';

describe('Asset Error Hierarchy and Message Resolution', () => {
    describe('DEFAULT_ASSET_MESSAGE_RESOLVER', () => {
        it('resolves asset.invalid-id', () => {
            const message = DEFAULT_ASSET_MESSAGE_RESOLVER(
                { code: 'asset.invalid-id', value: 'bad-id' },
                'en-US'
            );
            expect(message).toBe('Invalid asset id: bad-id');
        });

        it('resolves asset.invalid-importer', () => {
            const message = DEFAULT_ASSET_MESSAGE_RESOLVER(
                { code: 'asset.invalid-importer', value: null },
                'en-US'
            );
            expect(message).toBe('Invalid asset importer: null');
        });

        it('resolves asset.invalid-key', () => {
            const message = DEFAULT_ASSET_MESSAGE_RESOLVER(
                { code: 'asset.invalid-key', value: 123 },
                'en-US'
            );
            expect(message).toBe('Invalid asset key: 123');
        });

        it('resolves asset.invalid-kind', () => {
            const message = DEFAULT_ASSET_MESSAGE_RESOLVER(
                { code: 'asset.invalid-kind', value: undefined },
                'en-US'
            );
            expect(message).toBe('Invalid asset kind: undefined');
        });

        it('resolves asset.invalid-revision', () => {
            const message = DEFAULT_ASSET_MESSAGE_RESOLVER(
                { code: 'asset.invalid-revision', value: -1 },
                'en-US'
            );
            expect(message).toBe('Invalid asset revision: -1');
        });

        it('resolves asset.invalid-source', () => {
            const message = DEFAULT_ASSET_MESSAGE_RESOLVER(
                { code: 'asset.invalid-source', value: {} },
                'en-US'
            );
            expect(message).toBe('Invalid asset source: {}');
        });

        it('resolves asset.invalid-source-identity', () => {
            const message = DEFAULT_ASSET_MESSAGE_RESOLVER(
                { code: 'asset.invalid-source-identity', value: '' },
                'en-US'
            );
            expect(message).toBe('Invalid asset source identity: ');
        });

        it('resolves asset.invalid-stage', () => {
            const message = DEFAULT_ASSET_MESSAGE_RESOLVER(
                { code: 'asset.invalid-stage', value: 'bad-stage' },
                'en-US'
            );
            expect(message).toBe('Invalid asset import stage: bad-stage');
        });

        it('resolves asset.conflict.key-bound', () => {
            const message = DEFAULT_ASSET_MESSAGE_RESOLVER(
                {
                    code: 'asset.conflict.key-bound',
                    key: 'test-key',
                    currentId: 'id-1',
                    requestedId: 'id-2',
                },
                'en-US'
            );
            expect(message).toBe(
                'Asset key "test-key" is already bound to id-1; requested id-2'
            );
        });

        it('resolves asset.conflict.kind-mismatch', () => {
            const message = DEFAULT_ASSET_MESSAGE_RESOLVER(
                {
                    code: 'asset.conflict.kind-mismatch',
                    id: 'test-id',
                    expected: 'texture',
                    received: 'mesh',
                },
                'en-US'
            );
            expect(message).toBe(
                'Asset test-id expected kind "texture" but received "mesh"'
            );
        });

        it('resolves asset.dependency.missing', () => {
            const message = DEFAULT_ASSET_MESSAGE_RESOLVER(
                { code: 'asset.dependency.missing', dependency: 'dep-id' },
                'en-US'
            );
            expect(message).toBe('Missing asset dependency: dep-id');
        });

        it('resolves asset.disposed', () => {
            const message = DEFAULT_ASSET_MESSAGE_RESOLVER(
                { code: 'asset.disposed' },
                'en-US'
            );
            expect(message).toBe('Asset database has been disposed');
        });

        it('resolves asset.import.failed', () => {
            const message = DEFAULT_ASSET_MESSAGE_RESOLVER(
                {
                    code: 'asset.import.failed',
                    importerId: 'test.importer',
                    attempt: 3,
                    reason: 'timeout',
                },
                'en-US'
            );
            expect(message).toBe(
                'Asset import failed in importer "test.importer" on attempt 3: timeout'
            );
        });

        it('resolves asset.importer.not-found', () => {
            const message = DEFAULT_ASSET_MESSAGE_RESOLVER(
                {
                    code: 'asset.importer.not-found',
                    sourceKind: 'text',
                    uri: 'test.txt',
                    mimeType: 'text/plain',
                },
                'en-US'
            );
            expect(message).toBe(
                'No asset importer found for source kind "text" (test.txt) with mime type "text/plain"'
            );
        });

        it('resolves asset.lifecycle.dispose-failed', () => {
            const message = DEFAULT_ASSET_MESSAGE_RESOLVER(
                {
                    code: 'asset.lifecycle.dispose-failed',
                    id: 'test-id',
                    kind: 'texture',
                    reason: 'cleanup error',
                },
                'en-US'
            );
            expect(message).toBe(
                'Failed to dispose asset test-id (texture): cleanup error'
            );
        });

        it('resolves asset.reference.invalid', () => {
            const message = DEFAULT_ASSET_MESSAGE_RESOLVER(
                { code: 'asset.reference.invalid', value: 'bad-ref' },
                'en-US'
            );
            expect(message).toBe('Invalid asset reference: bad-ref');
        });

        it('resolves asset.snapshot.invalid', () => {
            const message = DEFAULT_ASSET_MESSAGE_RESOLVER(
                { code: 'asset.snapshot.invalid', reason: 'bad snapshot' },
                'en-US'
            );
            expect(message).toBe('Invalid asset snapshot: bad snapshot');
        });

        it('returns undefined for unknown code', () => {
            const message = DEFAULT_ASSET_MESSAGE_RESOLVER(
                { code: 'asset.unknown' as any, value: 'test' },
                'en-US'
            );
            expect(message).toBeUndefined();
        });

        it('formats Error objects in formatUnknown', () => {
            const message = DEFAULT_ASSET_MESSAGE_RESOLVER(
                {
                    code: 'asset.import.failed',
                    importerId: 'test',
                    attempt: 1,
                    reason: new Error('test error'),
                },
                'en-US'
            );
            expect(message).toContain('test error');
        });

        it('formats JSON-serializable objects in formatUnknown', () => {
            const message = DEFAULT_ASSET_MESSAGE_RESOLVER(
                {
                    code: 'asset.import.failed',
                    importerId: 'test',
                    attempt: 1,
                    reason: { code: 'ERR_TIMEOUT', duration: 5000 },
                },
                'en-US'
            );
            expect(message).toContain('ERR_TIMEOUT');
        });
    });

    describe('resolveAssetMessage', () => {
        it('uses custom resolver when provided', () => {
            const customResolver = (descriptor: any) =>
                descriptor.code === 'asset.disposed' ? 'Custom disposed message' : undefined;

            const message = resolveAssetMessage({ code: 'asset.disposed' }, 'en-US', customResolver);
            expect(message).toBe('Custom disposed message');
        });

        it('falls back to default resolver when custom returns undefined', () => {
            const customResolver = () => undefined;
            const message = resolveAssetMessage({ code: 'asset.disposed' }, 'en-US', customResolver);
            expect(message).toBe('Asset database has been disposed');
        });

        it('falls back to descriptor.code when both resolvers return undefined', () => {
            const customResolver = () => undefined;
            const message = resolveAssetMessage(
                { code: 'asset.unknown' as any, value: 'test' },
                'en-US',
                customResolver
            );
            expect(message).toBe('asset.unknown');
        });

        it('works without custom resolver', () => {
            const message = resolveAssetMessage({ code: 'asset.disposed' }, 'en-US');
            expect(message).toBe('Asset database has been disposed');
        });
    });

    describe('Error class hierarchy', () => {
        it('AssetError is instanceof Error and has correct properties', () => {
            const error = new AssetError('TestError', 'asset.disposed', 'test message');
            expect(error).toBeInstanceOf(Error);
            expect(error).toBeInstanceOf(AssetError);
            expect(error.name).toBe('TestError');
            expect(error.code).toBe('asset.disposed');
            expect(error.message).toBe('test message');
        });

        it('AssetConfigurationError has correct name and code', () => {
            const error = new AssetConfigurationError('asset.invalid-id', 'invalid id');
            expect(error).toBeInstanceOf(AssetError);
            expect(error.name).toBe('AssetConfigurationError');
            expect(error.code).toBe('asset.invalid-id');
        });

        it('AssetConflictError has correct name and code', () => {
            const error = new AssetConflictError('asset.conflict.key-bound', 'key bound');
            expect(error).toBeInstanceOf(AssetError);
            expect(error.name).toBe('AssetConflictError');
            expect(error.code).toBe('asset.conflict.key-bound');
        });

        it('AssetDisposedError has correct name and code', () => {
            const error = new AssetDisposedError('disposed');
            expect(error).toBeInstanceOf(AssetError);
            expect(error.name).toBe('AssetDisposedError');
            expect(error.code).toBe('asset.disposed');
        });

        it('AssetImportError has importerId and attempt', () => {
            const error = new AssetImportError('import failed', 'test.importer', 3);
            expect(error).toBeInstanceOf(AssetError);
            expect(error.name).toBe('AssetImportError');
            expect(error.code).toBe('asset.import.failed');
            expect(error.importerId).toBe('test.importer');
            expect(error.attempt).toBe(3);
        });

        it('AssetImporterNotFoundError has correct name and code', () => {
            const error = new AssetImporterNotFoundError('not found');
            expect(error).toBeInstanceOf(AssetError);
            expect(error.name).toBe('AssetImporterNotFoundError');
            expect(error.code).toBe('asset.importer.not-found');
        });

        it('AssetReferenceError has correct name and code', () => {
            const error = new AssetReferenceError('invalid ref');
            expect(error).toBeInstanceOf(AssetError);
            expect(error.name).toBe('AssetReferenceError');
            expect(error.code).toBe('asset.reference.invalid');
        });

        it('AssetDependencyError has dependency property', () => {
            const error = new AssetDependencyError('missing dep', 'dep-id');
            expect(error).toBeInstanceOf(AssetError);
            expect(error.name).toBe('AssetDependencyError');
            expect(error.code).toBe('asset.dependency.missing');
            expect(error.dependency).toBe('dep-id');
        });

        it('AssetSnapshotError has correct name and code', () => {
            const error = new AssetSnapshotError('bad snapshot');
            expect(error).toBeInstanceOf(AssetError);
            expect(error.name).toBe('AssetSnapshotError');
            expect(error.code).toBe('asset.snapshot.invalid');
        });

        it('AssetLifecycleError has id and kind properties', () => {
            const error = new AssetLifecycleError('dispose failed', 'test-id', 'texture');
            expect(error).toBeInstanceOf(AssetError);
            expect(error.name).toBe('AssetLifecycleError');
            expect(error.code).toBe('asset.lifecycle.dispose-failed');
            expect(error.id).toBe('test-id');
            expect(error.kind).toBe('texture');
        });

        it('Error classes support cause option', () => {
            const cause = new Error('root cause');
            const error = new AssetImportError('failed', 'test', 1, { cause });
            expect(error.cause).toBe(cause);
        });
    });
});
