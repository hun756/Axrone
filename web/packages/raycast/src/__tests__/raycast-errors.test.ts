import { describe, it, expect } from 'vitest';
import {
    RaycastError,
    InvalidRayError,
    RaycastQueryError,
    SpatialStructureError,
    BVHBuildError,
} from '../index';

describe('raycast-errors — hierarchy and contracts', () => {
    describe('RaycastError', () => {
        it('is an instance of Error', () => {
            const err = new RaycastError('test');
            expect(err).toBeInstanceOf(Error);
            expect(err).toBeInstanceOf(RaycastError);
        });

        it('has name "RaycastError"', () => {
            expect(new RaycastError('x').name).toBe('RaycastError');
        });

        it('preserves custom message', () => {
            expect(new RaycastError('custom').message).toBe('custom');
        });
    });

    describe('InvalidRayError', () => {
        it('is an instance of RaycastError', () => {
            const err = new InvalidRayError();
            expect(err).toBeInstanceOf(RaycastError);
            expect(err).toBeInstanceOf(InvalidRayError);
        });

        it('has default message', () => {
            expect(new InvalidRayError().message).toBe('Invalid ray parameters');
        });

        it('has name "InvalidRayError"', () => {
            expect(new InvalidRayError().name).toBe('InvalidRayError');
        });

        it('accepts custom message', () => {
            expect(new InvalidRayError('bad origin').message).toBe('bad origin');
        });
    });

    describe('RaycastQueryError', () => {
        it('is an instance of RaycastError', () => {
            expect(new RaycastQueryError()).toBeInstanceOf(RaycastError);
        });

        it('has default message', () => {
            expect(new RaycastQueryError().message).toBe('Invalid raycast query');
        });

        it('has name "RaycastQueryError"', () => {
            expect(new RaycastQueryError().name).toBe('RaycastQueryError');
        });
    });

    describe('SpatialStructureError', () => {
        it('is an instance of RaycastError', () => {
            expect(new SpatialStructureError()).toBeInstanceOf(RaycastError);
        });

        it('has default message', () => {
            expect(new SpatialStructureError().message).toBe('Spatial structure operation failed');
        });

        it('has name "SpatialStructureError"', () => {
            expect(new SpatialStructureError().name).toBe('SpatialStructureError');
        });
    });

    describe('BVHBuildError', () => {
        it('is an instance of RaycastError', () => {
            expect(new BVHBuildError()).toBeInstanceOf(RaycastError);
        });

        it('has default message', () => {
            expect(new BVHBuildError().message).toBe('BVH build failed');
        });

        it('has name "BVHBuildError"', () => {
            expect(new BVHBuildError().name).toBe('BVHBuildError');
        });
    });
});
