import { beforeAll, describe, expect, it } from 'vitest';
import { createRegisteredAudioClipSelector } from '../asset';
import { createAudioSystem } from '../system';
import {
    FakeAudioBuffer,
    FakeAudioContext,
    installFakeAudioGlobals,
} from './helpers/fake-audio-context';

describe('AudioSystem integration', () => {
    beforeAll(() => {
        installFakeAudioGlobals();
    });

    it('syncs listener activation and fallback through the registry layer', () => {
        const context = new FakeAudioContext();
        const system = createAudioSystem({
            context: context as unknown as AudioContext,
            listeners: [
                {
                    id: 'main',
                    active: true,
                    position: { x: 1, y: 2, z: 3 },
                },
                {
                    id: 'backup',
                    position: { x: 9, y: 8, z: 7 },
                },
            ],
        });

        expect(system.activeListener?.id).toBe('main');
        expect(context.listener.positionX.value).toBe(1);
        expect(context.listener.positionY.value).toBe(2);
        expect(context.listener.positionZ.value).toBe(3);

        system.setActiveListener('backup');

        expect(system.activeListener?.id).toBe('backup');
        expect(context.listener.positionX.value).toBe(9);
        expect(context.listener.positionY.value).toBe(8);
        expect(context.listener.positionZ.value).toBe(7);

        expect(system.removeListener('backup')).toBe(true);
        expect(system.activeListener?.id).toBe('main');
        expect(context.listener.positionX.value).toBe(1);
        expect(context.listener.positionY.value).toBe(2);
        expect(context.listener.positionZ.value).toBe(3);
    });

    it('re-routes active playback when a source bus changes', async () => {
        const context = new FakeAudioContext();
        const buffer = new FakeAudioBuffer(2, 96000, 48000) as unknown as AudioBuffer;
        const system = createAudioSystem({
            context: context as unknown as AudioContext,
        });

        system.upsertBus({ id: 'music' });
        system.upsertBus({ id: 'sfx' });
        system.upsertListener({ id: 'listener', active: true });
        system.upsertSource({
            id: 'laser',
            busId: 'music',
            clip: {
                kind: 'buffer',
                buffer,
            },
            spatial: {
                mode: '2d',
                position: { x: 2, y: 0, z: 0 },
            },
        });

        await system.playSource('laser');

        const musicBusGain = context.gainNodes[1];
        const sfxBusGain = context.gainNodes[2];
        const playbackPanner = context.stereoPannerNodes.at(-1);

        expect(playbackPanner?.connections[0]).toBe(musicBusGain);
        expect(system.getSource('laser')?.busId).toBe('music');

        system.updateSource('laser', { busId: 'sfx' });

        expect(playbackPanner?.connections[0]).toBe(sfxBusGain);
        expect(system.getSource('laser')?.busId).toBe('sfx');
    });

    it('restores snapshot playback into a fresh audio context with preserved offsets', async () => {
        const sourceContext = new FakeAudioContext();
        const buffer = new FakeAudioBuffer(2, 192000, 48000) as unknown as AudioBuffer;
        const sourceSystem = createAudioSystem({
            context: sourceContext as unknown as AudioContext,
            listeners: [
                {
                    id: 'main',
                    active: true,
                    position: { x: 4, y: 0, z: 0 },
                },
            ],
            buses: [{ id: 'music' }],
            sources: [
                {
                    id: 'theme',
                    busId: 'music',
                    clip: {
                        kind: 'buffer',
                        buffer,
                    },
                    loop: true,
                },
            ],
        });

        await sourceSystem.playSource('theme');
        sourceContext.advance(1.5);
        const snapshot = sourceSystem.snapshot();

        expect(snapshot.sources[0]?.currentOffsetSeconds).toBeCloseTo(1.5, 5);

        const restoredContext = new FakeAudioContext();
        const restoredSystem = createAudioSystem({
            context: restoredContext as unknown as AudioContext,
        });

        await restoredSystem.restore(snapshot, { restorePlayback: true });

        expect(restoredSystem.activeListener?.id).toBe('main');
        expect(restoredSystem.getBus('music')?.id).toBe('music');
        expect(restoredSystem.getSource('theme')?.playbackState).toBe('playing');
        expect(restoredContext.listener.positionX.value).toBe(4);

        const restoredPlayback = restoredContext.bufferSourceNodes.at(-1);
        expect(restoredPlayback?.startCalls[0]?.offset).toBeCloseTo(
            snapshot.sources[0]?.currentOffsetSeconds ?? 0,
            5
        );
    });

    it('captures pause offsets and resumes from the paused position', async () => {
        const context = new FakeAudioContext();
        const buffer = new FakeAudioBuffer(2, 192000, 48000) as unknown as AudioBuffer;
        const system = createAudioSystem({
            context: context as unknown as AudioContext,
            listeners: [{ id: 'main', active: true }],
            sources: [
                {
                    id: 'ambience',
                    clip: {
                        kind: 'buffer',
                        buffer,
                    },
                },
            ],
        });

        await system.playSource('ambience');
        context.advance(0.75);

        system.pauseSource('ambience');
        context.flush();

        expect(system.getSource('ambience')?.playbackState).toBe('paused');
        expect(system.getSource('ambience')?.currentOffsetSeconds).toBeCloseTo(0.75, 5);

        await system.resumeSource('ambience');

        const resumedNode = context.bufferSourceNodes.at(-1);
        expect(resumedNode?.startCalls[0]?.offset).toBeCloseTo(0.75, 5);
        expect(system.getSource('ambience')?.playbackState).toBe('playing');
    });

    it('cleans up transient sources after scheduled stop reaches ended state', async () => {
        const context = new FakeAudioContext();
        const buffer = new FakeAudioBuffer(2, 96000, 48000) as unknown as AudioBuffer;
        const system = createAudioSystem({
            context: context as unknown as AudioContext,
            listeners: [{ id: 'main', active: true }],
        });

        const handle = await system.play({
            clip: {
                kind: 'buffer',
                buffer,
            },
        });

        expect(system.getSource(handle.sourceId)).toBeDefined();

        handle.stop({ when: context.currentTime + 0.5 });
        context.advance(0.25);

        expect(system.getSource(handle.sourceId)).toBeDefined();

        context.advance(0.25);

        expect(system.getSource(handle.sourceId)).toBeUndefined();
    });

    it('transitions non-looping playback to stopped when natural duration elapses', async () => {
        const context = new FakeAudioContext();
        const buffer = new FakeAudioBuffer(2, 48000, 48000) as unknown as AudioBuffer;
        const system = createAudioSystem({
            context: context as unknown as AudioContext,
            listeners: [{ id: 'main', active: true }],
            sources: [
                {
                    id: 'voice',
                    clip: {
                        kind: 'buffer',
                        buffer,
                    },
                },
            ],
        });

        await system.playSource('voice');
        context.advance(1);

        expect(system.getSource('voice')?.playbackState).toBe('stopped');
        expect(system.getSource('voice')?.currentOffsetSeconds).toBe(0);
    });

    it('emits runtime events for playback commands and lifecycle transitions', async () => {
        const context = new FakeAudioContext();
        const buffer = new FakeAudioBuffer(2, 96000, 48000) as unknown as AudioBuffer;
        const system = createAudioSystem({
            context: context as unknown as AudioContext,
            listeners: [{ id: 'main', active: true }],
        });
        const observed: string[] = [];
        const playedSourceIds: string[] = [];
        const unsubscribeAll = system.events.on('audio:*', (event) => {
            observed.push(event.type);
        });
        const unsubscribePlayed = system.events.on('source:played', (event) => {
            playedSourceIds.push(event.source.id);
        });

        system.resetDiagnostics();
        system.upsertBus({ id: 'music' });
        system.upsertSource({
            id: 'theme',
            busId: 'music',
            clip: {
                kind: 'buffer',
                buffer,
            },
        });
        await system.playSource('theme');
        context.advance(0.25);
        system.pauseSource('theme');
        context.flush();
        await system.resumeSource('theme');
        system.stopSource('theme');
        context.flush();
        await system.suspend();
        await system.resume();

        unsubscribeAll();
        unsubscribePlayed();

        expect(playedSourceIds).toEqual(['theme']);
        expect(observed).toContain('bus:upserted');
        expect(observed).toContain('source:upserted');
        expect(observed).toContain('source:played');
        expect(observed).toContain('source:paused');
        expect(observed).toContain('source:resumed');
        expect(observed).toContain('source:stopped');
        expect(observed).toContain('source:ended');
        expect(observed).toContain('system:suspended');
        expect(observed).toContain('system:resumed');
    });

    it('captures diagnostics counters and the last emitted event snapshot', async () => {
        const context = new FakeAudioContext();
        const buffer = new FakeAudioBuffer(2, 48000, 48000) as unknown as AudioBuffer;
        const system = createAudioSystem({
            context: context as unknown as AudioContext,
            listeners: [{ id: 'main', active: true }],
        });

        system.resetDiagnostics();
        system.upsertBus({ id: 'sfx' });
        system.upsertSource({
            id: 'click',
            busId: 'sfx',
            clip: {
                kind: 'buffer',
                buffer,
            },
        });
        await system.playSource('click');
        context.advance(1);

        const diagnostics = system.getDiagnostics();

        expect(diagnostics.busCount).toBe(2);
        expect(diagnostics.listenerCount).toBe(1);
        expect(diagnostics.sourceCount).toBe(1);
        expect(diagnostics.activePlaybackCount).toBe(0);
        expect(diagnostics.counters.busMutationCount).toBe(1);
        expect(diagnostics.counters.sourceMutationCount).toBe(1);
        expect(diagnostics.counters.playbackCommandCount).toBe(1);
        expect(diagnostics.counters.playbackCompletionCount).toBe(1);
        expect(diagnostics.lastEvent?.type).toBe('source:ended');
    });

    it('disconnects all playback nodes when removeSource is called', async () => {
        const context = new FakeAudioContext();
        const buffer = new FakeAudioBuffer(2, 96000, 48000) as unknown as AudioBuffer;
        const system = createAudioSystem({
            context: context as unknown as AudioContext,
            listeners: [{ id: 'main', active: true }],
        });

        system.upsertBus({ id: 'music' });
        system.upsertSource({
            id: 'theme',
            busId: 'music',
            clip: {
                kind: 'buffer',
                buffer,
            },
        });
        await system.playSource('theme');

        // Capture the playback nodes before removal
        const sourceNodeBefore = context.bufferSourceNodes.at(-1);
        expect(sourceNodeBefore).toBeDefined();
        expect(sourceNodeBefore!.connections.length).toBeGreaterThan(0);

        // Remove the source — should disconnect all nodes
        const removed = system.removeSource('theme');
        expect(removed).toBe(true);
        expect(sourceNodeBefore!.connections.length).toBe(0);

        // Source should no longer exist
        expect(system.getSource('theme')).toBeUndefined();
    });

    it('disconnects all playback nodes when stopSource is called', async () => {
        const context = new FakeAudioContext();
        const buffer = new FakeAudioBuffer(2, 96000, 48000) as unknown as AudioBuffer;
        const system = createAudioSystem({
            context: context as unknown as AudioContext,
            listeners: [{ id: 'main', active: true }],
        });

        system.upsertBus({ id: 'sfx' });
        system.upsertSource({
            id: 'click',
            busId: 'sfx',
            clip: {
                kind: 'buffer',
                buffer,
            },
        });
        await system.playSource('click');

        const sourceNode = context.bufferSourceNodes.at(-1);
        expect(sourceNode).toBeDefined();
        expect(sourceNode!.connections.length).toBeGreaterThan(0);

        system.stopSource('click');
        context.flush();

        // After stop, nodes should be disconnected
        expect(sourceNode!.connections.length).toBe(0);
    });
});

