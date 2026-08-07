import { describe, expect, it } from 'vitest';
import {
    CameraCullingError,
    CameraValidationError,
    CameraSerializationError,
    resolveCameraCullingMessage,
} from '@axrone/geometry';

const ALL_CODES = [
    'CAMERA_DISPOSED',
    'CULLER_DISPOSED',
    'FRUSTUM_DISPOSED',
    'INVALID_ARGUMENT',
    'INVALID_BOUNDS',
    'INVALID_CAMERA_ID',
    'INVALID_MATRIX',
    'INVALID_POSE',
    'INVALID_PROJECTION',
    'INVALID_RADIUS',
    'INVALID_SERIALIZED_CAMERA',
    'INVALID_VECTOR',
    'OPERATION_ABORTED',
    'RESULT_OVERFLOW',
] as const;

describe('Camera Culling Error System', () => {
    describe('resolveCameraCullingMessage', () => {
        it('resolves all 14 codes for en locale', () => {
            for (const code of ALL_CODES) {
                const msg = resolveCameraCullingMessage(code, 'en');
                expect(typeof msg).toBe('string');
                expect(msg.length).toBeGreaterThan(0);
            }
        });

        it('resolves all 14 codes for tr locale', () => {
            for (const code of ALL_CODES) {
                const msg = resolveCameraCullingMessage(code, 'tr');
                expect(typeof msg).toBe('string');
                expect(msg.length).toBeGreaterThan(0);
            }
        });

        it('en and tr produce different messages', () => {
            for (const code of ALL_CODES) {
                const enMsg = resolveCameraCullingMessage(code, 'en');
                const trMsg = resolveCameraCullingMessage(code, 'tr');
                expect(enMsg).not.toBe(trMsg);
            }
        });

        it('default locale is en when no locale is supplied', () => {
            for (const code of ALL_CODES) {
                expect(resolveCameraCullingMessage(code)).toBe(
                    resolveCameraCullingMessage(code, 'en')
                );
            }
        });
    });

    describe('CameraCullingError', () => {
        it('exposes code, locale, context, cause, and name', () => {
            const cause = new Error('root');
            const context = { field: 'test' };
            const err = new CameraCullingError('INVALID_ARGUMENT', 'en', context, cause);

            expect(err.code).toBe('INVALID_ARGUMENT');
            expect(err.locale).toBe('en');
            expect(err.context).toBe(context);
            expect(err.cause).toBe(cause);
            expect(err.name).toBe('CameraCullingError');
        });

        it('is instanceof Error and CameraCullingError', () => {
            const err = new CameraCullingError('INVALID_ARGUMENT');
            expect(err).toBeInstanceOf(Error);
            expect(err).toBeInstanceOf(CameraCullingError);
        });

        it('message is resolved from the locale table', () => {
            const enErr = new CameraCullingError('CAMERA_DISPOSED', 'en');
            const trErr = new CameraCullingError('CAMERA_DISPOSED', 'tr');
            expect(enErr.message).toBe(resolveCameraCullingMessage('CAMERA_DISPOSED', 'en'));
            expect(trErr.message).toBe(resolveCameraCullingMessage('CAMERA_DISPOSED', 'tr'));
        });

        it('defaults locale to en and context to empty object', () => {
            const err = new CameraCullingError('INVALID_ARGUMENT');
            expect(err.locale).toBe('en');
            expect(err.context).toEqual({});
            expect(err.cause).toBeUndefined();
        });
    });

    describe('CameraValidationError', () => {
        it('is instanceof CameraCullingError and Error', () => {
            const err = new CameraValidationError('INVALID_ARGUMENT');
            expect(err).toBeInstanceOf(Error);
            expect(err).toBeInstanceOf(CameraCullingError);
            expect(err).toBeInstanceOf(CameraValidationError);
        });

        it('has name CameraValidationError', () => {
            const err = new CameraValidationError('CAMERA_DISPOSED');
            expect(err.name).toBe('CameraValidationError');
        });

        it('propagates code, locale, context, and cause', () => {
            const cause = new Error('inner');
            const err = new CameraValidationError('INVALID_POSE', 'tr', { x: 1 }, cause);
            expect(err.code).toBe('INVALID_POSE');
            expect(err.locale).toBe('tr');
            expect(err.context).toEqual({ x: 1 });
            expect(err.cause).toBe(cause);
        });
    });

    describe('CameraSerializationError', () => {
        it('is instanceof CameraCullingError and Error', () => {
            const err = new CameraSerializationError();
            expect(err).toBeInstanceOf(Error);
            expect(err).toBeInstanceOf(CameraCullingError);
            expect(err).toBeInstanceOf(CameraSerializationError);
        });

        it('has name CameraSerializationError', () => {
            const err = new CameraSerializationError();
            expect(err.name).toBe('CameraSerializationError');
        });

        it('always uses INVALID_SERIALIZED_CAMERA code', () => {
            const err = new CameraSerializationError();
            expect(err.code).toBe('INVALID_SERIALIZED_CAMERA');
        });

        it('message matches the locale table', () => {
            const enErr = new CameraSerializationError('en');
            expect(enErr.message).toBe(
                resolveCameraCullingMessage('INVALID_SERIALIZED_CAMERA', 'en')
            );
        });

        it('is NOT instanceof CameraValidationError', () => {
            const err = new CameraSerializationError();
            expect(err).not.toBeInstanceOf(CameraValidationError);
        });
    });

    describe('Error hierarchy', () => {
        it('CameraValidationError is CameraCullingError but CameraSerializationError is not CameraValidationError', () => {
            const validation = new CameraValidationError('INVALID_ARGUMENT');
            const serialization = new CameraSerializationError();

            expect(validation).toBeInstanceOf(CameraCullingError);
            expect(serialization).toBeInstanceOf(CameraCullingError);
            expect(validation).toBeInstanceOf(CameraValidationError);
            expect(serialization).not.toBeInstanceOf(CameraValidationError);
        });

        it('both are Error', () => {
            expect(new CameraValidationError('INVALID_ARGUMENT')).toBeInstanceOf(Error);
            expect(new CameraSerializationError()).toBeInstanceOf(Error);
        });
    });
});
