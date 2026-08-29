import { beforeAll, describe, expect, it } from 'vitest';
import {
    attenuationGainForDistance,
    applyPannerState,
    syncAudioListenerToContext,
    syncPlaybackSpatialState,
} from '../internal/spatial';
import type { AudioSpatialPlaybackNodes, AudioSpatialSourceState, AudioSpatialListenerState } from '../internal/spatial';
import { FakeAudioListener, FakePannerNode, FakeStereoPannerNode, FakeGainNode, FakeAudioParam, installFakeAudioGlobals } from './helpers/fake-audio-context';

beforeAll(() => {
    installFakeAudioGlobals();
});

describe('attenuationGainForDistance — all distance models', () => {
    it('returns 1 for "none" model regardless of distance', () => {
        expect(attenuationGainForDistance(0, { model: 'none' })).toBe(1);
        expect(attenuationGainForDistance(100, { model: 'none' })).toBe(1);
        expect(attenuationGainForDistance(10000, { model: 'none' })).toBe(1);
    });

    describe('linear model', () => {
        const config = { model: 'linear' as const, refDistance: 1, maxDistance: 10, rolloffFactor: 1, minGain: 0 };

        it('returns 1 at or within refDistance', () => {
            expect(attenuationGainForDistance(0, config)).toBe(1);
            expect(attenuationGainForDistance(1, config)).toBe(1);
        });

        it('returns a value between 0 and 1 between refDistance and maxDistance', () => {
            const gain = attenuationGainForDistance(5, config);
            expect(gain).toBeGreaterThan(0);
            expect(gain).toBeLessThan(1);
        });

        it('returns minGain at or beyond maxDistance', () => {
            expect(attenuationGainForDistance(10, config)).toBe(0);
            expect(attenuationGainForDistance(100, config)).toBe(0);
        });

        it('clamps to minGain when configured', () => {
            const clamped = { ...config, minGain: 0.3 };
            expect(attenuationGainForDistance(100, clamped)).toBe(0.3);
        });
    });

    describe('exponential model', () => {
        const config = { model: 'exponential' as const, refDistance: 1, maxDistance: 100, rolloffFactor: 1, minGain: 0 };

        it('returns 1 at refDistance', () => {
            expect(attenuationGainForDistance(1, config)).toBe(1);
        });

        it('returns a decreasing value beyond refDistance', () => {
            const gain2 = attenuationGainForDistance(2, config);
            const gain4 = attenuationGainForDistance(4, config);
            expect(gain2).toBeLessThan(1);
            expect(gain4).toBeLessThan(gain2);
        });

        it('returns (distance/refDistance)^-rolloff', () => {
            expect(attenuationGainForDistance(2, config)).toBeCloseTo(0.5, 5);
        });
    });

    describe('inverse model (default)', () => {
        const config = { model: 'inverse' as const, refDistance: 1, maxDistance: 100, rolloffFactor: 1, minGain: 0 };

        it('returns 1 at refDistance', () => {
            expect(attenuationGainForDistance(1, config)).toBe(1);
        });

        it('returns ref / (ref + rolloff * (distance - ref))', () => {
            expect(attenuationGainForDistance(2, config)).toBeCloseTo(1 / (1 + 1 * (2 - 1)), 5);
        });

        it('decreases with distance but stays above 0', () => {
            const gain = attenuationGainForDistance(100, config);
            expect(gain).toBeGreaterThan(0);
            expect(gain).toBeLessThan(1);
        });
    });

    it('uses "inverse" as the default model when model is undefined', () => {
        const withDefault = attenuationGainForDistance(2, undefined);
        const withInverse = attenuationGainForDistance(2, { model: 'inverse' });
        expect(withDefault).toBeCloseTo(withInverse, 10);
    });
});

