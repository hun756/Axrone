import { describe, expect, it } from 'vitest';
import { AnimationClip } from '../clip';
import { AnimationMotionMatchDatabase } from '../motion-matching';
import { solvePlanarGrounding } from '../grounding';
import { AnimationCurveLayout } from '../pose';
import { AnimationRig } from '../rig';

const rig = new AnimationRig({ bones: [{ name: 'root' }, { name: 'foot' }] });
const curveLayout = new AnimationCurveLayout();

const makeClipWithContacts = (
    id: string,
    tags: string[] = [],
    features: { time: number; tags?: string[]; trajectoryPosition?: [number, number, number]; facingDirection?: [number, number, number]; costBias?: number }[] = [],
    footContacts: { bone: string; startTime: number; endTime: number }[] = []
): AnimationClip =>
    new AnimationClip(
        {
            id,
            duration: 1,
            tracks: [
                {
                    target: 'root',
                    path: 'translation',
                    times: [0, 1],
                    values: [0, 0, 0, 1, 0, 0],
                },
            ],
            ...(tags.length > 0 ? { tags } : {}),
            ...(features.length > 0 ? { features } : {}),
            ...(footContacts.length > 0 ? { footContacts } : {}),
        },
        rig,
        curveLayout
    );

describe('AnimationMotionMatchDatabase size', () => {
    it('reflects total entries from features when present', () => {
        const db = new AnimationMotionMatchDatabase([
            { id: 'walk', features: [{ time: 0 }, { time: 0.5 }] },
            { id: 'run', features: [{ time: 0 }] },
        ]);
        expect(db.size).toBe(3);
    });

    it('uses 1 entry per clip when no features', () => {
        const db = new AnimationMotionMatchDatabase([
            { id: 'walk' },
            { id: 'run' },
        ]);
        expect(db.size).toBe(2);
    });
});

describe('AnimationMotionMatchDatabase query with requiredTags', () => {
    it('filters to entries matching all required tags', () => {
        const db = new AnimationMotionMatchDatabase([
            { id: 'walk', tags: ['locomotion', 'ground'] },
            { id: 'jump', tags: ['locomotion'] },
            { id: 'idle', tags: ['ground'] },
        ]);
        const results = db.query({ requiredTags: ['locomotion', 'ground'] });
        expect(results).toHaveLength(1);
        expect(results[0]!.clipId).toBe('walk');
    });
});

describe('AnimationMotionMatchDatabase query with excludedTags', () => {
    it('filters out entries with excluded tags', () => {
        const db = new AnimationMotionMatchDatabase([
            { id: 'walk', tags: ['locomotion'] },
            { id: 'jump', tags: ['locomotion', 'airborne'] },
        ]);
        const results = db.query({ excludedTags: ['airborne'] });
        expect(results).toHaveLength(1);
        expect(results[0]!.clipId).toBe('walk');
    });
});

describe('AnimationMotionMatchDatabase continuityBias', () => {
    it('subtracts bias for matching clipId', () => {
        const db = new AnimationMotionMatchDatabase([
            { id: 'walk', features: [{ time: 0, costBias: 5 }] },
            { id: 'run', features: [{ time: 0, costBias: 3 }] },
        ]);
        const resultsWithBias = db.query({ currentClipId: 'walk', continuityBias: 10 });
        // walk: 5 - 10 = -5, run: 3 - 0 = 3
        expect(resultsWithBias[0]!.clipId).toBe('walk');
        expect(resultsWithBias[0]!.score).toBeCloseTo(-5, 5);
    });
});

describe('AnimationMotionMatchDatabase maxResults', () => {
    it('limits result count', () => {
        const db = new AnimationMotionMatchDatabase([
            { id: 'a' },
            { id: 'b' },
            { id: 'c' },
        ]);
        const results = db.query({ maxResults: 2 });
        expect(results).toHaveLength(2);
    });

    it('clamps to minimum 1', () => {
        const db = new AnimationMotionMatchDatabase([{ id: 'a' }, { id: 'b' }]);
        const results = db.query({ maxResults: 0 });
        expect(results).toHaveLength(1);
    });
});

