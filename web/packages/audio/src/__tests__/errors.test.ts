import { describe, expect, it } from 'vitest';
import {
    AudioAssetError,
    AudioBusError,
    AudioConfigurationError,
    AudioDisposedError,
    AudioError,
    AudioLifecycleError,
    AudioListenerError,
    AudioSnapshotError,
    AudioSourceError,
    AudioUnavailableError,
    DEFAULT_AUDIO_MESSAGE_RESOLVER,
    resolveAudioMessage,
} from '../errors';

describe('DEFAULT_AUDIO_MESSAGE_RESOLVER', () => {
    it('resolves all validation message codes to human-readable strings', () => {
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.invalid-bus-id', value: 42 }, 'en')).toContain('42');
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.invalid-clip', value: null }, 'en')).toContain('clip');
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.invalid-context', value: 'x' }, 'en')).toContain('context');
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.invalid-distance', value: -1 }, 'en')).toContain('distance');
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.invalid-gain', value: NaN }, 'en')).toContain('gain');
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.invalid-listener', value: {} }, 'en')).toContain('listener');
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.invalid-pan', value: 999 }, 'en')).toContain('pan');
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.invalid-parent-bus', value: 'x' }, 'en')).toContain('parent');
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.invalid-playback-rate', value: 0 }, 'en')).toContain('rate');
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.invalid-source', value: 'y' }, 'en')).toContain('source');
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.invalid-snapshot', value: 'z' }, 'en')).toContain('snapshot');
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.invalid-time', value: -5 }, 'en')).toContain('time');
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.invalid-vector', value: 'v' }, 'en')).toContain('vector');
    });

    it('resolves bus cycle descriptor with bus and parent ids', () => {
        const msg = DEFAULT_AUDIO_MESSAGE_RESOLVER(
            { code: 'audio.bus.cycle', busId: 'A', parentId: 'B' },
            'en'
        );
        expect(msg).toContain('A');
        expect(msg).toContain('B');
        expect(msg).toContain('cycle');
    });

    it('resolves runtime message codes', () => {
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.asset.resolve-failed', selector: 's', reason: 'r' }, 'en')).toContain('s');
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.bus.missing', busId: 'bus1' }, 'en')).toContain('bus1');
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.context.resume-failed', reason: 'err' }, 'en')).toContain('resume');
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.context.suspend-failed', reason: 'err' }, 'en')).toContain('suspend');
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.disposed' }, 'en')).toContain('disposed');
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.listener.missing', listenerId: 'L1' }, 'en')).toContain('L1');
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.snapshot.invalid', reason: 'bad' }, 'en')).toContain('bad');
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.source.missing', sourceId: 'S1' }, 'en')).toContain('S1');
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.source.play-failed', sourceId: 'S2', reason: 'x' }, 'en')).toContain('S2');
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.source.resume-failed', sourceId: 'S3', reason: 'y' }, 'en')).toContain('S3');
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.unavailable', reason: 'no audio' }, 'en')).toContain('no audio');
    });

    it('returns undefined for unknown codes', () => {
        expect(DEFAULT_AUDIO_MESSAGE_RESOLVER({ code: 'audio.unknown-code' } as never, 'en')).toBeUndefined();
    });
});

describe('resolveAudioMessage', () => {
    it('uses the custom resolver when provided', () => {
        const custom = () => 'custom message';
        const result = resolveAudioMessage({ code: 'audio.disposed' }, 'en', custom);
        expect(result).toBe('custom message');
    });

    it('falls back to default resolver when custom returns undefined', () => {
        const custom = () => undefined;
        const result = resolveAudioMessage({ code: 'audio.disposed' }, 'en', custom);
        expect(result).toContain('disposed');
    });

    it('falls back to code string when both resolvers return undefined', () => {
        const custom = () => undefined;
        const result = resolveAudioMessage(
            { code: 'audio.unknown-code' } as never,
            'en',
            custom
        );
        expect(result).toBe('audio.unknown-code');
    });
});

