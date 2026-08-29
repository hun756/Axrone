import { describe, expect, it } from 'vitest';
import {
    asAudioBusId,
    asAudioClipId,
    asAudioListenerId,
    asAudioSourceId,
    asAudioSnapshotId,
    cloneAudioVector3,
    createAudioClipAssetReference,
    isAudioClipAssetRecord,
    isAudioClipAssetReference,
    MASTER_AUDIO_BUS_ID,
    normalizeAudioBusId,
    normalizeAudioClipId,
    normalizeAudioListenerId,
    normalizeAudioSourceId,
    normalizeAudioSnapshotId,
} from '../reference';

describe('reference — branded ID cast helpers', () => {
    it('asAudioBusId returns the string unchanged at runtime', () => {
        expect(asAudioBusId('music')).toBe('music');
    });

    it('asAudioClipId returns the string unchanged at runtime', () => {
        expect(asAudioClipId('click')).toBe('click');
    });

    it('asAudioListenerId returns the string unchanged at runtime', () => {
        expect(asAudioListenerId('main')).toBe('main');
    });

    it('asAudioSourceId returns the string unchanged at runtime', () => {
        expect(asAudioSourceId('voice')).toBe('voice');
    });

    it('asAudioSnapshotId returns the string unchanged at runtime', () => {
        expect(asAudioSnapshotId('snap')).toBe('snap');
    });
});

describe('reference — MASTER_AUDIO_BUS_ID', () => {
    it('equals the branded string "master"', () => {
        expect(MASTER_AUDIO_BUS_ID).toBe('master');
    });
});

describe('reference — normalizeAudioBusId is case-insensitive', () => {
    it('resolves the Editor-authored "Master" default to the pre-created master bus', () => {
        expect(normalizeAudioBusId('Master')).toBe(MASTER_AUDIO_BUS_ID);
    });

    it('case-folds after trimming', () => {
        expect(normalizeAudioBusId('  MiXeD-Case  ')).toBe('mixed-case');
    });

    it('maps every casing of a name onto one bus identity', () => {
        expect(normalizeAudioBusId('SFX')).toBe(normalizeAudioBusId('sfx'));
        expect(normalizeAudioBusId('Music')).toBe(normalizeAudioBusId('MUSIC'));
    });

    it('leaves non-bus identifiers case-sensitive', () => {
        expect(normalizeAudioSourceId('Laser')).not.toBe(normalizeAudioSourceId('laser'));
        expect(normalizeAudioClipId('Click')).not.toBe(normalizeAudioClipId('click'));
        expect(normalizeAudioListenerId('Main')).not.toBe(normalizeAudioListenerId('main'));
    });
});

describe('reference — normalize identifier helpers', () => {
    const normalizers = [
        { name: 'normalizeAudioBusId', fn: normalizeAudioBusId },
        { name: 'normalizeAudioClipId', fn: normalizeAudioClipId },
        { name: 'normalizeAudioListenerId', fn: normalizeAudioListenerId },
        { name: 'normalizeAudioSourceId', fn: normalizeAudioSourceId },
        { name: 'normalizeAudioSnapshotId', fn: normalizeAudioSnapshotId },
    ] as const;

    for (const { name, fn } of normalizers) {
        describe(name, () => {
            it('trims leading and trailing whitespace', () => {
                expect(fn('  hello  ')).toBe('hello');
            });

            it('returns the value as-is when already trimmed', () => {
                expect(fn('test-id')).toBe('test-id');
            });

            it('throws TypeError for empty string', () => {
                expect(() => fn('')).toThrow(TypeError);
            });

            it('throws TypeError for whitespace-only string', () => {
                expect(() => fn('   ')).toThrow(TypeError);
            });
        });
    }
});

describe('reference — createAudioClipAssetReference', () => {
    it('produces a frozen object with kind "audioClip" and the given id', () => {
        const ref = createAudioClipAssetReference('my-clip');
        expect(ref.kind).toBe('audioClip');
        expect(ref.id).toBe('my-clip');
        expect(typeof ref.token).toBe('string');
        expect(ref.token).toContain('audioClip');
        expect(Object.isFrozen(ref)).toBe(true);
    });
});

describe('reference — isAudioClipAssetReference', () => {
    it('returns true for a valid audioClip reference', () => {
        const ref = createAudioClipAssetReference('clip-1');
        expect(isAudioClipAssetReference(ref)).toBe(true);
    });

    it('returns false for a non-audioClip reference shape', () => {
        expect(isAudioClipAssetReference({ kind: 'texture', id: 'x', token: 'asset:texture:x' })).toBe(false);
    });

    it('returns false for null and primitives', () => {
        expect(isAudioClipAssetReference(null)).toBe(false);
        expect(isAudioClipAssetReference(undefined)).toBe(false);
        expect(isAudioClipAssetReference(42)).toBe(false);
        expect(isAudioClipAssetReference('string')).toBe(false);
    });

    it('returns false for objects missing required fields', () => {
        expect(isAudioClipAssetReference({ kind: 'audioClip' })).toBe(false);
        expect(isAudioClipAssetReference({})).toBe(false);
    });
});

describe('reference — isAudioClipAssetRecord', () => {
    it('returns true for an object with kind "audioClip" and a data field', () => {
        expect(isAudioClipAssetRecord({ kind: 'audioClip', data: { kind: 'buffer' } })).toBe(true);
    });

    it('returns false when kind is not "audioClip"', () => {
        expect(isAudioClipAssetRecord({ kind: 'texture', data: {} })).toBe(false);
    });

    it('returns false when data field is missing', () => {
        expect(isAudioClipAssetRecord({ kind: 'audioClip' })).toBe(false);
    });

    it('returns false for null and non-objects', () => {
        expect(isAudioClipAssetRecord(null)).toBe(false);
        expect(isAudioClipAssetRecord(undefined)).toBe(false);
        expect(isAudioClipAssetRecord(42)).toBe(false);
    });
});

describe('reference — cloneAudioVector3', () => {
    it('clones x, y, z from the provided value', () => {
        const result = cloneAudioVector3({ x: 1, y: 2, z: 3 });
        expect(result).toEqual({ x: 1, y: 2, z: 3 });
        expect(result).not.toBe({ x: 1, y: 2, z: 3 });
    });

    it('uses the fallback when value is undefined', () => {
        const fallback = { x: 10, y: 20, z: 30 };
        const result = cloneAudioVector3(undefined, fallback);
        expect(result).toEqual({ x: 10, y: 20, z: 30 });
    });

    it('falls back to {0,0,0} when both value and fallback are undefined', () => {
        const result = cloneAudioVector3(undefined);
        expect(result).toEqual({ x: 0, y: 0, z: 0 });
    });

    it('coerces NaN to 0', () => {
        const result = cloneAudioVector3({ x: NaN, y: NaN, z: NaN });
        expect(result).toEqual({ x: 0, y: 0, z: 0 });
    });

    it('coerces non-numeric values to 0', () => {
        const result = cloneAudioVector3({
            x: 'abc' as unknown as number,
            y: undefined as unknown as number,
            z: null as unknown as number,
        });
        expect(result).toEqual({ x: 0, y: 0, z: 0 });
    });
});