describe('syncAudioListenerToContext', () => {
    it('sets modern AudioListener params from target state', () => {
        const listener = new FakeAudioListener();
        const target: AudioSpatialListenerState = {
            enabled: true,
            position: { x: 1, y: 2, z: 3 },
            forward: { x: 0, y: 0, z: -1 },
            up: { x: 0, y: 1, z: 0 },
        };

        syncAudioListenerToContext(listener as unknown as AudioListener, target);

        expect(listener.positionX.value).toBe(1);
        expect(listener.positionY.value).toBe(2);
        expect(listener.positionZ.value).toBe(3);
        expect(listener.forwardX.value).toBe(0);
        expect(listener.forwardY.value).toBe(0);
        expect(listener.forwardZ.value).toBe(-1);
        expect(listener.upX.value).toBe(0);
        expect(listener.upY.value).toBe(1);
        expect(listener.upZ.value).toBe(0);
    });

    it('uses defaults when target is undefined', () => {
        const listener = new FakeAudioListener();
        syncAudioListenerToContext(listener as unknown as AudioListener, undefined);

        expect(listener.positionX.value).toBe(0);
        expect(listener.positionY.value).toBe(0);
        expect(listener.positionZ.value).toBe(0);
        expect(listener.forwardZ.value).toBe(-1);
        expect(listener.upY.value).toBe(1);
    });

    it('calls legacy setPosition/setOrientation when modern API is absent', () => {
        const positionCalls: number[][] = [];
        const orientationCalls: number[][] = [];
        const legacyListener = {
            setPosition(x: number, y: number, z: number) {
                positionCalls.push([x, y, z]);
            },
            setOrientation(fx: number, fy: number, fz: number, ux: number, uy: number, uz: number) {
                orientationCalls.push([fx, fy, fz, ux, uy, uz]);
            },
        };

        syncAudioListenerToContext(legacyListener as unknown as AudioListener, {
            enabled: true,
            position: { x: 5, y: 6, z: 7 },
            forward: { x: 0, y: 0, z: -1 },
            up: { x: 0, y: 1, z: 0 },
        });

        expect(positionCalls[0]).toEqual([5, 6, 7]);
        expect(orientationCalls[0]).toEqual([0, 0, -1, 0, 1, 0]);
    });
});

describe('applyPannerState', () => {
    it('sets modern PannerNode params from 3D spatialization', () => {
        const panner = new FakePannerNode();
        applyPannerState(
            panner as unknown as PannerNode,
            {
                mode: '3d',
                panningModel: 'HRTF',
                coneInnerAngle: 180,
                coneOuterAngle: 270,
                coneOuterGain: 0.5,
            },
            { x: 10, y: 20, z: 30 },
            { x: 0, y: 0, z: -1 }
        );

        expect(panner.panningModel).toBe('HRTF');
        expect(panner.coneInnerAngle).toBe(180);
        expect(panner.coneOuterAngle).toBe(270);
        expect(panner.coneOuterGain).toBe(0.5);
        expect(panner.positionX.value).toBe(10);
        expect(panner.positionY.value).toBe(20);
        expect(panner.positionZ.value).toBe(30);
        expect(panner.orientationZ.value).toBe(-1);
    });

    it('uses default cone angles when not specified', () => {
        const panner = new FakePannerNode();
        applyPannerState(
            panner as unknown as PannerNode,
            { mode: '3d' },
            { x: 0, y: 0, z: 0 },
            { x: 0, y: 0, z: -1 }
        );

        expect(panner.coneInnerAngle).toBe(360);
        expect(panner.coneOuterAngle).toBe(360);
        expect(panner.coneOuterGain).toBe(0);
    });
});

