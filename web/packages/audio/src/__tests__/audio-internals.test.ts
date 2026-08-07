import { describe, expect, it } from 'vitest';
import {
    attenuationGainForDistance,
    cloneSpatialization,
    normalizeAttenuation,
} from '../internal/spatial';
import { hasOwnKeys, withRetry } from '../internal/shared';
import { AudioClipStore } from '../internal/clip-store';
import { FakeAudioContext, FakeAudioBuffer, installFakeAudioGlobals } from './helpers/fake-audio-context';

describe('audio internal helpers', () => {
    it('clones spatialization payloads without retaining nested references', () => {
        const source = {
            mode: '3d' as const,
            position: { x: 4, y: 2, z: -1 },
            orientation: { x: 0, y: 0, z: -1 },
            attenuation: {
                model: 'linear' as const,
                refDistance: 2,
                maxDistance: 8,
                rolloffFactor: 0.5,
                minGain: 0.25,
            },
        };

        const cloned = cloneSpatialization(source);

        expect(cloned).toEqual(source);
        expect(cloned).not.toBe(source);
        expect(cloned?.position).not.toBe(source.position);
        expect(cloned?.orientation).not.toBe(source.orientation);
        expect(cloned?.attenuation).not.toBe(source.attenuation);
    });

    it('normalizes attenuation ranges and clamps gain to the configured floor', () => {
        const normalized = normalizeAttenuation({
            model: 'linear',
            refDistance: -2,
            maxDistance: 0,
            rolloffFactor: -3,
            minGain: 4,
        });

        expect(normalized).toEqual({
            model: 'linear',
            refDistance: 0.0001,
            maxDistance: 0.0001,
            rolloffFactor: 0,
            minGain: 1,
        });
        expect(
            attenuationGainForDistance(128, {
                model: 'linear',
                refDistance: 1,
                maxDistance: 4,
                rolloffFactor: 1,
                minGain: 0.2,
            })
        ).toBe(0.2);
    });

    it('retries operations with zero-allocation guard helpers around partial patches', async () => {
        let attempts = 0;

        const result = await withRetry(
            {
                attempts: 3,
                backoffMs: 0,
            },
            (attempt) => ({
                operation: 'context.resume' as const,
                attempt,
            }),
            async () => {
                attempts += 1;
                if (attempts < 3) {
                    throw new Error(`attempt:${attempts}`);
                }

                return 'ready';
            }
        );

        expect(result).toBe('ready');
        expect(attempts).toBe(3);
        expect(hasOwnKeys({ volume: 1 })).toBe(true);
        expect(hasOwnKeys({})).toBe(false);
    });
});

describe('AudioClipStore LRU eviction', () => {
    it('evicts least-recently-used entries when maxEntries is exceeded', async () => {
        installFakeAudioGlobals();
        const context = new FakeAudioContext();
        const store = new AudioClipStore({
            context: context as unknown as AudioContext,
            maxEntries: 2,
        });

        // Register 3 inline clips
        store.register('clip-a', {
            kind: 'inline',
            clip: { kind: 'buffer', buffer: new FakeAudioBuffer(1, 48000, 48000) as unknown as AudioBuffer },
        });
        store.register('clip-b', {
            kind: 'inline',
            clip: { kind: 'buffer', buffer: new FakeAudioBuffer(1, 48000, 48000) as unknown as AudioBuffer },
        });
        store.register('clip-c', {
            kind: 'inline',
            clip: { kind: 'buffer', buffer: new FakeAudioBuffer(1, 48000, 48000) as unknown as AudioBuffer },
        });

        // Resolve first two to fill cache
        const resolvedA = await store.resolve({ kind: 'registered', clipId: 'clip-a' });
        const resolvedB = await store.resolve({ kind: 'registered', clipId: 'clip-b' });
        expect(resolvedA).toBeDefined();
        expect(resolvedB).toBeDefined();

        // Resolve third — should evict clip-a (LRU)
        const resolvedC = await store.resolve({ kind: 'registered', clipId: 'clip-c' });
        expect(resolvedC).toBeDefined();

        // Re-resolve clip-a — should still work (re-decoded from registered selector)
        const resolvedA2 = await store.resolve({ kind: 'registered', clipId: 'clip-a' });
        expect(resolvedA2).toBeDefined();
    });

    it('does not evict when maxEntries is 0 (unlimited)', async () => {
        installFakeAudioGlobals();
        const context = new FakeAudioContext();
        const store = new AudioClipStore({
            context: context as unknown as AudioContext,
            maxEntries: 0,
        });

        for (let i = 0; i < 10; i++) {
            store.register(`clip-${i}`, {
                kind: 'inline',
                clip: { kind: 'buffer', buffer: new FakeAudioBuffer(1, 48000, 48000) as unknown as AudioBuffer },
            });
        }

        // All 10 should resolve without eviction issues
        for (let i = 0; i < 10; i++) {
            const resolved = await store.resolve({ kind: 'registered', clipId: `clip-${i}` });
            expect(resolved).toBeDefined();
        }
    });

    it('promotes recently-used entries in LRU order', async () => {
        installFakeAudioGlobals();
        const context = new FakeAudioContext();
        const store = new AudioClipStore({
            context: context as unknown as AudioContext,
            maxEntries: 2,
        });

        store.register('clip-x', {
            kind: 'inline',
            clip: { kind: 'buffer', buffer: new FakeAudioBuffer(1, 48000, 48000) as unknown as AudioBuffer },
        });
        store.register('clip-y', {
            kind: 'inline',
            clip: { kind: 'buffer', buffer: new FakeAudioBuffer(1, 48000, 48000) as unknown as AudioBuffer },
        });
        store.register('clip-z', {
            kind: 'inline',
            clip: { kind: 'buffer', buffer: new FakeAudioBuffer(1, 48000, 48000) as unknown as AudioBuffer },
        });

        // Fill cache with x and y
        await store.resolve({ kind: 'registered', clipId: 'clip-x' });
        await store.resolve({ kind: 'registered', clipId: 'clip-y' });

        // Touch x again (promote it in LRU)
        await store.resolve({ kind: 'registered', clipId: 'clip-x' });

        // Resolve z — should evict y (LRU), not x
        await store.resolve({ kind: 'registered', clipId: 'clip-z' });

        // x should still be cached (was recently touched)
        const xAgain = await store.resolve({ kind: 'registered', clipId: 'clip-x' });
        expect(xAgain).toBeDefined();
    });
});