// The old recursive AudioPatch type made every nested field optional, so the incomplete
// spatial patch below compiled while the registry replaced spatial wholesale — silently
// destroying position, orientation and panningModel. The patch aliases are now honest
// shallow Partials, which turns that data loss into a type error.
describe('AudioSystem patch semantics — nested replacement is explicit', () => {
    const createFixture = () => {
        const context = new FakeAudioContext();
        const buffer = new FakeAudioBuffer(2, 96000, 48000);
        return {
            system: createAudioSystem({ context: context as unknown as AudioContext }),
            clip: { kind: 'buffer' as const, buffer: buffer as unknown as AudioBuffer },
        };
    };

    it('leaves sibling spatial fields intact when a top-level primitive is patched', () => {
        const { system, clip } = createFixture();
        system.upsertSource({
            id: 'p',
            clip,
            spatial: {
                mode: '3d',
                position: { x: 1, y: 2, z: 3 },
                orientation: { x: 0, y: 0, z: -1 },
                panningModel: 'HRTF',
            },
        });

        const updated = system.updateSource('p', { volume: 0.25 });

        expect(updated.volume).toBe(0.25);
        expect(updated.spatial).toEqual({
            mode: '3d',
            position: { x: 1, y: 2, z: 3 },
            orientation: { x: 0, y: 0, z: -1 },
            panningModel: 'HRTF',
        });
    });

    it('replaces spatial wholesale when a complete value is supplied', () => {
        const { system, clip } = createFixture();
        system.upsertSource({
            id: 'q',
            clip,
            spatial: { mode: '3d', position: { x: 1, y: 2, z: 3 }, panningModel: 'HRTF' },
        });

        const updated = system.updateSource('q', {
            spatial: { mode: '2d', position: { x: 8, y: 0, z: 0 }, pan: 0.5 },
        });

        expect(updated.spatial).toEqual({ mode: '2d', position: { x: 8, y: 0, z: 0 }, pan: 0.5 });
    });

    it('rejects an incomplete spatial patch at compile time', () => {
        const { system, clip } = createFixture();
        system.upsertSource({ id: 'r', clip });

        // Self-validating: if the patch type ever regresses to deep-partial this line stops
        // erroring, and the unused-directive error breaks the build.
        // @ts-expect-error spatial requires a complete AudioSpatialization (mode is mandatory)
        system.updateSource('r', { spatial: { attenuation: { refDistance: 5 } } });

        // The guard is the compiler, not the runtime — the call above still executes, and
        // cloneSpatialization keys on `mode === '2d'`, so an incomplete value coerces into a
        // 3d spatial that has lost position, orientation and panningModel. Pinning the exact
        // shape keeps the contract honest: a future real deep merge changes this on purpose.
        expect(JSON.stringify(system.getSource('r')?.spatial)).toBe(
            '{"mode":"3d","attenuation":{"refDistance":5}}'
        );
    });
});

