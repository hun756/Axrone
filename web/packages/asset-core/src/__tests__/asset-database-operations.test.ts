import { describe, expect, it, vi } from 'vitest';
import {
    AssetDatabase,
    AssetDisposedError,
    AssetReferenceError,
    AssetDependencyError,
    AssetSnapshotError,
    type AssetImporter,
    type AssetSchema,
} from '@axrone/asset-core';

interface TestSchema extends AssetSchema {
    text: string;
    bundle: { entry: string };
}

const textImporter: AssetImporter<TestSchema, { kind: 'text'; data: string; uri?: string }, 'text'> = {
    id: 'test.text',
    sourceKinds: ['text'],
    import: ({ source }) => ({
        primary: {
            kind: 'text',
            data: source.data,
        },
    }),
};

const bundleImporter: AssetImporter<
    TestSchema,
    { kind: 'text'; data: { text: string }; uri?: string },
    'bundle'
> = {
    id: 'test.bundle',
    sourceKinds: ['text'],
    import: ({ source }) => ({
        primary: {
            kind: 'bundle',
            data: { entry: 'text' },
            dependencies: [{ key: '#text', kind: 'text' }],
        },
        additional: [
            {
                kind: 'text',
                stableKey: '#text',
                data: source.data.text,
            },
        ],
    }),
};

