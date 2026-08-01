import { describe, expect, it } from 'vitest';
import { AudioBusRegistry } from '../internal/bus-registry';
import { AudioBusError } from '../errors';
import { MASTER_AUDIO_BUS_ID } from '../reference';
import { FakeAudioContext } from './helpers/fake-audio-context';

const createRegistry = (ctx?: FakeAudioContext) => {
    const context = (ctx ?? new FakeAudioContext()) as unknown as AudioContext;
    const destination = new FakeAudioContext().destination as unknown as AudioNode;
    return new AudioBusRegistry({
        context,
        destination,
        createConfigurationError: (descriptor) => {
            const err = new Error(descriptor.code) as Error & { code?: string };
            err.code = descriptor.code;
            return err;
        },
        normalizeGain: (value, code) => {
            if (!Number.isFinite(value) || value < 0) {
                throw new Error(code);
            }
            return value;
        },
        normalizePan: (value) => {
            if (!Number.isFinite(value)) throw new Error('audio.invalid-pan');
            return Math.min(1, Math.max(-1, value));
        },
    });
};

describe('AudioBusRegistry', () => {
    describe('constructor', () => {
        it('creates a master bus automatically', () => {
            const registry = createRegistry();
            const master = registry.get(MASTER_AUDIO_BUS_ID);
            expect(master).toBeDefined();
            expect(master!.id).toBe(MASTER_AUDIO_BUS_ID);
            expect(master!.volume).toBe(1);
            expect(master!.mute).toBe(false);
            expect(master!.pan).toBe(0);
            expect(master!.parentId).toBeUndefined();
        });
    });

    describe('upsert', () => {
        it('creates a new bus with default values', () => {
            const registry = createRegistry();
            const state = registry.upsert({ id: 'music' });
            expect(state.id).toBe('music');
            expect(state.volume).toBe(1);
            expect(state.mute).toBe(false);
            expect(state.pan).toBe(0);
        });

        it('applies volume, mute, pan, and metadata on creation', () => {
            const registry = createRegistry();
            const state = registry.upsert({
                id: 'sfx',
                volume: 0.5,
                mute: true,
                pan: -0.3,
                metadata: { tag: 'effects' },
            });
            expect(state.volume).toBe(0.5);
            expect(state.mute).toBe(true);
            expect(state.pan).toBe(-0.3);
            expect(state.metadata).toEqual({ tag: 'effects' });
        });

        it('updates an existing bus in place', () => {
            const registry = createRegistry();
            registry.upsert({ id: 'music', volume: 0.8 });
            const updated = registry.upsert({ id: 'music', volume: 0.3 });
            expect(updated.volume).toBe(0.3);
            expect(registry.list().filter((b) => b.id === 'music')).toHaveLength(1);
        });

        it('sets up parent-child hierarchy', () => {
            const registry = createRegistry();
            registry.upsert({ id: 'parent' });
            const child = registry.upsert({ id: 'child', parentId: 'parent' });
            expect(child.parentId).toBe('parent');

            const parent = registry.get('parent');
            expect(parent!.childIds).toContain('child');
        });

        it('throws when master bus is given a parentId', () => {
            const registry = createRegistry();
            expect(() => registry.upsert({ id: MASTER_AUDIO_BUS_ID, parentId: 'music' })).toThrow();
        });

        it('throws on self-parent (cycle)', () => {
            const registry = createRegistry();
            expect(() => registry.upsert({ id: 'a', parentId: 'a' })).toThrow();
        });

        it('throws on transitive cycle A -> B -> A', () => {
            const registry = createRegistry();
            registry.upsert({ id: 'a' });
            registry.upsert({ id: 'b', parentId: 'a' });
            expect(() => registry.upsert({ id: 'a', parentId: 'b' })).toThrow();
        });

        it('throws AudioBusError when parent does not exist', () => {
            const registry = createRegistry();
            expect(() => registry.upsert({ id: 'child', parentId: 'nonexistent' })).toThrow(AudioBusError);
        });
    });

    describe('remove', () => {
        it('returns { removed: false } for master bus', () => {
            const registry = createRegistry();
            const result = registry.remove(MASTER_AUDIO_BUS_ID);
            expect(result.removed).toBe(false);
        });

        it('returns { removed: false } for unknown bus', () => {
            const registry = createRegistry();
            expect(registry.remove('nonexistent').removed).toBe(false);
        });

        it('removes a non-master bus and returns fallback to master', () => {
            const registry = createRegistry();
            registry.upsert({ id: 'music' });
            const result = registry.remove('music');
            expect(result.removed).toBe(true);
            expect(result.fallbackBusId).toBe(MASTER_AUDIO_BUS_ID);
            expect(registry.get('music')).toBeUndefined();
        });

        it('re-parents children to fallback bus when parent is removed', () => {
            const registry = createRegistry();
            registry.upsert({ id: 'parent' });
            registry.upsert({ id: 'child', parentId: 'parent' });
            registry.remove('parent');

            const child = registry.get('child');
            expect(child!.parentId).toBe(MASTER_AUDIO_BUS_ID);

            const master = registry.get(MASTER_AUDIO_BUS_ID);
            expect(master!.childIds).toContain('child');
        });

        it('uses parent as fallback when removing a child bus', () => {
            const registry = createRegistry();
            registry.upsert({ id: 'grandparent' });
            registry.upsert({ id: 'parent', parentId: 'grandparent' });
            registry.upsert({ id: 'child', parentId: 'parent' });

            const result = registry.remove('parent');
            expect(result.fallbackBusId).toBe('grandparent');

            const child = registry.get('child');
            expect(child!.parentId).toBe('grandparent');
        });
    });

    describe('require', () => {
        it('returns the internal bus for a valid id', () => {
            const registry = createRegistry();
            const bus = registry.require(MASTER_AUDIO_BUS_ID);
            expect(bus.id).toBe(MASTER_AUDIO_BUS_ID);
            expect(bus.gainNode).toBeDefined();
        });

        it('throws AudioBusError for unknown id', () => {
            const registry = createRegistry();
            expect(() => registry.require('nonexistent')).toThrow(AudioBusError);
        });
    });

    describe('effectiveGain (via snapshot)', () => {
        it('returns volume for a single bus', () => {
            const registry = createRegistry();
            registry.upsert({ id: 'music', volume: 0.6 });
            const state = registry.get('music');
            expect(state!.effectiveGain).toBeCloseTo(0.6, 10);
        });

        it('multiplies gains through parent chain', () => {
            const registry = createRegistry();
            registry.upsert({ id: 'parent', volume: 0.5 });
            registry.upsert({ id: 'child', parentId: 'parent', volume: 0.4 });
            const state = registry.get('child');
            expect(state!.effectiveGain).toBeCloseTo(0.2, 10);
        });

        it('zeroes gain when any bus in the chain is muted', () => {
            const registry = createRegistry();
            registry.upsert({ id: 'parent', volume: 0.8 });
            registry.upsert({ id: 'child', parentId: 'parent', volume: 0.5, mute: true });
            const state = registry.get('child');
            expect(state!.effectiveGain).toBe(0);
        });
    });

    describe('captureSnapshot / applySnapshot', () => {
        it('captures all bus states including master', () => {
            const registry = createRegistry();
            registry.upsert({ id: 'music', volume: 0.7 });
            const snapshot = registry.captureSnapshot('snap-1');
            expect(snapshot.id).toBe('snap-1');
            expect(snapshot.buses.length).toBeGreaterThanOrEqual(2);
            const musicEntry = snapshot.buses.find((b) => b.id === 'music');
            expect(musicEntry!.volume).toBe(0.7);
        });

        it('applies snapshot to restore volume, mute, and pan', () => {
            const registry = createRegistry();
            registry.upsert({ id: 'music', volume: 0.7 });
            const snapshot = registry.captureSnapshot();

            registry.upsert({ id: 'music', volume: 0.1 });
            registry.applySnapshot(snapshot);

            const state = registry.get('music');
            expect(state!.volume).toBe(0.7);
        });

        it('skips buses in the snapshot that no longer exist', () => {
            const registry = createRegistry();
            registry.upsert({ id: 'temp', volume: 0.5 });
            const snapshot = registry.captureSnapshot();

            registry.remove('temp');
            // Should not throw
            expect(() => registry.applySnapshot(snapshot)).not.toThrow();
        });
    });

    describe('clear', () => {
        it('removes all non-master buses and resets master to defaults', () => {
            const registry = createRegistry();
            registry.upsert({ id: 'music', volume: 0.3 });
            registry.upsert({ id: 'sfx', volume: 0.8 });
            registry.upsert({ id: MASTER_AUDIO_BUS_ID, volume: 0.5 });

            registry.clear();

            expect(registry.get('music')).toBeUndefined();
            expect(registry.get('sfx')).toBeUndefined();
            const master = registry.get(MASTER_AUDIO_BUS_ID);
            expect(master).toBeDefined();
            expect(master!.volume).toBe(1);
            expect(master!.mute).toBe(false);
            expect(master!.pan).toBe(0);
            expect(master!.childIds.length).toBe(0);
        });
    });

    describe('list', () => {
        it('returns a frozen array of frozen bus states', () => {
            const registry = createRegistry();
            registry.upsert({ id: 'music' });
            const list = registry.list();
            expect(Object.isFrozen(list)).toBe(true);
            for (const bus of list) {
                expect(Object.isFrozen(bus)).toBe(true);
            }
        });
    });

    describe('initialize', () => {
        it('creates buses in two passes: first without parents, then with parents', () => {
            const registry = createRegistry();
            registry.initialize([
                { id: 'child', parentId: 'parent' },
                { id: 'parent' },
            ]);

            const child = registry.get('child');
            expect(child).toBeDefined();
            expect(child!.parentId).toBe('parent');
        });
    });
});