// A play request may carry a timed stop (durationSeconds) so only part of a long clip is
// heard. pauseSource dropped it: resume restarted the node without a duration and the rest
// of the clip played, turning a 0.5s sting into its full length.
describe('AudioSystem sub-clip scheduling survives pause and resume', () => {
    beforeAll(() => {
        installFakeAudioGlobals();
    });

    const createSting = async (durationSeconds: number) => {
        const context = new FakeAudioContext();
        // 2 second clip
        const buffer = new FakeAudioBuffer(2, 96000, 48000);
        const system = createAudioSystem({ context: context as unknown as AudioContext });
        system.upsertSource({
            id: 'sting',
            clip: { kind: 'buffer', buffer: buffer as unknown as AudioBuffer },
        });
        await system.playSource('sting', { durationSeconds });
        return { context, system };
    };

    it('resumes with only the time left in the sub-clip', async () => {
        const { context, system } = await createSting(0.5);
        const sourceNode = context.bufferSourceNodes.at(-1)!;
        expect(sourceNode.startCalls.at(-1)?.duration).toBeCloseTo(0.5, 5);

        context.advance(0.3);
        system.pauseSource('sting');
        await system.resumeSource('sting');

        const resumedNode = context.bufferSourceNodes.at(-1)!;
        expect(resumedNode).not.toBe(sourceNode);
        expect(resumedNode.startCalls.at(-1)?.offset).toBeCloseTo(0.3, 5);
        expect(resumedNode.startCalls.at(-1)?.duration).toBeCloseTo(0.2, 5);
    });

    it('resumes without a timed stop when the original play had none', async () => {
        const context = new FakeAudioContext();
        const buffer = new FakeAudioBuffer(2, 96000, 48000);
        const system = createAudioSystem({ context: context as unknown as AudioContext });
        system.upsertSource({
            id: 'music',
            clip: { kind: 'buffer', buffer: buffer as unknown as AudioBuffer },
        });
        await system.playSource('music');

        context.advance(0.4);
        system.pauseSource('music');
        await system.resumeSource('music');

        expect(context.bufferSourceNodes.at(-1)!.startCalls.at(-1)?.duration).toBeUndefined();
    });

    it('clears a pending sub-clip stop when the source is stopped', async () => {
        const { context, system } = await createSting(0.5);
        context.advance(0.1);
        system.pauseSource('sting');
        system.stopSource('sting');

        await system.playSource('sting');
        expect(context.bufferSourceNodes.at(-1)!.startCalls.at(-1)?.duration).toBeUndefined();
    });
});

