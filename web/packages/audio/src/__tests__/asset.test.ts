import { beforeAll, describe, expect, it } from 'vitest';
import {
    AUDIO_CLIP_ASSET_KIND,
    createInlineAudioClipSelector,
    createRegisteredAudioClipSelector,
    isAudioClipAssetData,
    isAudioClipSelector,
    toAudioClipSelector,
    toAudioClipSelectorFromRecord,
} from '../asset';
import { FakeAudioBuffer, installFakeAudioGlobals } from './helpers/fake-audio-context';

beforeAll(() => {
    installFakeAudioGlobals();
});

describe('AUDIO_CLIP_ASSET_KIND', () => {
    it('equals "audioClip"', () => {
        expect(AUDIO_CLIP_ASSET_KIND).toBe('audioClip');
    });
});

describe('isAudioClipAssetData', () => {
    it('returns true for buffer kind with an AudioBuffer instance', () => {
        const buffer = new FakeAudioBuffer(1, 48000, 48000) as unknown as AudioBuffer;
        expect(isAudioClipAssetData({ kind: 'buffer', buffer })).toBe(true);
    });

    it('returns true for pcm kind with valid sampleRate and channelData', () => {
        expect(
            isAudioClipAssetData({
                kind: 'pcm',
                sampleRate: 48000,
                channelData: [new Float32Array(100)],
            })
        ).toBe(true);
    });

    it('returns false for pcm kind with missing sampleRate', () => {
        expect(
            isAudioClipAssetData({
                kind: 'pcm',
                channelData: [new Float32Array(100)],
            })
        ).toBe(false);
    });

    it('returns false for pcm kind with non-Float32Array channelData', () => {
        expect(
            isAudioClipAssetData({
                kind: 'pcm',
                sampleRate: 48000,
                channelData: [new Uint8Array(100)],
            })
        ).toBe(false);
    });

    it('returns true for encoded kind with ArrayBuffer', () => {
        expect(isAudioClipAssetData({ kind: 'encoded', data: new ArrayBuffer(16) })).toBe(true);
    });

    it('returns true for encoded kind with ArrayBufferView', () => {
        expect(isAudioClipAssetData({ kind: 'encoded', data: new Uint8Array(16) })).toBe(true);
    });

    it('returns true for url kind with non-empty url string', () => {
        expect(isAudioClipAssetData({ kind: 'url', url: 'https://example.com/audio.mp3' })).toBe(true);
    });

    it('returns false for url kind with empty url', () => {
        expect(isAudioClipAssetData({ kind: 'url', url: '' })).toBe(false);
    });

    it('returns false for unknown kind', () => {
        expect(isAudioClipAssetData({ kind: 'unknown' })).toBe(false);
    });

    it('returns false for non-objects', () => {
        expect(isAudioClipAssetData(null)).toBe(false);
        expect(isAudioClipAssetData(undefined)).toBe(false);
        expect(isAudioClipAssetData(42)).toBe(false);
        expect(isAudioClipAssetData('string')).toBe(false);
    });

    it('returns false for objects without kind field', () => {
        expect(isAudioClipAssetData({ data: 'test' })).toBe(false);
    });
});

describe('isAudioClipSelector', () => {
    it('returns true for registered selector with string clipId', () => {
        expect(isAudioClipSelector({ kind: 'registered', clipId: 'clip-1' })).toBe(true);
    });

    it('returns false for registered selector with non-string clipId', () => {
        expect(isAudioClipSelector({ kind: 'registered', clipId: 42 })).toBe(false);
    });

    it('returns true for asset selector with selector field', () => {
        expect(isAudioClipSelector({ kind: 'asset', selector: 'some-asset-id' })).toBe(true);
    });

    it('returns false for asset selector without selector field', () => {
        expect(isAudioClipSelector({ kind: 'asset' })).toBe(false);
    });

    it('returns true for inline selector with valid clip data', () => {
        expect(
            isAudioClipSelector({
                kind: 'inline',
                clip: { kind: 'url', url: 'https://example.com/audio.mp3' },
            })
        ).toBe(true);
    });

    it('returns false for inline selector with invalid clip data', () => {
        expect(isAudioClipSelector({ kind: 'inline', clip: { kind: 'url', url: '' } })).toBe(false);
    });

    it('returns false for unknown kind', () => {
        expect(isAudioClipSelector({ kind: 'unknown' })).toBe(false);
    });

    it('returns false for non-objects', () => {
        expect(isAudioClipSelector(null)).toBe(false);
        expect(isAudioClipSelector(undefined)).toBe(false);
        expect(isAudioClipSelector(42)).toBe(false);
    });
});

describe('createRegisteredAudioClipSelector', () => {
    it('creates a registered selector with normalized clipId', () => {
        const selector = createRegisteredAudioClipSelector('  my-clip  ');
        expect(selector.kind).toBe('registered');
        expect(selector.clipId).toBe('my-clip');
    });
});

describe('createInlineAudioClipSelector', () => {
    it('creates an inline selector wrapping the clip data', () => {
        const clip = { kind: 'url' as const, url: 'https://example.com/audio.mp3' };
        const selector = createInlineAudioClipSelector(clip);
        expect(selector.kind).toBe('inline');
        expect(selector.clip).toBe(clip);
    });
});

describe('toAudioClipSelector', () => {
    it('returns undefined for undefined input', () => {
        expect(toAudioClipSelector(undefined)).toBeUndefined();
    });

    it('wraps an AudioBuffer into an inline buffer selector', () => {
        const buffer = new FakeAudioBuffer(1, 48000, 48000) as unknown as AudioBuffer;
        const selector = toAudioClipSelector(buffer);
        expect(selector).toBeDefined();
        expect(selector!.kind).toBe('inline');
        if (selector!.kind === 'inline') {
            expect(selector!.clip.kind).toBe('buffer');
        }
    });

    it('passes through an existing selector unchanged', () => {
        const existing = { kind: 'registered' as const, clipId: 'clip-1' as any };
        const selector = toAudioClipSelector(existing);
        expect(selector).toBe(existing);
    });

    it('wraps raw AudioClipAssetData into an inline selector', () => {
        const data = { kind: 'url' as const, url: 'https://example.com/audio.mp3' };
        const selector = toAudioClipSelector(data);
        expect(selector).toBeDefined();
        expect(selector!.kind).toBe('inline');
    });

    it('wraps an asset selector shape as asset kind', () => {
        const assetSelector = { kind: 'audioClip' as any, id: 'asset-1' } as any;
        const selector = toAudioClipSelector(assetSelector);
        expect(selector).toBeDefined();
        expect(selector!.kind).toBe('asset');
    });
});

describe('toAudioClipSelectorFromRecord', () => {
    it('wraps an AssetRecord-like object as asset selector', () => {
        const record = { kind: 'audioClip' as const, data: { kind: 'buffer' as const } };
        const selector = toAudioClipSelectorFromRecord(record as any);
        expect(selector.kind).toBe('asset');
    });

    it('wraps a raw selector as asset selector', () => {
        const raw = { id: 'asset-1', revision: 1 } as any;
        const selector = toAudioClipSelectorFromRecord(raw);
        expect(selector.kind).toBe('asset');
    });
});
