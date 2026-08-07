import { describe, expect, it } from 'vitest';
import { UIHost } from '../components/ui-host';

describe('UIHost component', () => {
    it('defaults to screen-overlay with sensible world-space fallbacks', () => {
        const host = new UIHost();

        expect(host.renderMode).toBe('screen-overlay');
        expect(host.receiveInput).toBe(true);
        expect(host.worldWidth).toBe(1);
        expect(host.worldHeight).toBe(0.5);
        expect(host.billboard).toBe('none');
        expect(host.textureScale).toBe(512);
    });

    it('accepts world-space configuration', () => {
        const host = new UIHost({
            assetId: 'UI/machine-progress.ui.json',
            renderMode: 'world-space',
            worldWidth: 1.2,
            worldHeight: 0.4,
            billboard: 'camera-facing',
            textureScale: 1024,
        });

        expect(host.renderMode).toBe('world-space');
        expect(host.worldWidth).toBeCloseTo(1.2);
        expect(host.worldHeight).toBeCloseTo(0.4);
        expect(host.billboard).toBe('camera-facing');
        expect(host.textureScale).toBe(1024);
    });

    it('rejects unknown render and billboard modes', () => {
        const host = new UIHost({
            renderMode: 'holographic' as never,
            billboard: 'spin' as never,
        });

        expect(host.renderMode).toBe('screen-overlay');
        expect(host.billboard).toBe('none');

        host.renderMode = 'nonsense' as never;
        host.billboard = 'nonsense' as never;
        expect(host.renderMode).toBe('screen-overlay');
        expect(host.billboard).toBe('none');
    });

    it('clamps world size and texture scale into the supported range', () => {
        const host = new UIHost({ worldWidth: -5, worldHeight: 100000, textureScale: 8 });

        expect(host.worldWidth).toBeGreaterThan(0);
        expect(host.worldHeight).toBeLessThanOrEqual(1000);
        expect(host.textureScale).toBe(64);

        host.textureScale = 99999;
        expect(host.textureScale).toBe(4096);

        // Non-finite input keeps the previous value instead of poisoning it.
        host.worldWidth = Number.NaN;
        expect(Number.isFinite(host.worldWidth)).toBe(true);
    });

    it('round-trips every property through serialize/deserialize', () => {
        const source = new UIHost({
            assetId: 'UI/hud.ui.json',
            renderMode: 'world-space',
            receiveInput: false,
            worldWidth: 2,
            worldHeight: 1.25,
            billboard: 'camera-facing',
            textureScale: 256,
        });

        const restored = new UIHost();
        restored.deserialize(source.serialize());

        expect(restored.assetId).toBe('UI/hud.ui.json');
        expect(restored.renderMode).toBe('world-space');
        expect(restored.receiveInput).toBe(false);
        expect(restored.worldWidth).toBeCloseTo(2);
        expect(restored.worldHeight).toBeCloseTo(1.25);
        expect(restored.billboard).toBe('camera-facing');
        expect(restored.textureScale).toBe(256);
    });

    it('ignores invalid serialized values while keeping valid ones', () => {
        const host = new UIHost({ renderMode: 'world-space', worldWidth: 3 });

        host.deserialize({ renderMode: 'bogus', billboard: 'bogus', worldWidth: 'wide' });

        expect(host.renderMode).toBe('world-space');
        expect(host.billboard).toBe('none');
        expect(host.worldWidth).toBeCloseTo(3);
    });
});