// AudioMixerSnapshot and AudioSystemSnapshot both carry a `buses` array, so the mixer guard
// accepted a whole system snapshot and applied it bus-by-bus. The two shapes now carry
// explicit discriminators, which also matters because snapshots are the save/load format.
describe('AudioSystem snapshot type guards discriminate', () => {
    beforeAll(() => {
        installFakeAudioGlobals();
    });

    const createSystem = () =>
        createAudioSystem({ context: new FakeAudioContext() as unknown as AudioContext });

    it('rejects a system snapshot handed to applyMixerSnapshot', () => {
        const system = createSystem();

        expect(() => system.applyMixerSnapshot(system.snapshot() as never)).toThrow(
            /snapshot/i
        );
    });

    it('rejects a mixer snapshot handed to restore', async () => {
        const system = createSystem();

        await expect(system.restore(system.captureMixerSnapshot() as never)).rejects.toThrow(
            /snapshot/i
        );
    });

    it('carries a discriminator on every snapshot it produces', () => {
        const system = createSystem();

        expect(system.captureMixerSnapshot().kind).toBe('audio.mixer-snapshot');
        expect(system.snapshot().kind).toBe('audio.system-snapshot');
    });

    it('still round-trips its own system snapshot through restore', async () => {
        const system = createSystem();
        system.upsertBus({ id: 'sfx', volume: 0.4 });
        const snapshot = system.snapshot();

        const target = createSystem();
        await target.restore(snapshot);

        expect(target.getBus('sfx')?.volume).toBe(0.4);
    });
});