describe('Asset Database Operations', () => {
    describe('list', () => {
        it('returns all assets when no kind filter', () => {
            const database = new AssetDatabase<TestSchema>();
            database.upsert({ kind: 'text', stableKey: 'test1.txt', data: 'first' });
            database.upsert({ kind: 'text', stableKey: 'test2.txt', data: 'second' });

            const list = database.list();
            expect(list).toHaveLength(2);
        });

        it('returns only matching kind when filter provided', () => {
            const database = new AssetDatabase<TestSchema>({
                importers: [bundleImporter],
            });
            database.upsert({ kind: 'text', stableKey: 'test.txt', data: 'text' });
            database.upsert({ kind: 'text', stableKey: 'bundle.txt', data: 'bundle' });

            const textList = database.list('text');
            expect(textList).toHaveLength(2);
        });

        it('returns frozen array', () => {
            const database = new AssetDatabase<TestSchema>();
            const list = database.list();
            expect(Object.isFrozen(list)).toBe(true);
        });
    });

    describe('has', () => {
        it('returns true for existing selector', () => {
            const database = new AssetDatabase<TestSchema>();
            const record = database.upsert({
                kind: 'text',
                stableKey: 'test.txt',
                data: 'hello',
            });

            expect(database.has(record.reference)).toBe(true);
            expect(database.has('test.txt')).toBe(true);
        });

        it('returns false for missing selector', () => {
            const database = new AssetDatabase<TestSchema>();
            expect(database.has('missing.txt')).toBe(false);
        });
    });

    describe('require', () => {
        it('returns record for existing selector', () => {
            const database = new AssetDatabase<TestSchema>();
            const created = database.upsert({
                kind: 'text',
                stableKey: 'test.txt',
                data: 'hello',
            });

            const record = database.require(created.reference);
            expect(record.id).toBe(created.id);
            expect(record.data).toBe('hello');
        });

        it('throws AssetReferenceError for missing selector', () => {
            const database = new AssetDatabase<TestSchema>();
            expect(() => database.require('missing.txt')).toThrow(AssetReferenceError);
        });
    });

    describe('reference and versionedReference', () => {
        it('returns correct reference objects', () => {
            const database = new AssetDatabase<TestSchema>();
            const record = database.upsert({
                kind: 'text',
                stableKey: 'test.txt',
                data: 'hello',
            });

            const ref = database.reference(record.id);
            expect(ref.kind).toBe('text');
            expect(ref.id).toBe(record.id);
            expect(ref.token).toBe(`asset:text:${record.id}`);

            const versionedRef = database.versionedReference(record.id);
            expect(versionedRef.revision).toBe(1);
            expect(versionedRef.versionedToken).toBe(`asset:text:${record.id}@1`);
        });
    });

    describe('getDependencies', () => {
        it('returns dependency records', async () => {
            const database = new AssetDatabase<TestSchema>({
                importers: [bundleImporter],
            });

            const receipt = await database.import({
                kind: 'text',
                data: { text: 'dependency' },
                uri: 'bundle.txt',
            });

            const deps = database.getDependencies(receipt.primary.reference);
            expect(deps).toHaveLength(1);
            expect(deps[0].kind).toBe('text');
            expect(deps[0].data).toBe('dependency');
        });

        it('returns empty when no dependencies', () => {
            const database = new AssetDatabase<TestSchema>();
            const record = database.upsert({
                kind: 'text',
                stableKey: 'test.txt',
                data: 'hello',
            });

            const deps = database.getDependencies(record.reference);
            expect(deps).toHaveLength(0);
        });
    });

    describe('getDependents', () => {
        it('returns dependent records after dependency linking', async () => {
            const database = new AssetDatabase<TestSchema>({
                importers: [bundleImporter],
            });

            const receipt = await database.import({
                kind: 'text',
                data: { text: 'dependency' },
                uri: 'bundle.txt',
            });

            const textRecord = database.list('text')[0];
            const dependents = database.getDependents(textRecord.reference);
            expect(dependents).toHaveLength(1);
            expect(dependents[0].kind).toBe('bundle');
        });

        it('returns empty when no dependents', () => {
            const database = new AssetDatabase<TestSchema>();
            const record = database.upsert({
                kind: 'text',
                stableKey: 'test.txt',
                data: 'hello',
            });

            const dependents = database.getDependents(record.reference);
            expect(dependents).toHaveLength(0);
        });
    });

    describe('delete', () => {
        it('returns true on success', () => {
            const database = new AssetDatabase<TestSchema>();
            const record = database.upsert({
                kind: 'text',
                stableKey: 'test.txt',
                data: 'hello',
            });

            expect(database.delete(record.reference)).toBe(true);
            expect(database.has(record.reference)).toBe(false);
        });

        it('returns false for missing selector', () => {
            const database = new AssetDatabase<TestSchema>();
            expect(database.delete('missing.txt')).toBe(false);
        });

        it('throws AssetDependencyError when dependents exist and cascade is false', async () => {
            const database = new AssetDatabase<TestSchema>({
                importers: [bundleImporter],
            });

            await database.import({
                kind: 'text',
                data: { text: 'dependency' },
                uri: 'bundle.txt',
            });

            const textRecord = database.list('text')[0];
            expect(() => database.delete(textRecord.reference)).toThrow(AssetDependencyError);
        });

        it('cascade delete removes dependents too', async () => {
            const database = new AssetDatabase<TestSchema>({
                importers: [bundleImporter],
            });

            await database.import({
                kind: 'text',
                data: { text: 'dependency' },
                uri: 'bundle.txt',
            });

            const textRecord = database.list('text')[0];
            const bundleRecord = database.list('bundle')[0];

            expect(database.delete(textRecord.reference, { cascade: true })).toBe(true);
            expect(database.has(textRecord.reference)).toBe(false);
            expect(database.has(bundleRecord.reference)).toBe(false);
        });
    });

    describe('clear', () => {
        it('removes all assets', () => {
            const database = new AssetDatabase<TestSchema>();
            database.upsert({ kind: 'text', stableKey: 'test1.txt', data: 'first' });
            database.upsert({ kind: 'text', stableKey: 'test2.txt', data: 'second' });

            expect(database.size).toBe(2);
            database.clear();
            expect(database.size).toBe(0);
        });

        it('is no-op on empty database', () => {
            const database = new AssetDatabase<TestSchema>();
            database.clear();
            expect(database.size).toBe(0);
        });
    });

    describe('upsertMany', () => {
        it('inserts multiple assets', () => {
            const database = new AssetDatabase<TestSchema>();
            const records = database.upsertMany([
                { kind: 'text', stableKey: 'test1.txt', data: 'first' },
                { kind: 'text', stableKey: 'test2.txt', data: 'second' },
                { kind: 'text', stableKey: 'test3.txt', data: 'third' },
            ]);

            expect(records).toHaveLength(3);
            expect(database.size).toBe(3);
        });
    });

    describe('importMany', () => {
        it('imports multiple sources', async () => {
            const database = new AssetDatabase<TestSchema>({
                importers: [textImporter],
            });

            const receipts = await database.importMany([
                { kind: 'text', data: 'first', uri: 'test1.txt' },
                { kind: 'text', data: 'second', uri: 'test2.txt' },
            ]);

            expect(receipts).toHaveLength(2);
            expect(database.size).toBe(2);
        });

        it('returns empty array for empty sources', async () => {
            const database = new AssetDatabase<TestSchema>({
                importers: [textImporter],
            });

            const receipts = await database.importMany([]);
            expect(receipts).toHaveLength(0);
        });
    });

    describe('Disposed database', () => {
        it('mutation methods throw AssetDisposedError after dispose', () => {
            const database = new AssetDatabase<TestSchema>();
            database.dispose();

            expect(() => database.list()).toThrow(AssetDisposedError);
            expect(() => database.upsert({ kind: 'text', data: 'test' })).toThrow(
                AssetDisposedError
            );
            expect(() => database.delete('test')).toThrow(AssetDisposedError);
            expect(() => database.clear()).toThrow(AssetDisposedError);
            expect(() => database.snapshot()).toThrow(AssetDisposedError);
            expect(() => database.subscribe(() => { })).toThrow(AssetDisposedError);
        });

        it('query methods return empty/undefined after dispose', () => {
            const database = new AssetDatabase<TestSchema>();
            database.dispose();

            expect(database.has('test')).toBe(false);
            expect(database.get('test')).toBeUndefined();
        });

        it('double dispose is no-op', () => {
            const database = new AssetDatabase<TestSchema>();
            database.dispose();
            database.dispose();
            expect(database.isDisposed).toBe(true);
        });

        it('isDisposed flag is correct', () => {
            const database = new AssetDatabase<TestSchema>();
            expect(database.isDisposed).toBe(false);
            database.dispose();
            expect(database.isDisposed).toBe(true);
        });
    });

    describe('registerImporter / unregisterImporter / listImporters', () => {
        it('registers and lists importers', () => {
            const database = new AssetDatabase<TestSchema>();
            database.registerImporter(textImporter);

            const importers = database.listImporters();
            expect(importers).toContain(textImporter);
        });

        it('unregisters importer', () => {
            const database = new AssetDatabase<TestSchema>();
            database.registerImporter(textImporter);
            expect(database.unregisterImporter('test.text')).toBe(true);
            expect(database.listImporters()).not.toContain(textImporter);
        });
    });

    describe('registerStage / unregisterStage / listStages', () => {
        it('registers and lists stages', () => {
            const database = new AssetDatabase<TestSchema>();
            const stage = {
                id: 'test.stage',
                run: async () => ({}),
            };

            database.registerStage(stage);
            const stages = database.listStages();
            expect(stages).toContain(stage);
        });

        it('unregisters stage', () => {
            const database = new AssetDatabase<TestSchema>();
            const stage = {
                id: 'test.stage',
                run: async () => ({}),
            };

            database.registerStage(stage);
            expect(database.unregisterStage('test.stage')).toBe(true);
            expect(database.listStages()).not.toContain(stage);
        });
    });

    describe('bindSourceIdentity / unbindSourceIdentity', () => {
        it('manually binds and unbinds source identity', () => {
            const database = new AssetDatabase<TestSchema>();
            const record = database.upsert({
                kind: 'text',
                stableKey: 'test.txt',
                data: 'hello',
            });

            database.bindSourceIdentity('source:test', record.reference);
            expect(database.resolveSourceIdentity('source:test')?.id).toBe(record.id);

            expect(database.unbindSourceIdentity('source:test')).toBe(true);
            expect(database.resolveSourceIdentity('source:test')).toBeUndefined();
        });
    });

    describe('hydrate error paths', () => {
        it('throws for invalid snapshot shape', () => {
            const database = new AssetDatabase<TestSchema>();
            expect(() => database.hydrate({} as any)).toThrow(AssetSnapshotError);
        });

        it('throws for history mismatch', () => {
            const database = new AssetDatabase<TestSchema>();
            const snapshot = {
                version: 4 as const,
                locale: 'en-US',
                capturedAtEpochMs: Date.now(),
                assets: [
                    {
                        kind: 'text',
                        id: 'test-id',
                        key: 'test.txt',
                        aliases: [],
                        name: 'Test',
                        revision: 2,
                        fingerprint: 'fp:test',
                        createdAtEpochMs: 1000,
                        updatedAtEpochMs: 2000,
                        metadata: {
                            tags: [],
                            properties: {},
                        },
                        dependencyIds: [],
                        data: 'current',
                        history: [
                            {
                                kind: 'text',
                                id: 'different-id',
                                key: 'test.txt',
                                aliases: [],
                                name: 'Test',
                                revision: 1,
                                fingerprint: 'fp:test',
                                createdAtEpochMs: 1000,
                                updatedAtEpochMs: 1500,
                                metadata: {
                                    tags: [],
                                    properties: {},
                                },
                                dependencyIds: [],
                                data: 'old',
                            },
                        ],
                    },
                ],
            };

            expect(() => database.hydrate(snapshot)).toThrow(AssetSnapshotError);
        });

        it('throws for non-monotonic revision', () => {
            const database = new AssetDatabase<TestSchema>();
            const snapshot = {
                version: 4 as const,
                locale: 'en-US',
                capturedAtEpochMs: Date.now(),
                assets: [
                    {
                        kind: 'text',
                        id: 'test-id',
                        key: 'test.txt',
                        aliases: [],
                        name: 'Test',
                        revision: 2,
                        fingerprint: 'fp:test',
                        createdAtEpochMs: 1000,
                        updatedAtEpochMs: 2000,
                        metadata: {
                            tags: [],
                            properties: {},
                        },
                        dependencyIds: [],
                        data: 'current',
                        history: [
                            {
                                kind: 'text',
                                id: 'test-id',
                                key: 'test.txt',
                                aliases: [],
                                name: 'Test',
                                revision: 5,
                                fingerprint: 'fp:test',
                                createdAtEpochMs: 1000,
                                updatedAtEpochMs: 1500,
                                metadata: {
                                    tags: [],
                                    properties: {},
                                },
                                dependencyIds: [],
                                data: 'old',
                            },
                        ],
                    },
                ],
            };

            expect(() => database.hydrate(snapshot)).toThrow(AssetSnapshotError);
        });
    });
});
