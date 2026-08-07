import { describe, expect, it } from 'vitest';
import {
    LightingDisposedError,
    LightingError,
    LightingResolveError,
    LightingSerializationError,
    LightingValidationError,
} from '../errors';

describe('lighting errors', () => {
    describe('LightingError', () => {
        it('stores code, message, details, and cause', () => {
            const cause = new Error('root cause');
            const error = new LightingError('lighting.rig.test', 'test message', { key: 'value' }, cause);

            expect(error).toBeInstanceOf(Error);
            expect(error).toBeInstanceOf(LightingError);
            expect(error.code).toBe('lighting.rig.test');
            expect(error.message).toBe('test message');
            expect(error.details).toEqual({ key: 'value' });
            expect(error.cause).toBe(cause);
            expect(error.name).toBe('Error');
        });

        it('works without optional details and cause', () => {
            const error = new LightingError('lighting.light.test', 'bare error');

            expect(error.code).toBe('lighting.light.test');
            expect(error.message).toBe('bare error');
            expect(error.details).toBeUndefined();
            expect(error.cause).toBeUndefined();
        });

        it('has correct prototype chain for instanceof checks', () => {
            const error = new LightingError('lighting.resolve.test', 'proto check');

            expect(error instanceof LightingError).toBe(true);
            expect(error instanceof Error).toBe(true);
        });
    });

    describe('LightingValidationError', () => {
        it('extends LightingError with a validation code pattern', () => {
            const error = new LightingValidationError('lighting.light.invalid-field', 'field is invalid', {
                field: 'range',
            });

            expect(error).toBeInstanceOf(LightingError);
            expect(error).toBeInstanceOf(LightingValidationError);
            expect(error).toBeInstanceOf(Error);
            expect(error.code).toBe('lighting.light.invalid-field');
            expect(error.message).toBe('field is invalid');
            expect(error.details).toEqual({ field: 'range' });
            expect(error.cause).toBeUndefined();
        });

        it('accepts rig-level validation codes', () => {
            const error = new LightingValidationError('lighting.rig.disposed', 'rig is gone');

            expect(error.code).toBe('lighting.rig.disposed');
        });
    });

    describe('LightingResolveError', () => {
        it('extends LightingError with a resolve code pattern', () => {
            const error = new LightingResolveError('lighting.resolve.capacity', 'capacity exceeded', {
                max: 8,
                actual: 12,
            });

            expect(error).toBeInstanceOf(LightingError);
            expect(error).toBeInstanceOf(LightingResolveError);
            expect(error).toBeInstanceOf(Error);
            expect(error.code).toBe('lighting.resolve.capacity');
            expect(error.details).toEqual({ max: 8, actual: 12 });
        });
    });

    describe('LightingSerializationError', () => {
        it('extends LightingError with a serialize code pattern', () => {
            const error = new LightingSerializationError(
                'lighting.serialize.document',
                'unable to parse document'
            );

            expect(error).toBeInstanceOf(LightingError);
            expect(error).toBeInstanceOf(LightingSerializationError);
            expect(error).toBeInstanceOf(Error);
            expect(error.code).toBe('lighting.serialize.document');
            expect(error.cause).toBeUndefined();
        });

        it('preserves the cause parameter for wrapping underlying errors', () => {
            const rootCause = new SyntaxError('unexpected token');
            const error = new LightingSerializationError(
                'lighting.serialize.parse',
                'JSON parse failed',
                { line: 42 },
                rootCause
            );

            expect(error.code).toBe('lighting.serialize.parse');
            expect(error.details).toEqual({ line: 42 });
            expect(error.cause).toBe(rootCause);
            expect(error.cause).toBeInstanceOf(SyntaxError);
        });
    });

    describe('LightingDisposedError', () => {
        it('auto-generates message and sets resource detail', () => {
            const error = new LightingDisposedError('LightingRig');

            expect(error).toBeInstanceOf(LightingError);
            expect(error).toBeInstanceOf(LightingDisposedError);
            expect(error).toBeInstanceOf(Error);
            expect(error.code).toBe('lighting.rig.disposed');
            expect(error.message).toBe('LightingRig has already been disposed');
            expect(error.details).toEqual({ resource: 'LightingRig' });
        });

        it('works for different resource names', () => {
            const resolverError = new LightingDisposedError('LightingFrameResolver');

            expect(resolverError.message).toBe('LightingFrameResolver has already been disposed');
            expect(resolverError.details).toEqual({ resource: 'LightingFrameResolver' });
        });
    });
});