// The audit measured a play against an unresolvable clip reporting playbackState 'idle',
// zero console errors and playbackErrorCount 0 — total silence. Resolution now happens
// inside the guarded region, so every failure path emits source:error and throws a typed
// AudioSourceError that carries the original cause.
describe('AudioSystem playback failure reporting', () => {
    beforeAll(() => {
        installFakeAudioGlobals();
    });

    const createSystem = () =>
        createAudioSystem({ context: new FakeAudioContext() as unknown as AudioContext });

    it('emits source:error when a clip cannot be resolved', async () => {
        const system = createSystem();
        system.upsertSource({ id: 'ghost', clip: createRegisteredAudioClipSelector('nope') });

        const errors: Array<{ operation: string; reason: unknown }> = [];
        system.events.on('source:error', (event) => errors.push(event));

        await expect(system.playSource('ghost')).rejects.toMatchObject({
            code: 'audio.source.play-failed',
        });

        expect(errors).toHaveLength(1);
        expect(errors[0].operation).toBe('play');
        expect(system.getDiagnostics().counters.playbackErrorCount).toBe(1);
    });

    it('keeps the original failure reachable as the cause', async () => {
        const system = createSystem();
        system.upsertSource({ id: 'ghost2', clip: createRegisteredAudioClipSelector('nope') });

        const error = await system.playSource('ghost2').then(
            () => null,
            (reason: unknown) => reason as Error & { cause?: Error }
        );

        expect(error).not.toBeNull();
        expect(error!.cause?.constructor.name).toBe('AudioAssetError');
    });

    it('reports a resume failure with the resume operation', async () => {
        const system = createSystem();
        system.upsertSource({ id: 'resumer', clip: createRegisteredAudioClipSelector('nope') });

        const errors: string[] = [];
        system.events.on('source:error', (event) => errors.push(event.operation));

        await expect(system.resumeSource('resumer')).rejects.toMatchObject({
            code: 'audio.source.resume-failed',
        });

        expect(errors).toEqual(['resume']);
    });
});
