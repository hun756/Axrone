import { describe, expect, it } from 'vitest';
import { AudioSourceRegistry } from '../internal/source-registry';
import { AudioSourceError } from '../errors';
import { MASTER_AUDIO_BUS_ID } from '../reference';

const createRegistry = () =>
    new AudioSourceRegistry({
        normalizeGain: (value, code) => {
            if (!Number.isFinite(value) || value < 0) throw new Error(code);
            return value;
        },
        normalizePan: (value) => {
            if (!Number.isFinite(value)) throw new Error('audio.invalid-pan');
            return Math.min(1, Math.max(-1, value));
        },
        normalizePlaybackRate: (value) => {
            if (!Number.isFinite(value) || value <= 0) throw new Error('audio.invalid-playback-rate');
            return value;
        },
        normalizeTime: (value) => {
            if (!Number.isFinite(value) || value < 0) throw new Error('audio.invalid-time');
            return value;
        },
    });

const noopOptions = {
    requireBus: () => {},
};

describe('AudioSourceRegistry', () => {
    describe('upsert', () => {
        it('creates a new source with defaults', () => {
            const registry = createRegistry();
            const source = registry.upsert({ id: 's1' }, noopOptions);
            expect(source.id).toBe('s1');
            expect(source.busId).toBe(MASTER_AUDIO_BUS_ID);
            expect(source.volume).toBe(1);
            expect(source.muted).toBe(false);
            expect(source.loop).toBe(false);
            expect(source.autoplay).toBe(false);
            expect(source.playbackRate).toBe(1);
            expect(source.detuneCents).toBe(0);
            expect(source.pan).toBe(0);
            expect(source.playbackState).toBe('idle');
        });

        it('auto-generates id when not provided', () => {
            const registry = createRegistry();
            const s1 = registry.upsert({}, noopOptions);
            const s2 = registry.upsert({}, noopOptions);
            expect(s1.id).toMatch(/^source:/);
            expect(s2.id).toMatch(/^source:/);
            expect(s1.id).not.toBe(s2.id);
        });

        it('applies field values from definition', () => {
            const registry = createRegistry();
            const source = registry.upsert(
                {
                    id: 's1',
                    busId: 'music',
                    volume: 0.5,
                    muted: true,
                    loop: true,
                    autoplay: true,
                    playbackRate: 1.5,
                    detuneCents: 100,
                    pan: -0.3,
                    startOffsetSeconds: 2,
                    metadata: { tag: 'bgm' },
                },
                noopOptions
            );
            expect(source.busId).toBe('music');
            expect(source.volume).toBe(0.5);
            expect(source.muted).toBe(true);
            expect(source.loop).toBe(true);
            expect(source.autoplay).toBe(true);
            expect(source.playbackRate).toBe(1.5);
            expect(source.detuneCents).toBe(100);
            expect(source.pan).toBe(-0.3);
            expect(source.startOffsetSeconds).toBe(2);
            expect(source.metadata).toEqual({ tag: 'bgm' });
        });

        it('patches individual fields on existing source', () => {
            const registry = createRegistry();
            registry.upsert({ id: 's1', volume: 1 }, noopOptions);
            const updated = registry.upsert({ id: 's1', volume: 0.3 }, noopOptions);
            expect(updated.volume).toBe(0.3);
            expect(updated.id).toBe('s1');
        });

        it('reconnects playback output when bus changes with active playback', () => {
            const registry = createRegistry();
            const reconnections: string[] = [];
            const source = registry.upsert({ id: 's1', busId: 'music' }, noopOptions);
            // Simulate active playback
            source.active = { sequence: 1 } as any;

            registry.upsert(
                { id: 's1', busId: 'sfx' },
                {
                    requireBus: () => {},
                    reconnectPlaybackOutput: (_playback, nextBusId) => {
                        reconnections.push(nextBusId);
                    },
                }
            );

            expect(source.busId).toBe('sfx');
            expect(reconnections).toEqual(['sfx']);
        });

        it('invokes requireBus callback for bus validation', () => {
            const registry = createRegistry();
            const required: string[] = [];
            registry.upsert(
                { id: 's1', busId: 'music' },
                { requireBus: (id) => required.push(id) }
            );
            expect(required).toEqual(['music']);
        });
    });

    describe('reassignBus', () => {
        it('moves all sources from one bus to another', () => {
            const registry = createRegistry();
            registry.upsert({ id: 's1', busId: 'old' }, noopOptions);
            registry.upsert({ id: 's2', busId: 'old' }, noopOptions);
            registry.upsert({ id: 's3', busId: 'other' }, noopOptions);

            registry.reassignBus('old', 'new', () => {});

            expect(registry.get('s1')!.busId).toBe('new');
            expect(registry.get('s2')!.busId).toBe('new');
            expect(registry.get('s3')!.busId).toBe('other');
        });

        it('reconnects active playbacks during reassignment', () => {
            const registry = createRegistry();
            const source = registry.upsert({ id: 's1', busId: 'old' }, noopOptions);
            source.active = { sequence: 1 } as any;

            const reconnections: string[] = [];
            registry.reassignBus('old', 'new', (_playback, nextBusId) => {
                reconnections.push(nextBusId);
            });

            expect(reconnections).toEqual(['new']);
        });
    });

    describe('remove', () => {
        it('deletes the source and returns it', () => {
            const registry = createRegistry();
            registry.upsert({ id: 's1' }, noopOptions);
            const removed = registry.remove('s1');
            expect(removed).toBeDefined();
            expect(removed!.id).toBe('s1');
            expect(registry.get('s1')).toBeUndefined();
        });

        it('returns undefined for unknown id', () => {
            const registry = createRegistry();
            expect(registry.remove('nonexistent')).toBeUndefined();
        });

        it('also clears transient flag', () => {
            const registry = createRegistry();
            registry.upsert({ id: 's1' }, noopOptions);
            registry.markTransient('s1');
            registry.remove('s1');
            expect(registry.isTransient('s1')).toBe(false);
        });
    });

    describe('require', () => {
        it('returns the source for a valid id', () => {
            const registry = createRegistry();
            registry.upsert({ id: 's1' }, noopOptions);
            expect(registry.require('s1').id).toBe('s1');
        });

        it('throws AudioSourceError for unknown id', () => {
            const registry = createRegistry();
            expect(() => registry.require('nonexistent')).toThrow(AudioSourceError);
        });
    });

    describe('nextOneShotId', () => {
        it('generates sequential oneshot ids', () => {
            const registry = createRegistry();
            const id1 = registry.nextOneShotId();
            const id2 = registry.nextOneShotId();
            expect(id1).toBe('oneshot:1');
            expect(id2).toBe('oneshot:2');
        });
    });

    describe('markTransient / isTransient', () => {
        it('tracks transient sources', () => {
            const registry = createRegistry();
            expect(registry.isTransient('s1')).toBe(false);
            registry.markTransient('s1');
            expect(registry.isTransient('s1')).toBe(true);
        });
    });

    describe('snapshot', () => {
        it('produces a frozen state with resolved offset', () => {
            const registry = createRegistry();
            const source = registry.upsert({ id: 's1', volume: 0.7 }, noopOptions);
            const state = registry.snapshot(source, () => 1.5);
            expect(Object.isFrozen(state)).toBe(true);
            expect(state.id).toBe('s1');
            expect(state.volume).toBe(0.7);
            expect(state.currentOffsetSeconds).toBe(1.5);
        });

        it('clones spatialization to prevent external mutation', () => {
            const registry = createRegistry();
            const source = registry.upsert(
                { id: 's1', spatial: { mode: '2d', position: { x: 1, y: 2, z: 3 } } },
                noopOptions
            );
            const state = registry.snapshot(source, () => 0);
            expect(state.spatial).toEqual({
                mode: '2d',
                position: { x: 1, y: 2, z: 3 },
                pan: undefined,
                attenuation: undefined,
            });
            expect(state.spatial).not.toBe(source.spatial);
        });
    });

    describe('clear', () => {
        it('returns all sources and empties internal maps', () => {
            const registry = createRegistry();
            registry.upsert({ id: 's1' }, noopOptions);
            registry.upsert({ id: 's2' }, noopOptions);
            registry.markTransient('s1');

            const sources = registry.clear();
            expect(Object.isFrozen(sources)).toBe(true);
            expect(sources.length).toBe(2);
            expect(registry.get('s1')).toBeUndefined();
            expect(registry.get('s2')).toBeUndefined();
            expect(registry.isTransient('s1')).toBe(false);
        });
    });

    describe('get / list / values', () => {
        it('get returns undefined for unknown id', () => {
            const registry = createRegistry();
            expect(registry.get('nonexistent')).toBeUndefined();
        });

        it('list returns a frozen array', () => {
            const registry = createRegistry();
            registry.upsert({ id: 's1' }, noopOptions);
            const list = registry.list();
            expect(Object.isFrozen(list)).toBe(true);
            expect(list.length).toBe(1);
        });

        it('values returns an iterator over sources', () => {
            const registry = createRegistry();
            registry.upsert({ id: 's1' }, noopOptions);
            registry.upsert({ id: 's2' }, noopOptions);
            const values = [...registry.values()];
            expect(values.length).toBe(2);
        });
    });
});