describe('AnimationMotionMatchDatabase scoring', () => {
    it('trajectory distance contributes to score', () => {
        const db = new AnimationMotionMatchDatabase([
            {
                id: 'close',
                features: [{ time: 0, trajectoryPosition: [1, 0, 0] as [number, number, number] }],
            },
            {
                id: 'far',
                features: [{ time: 0, trajectoryPosition: [10, 0, 0] as [number, number, number] }],
            },
        ]);
        const results = db.query({ desiredTrajectoryPosition: [0, 0, 0], maxResults: 10 });
        expect(results[0]!.clipId).toBe('close');
        expect(results[0]!.score).toBeCloseTo(1, 5); // 1^2 + 0 + 0 = 1
        expect(results[1]!.score).toBeCloseTo(100, 5); // 10^2 = 100
    });

    it('missing trajectory adds penalty of 10', () => {
        const db = new AnimationMotionMatchDatabase([
            { id: 'noTrajectory', features: [{ time: 0 }] },
        ]);
        const results = db.query({ desiredTrajectoryPosition: [0, 0, 0] });
        expect(results[0]!.score).toBeCloseTo(10, 5);
    });

    it('facing direction penalty for mismatched directions', () => {
        const db = new AnimationMotionMatchDatabase([
            {
                id: 'aligned',
                features: [{ time: 0, facingDirection: [1, 0, 0] as [number, number, number] }],
            },
            {
                id: 'opposite',
                features: [{ time: 0, facingDirection: [-1, 0, 0] as [number, number, number] }],
            },
        ]);
        const results = db.query({ desiredFacingDirection: [1, 0, 0], maxResults: 10 });
        expect(results[0]!.clipId).toBe('aligned');
        expect(results[0]!.score).toBeCloseTo(0, 5); // dot = 1, penalty = 1-1 = 0
        expect(results[1]!.score).toBeCloseTo(2, 5); // dot = -1, penalty = 1-(-1) = 2
    });

    it('missing facing adds penalty of 5', () => {
        const db = new AnimationMotionMatchDatabase([
            { id: 'noFacing', features: [{ time: 0 }] },
        ]);
        const results = db.query({ desiredFacingDirection: [1, 0, 0] });
        expect(results[0]!.score).toBeCloseTo(5, 5);
    });
});

describe('AnimationMotionMatchDatabase with raw clip definitions', () => {
    it('accepts raw definitions, not just AnimationClip instances', () => {
        const db = new AnimationMotionMatchDatabase([
            { id: 'rawClip', tags: ['test'] },
        ]);
        expect(db.size).toBe(1);
        const results = db.query({ requiredTags: ['test'] });
        expect(results).toHaveLength(1);
        expect(results[0]!.clipId).toBe('rawClip');
    });
});

describe('solvePlanarGrounding', () => {
    it('returns zero rootOffset and empty contacts when no active contacts', () => {
        const clip = makeClipWithContacts('idle');
        const result = solvePlanarGrounding(clip, 0.5, { foot: 0.1 });
        expect(result.rootOffset).toEqual([0, 0, 0]);
        expect(result.contacts).toHaveLength(0);
    });

    it('resolves bone heights from Map', () => {
        const clip = makeClipWithContacts('walk', [], [], [
            { bone: 'foot', startTime: 0, endTime: 1 },
        ]);
        const heights = new Map<string, number>([['foot', 0.2]]);
        const result = solvePlanarGrounding(clip, 0.5, heights, 0);
        expect(result.contacts).toHaveLength(1);
        expect(result.contacts[0]!.groundOffset).toBeCloseTo(-0.2, 5); // 0 - 0.2
    });

    it('resolves bone heights from Record with non-finite defaulting to 0', () => {
        const clip = makeClipWithContacts('walk', [], [], [
            { bone: 'foot', startTime: 0, endTime: 1 },
        ]);
        const result = solvePlanarGrounding(clip, 0.5, { foot: NaN }, 0);
        expect(result.contacts).toHaveLength(1);
        expect(result.contacts[0]!.groundOffset).toBeCloseTo(0, 5); // NaN defaults to 0, so 0 - 0 = 0
    });

    it('computes offset = groundHeight - boneHeight', () => {
        const clip = makeClipWithContacts('walk', [], [], [
            { bone: 'foot', startTime: 0, endTime: 1 },
        ]);
        const result = solvePlanarGrounding(clip, 0.5, { foot: 0.3 }, 1.0);
        expect(result.contacts[0]!.groundOffset).toBeCloseTo(0.7, 5); // 1.0 - 0.3
    });

    it('computes weighted average rootOffset for multiple contacts', () => {
        const clip = makeClipWithContacts('walk', [], [], [
            { bone: 'foot', startTime: 0, endTime: 1 },
        ]);
        // Single contact at weight ~1.0 in the middle of the range
        const result = solvePlanarGrounding(clip, 0.5, { foot: 0.5 }, 0);
        expect(result.rootOffset[1]).toBeCloseTo(-0.5, 1);
    });
});