describe('syncPlaybackSpatialState', () => {
    const makePlayback = (mode: 'stereo' | 'panner'): AudioSpatialPlaybackNodes => {
        const gainNode = new FakeGainNode();
        const attenuationNode = new FakeGainNode();
        let spatialNode: StereoPannerNode | PannerNode | undefined;
        if (mode === 'stereo') {
            spatialNode = new FakeStereoPannerNode() as unknown as StereoPannerNode;
        } else {
            spatialNode = new FakePannerNode() as unknown as PannerNode;
        }
        return {
            gainNode: gainNode as unknown as GainNode,
            attenuationNode: attenuationNode as unknown as GainNode,
            spatialNode,
        };
    };

    it('zeroes gain when source is muted', () => {
        const playback = makePlayback('stereo');
        const source: AudioSpatialSourceState = { muted: true, volume: 0.8, pan: 0 };

        syncPlaybackSpatialState(playback, source);

        expect(playback.gainNode.gain.value).toBe(0);
    });

    it('sets volume on gain node when not muted', () => {
        const playback = makePlayback('stereo');
        const source: AudioSpatialSourceState = { muted: false, volume: 0.7, pan: 0.3 };

        syncPlaybackSpatialState(playback, source);

        expect(playback.gainNode.gain.value).toBe(0.7);
    });

    it('applies pan directly when no spatial config', () => {
        const playback = makePlayback('stereo');
        const source: AudioSpatialSourceState = { muted: false, volume: 1, pan: -0.5 };

        syncPlaybackSpatialState(playback, source);

        expect((playback.spatialNode as FakeStereoPannerNode).pan.value).toBe(-0.5);
    });

    it('applies 2D attenuation and panning relative to listener', () => {
        const playback = makePlayback('stereo');
        const source: AudioSpatialSourceState = {
            muted: false,
            volume: 1,
            pan: 0,
            spatial: {
                mode: '2d',
                position: { x: 5, y: 0, z: 0 },
                attenuation: { model: 'linear', refDistance: 1, maxDistance: 10, rolloffFactor: 1, minGain: 0 },
            },
        };
        const listener: AudioSpatialListenerState = {
            enabled: true,
            position: { x: 0, y: 0, z: 0 },
            forward: { x: 0, y: 0, z: -1 },
            up: { x: 0, y: 1, z: 0 },
        };

        syncPlaybackSpatialState(playback, source, listener);

        // Attenuation should be between 0 and 1 for distance 5
        expect(playback.attenuationNode.gain.value).toBeLessThan(1);
        expect(playback.attenuationNode.gain.value).toBeGreaterThan(0);
    });

    it('bypasses attenuation when listener is disabled', () => {
        const playback = makePlayback('stereo');
        const source: AudioSpatialSourceState = {
            muted: false,
            volume: 1,
            pan: 0,
            spatial: {
                mode: '2d',
                position: { x: 100, y: 0, z: 0 },
            },
        };
        const listener: AudioSpatialListenerState = {
            enabled: false,
            position: { x: 0, y: 0, z: 0 },
            forward: { x: 0, y: 0, z: -1 },
            up: { x: 0, y: 1, z: 0 },
        };

        syncPlaybackSpatialState(playback, source, listener);

        // Attenuation should be 1 when listener is disabled
        expect(playback.attenuationNode.gain.value).toBe(1);
    });

    it('applies 3D attenuation and panner state', () => {
        const playback = makePlayback('panner');
        const source: AudioSpatialSourceState = {
            muted: false,
            volume: 1,
            pan: 0,
            spatial: {
                mode: '3d',
                position: { x: 5, y: 0, z: 0 },
                orientation: { x: 0, y: 0, z: -1 },
                attenuation: { model: 'inverse', refDistance: 1, maxDistance: 100, rolloffFactor: 1, minGain: 0 },
            },
        };
        const listener: AudioSpatialListenerState = {
            enabled: true,
            position: { x: 0, y: 0, z: 0 },
            forward: { x: 0, y: 0, z: -1 },
            up: { x: 0, y: 1, z: 0 },
        };

        syncPlaybackSpatialState(playback, source, listener);

        // 3D attenuation should reduce gain
        expect(playback.attenuationNode.gain.value).toBeLessThan(1);
        expect(playback.attenuationNode.gain.value).toBeGreaterThan(0);

        // Panner position should be set
        const panner = playback.spatialNode as unknown as FakePannerNode;
        expect(panner.positionX.value).toBe(5);
    });
});

