import { describe, expect, it } from 'vitest';
import {
    encodeAnimationClipStreamingChunkPayload,
    decodeAnimationClipStreamingChunkPayload,
    applyAnimationClipStreamingChunkDefinition,
} from '../streaming-chunk';
import { AnimationValidationError } from '../errors';
import type { AnimationClipDefinition } from '../types';

const baseClip: AnimationClipDefinition = {
    id: 'walk',
    duration: 1,
    tracks: [
        {
            target: 'root',
            path: 'translation',
            times: [0, 0.5, 1],
            values: [0, 0, 0, 0.5, 0, 0, 1, 0, 0],
        },
    ],
};

describe('decodeAnimationClipStreamingChunkPayload variants', () => {
    it('decodes from string', () => {
        const json = JSON.stringify({ tracks: [] });
        const result = decodeAnimationClipStreamingChunkPayload(json);
        expect(result.tracks).toHaveLength(0);
    });

    it('decodes from Uint8Array', () => {
        const payload = { tracks: [{ target: 'root', path: 'translation', times: [0, 1], values: [0, 0, 0, 1, 0, 0] }] };
        const encoded = encodeAnimationClipStreamingChunkPayload(payload);
        const result = decodeAnimationClipStreamingChunkPayload(encoded);
        expect(result.tracks).toHaveLength(1);
    });

    it('decodes from ArrayBuffer', () => {
        const payload = { tracks: [{ target: 'root', path: 'translation', times: [0, 1], values: [0, 0, 0, 1, 0, 0] }] };
        const encoded = encodeAnimationClipStreamingChunkPayload(payload);
        const buffer = encoded.buffer.slice(encoded.byteOffset, encoded.byteOffset + encoded.byteLength);
        const result = decodeAnimationClipStreamingChunkPayload(buffer);
        expect(result.tracks).toHaveLength(1);
    });

    it('decodes from ArrayBufferView (DataView)', () => {
        const payload = { tracks: [{ target: 'root', path: 'translation', times: [0, 1], values: [0, 0, 0, 1, 0, 0] }] };
        const encoded = encodeAnimationClipStreamingChunkPayload(payload);
        const view = new DataView(encoded.buffer, encoded.byteOffset, encoded.byteLength);
        const result = decodeAnimationClipStreamingChunkPayload(view);
        expect(result.tracks).toHaveLength(1);
    });
});

describe('decodeAnimationClipStreamingChunkPayload errors', () => {
    it('throws on invalid JSON', () => {
        expect(() => decodeAnimationClipStreamingChunkPayload('not json')).toThrow(
            AnimationValidationError
        );
    });

    it('throws on non-object JSON', () => {
        expect(() => decodeAnimationClipStreamingChunkPayload('"just a string"')).toThrow(
            AnimationValidationError
        );
    });

    it('throws on array JSON', () => {
        expect(() => decodeAnimationClipStreamingChunkPayload('[1,2,3]')).toThrow(
            AnimationValidationError
        );
    });
});

describe('applyAnimationClipStreamingChunkDefinition clipId mismatch', () => {
    it('throws when chunk clipId does not match expected', () => {
        expect(() =>
            applyAnimationClipStreamingChunkDefinition(
                baseClip,
                { clipId: 'run', tracks: [] },
                { clipId: 'walk' }
            )
        ).toThrow(AnimationValidationError);
    });
});

describe('applyAnimationClipStreamingDefinition duplicate track', () => {
    it('throws on duplicate path:target key in chunk', () => {
        expect(() =>
            applyAnimationClipStreamingChunkDefinition(baseClip, {
                tracks: [
                    { target: 'root', path: 'translation', times: [0], values: [0, 0, 0] },
                    { target: 'root', path: 'translation', times: [0.5], values: [0.5, 0, 0] },
                ],
            })
        ).toThrow(AnimationValidationError);
    });
});

describe('applyAnimationClipStreamingChunkDefinition replace-all merge mode', () => {
    it('replaces entire track regardless of time range', () => {
        const result = applyAnimationClipStreamingChunkDefinition(baseClip, {
            mergeMode: 'replace-all',
            tracks: [
                {
                    target: 'root',
                    path: 'translation',
                    times: [0, 1],
                    values: [10, 0, 0, 20, 0, 0],
                },
            ],
        });
        const track = result.tracks[0]!;
        expect(track.times).toHaveLength(2);
        expect(track.values[0]).toBeCloseTo(10, 5);
        expect(track.values[3]).toBeCloseTo(20, 5);
    });
});

describe('applyAnimationClipStreamingChunkDefinition track normalization validation', () => {
    it('throws on empty target', () => {
        expect(() =>
            applyAnimationClipStreamingChunkDefinition(baseClip, {
                tracks: [{ target: '', path: 'translation', times: [0], values: [0, 0, 0] }],
            })
        ).toThrow(AnimationValidationError);
    });

    it('throws on inconsistent values length', () => {
        expect(() =>
            applyAnimationClipStreamingChunkDefinition(baseClip, {
                tracks: [
                    {
                        target: 'root',
                        path: 'translation',
                        times: [0, 1],
                        values: [0, 0, 0], // should be 6 for 2 keyframes * 3 components
                    },
                ],
            })
        ).toThrow(AnimationValidationError);
    });

    it('throws on times length mismatch with keyframeCount', () => {
        expect(() =>
            applyAnimationClipStreamingChunkDefinition(baseClip, {
                tracks: [
                    {
                        target: 'root',
                        path: 'translation',
                        times: [0, 0.5, 1],
                        values: [0, 0, 0, 1, 0, 0],
                        keyframeCount: 2, // times has 3 entries but keyframeCount says 2
                    },
                ],
            })
        ).toThrow(AnimationValidationError);
    });
});

describe('applyAnimationClipStreamingChunkDefinition duration inference', () => {
    it('infers duration from tracks when payload has no duration', () => {
        const result = applyAnimationClipStreamingChunkDefinition(
            { id: 'walk', tracks: [] },
            {
                tracks: [
                    {
                        target: 'root',
                        path: 'translation',
                        times: [0, 2.5],
                        values: [0, 0, 0, 1, 0, 0],
                    },
                ],
            }
        );
        expect(result.duration).toBeCloseTo(2.5, 5);
    });

    it('uses explicit payload duration when provided', () => {
        const result = applyAnimationClipStreamingChunkDefinition(
            { id: 'walk', duration: 1, tracks: [] },
            {
                duration: 5,
                tracks: [
                    {
                        target: 'root',
                        path: 'translation',
                        times: [0, 1],
                        values: [0, 0, 0, 1, 0, 0],
                    },
                ],
            }
        );
        expect(result.duration).toBe(5);
    });
});

describe('encode/decode roundtrip', () => {
    it('preserves payload through encode/decode cycle', () => {
        const payload = {
            version: 1 as const,
            clipId: 'walk',
            mergeMode: 'replace-all' as const,
            startTime: 0,
            endTime: 1,
            duration: 2,
            tracks: [
                {
                    target: 'root',
                    path: 'translation' as const,
                    times: [0, 1],
                    values: [0, 0, 0, 1, 0, 0],
                },
            ],
        };
        const encoded = encodeAnimationClipStreamingChunkPayload(payload);
        const decoded = decodeAnimationClipStreamingChunkPayload(encoded);
        expect(decoded.clipId).toBe('walk');
        expect(decoded.mergeMode).toBe('replace-all');
        expect(decoded.tracks).toHaveLength(1);
        expect(decoded.tracks[0]!.target).toBe('root');
    });
});
