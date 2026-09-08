import { tuple2, tuple3, tuple4 } from '@axrone/utility';
import type { AnimationTrackDefinition } from './types';

// Re-export zero-cost tuple utilities for backward compatibility within this package.
// These replace the previous Object.freeze-based implementations with compile-time-only
// readonly guarantees, eliminating runtime freeze overhead.
export const freezeTuple2 = tuple2;
export const freezeTuple3 = tuple3;
export const freezeTuple4 = tuple4;

export const isFiniteNumber = (value: unknown): value is number =>
    typeof value === 'number' && Number.isFinite(value);

const EMPTY_SPREAD: Readonly<Record<string, never>> = Object.freeze({}) as Readonly<Record<string, never>>;

export const spreadIfFinite = (key: string, value: unknown): Readonly<Record<string, unknown>> =>
    isFiniteNumber(value) ? { [key]: value } as Readonly<Record<string, unknown>> : EMPTY_SPREAD;

export const spreadIfNonEmptyString = (key: string, value: unknown): Readonly<Record<string, unknown>> =>
    typeof value === 'string' && value.length > 0 ? { [key]: value } as Readonly<Record<string, unknown>> : EMPTY_SPREAD;

export const getTrackComponentCount = (track: AnimationTrackDefinition): number => {
    if (typeof track.valueComponentCount === 'number' && Number.isFinite(track.valueComponentCount)) {
        return track.valueComponentCount;
    }
    switch (track.path) {
        case 'translation':
        case 'scale':
            return 3;
        case 'rotation':
            return 4;
        case 'weights': {
            const keyframeCount = track.keyframeCount ?? track.times.length;
            if (keyframeCount <= 0) {
                return 0;
            }
            return Math.max(1, Math.trunc(track.values.length / keyframeCount));
        }
        default:
            return 0;
    }
};
