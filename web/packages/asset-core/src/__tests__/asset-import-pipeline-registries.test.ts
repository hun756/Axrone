import { describe, expect, it } from 'vitest';
import {
    AssetImporterRegistry,
    isAssetImporter,
    AssetConfigurationError,
    AssetDisposedError,
    type AssetImporter,
    type AssetImportStage,
    type AssetSchema,
} from '@axrone/asset-core';
import {
    AssetImportStageRegistry,
    isAssetImportStage,
    matchesStage,
} from '../internal/import-pipeline-registries';

interface TestSchema extends AssetSchema {
    text: string;
    json: { value: number };
}

const createMockImporter = (
    id: string,
    options: Partial<AssetImporter<TestSchema>> = {}
): AssetImporter<TestSchema> => ({
    id,
    import: async () => ({ primary: { kind: 'text', data: 'test' } }),
    ...options,
});

const createMockStage = (
    id: string,
    options: Partial<AssetImportStage<TestSchema>> = {}
): AssetImportStage<TestSchema> => ({
    id,
    run: async () => ({}),
    ...options,
});

describe('Import Pipeline Registries', () => {
    describe('isAssetImporter', () => {
        it('returns true for valid importer', () => {
            const importer = createMockImporter('test');
            expect(isAssetImporter(importer)).toBe(true);
        });

        it('returns false for null', () => {
            expect(isAssetImporter(null)).toBe(false);
        });

        it('returns false for missing id', () => {
            expect(isAssetImporter({ import: async () => ({ primary: { kind: 'text', data: 'test' } }) })).toBe(false);
        });

        it('returns false for missing import function', () => {
            expect(isAssetImporter({ id: 'test' })).toBe(false);
        });

        it('returns false for primitives', () => {
            expect(isAssetImporter('string')).toBe(false);
            expect(isAssetImporter(123)).toBe(false);
        });
    });

    describe('isAssetImportStage', () => {
        it('returns true for valid stage', () => {
            const stage = createMockStage('test');
            expect(isAssetImportStage(stage)).toBe(true);
        });

        it('returns false for null', () => {
            expect(isAssetImportStage(null)).toBe(false);
        });

        it('returns false for missing id', () => {
            expect(isAssetImportStage({ run: async () => ({}) })).toBe(false);
        });

        it('returns false for missing run function', () => {
            expect(isAssetImportStage({ id: 'test' })).toBe(false);
        });
    });

    describe('AssetImporterRegistry', () => {
        describe('register', () => {
            it('registers valid importer', () => {
                const registry = new AssetImporterRegistry<TestSchema>();
                const importer = createMockImporter('test');
                registry.register(importer);
                expect(registry.list()).toContain(importer);
            });

            it('throws AssetConfigurationError for invalid importer', () => {
                const registry = new AssetImporterRegistry<TestSchema>();
                expect(() => registry.register({} as any)).toThrow(AssetConfigurationError);
            });

            it('throws for empty id', () => {
                const registry = new AssetImporterRegistry<TestSchema>();
                expect(() => registry.register(createMockImporter('  '))).toThrow(
                    AssetConfigurationError
                );
            });

            it('throws when disposed', () => {
                const registry = new AssetImporterRegistry<TestSchema>();
                registry.dispose();
                expect(() =>
                    registry.register(createMockImporter('test'))
                ).toThrow(AssetDisposedError);
            });
        });

        describe('unregister', () => {
            it('returns true for existing importer', () => {
                const registry = new AssetImporterRegistry<TestSchema>();
                registry.register(createMockImporter('test'));
                expect(registry.unregister('test')).toBe(true);
                expect(registry.list()).toHaveLength(0);
            });

            it('returns false for missing importer', () => {
                const registry = new AssetImporterRegistry<TestSchema>();
                expect(registry.unregister('missing')).toBe(false);
            });

            it('returns false when disposed', () => {
                const registry = new AssetImporterRegistry<TestSchema>();
                registry.register(createMockImporter('test'));
                registry.dispose();
                expect(registry.unregister('test')).toBe(false);
            });
        });

        describe('list', () => {
            it('returns importers sorted by priority (higher first)', () => {
                const registry = new AssetImporterRegistry<TestSchema>();
                const low = createMockImporter('low', { priority: 1 });
                const high = createMockImporter('high', { priority: 10 });
                const mid = createMockImporter('mid', { priority: 5 });

                registry.register(low);
                registry.register(high);
                registry.register(mid);

                const list = registry.list();
                expect(list[0]).toBe(high);
                expect(list[1]).toBe(mid);
                expect(list[2]).toBe(low);
            });

            it('uses alphabetical order for same priority', () => {
                const registry = new AssetImporterRegistry<TestSchema>();
                const b = createMockImporter('b');
                const a = createMockImporter('a');
                const c = createMockImporter('c');

                registry.register(b);
                registry.register(a);
                registry.register(c);

                const list = registry.list();
                expect(list[0]).toBe(a);
                expect(list[1]).toBe(b);
                expect(list[2]).toBe(c);
            });
        });

        describe('find', () => {
            it('matches by sourceKind', () => {
                const registry = new AssetImporterRegistry<TestSchema>();
                const textImporter = createMockImporter('text', { sourceKinds: ['text'] });
                const jsonImporter = createMockImporter('json', { sourceKinds: ['json'] });

                registry.register(textImporter);
                registry.register(jsonImporter);

                const found = registry.find({
                    source: { kind: 'text', data: 'test' },
                    locale: 'en-US',
                    database: {} as any,
                });

                expect(found).toBe(textImporter);
            });

            it('matches by mimeType', () => {
                const registry = new AssetImporterRegistry<TestSchema>();
                const importer = createMockImporter('test', {
                    mimeTypes: ['application/json'],
                });

                registry.register(importer);

                const found = registry.find({
                    source: { kind: 'json', data: { value: 1 }, mimeType: 'application/json' },
                    locale: 'en-US',
                    database: {} as any,
                });

                expect(found).toBe(importer);
            });

            it('matches by extension', () => {
                const registry = new AssetImporterRegistry<TestSchema>();
                const importer = createMockImporter('test', { extensions: ['json'] });

                registry.register(importer);

                const found = registry.find({
                    source: { kind: 'json', data: { value: 1 }, uri: 'test.json' },
                    locale: 'en-US',
                    database: {} as any,
                });

                expect(found).toBe(importer);
            });

            it('matches compound extensions', () => {
                const registry = new AssetImporterRegistry<TestSchema>();
                const importer = createMockImporter('test', { extensions: ['effect.json'] });

                registry.register(importer);

                const found = registry.find({
                    source: { kind: 'json', data: { value: 1 }, uri: 'test.effect.json' },
                    locale: 'en-US',
                    database: {} as any,
                });

                expect(found).toBe(importer);
            });

            it('matches by canImport callback', () => {
                const registry = new AssetImporterRegistry<TestSchema>();
                const importer = createMockImporter('test', {
                    canImport: (ctx) => ctx.source.kind === 'text',
                });

                registry.register(importer);

                const found = registry.find({
                    source: { kind: 'text', data: 'test' },
                    locale: 'en-US',
                    database: {} as any,
                });

                expect(found).toBe(importer);
            });

            it('matches wildcard importers (no sourceKinds)', () => {
                const registry = new AssetImporterRegistry<TestSchema>();
                const wildcard = createMockImporter('wildcard');
                const specific = createMockImporter('specific', { sourceKinds: ['text'] });

                registry.register(wildcard);
                registry.register(specific);

                const found = registry.find({
                    source: { kind: 'json', data: { value: 1 } },
                    locale: 'en-US',
                    database: {} as any,
                });

                expect(found).toBe(wildcard);
            });

            it('returns undefined when no match', () => {
                const registry = new AssetImporterRegistry<TestSchema>();
                registry.register(createMockImporter('test', { sourceKinds: ['text'] }));

                const found = registry.find({
                    source: { kind: 'json', data: { value: 1 } },
                    locale: 'en-US',
                    database: {} as any,
                });

                expect(found).toBeUndefined();
            });

            it('throws when disposed', () => {
                const registry = new AssetImporterRegistry<TestSchema>();
                registry.dispose();

                expect(() =>
                    registry.find({
                        source: { kind: 'text', data: 'test' },
                        locale: 'en-US',
                        database: {} as any,
                    })
                ).toThrow(AssetDisposedError);
            });
        });

        describe('clear', () => {
            it('removes all importers', () => {
                const registry = new AssetImporterRegistry<TestSchema>();
                registry.register(createMockImporter('test1'));
                registry.register(createMockImporter('test2'));
                registry.clear();
                expect(registry.list()).toHaveLength(0);
            });

            it('is no-op when disposed', () => {
                const registry = new AssetImporterRegistry<TestSchema>();
                registry.register(createMockImporter('test'));
                registry.dispose();
                registry.clear();
                expect(registry.list()).toHaveLength(0);
            });
        });

        describe('dispose', () => {
            it('is idempotent', () => {
                const registry = new AssetImporterRegistry<TestSchema>();
                registry.dispose();
                registry.dispose();
                expect(registry.isDisposed).toBe(true);
            });

            it('clears all indexes', () => {
                const registry = new AssetImporterRegistry<TestSchema>();
                registry.register(createMockImporter('test', { sourceKinds: ['text'] }));
                registry.dispose();
                expect(registry.list()).toHaveLength(0);
            });
        });
    });

    describe('AssetImportStageRegistry', () => {
        describe('register', () => {
            it('registers valid stage', () => {
                const registry = new AssetImportStageRegistry<TestSchema>();
                const stage = createMockStage('test');
                registry.register(stage);
                expect(registry.list()).toContain(stage);
            });

            it('throws AssetConfigurationError for invalid stage', () => {
                const registry = new AssetImportStageRegistry<TestSchema>();
                expect(() => registry.register({} as any)).toThrow(AssetConfigurationError);
            });

            it('throws for empty id', () => {
                const registry = new AssetImportStageRegistry<TestSchema>();
                expect(() => registry.register(createMockStage('  '))).toThrow(
                    AssetConfigurationError
                );
            });

            it('throws when disposed', () => {
                const registry = new AssetImportStageRegistry<TestSchema>();
                registry.dispose();
                expect(() => registry.register(createMockStage('test'))).toThrow(
                    AssetDisposedError
                );
            });
        });

        describe('unregister', () => {
            it('returns true for existing stage', () => {
                const registry = new AssetImportStageRegistry<TestSchema>();
                registry.register(createMockStage('test'));
                expect(registry.unregister('test')).toBe(true);
                expect(registry.list()).toHaveLength(0);
            });

            it('returns false for missing stage', () => {
                const registry = new AssetImportStageRegistry<TestSchema>();
                expect(registry.unregister('missing')).toBe(false);
            });

            it('returns false when disposed', () => {
                const registry = new AssetImportStageRegistry<TestSchema>();
                registry.register(createMockStage('test'));
                registry.dispose();
                expect(registry.unregister('test')).toBe(false);
            });
        });

        describe('listByPhase', () => {
            it('returns stages for specific phase', () => {
                const registry = new AssetImportStageRegistry<TestSchema>();
                const sourceStage = createMockStage('source', { phases: ['source'] });
                const beforeStage = createMockStage('before', { phases: ['before-import'] });
                const afterStage = createMockStage('after', { phases: ['after-import'] });

                registry.register(sourceStage);
                registry.register(beforeStage);
                registry.register(afterStage);

                expect(registry.listByPhase('source')).toContain(sourceStage);
                expect(registry.listByPhase('before-import')).toContain(beforeStage);
                expect(registry.listByPhase('after-import')).toContain(afterStage);
            });

            it('assigns stages to all phases when phases not specified', () => {
                const registry = new AssetImportStageRegistry<TestSchema>();
                const stage = createMockStage('test');
                registry.register(stage);

                expect(registry.listByPhase('source')).toContain(stage);
                expect(registry.listByPhase('before-import')).toContain(stage);
                expect(registry.listByPhase('after-import')).toContain(stage);
            });
        });

        describe('findMatching', () => {
            it('respects phase filter', () => {
                const registry = new AssetImportStageRegistry<TestSchema>();
                const sourceStage = createMockStage('source', { phases: ['source'] });
                const beforeStage = createMockStage('before', { phases: ['before-import'] });

                registry.register(sourceStage);
                registry.register(beforeStage);

                const matches = registry.findMatching('source', 'text', {} as any);
                expect(matches).toContain(sourceStage);
                expect(matches).not.toContain(beforeStage);
            });

            it('respects sourceKind filter', () => {
                const registry = new AssetImportStageRegistry<TestSchema>();
                const textStage = createMockStage('text', { sourceKinds: ['text'] });
                const jsonStage = createMockStage('json', { sourceKinds: ['json'] });

                registry.register(textStage);
                registry.register(jsonStage);

                const matches = registry.findMatching('source', 'text', {} as any);
                expect(matches).toContain(textStage);
                expect(matches).not.toContain(jsonStage);
            });

            it('respects canProcess callback', () => {
                const registry = new AssetImportStageRegistry<TestSchema>();
                const stage = createMockStage('test', {
                    canProcess: (ctx) => ctx.source.kind === 'text',
                });

                registry.register(stage);

                const textMatches = registry.findMatching('source', 'text', {
                    source: { kind: 'text', data: 'test' },
                } as any);
                expect(textMatches).toContain(stage);

                const jsonMatches = registry.findMatching('source', 'json', {
                    source: { kind: 'json', data: { value: 1 } },
                } as any);
                expect(jsonMatches).not.toContain(stage);
            });
        });

        describe('clear', () => {
            it('removes all stages', () => {
                const registry = new AssetImportStageRegistry<TestSchema>();
                registry.register(createMockStage('test1'));
                registry.register(createMockStage('test2'));
                registry.clear();
                expect(registry.list()).toHaveLength(0);
            });
        });

        describe('dispose', () => {
            it('is idempotent', () => {
                const registry = new AssetImportStageRegistry<TestSchema>();
                registry.dispose();
                registry.dispose();
                expect(registry.isDisposed).toBe(true);
            });

            it('clears all stages', () => {
                const registry = new AssetImportStageRegistry<TestSchema>();
                registry.register(createMockStage('test'));
                registry.dispose();
                expect(registry.list()).toHaveLength(0);
            });
        });
    });
});