// A node created by another realm's AudioContext (Worker / OffscreenCanvas / iframe) has
// the full PannerNode surface but a different constructor identity, so `instanceof`
// rejects it and the source silently loses spatialization. Detection must key on shape.
describe('syncPlaybackSpatialState — realm-agnostic panner detection', () => {
    const makeSource = (pan: number): AudioSpatialSourceState => ({
        muted: false,
        volume: 1,
        pan,
        spatial: { mode: '3d', position: { x: 7, y: 8, z: 9 } },
    });

    const makeListener = (): AudioSpatialListenerState => ({
        enabled: true,
        position: { x: 0, y: 0, z: 0 },
        forward: { x: 0, y: 0, z: -1 },
        up: { x: 0, y: 1, z: 0 },
    });

    it('writes position to a foreign-realm panner that is not instanceof PannerNode', () => {
        const foreignPanner = {
            distanceModel: 'inverse',
            panningModel: 'equalpower',
            refDistance: 1,
            maxDistance: 10000,
            rolloffFactor: 1,
            coneInnerAngle: 360,
            coneOuterAngle: 360,
            coneOuterGain: 0,
            positionX: new FakeAudioParam(0),
            positionY: new FakeAudioParam(0),
            positionZ: new FakeAudioParam(0),
            orientationX: new FakeAudioParam(1),
            orientationY: new FakeAudioParam(0),
            orientationZ: new FakeAudioParam(0),
        };

        expect(foreignPanner).not.toBeInstanceOf(PannerNode);

        const playback: AudioSpatialPlaybackNodes = {
            gainNode: new FakeGainNode() as unknown as GainNode,
            attenuationNode: new FakeGainNode() as unknown as GainNode,
            spatialNode: foreignPanner as unknown as PannerNode,
        };

        syncPlaybackSpatialState(playback, makeSource(0), makeListener());

        expect(foreignPanner.positionX.value).toBe(7);
        expect(foreignPanner.positionY.value).toBe(8);
        expect(foreignPanner.positionZ.value).toBe(9);
    });

    it('still drives the legacy setPosition API when positionX is absent', () => {
        const calls: number[][] = [];
        const legacyPanner = {
            distanceModel: 'inverse',
            panningModel: 'equalpower',
            setPosition: (x: number, y: number, z: number) => calls.push([x, y, z]),
            setOrientation: (x: number, y: number, z: number) => calls.push([x, y, z]),
        };

        const playback: AudioSpatialPlaybackNodes = {
            gainNode: new FakeGainNode() as unknown as GainNode,
            attenuationNode: new FakeGainNode() as unknown as GainNode,
            spatialNode: legacyPanner as unknown as PannerNode,
        };

        syncPlaybackSpatialState(playback, makeSource(0), makeListener());

        expect(calls[0]).toEqual([7, 8, 9]);
    });

    it('writes pan on a foreign-realm stereo panner', () => {
        const foreignStereo = { pan: new FakeAudioParam(0) };

        const playback: AudioSpatialPlaybackNodes = {
            gainNode: new FakeGainNode() as unknown as GainNode,
            attenuationNode: new FakeGainNode() as unknown as GainNode,
            spatialNode: foreignStereo as unknown as StereoPannerNode,
        };

        syncPlaybackSpatialState(playback, { muted: false, volume: 1, pan: -0.4 }, makeListener());

        expect(foreignStereo.pan.value).toBe(-0.4);
    });
});

// Snapping AudioParam.value once per frame is audible as zipper noise on a moving source.
// Writes now ramp, and an unchanged value is skipped so a static voice costs nothing.
describe('syncPlaybackSpatialState — ramps writes and skips unchanged', () => {
    const makeRig = () => {
        const gainNode = new FakeGainNode();
        const attenuationNode = new FakeGainNode();
        const stereo = new FakeStereoPannerNode();
        return {
            gainNode,
            stereo,
            playback: {
                gainNode: gainNode as unknown as GainNode,
                attenuationNode: attenuationNode as unknown as GainNode,
                spatialNode: stereo as unknown as StereoPannerNode,
            } as AudioSpatialPlaybackNodes,
        };
    };

    const sourceOf = (volume: number, pan = 0): AudioSpatialSourceState => ({
        muted: false,
        volume,
        pan,
    });

    it('snaps the first write because there is no baseline to ramp from', () => {
        const { playback, gainNode } = makeRig();

        syncPlaybackSpatialState(playback, sourceOf(0.8), undefined, 10);

        expect(gainNode.gain.events.map((event) => event.type)).toEqual(['cancel', 'set']);
        expect(gainNode.gain.value).toBe(0.8);
    });

    it('ramps a changed value towards the audio clock', () => {
        const { playback, gainNode } = makeRig();
        syncPlaybackSpatialState(playback, sourceOf(0.8), undefined, 10);

        syncPlaybackSpatialState(playback, sourceOf(0.4), undefined, 20);

        const last = gainNode.gain.events.at(-1)!;
        expect(last.type).toBe('ramp');
        if (last.type === 'ramp') {
            expect(last.value).toBe(0.4);
            expect(last.atTime).toBeGreaterThan(20);
        }
    });

    it('writes nothing when neither gain nor pan moved', () => {
        const { playback, gainNode, stereo } = makeRig();
        syncPlaybackSpatialState(playback, sourceOf(0.8, 0.3), undefined, 10);
        const writesBefore = gainNode.gain.events.length + stereo.pan.events.length;

        syncPlaybackSpatialState(playback, sourceOf(0.8, 0.3), undefined, 20);

        expect(gainNode.gain.events.length + stereo.pan.events.length).toBe(writesBefore);
    });

    it('still writes pan on a stereo panner when only pan changed', () => {
        const { playback, stereo } = makeRig();
        syncPlaybackSpatialState(playback, sourceOf(0.8, 0.3), undefined, 10);

        syncPlaybackSpatialState(playback, sourceOf(0.8, -0.6), undefined, 20);

        expect(stereo.pan.value).toBe(-0.6);
    });
});
