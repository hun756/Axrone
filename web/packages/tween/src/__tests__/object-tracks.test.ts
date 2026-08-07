import { describe, expect, it } from 'vitest';
import { createObjectTweenTrack } from '../object-tracks';
import { createTweenPropertyAccessor } from '../property-accessor';
import { Interpolation } from '../interpolation';

describe('object-tracks', () => {
    describe('createObjectTweenTrack', () => {
        it('number -> NumberTrack', () => {
            const accessor = createTweenPropertyAccessor('x');
            const track = createObjectTweenTrack(accessor, 0, 100);
            expect(track).toBeDefined();
            expect(track!.path).toBe('x');
        });

        it('array -> SequenceTrack', () => {
            const accessor = createTweenPropertyAccessor('position');
            const track = createObjectTweenTrack(accessor, [0, 0, 0], [100, 50, 25]);
            expect(track).toBeDefined();
            expect(track!.path).toBe('position');
        });

        it('typedarray -> SequenceTrack', () => {
            const accessor = createTweenPropertyAccessor('data');
            const track = createObjectTweenTrack(
                accessor,
                new Float32Array([0, 0, 0]),
                new Float32Array([1, 2, 3])
            );
            expect(track).toBeDefined();
            expect(track!.path).toBe('data');
        });

        it('null for unsupported (string end)', () => {
            const accessor = createTweenPropertyAccessor('name');
            const track = createObjectTweenTrack(accessor, 'hello', 'world');
            expect(track).toBeNull();
        });
    });

    describe('NumberTrack', () => {
        it('apply interpolates', () => {
            const accessor = createTweenPropertyAccessor('x');
            const track = createObjectTweenTrack(accessor, 0, 100)!;
            const target: any = { x: 0 };
            track.apply(target, 0.5, Interpolation.Linear, [0, 0]);
            expect(target.x).toBeCloseTo(50, 10);
        });

        it('apply at progress=0 returns start', () => {
            const accessor = createTweenPropertyAccessor('x');
            const track = createObjectTweenTrack(accessor, 10, 100)!;
            const target: any = { x: 0 };
            track.apply(target, 0, Interpolation.Linear, [0, 0]);
            expect(target.x).toBeCloseTo(10, 10);
        });

        it('apply at progress=1 returns end', () => {
            const accessor = createTweenPropertyAccessor('x');
            const track = createObjectTweenTrack(accessor, 10, 100)!;
            const target: any = { x: 0 };
            track.apply(target, 1, Interpolation.Linear, [0, 0]);
            expect(target.x).toBeCloseTo(100, 10);
        });

        it('reset restores start', () => {
            const accessor = createTweenPropertyAccessor('x');
            const track = createObjectTweenTrack(accessor, 10, 100)!;
            const target: any = { x: 50 };
            track.reset(target);
            expect(target.x).toBe(10);
        });
    });

    describe('SequenceTrack', () => {
        it('apply with Linear interpolation', () => {
            const accessor = createTweenPropertyAccessor('pos');
            const track = createObjectTweenTrack(accessor, [0, 0, 0], [100, 50, 25])!;
            const target: any = { pos: [0, 0, 0] };
            track.apply(target, 0.5, Interpolation.Linear, [0, 0]);
            expect(target.pos[0]).toBeCloseTo(50, 5);
            expect(target.pos[1]).toBeCloseTo(25, 5);
            expect(target.pos[2]).toBeCloseTo(12.5, 5);
        });

        it('apply with non-Linear interpolation uses twoValueBuffer', () => {
            const accessor = createTweenPropertyAccessor('pos');
            const track = createObjectTweenTrack(accessor, [0, 0], [100, 200])!;
            const target: any = { pos: [0, 0] };
            const customInterp = (v: ArrayLike<number>, k: number) => v[0] + (v[1] - v[0]) * k * k;
            track.apply(target, 0.5, customInterp, [0, 0]);
            expect(target.pos[0]).toBeCloseTo(25, 5);
            expect(target.pos[1]).toBeCloseTo(50, 5);
        });

        it('reset restores start values (in-place for typed array)', () => {
            const accessor = createTweenPropertyAccessor('data');
            const start = new Float32Array([1, 2, 3]);
            const end = new Float32Array([10, 20, 30]);
            const track = createObjectTweenTrack(accessor, start, end)!;
            const target: any = { data: new Float32Array([5, 10, 15]) };
            track.reset(target);
            expect(Array.from(target.data)).toEqual([1, 2, 3]);
        });

        it('reset restores start values (clone for regular array)', () => {
            const accessor = createTweenPropertyAccessor('pos');
            const track = createObjectTweenTrack(accessor, [10, 20, 30], [100, 200, 300])!;
            const target: any = { pos: [50, 100, 150] };
            track.reset(target);
            expect(target.pos).toEqual([10, 20, 30]);
        });
    });
});
