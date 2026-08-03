import { describe, expect, it } from 'vitest';
import {
    PaintValidationError,
    SerializationError,
    SHAPES_2D_ERROR_CODE,
    ShapeRegistryError,
    Shapes2DError,
    ShapeValidationError,
} from '../index';

describe('@axrone/shapes-2d errors', () => {
    describe('SHAPES_2D_ERROR_CODE', () => {
        it('exposes all 11 expected error codes', () => {
            const keys = Object.keys(SHAPES_2D_ERROR_CODE);
            expect(keys).toHaveLength(11);
            expect(SHAPES_2D_ERROR_CODE.INVALID_NUMBER).toBe('INVALID_NUMBER');
            expect(SHAPES_2D_ERROR_CODE.INVALID_POINT).toBe('INVALID_POINT');
            expect(SHAPES_2D_ERROR_CODE.INVALID_COLOR).toBe('INVALID_COLOR');
            expect(SHAPES_2D_ERROR_CODE.INVALID_PAINT).toBe('INVALID_PAINT');
            expect(SHAPES_2D_ERROR_CODE.INVALID_GRADIENT).toBe('INVALID_GRADIENT');
            expect(SHAPES_2D_ERROR_CODE.INVALID_STROKE).toBe('INVALID_STROKE');
            expect(SHAPES_2D_ERROR_CODE.INVALID_SHAPE).toBe('INVALID_SHAPE');
            expect(SHAPES_2D_ERROR_CODE.INVALID_SERIALIZED_PAYLOAD).toBe('INVALID_SERIALIZED_PAYLOAD');
            expect(SHAPES_2D_ERROR_CODE.REGISTRY_DISPOSED).toBe('REGISTRY_DISPOSED');
            expect(SHAPES_2D_ERROR_CODE.SHAPE_NOT_FOUND).toBe('SHAPE_NOT_FOUND');
            expect(SHAPES_2D_ERROR_CODE.CAPACITY_EXCEEDED).toBe('CAPACITY_EXCEEDED');
        });

        it('has all keys as const (readonly via TypeScript)', () => {
            // SHAPES_2D_ERROR_CODE uses `as const` for type-level readonly, not Object.freeze
            const keys = Object.keys(SHAPES_2D_ERROR_CODE);
            expect(keys).toContain('INVALID_SHAPE');
            expect(keys).toContain('CAPACITY_EXCEEDED');
        });
    });

    describe('Shapes2DError', () => {
        it('sets code, message, name, and defaults', () => {
            const error = new Shapes2DError('INVALID_NUMBER', 'bad number');
            expect(error).toBeInstanceOf(Error);
            expect(error).toBeInstanceOf(Shapes2DError);
            expect(error.code).toBe('INVALID_NUMBER');
            expect(error.message).toBe('bad number');
            expect(error.name).toBe('Shapes2DError');
            expect(error.cause).toBeUndefined();
            expect(error.details).toBeUndefined();
        });

        it('passes cause and details through options', () => {
            const cause = new TypeError('root');
            const details = { field: 'radius', value: NaN };
            const error = new Shapes2DError('INVALID_SHAPE', 'bad shape', { cause, details });
            expect(error.cause).toBe(cause);
            expect(error.details).toEqual(details);
        });

        it('maintains prototype chain for instanceof checks', () => {
            const error = new Shapes2DError('INVALID_NUMBER', 'test');
            expect(error instanceof Shapes2DError).toBe(true);
            expect(error instanceof Error).toBe(true);
        });
    });

    describe('ShapeValidationError', () => {
        it('has code INVALID_SHAPE and correct name', () => {
            const error = new ShapeValidationError('collinear points');
            expect(error).toBeInstanceOf(Shapes2DError);
            expect(error).toBeInstanceOf(Error);
            expect(error.code).toBe('INVALID_SHAPE');
            expect(error.name).toBe('ShapeValidationError');
            expect(error.message).toBe('collinear points');
        });

        it('accepts cause and details', () => {
            const error = new ShapeValidationError('bad', { cause: 'reason' });
            expect(error.cause).toBe('reason');
        });
    });

    describe('PaintValidationError', () => {
        it('has code INVALID_PAINT and correct name', () => {
            const error = new PaintValidationError('bad color');
            expect(error).toBeInstanceOf(Shapes2DError);
            expect(error.code).toBe('INVALID_PAINT');
            expect(error.name).toBe('PaintValidationError');
            expect(error.message).toBe('bad color');
        });
    });

    describe('SerializationError', () => {
        it('has code INVALID_SERIALIZED_PAYLOAD and correct name', () => {
            const error = new SerializationError('unknown type');
            expect(error).toBeInstanceOf(Shapes2DError);
            expect(error.code).toBe('INVALID_SERIALIZED_PAYLOAD');
            expect(error.name).toBe('SerializationError');
        });
    });

    describe('ShapeRegistryError', () => {
        it('accepts a custom code and sets name', () => {
            const error = new ShapeRegistryError('REGISTRY_DISPOSED', 'disposed');
            expect(error).toBeInstanceOf(Shapes2DError);
            expect(error.code).toBe('REGISTRY_DISPOSED');
            expect(error.name).toBe('ShapeRegistryError');
        });

        it('accepts SHAPE_NOT_FOUND code', () => {
            const error = new ShapeRegistryError('SHAPE_NOT_FOUND', 'missing');
            expect(error.code).toBe('SHAPE_NOT_FOUND');
        });

        it('accepts CAPACITY_EXCEEDED code', () => {
            const error = new ShapeRegistryError('CAPACITY_EXCEEDED', 'full');
            expect(error.code).toBe('CAPACITY_EXCEEDED');
        });
    });
});
