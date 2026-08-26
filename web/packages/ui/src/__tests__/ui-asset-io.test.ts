import { describe, expect, it } from 'vitest';
import { InvalidUIAssetError, UIRuntime, deserializeUIAsset, serializeUIAsset, validateUIAsset } from '../index';
import type { UIAsset } from '../types/ui-asset';

const createMinimalAsset = (overrides: Partial<Record<string, unknown>> = {}): UIAsset => ({
    id: 'test-ui-asset',
    name: 'Test UI',
    version: 1,
    canvas: {
        referenceWidth: 1920,
        referenceHeight: 1080,
        scaleMode: 'match-width-or-height',
        matchBias: 0.5,
    },
    root: {
        role: 'root',
        enabled: true,
        interactive: false,
        layout: { display: 'overlay', width: '100%', height: '100%' },
        children: [
            {
                role: 'container',
                enabled: true,
                interactive: true,
                layout: { width: 200, height: 50 },
                style: { background: '#ff0000ff' },
                children: [],
            },
        ],
    },
    ...overrides,
} as UIAsset);

describe('@axrone/ui ui-asset-io', () => {
    describe('validateUIAsset', () => {
        it('returns true for a valid minimal asset', () => {
            expect(validateUIAsset(createMinimalAsset())).toBe(true);
        });

        it('returns false for null', () => {
            expect(validateUIAsset(null)).toBe(false);
        });

        it('returns false for a string', () => {
            expect(validateUIAsset('not an asset')).toBe(false);
        });

        it('returns false when id is missing', () => {
            const asset = createMinimalAsset();
            const broken = { ...asset, id: '' };
            expect(validateUIAsset(broken)).toBe(false);
        });

        it('returns false when canvas.referenceWidth is zero', () => {
            const asset = createMinimalAsset();
            const broken = {
                ...asset,
                canvas: { ...asset.canvas, referenceWidth: 0 },
            };
            expect(validateUIAsset(broken)).toBe(false);
        });

        it('returns false when canvas.scaleMode is invalid', () => {
            const asset = createMinimalAsset();
            const broken = {
                ...asset,
                canvas: { ...asset.canvas, scaleMode: 'invalid-mode' },
            };
            expect(validateUIAsset(broken)).toBe(false);
        });
    });

    describe('serializeUIAsset / deserializeUIAsset round-trip', () => {
        it('round-trips a minimal asset', () => {
            const original = createMinimalAsset();
            const json = serializeUIAsset(original);
            const restored = deserializeUIAsset(json);

            expect(restored.id).toBe(original.id);
            expect(restored.name).toBe(original.name);
            expect(restored.version).toBe(original.version);
            expect(restored.canvas.referenceWidth).toBe(original.canvas.referenceWidth);
            expect(restored.canvas.referenceHeight).toBe(original.canvas.referenceHeight);
            expect(restored.canvas.scaleMode).toBe(original.canvas.scaleMode);
            expect(restored.canvas.matchBias).toBe(original.canvas.matchBias);
            expect(restored.root.role).toBe('root');
            expect(restored.root.children).toHaveLength(1);
            expect(restored.root.children[0].role).toBe('container');
        });

        it('round-trips an asset with bindings', () => {
            const original: UIAsset = {
                ...createMinimalAsset(),
                bindings: { 'score-label': 'score', 'health-bar': 12 },
            };
            const json = serializeUIAsset(original);
            const restored = deserializeUIAsset(json);

            expect(restored.bindings).toBeDefined();
            expect(restored.bindings!['score-label']).toBe('score');
            expect(restored.bindings!['health-bar']).toBe(12);
        });

        it('rejects bindings whose values are not widget keys', () => {
            const asset = {
                ...createMinimalAsset(),
                bindings: { broken: true },
            };
            expect(() => deserializeUIAsset(JSON.stringify(asset))).toThrowError(
                InvalidUIAssetError
            );
        });

        it('rejects bindings with empty-string keys', () => {
            const asset = {
                ...createMinimalAsset(),
                bindings: { broken: '' },
            };
            expect(() => deserializeUIAsset(JSON.stringify(asset))).toThrowError(
                InvalidUIAssetError
            );
        });

        it('round-trips an asset with safe area inset', () => {
            const original: UIAsset = {
                ...createMinimalAsset(),
                canvas: {
                    ...createMinimalAsset().canvas,
                    safeAreaInset: { top: 44, right: 0, bottom: 34, left: 0 },
                },
            };
            const json = serializeUIAsset(original);
            const restored = deserializeUIAsset(json);

            expect(restored.canvas.safeAreaInset).toBeDefined();
            expect(restored.canvas.safeAreaInset!.top).toBe(44);
            expect(restored.canvas.safeAreaInset!.bottom).toBe(34);
        });

        it('round-trips widget material field preserving all keys', () => {
            const materialData = { shader: 'pbr', roughness: 0.7, metalness: 0.3, textureSlots: { albedo: 'wood.png' } };
            const original: UIAsset = {
                ...createMinimalAsset(),
                root: {
                    role: 'root',
                    enabled: true,
                    interactive: false,
                    material: materialData,
                    children: [],
                },
            } as UIAsset;
            const json = serializeUIAsset(original);
            const restored = deserializeUIAsset(json);

            expect(restored.root.material).toBeDefined();
            expect(restored.root.material!['shader']).toBe('pbr');
            expect(restored.root.material!['roughness']).toBe(0.7);
            expect(restored.root.material!['metalness']).toBe(0.3);
            expect(restored.root.material!['textureSlots']).toEqual({ albedo: 'wood.png' });
        });

        it('preserves null material through round-trip', () => {
            const original: UIAsset = {
                ...createMinimalAsset(),
                root: {
                    role: 'root',
                    enabled: true,
                    interactive: false,
                    material: null,
                    children: [],
                },
            } as UIAsset;
            const json = serializeUIAsset(original);
            const restored = deserializeUIAsset(json);

            // null material is not a plain object, so it becomes undefined
            expect(restored.root.material).toBeUndefined();
        });

        it('preserves nested child material through round-trip', () => {
            const original: UIAsset = {
                ...createMinimalAsset(),
                root: {
                    role: 'root',
                    enabled: true,
                    interactive: false,
                    children: [
                        {
                            role: 'container',
                            enabled: true,
                            interactive: false,
                            material: { type: 'transparent', opacity: 0.5 },
                            children: [],
                        },
                    ],
                },
            } as UIAsset;
            const json = serializeUIAsset(original);
            const restored = deserializeUIAsset(json);

            const child = restored.root.children[0];
            expect(child.material).toBeDefined();
            expect(child.material!['type']).toBe('transparent');
            expect(child.material!['opacity']).toBe(0.5);
        });
    });

    describe('deserializeUIAsset error handling', () => {
        it('throws on malformed JSON', () => {
            expect(() => deserializeUIAsset('{ broken json')).toThrow('Failed to parse UI asset JSON');
        });

        it('throws when id is missing', () => {
            const json = JSON.stringify({ name: 'Test', version: 1, canvas: {}, root: {} });
            expect(() => deserializeUIAsset(json)).toThrow('"id" must be a non-empty string');
        });

        it('throws when canvas.referenceHeight is negative', () => {
            const json = JSON.stringify({
                id: 'test',
                name: 'Test',
                version: 1,
                canvas: { referenceWidth: 1920, referenceHeight: -100, scaleMode: 'fill', matchBias: 0.5 },
                root: { role: 'root', children: [] },
            });
            expect(() => deserializeUIAsset(json)).toThrow('"referenceHeight" must be a positive finite number');
        });

        it('throws when canvas.scaleMode is invalid', () => {
            const json = JSON.stringify({
                id: 'test',
                name: 'Test',
                version: 1,
                canvas: { referenceWidth: 1920, referenceHeight: 1080, scaleMode: 'bogus', matchBias: 0.5 },
                root: { role: 'root', children: [] },
            });
            expect(() => deserializeUIAsset(json)).toThrow('"scaleMode" must be one of');
        });
    });

    describe('deserializeUIAsset defaults', () => {
        it('defaults matchBias to 0.5 when not provided', () => {
            const json = JSON.stringify({
                id: 'test',
                name: 'Test',
                version: 1,
                canvas: { referenceWidth: 1920, referenceHeight: 1080, scaleMode: 'match-width-or-height' },
                root: { role: 'root', children: [] },
            });
            const asset = deserializeUIAsset(json);
            expect(asset.canvas.matchBias).toBe(0.5);
        });

        it('clamps matchBias to [0, 1]', () => {
            const jsonLow = JSON.stringify({
                id: 'test',
                name: 'Test',
                version: 1,
                canvas: { referenceWidth: 1920, referenceHeight: 1080, scaleMode: 'match-width-or-height', matchBias: -0.5 },
                root: { role: 'root', children: [] },
            });
            expect(deserializeUIAsset(jsonLow).canvas.matchBias).toBe(0);

            const jsonHigh = JSON.stringify({
                id: 'test',
                name: 'Test',
                version: 1,
                canvas: { referenceWidth: 1920, referenceHeight: 1080, scaleMode: 'match-width-or-height', matchBias: 2.0 },
                root: { role: 'root', children: [] },
            });
            expect(deserializeUIAsset(jsonHigh).canvas.matchBias).toBe(1);
        });

        it('defaults version to 1 when not provided', () => {
            const json = JSON.stringify({
                id: 'test',
                name: 'Test',
                canvas: { referenceWidth: 1920, referenceHeight: 1080, scaleMode: 'fill', matchBias: 0 },
                root: { role: 'root', children: [] },
            });
            const asset = deserializeUIAsset(json);
            expect(asset.version).toBe(1);
        });
    });

    describe('UIRuntime.loadFromAsset', () => {
        it('loads an asset and sets viewport to reference resolution', () => {
            const runtime = new UIRuntime({ width: 100, height: 100 });
            const asset = createMinimalAsset();

            runtime.loadFromAsset(asset);

            expect(runtime.width).toBe(1920);
            expect(runtime.height).toBe(1080);
            expect(runtime.getCanvasConfig()).not.toBeNull();
            expect(runtime.getCanvasConfig()!.referenceWidth).toBe(1920);
            expect(runtime.getCanvasConfig()!.scaleMode).toBe('match-width-or-height');
        });

        it('restores the widget tree from the asset', () => {
            const runtime = new UIRuntime({ width: 100, height: 100 });
            const asset = createMinimalAsset();

            runtime.loadFromAsset(asset);

            // getWidgetCount() excludes root; asset has 1 child widget
            const frame = runtime.commit();
            expect(frame.metrics.widgetCount).toBeGreaterThanOrEqual(1);
        });
    });
});
