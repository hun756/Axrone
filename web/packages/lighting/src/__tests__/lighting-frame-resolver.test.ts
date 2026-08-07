import { Vec3 } from '@axrone/numeric';
import { describe, expect, it } from 'vitest';
import { LightSortMode } from '../constants';
import { LightingDisposedError } from '../errors';
import { LightingFrameResolver } from '../frame-resolver';
import { LightingRig } from '../rig';
import {
    createLightingUniformLayout,
    createLightingUniformValueMap,
} from '../uniform-layout';

describe('LightingFrameResolver', () => {
    it('preserves insertion order under none sort and exposes uniform layout metadata', () => {
        const rig = new LightingRig();

        rig.addPoint({
            id: 'first',
            position: [0, 0, 10],
            range: 20,
            intensity: 1,
        });
        rig.addPoint({
            id: 'second',
            position: [0, 0, 1],
            range: 20,
            intensity: 50,
        });

        const resolver = new LightingFrameResolver({
            capacity: {
                maxDirectionalLights: 0,
                maxPointLights: 1,
                maxSpotLights: 0,
                maxLocalLights: 1,
            },
            sortMode: LightSortMode.None,
        });
        const state = resolver.resolve(rig, {
            cameraPosition: [0, 0, 0],
        });
        const uniforms = createLightingUniformValueMap(state);
        const layout = createLightingUniformLayout({
            maxDirectionalLights: 0,
            maxPointLights: 1,
            maxSpotLights: 0,
            maxLocalLights: 1,
        });

        expect(Array.from(state.pointPositions)).toEqual([0, 0, 10]);
        expect(state.stats.selectedPointCount).toBe(1);
        expect(state.stats.omittedPointCount).toBe(1);
        expect(layout.defines.AXRONE_LIGHTING_MAX_LOCAL_LIGHTS).toBe('1');
        expect(uniforms.u_PointLightCount).toBe(1);
        expect(uniforms.u_LocalLightCount).toBe(1);
    });

    it('ranks priority mode deterministically and uses insertion order as a tie-breaker', () => {
        const rig = new LightingRig();

        rig.addPoint({
            id: 'first',
            position: [1, 0, 0],
            range: 10,
            intensity: 1,
            priority: 2,
        });
        rig.addPoint({
            id: 'second',
            position: [2, 0, 0],
            range: 10,
            intensity: 1,
            priority: 2,
        });
        rig.addPoint({
            id: 'winner',
            position: [3, 0, 0],
            range: 10,
            intensity: 1,
            priority: 3,
        });

        const state = new LightingFrameResolver({
            capacity: {
                maxDirectionalLights: 0,
                maxPointLights: 2,
                maxSpotLights: 0,
                maxLocalLights: 2,
            },
            sortMode: LightSortMode.Priority,
        }).resolve(rig);

        expect(Array.from(state.pointPositions)).toEqual([3, 0, 0, 1, 0, 0]);
        expect(Array.from(state.localLightKinds)).toEqual([1, 1]);
    });

    it('changes point influence when camera context is present', () => {
        const rig = new LightingRig();

        rig.addPoint({
            id: 'far-strong',
            position: [0, 0, 20],
            range: 4,
            intensity: 11,
        });
        rig.addPoint({
            id: 'near-weak',
            position: [0, 0, 1],
            range: 10,
            intensity: 1,
        });

        const resolver = new LightingFrameResolver({
            capacity: {
                maxDirectionalLights: 0,
                maxPointLights: 1,
                maxSpotLights: 0,
                maxLocalLights: 1,
            },
            sortMode: LightSortMode.Influence,
        });

        const withoutCameraPositions = Array.from(resolver.resolve(rig).pointPositions);
        const withTuplePositions = Array.from(
            resolver.resolve(rig, {
                cameraPosition: [0, 0, 0],
            }).pointPositions
        );
        const withVecState = resolver.resolve(rig, {
            cameraPosition: new Vec3(0, 0, 0),
        });
        const withVecPositions = Array.from(withVecState.pointPositions);
        const cachedState = resolver.resolve(rig, {
            cameraPosition: new Vec3(0, 0, 0),
        });

        expect(withoutCameraPositions).toEqual([0, 0, 20]);
        expect(withTuplePositions).toEqual([0, 0, 1]);
        expect(withVecPositions).toEqual([0, 0, 1]);
        expect(cachedState).toBe(withVecState);
    });

    it('suppresses spot influence outside the cone and re-enables it at the light position', () => {
        const rig = new LightingRig();

        rig.addPoint({
            id: 'point',
            position: [0, 0, 1],
            range: 10,
            intensity: 1,
        });
        rig.addSpot({
            id: 'spot',
            position: [0, 0, 5],
            direction: [0, 1, 0],
            range: 10,
            intensity: 20,
            coneMode: 'cosine',
            innerConeCosine: 0.95,
            outerConeCosine: 0.8,
        });

        const resolver = new LightingFrameResolver({
            capacity: {
                maxDirectionalLights: 0,
                maxPointLights: 1,
                maxSpotLights: 1,
                maxLocalLights: 1,
            },
            sortMode: LightSortMode.Influence,
        });

        const outsideConeKinds = Array.from(
            resolver.resolve(rig, {
                cameraPosition: [0, 0, 0],
            }).localLightKinds
        );
        const atLightKinds = Array.from(
            resolver.resolve(rig, {
                cameraPosition: [0, 0, 5],
            }).localLightKinds
        );

        expect(outsideConeKinds).toEqual([1]);
        expect(atLightKinds).toEqual([2]);
    });

    it('supports zero-capacity resolvers and disposal boundaries', () => {
        const rig = new LightingRig();

        rig.addDirectional({
            direction: [0, -1, 0],
        });

        const resolver = new LightingFrameResolver({
            capacity: {
                maxDirectionalLights: 0,
                maxPointLights: 0,
                maxSpotLights: 0,
                maxLocalLights: 0,
            },
        });
        const state = resolver.resolve(rig);

        expect(resolver.capacity.maxLocalLights).toBe(0);
        expect(resolver.isDisposed).toBe(false);
        expect(state.stats.selectedDirectionalCount).toBe(0);
        expect(state.stats.omittedDirectionalCount).toBe(1);

        resolver.dispose();
        expect(resolver.isDisposed).toBe(true);

        resolver.dispose();
        expect(() => resolver.resolve(rig)).toThrow(LightingDisposedError);
    });

    it('returns cached state when camera and version are unchanged', () => {
        const rig = new LightingRig();
        rig.addPoint({ id: 'near', position: [0, 0, 1], range: 5, intensity: 2 });
        rig.addPoint({ id: 'far', position: [0, 0, 20], range: 5, intensity: 2 });

        const resolver = new LightingFrameResolver({
            capacity: { maxPointLights: 1, maxLocalLights: 1 },
            sortMode: LightSortMode.Influence,
        });

        const first = resolver.resolve(rig, { cameraPosition: new Vec3(0, 0, 0) });
        const cachedVec3 = resolver.resolve(rig, { cameraPosition: new Vec3(0, 0, 0) });
        expect(cachedVec3).toBe(first);

        const cachedTuple = resolver.resolve(rig, { cameraPosition: [0, 0, 0] });
        expect(cachedTuple).toBe(first);

        const afterUndefined = resolver.resolve(rig);
        expect(afterUndefined).toBe(first);

        const positionsBeforeChange = Array.from(first.pointPositions);

        const afterCameraChange = resolver.resolve(rig, { cameraPosition: [0, 0, 20] });
        expect(afterCameraChange).toBe(first);
        const positionsAfterChange = Array.from(first.pointPositions);
        expect(positionsAfterChange).not.toEqual(positionsBeforeChange);
    });

    it('handles very-short-range point lights by deprioritizing distant ones', () => {
        const rig = new LightingRig();
        rig.addPoint({ id: 'short-range', position: [0, 0, 1], range: 0.1, intensity: 10 });
        rig.addPoint({ id: 'normal', position: [0, 0, 1], range: 10, intensity: 1 });

        const resolver = new LightingFrameResolver({
            capacity: { maxPointLights: 1, maxLocalLights: 1 },
            sortMode: LightSortMode.Influence,
        });

        const state = resolver.resolve(rig, { cameraPosition: [0, 0, 0] });
        expect(state.stats.selectedPointCount).toBe(1);
    });

    it('returns full intensity when camera is at the exact spot light position', () => {
        const rig = new LightingRig();
        rig.addSpot({
            id: 'at-pos',
            position: [5, 5, 5],
            direction: [0, 1, 0],
            range: 10,
            intensity: 20,
            coneMode: 'cosine',
            innerConeCosine: 0.95,
            outerConeCosine: 0.8,
        });

        const resolver = new LightingFrameResolver({
            capacity: { maxPointLights: 0, maxSpotLights: 1, maxLocalLights: 1 },
            sortMode: LightSortMode.Influence,
        });

        const state = resolver.resolve(rig, { cameraPosition: [5, 5, 5] });
        expect(state.stats.selectedSpotCount).toBe(1);
        expect(Array.from(state.spotIntensities)[0]).toBe(20);
    });

    it('treats equal inner and outer cone cosines as full intensity within cone', () => {
        const rig = new LightingRig();
        rig.addSpot({
            id: 'equal-cone',
            position: [0, 0, 5],
            direction: [0, 0, -1],
            range: 10,
            intensity: 10,
            coneMode: 'cosine',
            innerConeCosine: 0.9,
            outerConeCosine: 0.9,
        });

        const resolver = new LightingFrameResolver({
            capacity: { maxSpotLights: 1, maxLocalLights: 1 },
            sortMode: LightSortMode.Influence,
        });

        const state = resolver.resolve(rig, { cameraPosition: [0, 0, 0] });
        expect(state.stats.selectedSpotCount).toBe(1);
    });

    it('reports correct omitted stats when lights exceed capacity', () => {
        const rig = new LightingRig();
        rig.addDirectional({ id: 'd1', direction: [0, -1, 0], intensity: 5 });
        rig.addDirectional({ id: 'd2', direction: [1, -1, 0], intensity: 3 });
        rig.addDirectional({ id: 'd3', direction: [-1, -1, 0], intensity: 1 });
        rig.addPoint({ id: 'p1', position: [0, 0, 1], range: 5, intensity: 2 });
        rig.addPoint({ id: 'p2', position: [0, 0, 2], range: 5, intensity: 4 });
        rig.addPoint({ id: 'p3', position: [0, 0, 3], range: 5, intensity: 6 });

        const resolver = new LightingFrameResolver({
            capacity: {
                maxDirectionalLights: 1,
                maxPointLights: 1,
                maxSpotLights: 0,
                maxLocalLights: 1,
            },
            sortMode: LightSortMode.Priority,
        });

        const state = resolver.resolve(rig);

        expect(state.stats.totalDirectionalCount).toBe(3);
        expect(state.stats.totalPointCount).toBe(3);
        expect(state.stats.totalSpotCount).toBe(0);
        expect(state.stats.totalLightCount).toBe(6);
        expect(state.stats.selectedDirectionalCount).toBe(1);
        expect(state.stats.selectedPointCount).toBe(1);
        expect(state.stats.selectedSpotCount).toBe(0);
        expect(state.stats.selectedLocalLightCount).toBe(1);
        expect(state.stats.omittedDirectionalCount).toBe(2);
        expect(state.stats.omittedPointCount).toBe(2);
        expect(state.stats.omittedSpotCount).toBe(0);
        expect(state.stats.omittedLocalLightCount).toBe(2);
    });

    it('skips disabled lights in sort mode None and counts them in totals', () => {
        const rig = new LightingRig();
        rig.addDirectional({ id: 'd-on', direction: [0, -1, 0], enabled: true });
        rig.addDirectional({ id: 'd-off', direction: [1, 0, 0], enabled: false });
        rig.addPoint({ id: 'p-on', position: [0, 0, 1], range: 5, enabled: true });
        rig.addPoint({ id: 'p-off', position: [0, 0, 2], range: 5, enabled: false });
        rig.addSpot({
            id: 's-off',
            direction: [0, -1, 0],
            enabled: false,
            coneMode: 'cosine',
            innerConeCosine: 0.9,
            outerConeCosine: 0.7,
        });

        const resolver = new LightingFrameResolver({
            capacity: {
                maxDirectionalLights: 4,
                maxPointLights: 4,
                maxSpotLights: 4,
                maxLocalLights: 8,
            },
            sortMode: LightSortMode.None,
        });

        const state = resolver.resolve(rig);

        expect(state.stats.totalDirectionalCount).toBe(1);
        expect(state.stats.totalPointCount).toBe(1);
        expect(state.stats.totalSpotCount).toBe(0);
        expect(state.stats.selectedDirectionalCount).toBe(1);
        expect(state.stats.selectedPointCount).toBe(1);
        expect(state.stats.selectedSpotCount).toBe(0);
    });

    it('handles local light capacity overflow with mixed point and spot lights', () => {
        const rig = new LightingRig();
        rig.addPoint({ id: 'p1', position: [0, 0, 1], range: 5, intensity: 10 });
        rig.addPoint({ id: 'p2', position: [0, 0, 2], range: 5, intensity: 8 });
        rig.addSpot({
            id: 's1',
            position: [0, 0, 3],
            direction: [0, 0, -1],
            range: 5,
            intensity: 6,
            coneMode: 'cosine',
            innerConeCosine: 0.9,
            outerConeCosine: 0.7,
        });

        const resolver = new LightingFrameResolver({
            capacity: {
                maxPointLights: 4,
                maxSpotLights: 4,
                maxLocalLights: 2,
            },
            sortMode: LightSortMode.Influence,
        });

        const state = resolver.resolve(rig, { cameraPosition: [0, 0, 0] });

        expect(state.stats.selectedPointCount).toBe(2);
        expect(state.stats.selectedSpotCount).toBe(1);
        expect(state.stats.selectedLocalLightCount).toBe(2);
        expect(state.stats.omittedLocalLightCount).toBe(1);
    });

    it('writes correct per-kind buffer values under sort mode None', () => {
        const rig = new LightingRig();
        rig.addDirectional({
            id: 'sun',
            direction: [0, -1, 0],
            color: [1, 0.9, 0.8],
            ambient: [0.1, 0.1, 0.08],
            intensity: 3,
        });
        rig.addPoint({
            id: 'bulb',
            position: [1, 2, 3],
            color: [0, 1, 0],
            range: 7,
            intensity: 2,
        });
        rig.addSpot({
            id: 'lamp',
            position: [4, 5, 6],
            direction: [0, 0, -1],
            color: [0, 0, 1],
            range: 9,
            intensity: 4,
            coneMode: 'cosine',
            innerConeCosine: 0.85,
            outerConeCosine: 0.65,
        });

        const resolver = new LightingFrameResolver({
            capacity: {
                maxDirectionalLights: 1,
                maxPointLights: 1,
                maxSpotLights: 1,
                maxLocalLights: 2,
            },
            sortMode: LightSortMode.None,
        });

        const state = resolver.resolve(rig);

        expect(Array.from(state.directionalDirections)).toEqual([0, -1, 0]);
        expect(state.directionalColors[0]).toBeCloseTo(1);
        expect(state.directionalColors[1]).toBeCloseTo(0.9);
        expect(state.directionalColors[2]).toBeCloseTo(0.8);
        expect(state.directionalAmbientColors[0]).toBeCloseTo(0.1);
        expect(state.directionalAmbientColors[1]).toBeCloseTo(0.1);
        expect(state.directionalAmbientColors[2]).toBeCloseTo(0.08);
        expect(Array.from(state.directionalIntensities)).toEqual([3]);
        expect(Array.from(state.pointPositions)).toEqual([1, 2, 3]);
        expect(state.pointColors[0]).toBeCloseTo(0);
        expect(state.pointColors[1]).toBeCloseTo(1);
        expect(state.pointColors[2]).toBeCloseTo(0);
        expect(Array.from(state.pointIntensities)).toEqual([2]);
        expect(Array.from(state.pointRanges)).toEqual([7]);
        expect(Array.from(state.spotPositions)).toEqual([4, 5, 6]);
        expect(Array.from(state.spotDirections)).toEqual([0, 0, -1]);
        expect(state.spotColors[0]).toBeCloseTo(0);
        expect(state.spotColors[1]).toBeCloseTo(0);
        expect(state.spotColors[2]).toBeCloseTo(1);
        expect(Array.from(state.spotIntensities)).toEqual([4]);
        expect(Array.from(state.spotRanges)).toEqual([9]);
        expect(Array.from(state.spotInnerConeCosines)[0]).toBeCloseTo(0.85);
        expect(Array.from(state.spotOuterConeCosines)[0]).toBeCloseTo(0.65);
    });
});