describe('formatUnknown (via error messages)', () => {
    it('uses Error.message for Error values', () => {
        const msg = DEFAULT_AUDIO_MESSAGE_RESOLVER(
            { code: 'audio.source.play-failed', sourceId: 's', reason: new Error('boom') },
            'en'
        );
        expect(msg).toContain('boom');
    });

    it('passes strings through directly', () => {
        const msg = DEFAULT_AUDIO_MESSAGE_RESOLVER(
            { code: 'audio.source.play-failed', sourceId: 's', reason: 'direct string' },
            'en'
        );
        expect(msg).toContain('direct string');
    });

    it('JSON.stringifies objects', () => {
        const msg = DEFAULT_AUDIO_MESSAGE_RESOLVER(
            { code: 'audio.source.play-failed', sourceId: 's', reason: { detail: 'nested' } },
            'en'
        );
        expect(msg).toContain('nested');
    });
});

describe('error class hierarchy', () => {
    it('AudioError is an instance of Error with correct name and code', () => {
        const err = new AudioError('AudioError', 'audio.disposed', 'test message');
        expect(err).toBeInstanceOf(Error);
        expect(err).toBeInstanceOf(AudioError);
        expect(err.name).toBe('AudioError');
        expect(err.code).toBe('audio.disposed');
        expect(err.message).toBe('test message');
    });

    it('AudioConfigurationError carries a validation code', () => {
        const err = new AudioConfigurationError('audio.invalid-gain', 'bad gain');
        expect(err).toBeInstanceOf(Error);
        expect(err).toBeInstanceOf(AudioError);
        expect(err.name).toBe('AudioConfigurationError');
        expect(err.code).toBe('audio.invalid-gain');
    });

    it('AudioLifecycleError carries resume-failed or suspend-failed code', () => {
        const resumeErr = new AudioLifecycleError('audio.context.resume-failed', 'resume failed');
        expect(resumeErr).toBeInstanceOf(AudioError);
        expect(resumeErr.code).toBe('audio.context.resume-failed');

        const suspendErr = new AudioLifecycleError('audio.context.suspend-failed', 'suspend failed');
        expect(suspendErr.code).toBe('audio.context.suspend-failed');
    });

    it('AudioDisposedError always has code audio.disposed', () => {
        const err = new AudioDisposedError('already disposed');
        expect(err).toBeInstanceOf(AudioError);
        expect(err.code).toBe('audio.disposed');
        expect(err.name).toBe('AudioDisposedError');
    });

    it('AudioUnavailableError always has code audio.unavailable', () => {
        const err = new AudioUnavailableError('no audio context');
        expect(err).toBeInstanceOf(AudioError);
        expect(err.code).toBe('audio.unavailable');
        expect(err.name).toBe('AudioUnavailableError');
    });

    it('AudioAssetError always has code audio.asset.resolve-failed', () => {
        const err = new AudioAssetError('asset failed');
        expect(err).toBeInstanceOf(AudioError);
        expect(err.code).toBe('audio.asset.resolve-failed');
        expect(err.name).toBe('AudioAssetError');
    });

    it('AudioBusError carries busId', () => {
        const err = new AudioBusError('bus missing', 'music');
        expect(err).toBeInstanceOf(AudioError);
        expect(err.code).toBe('audio.bus.missing');
        expect(err.busId).toBe('music');
        expect(err.name).toBe('AudioBusError');
    });

    it('AudioListenerError carries listenerId', () => {
        const err = new AudioListenerError('listener missing', 'main');
        expect(err).toBeInstanceOf(AudioError);
        expect(err.code).toBe('audio.listener.missing');
        expect(err.listenerId).toBe('main');
        expect(err.name).toBe('AudioListenerError');
    });

    it('AudioSourceError carries sourceId and supports 3 code variants', () => {
        const missing = new AudioSourceError('audio.source.missing', 'missing', 'src1');
        expect(missing.sourceId).toBe('src1');
        expect(missing.code).toBe('audio.source.missing');

        const playFailed = new AudioSourceError('audio.source.play-failed', 'play failed', 'src2');
        expect(playFailed.code).toBe('audio.source.play-failed');

        const resumeFailed = new AudioSourceError('audio.source.resume-failed', 'resume failed', 'src3');
        expect(resumeFailed.code).toBe('audio.source.resume-failed');
    });

    it('AudioSnapshotError always has code audio.snapshot.invalid', () => {
        const err = new AudioSnapshotError('invalid snapshot');
        expect(err).toBeInstanceOf(AudioError);
        expect(err.code).toBe('audio.snapshot.invalid');
        expect(err.name).toBe('AudioSnapshotError');
    });

    it('all error classes support cause option', () => {
        const cause = new Error('root cause');
        const err = new AudioDisposedError('disposed', { cause });
        expect(err.cause).toBe(cause);
    });
});
