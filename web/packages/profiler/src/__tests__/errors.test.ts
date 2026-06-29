import { describe, it, expect } from 'vitest';
import {
    ProfilerErrorBase,
    ProfilerStartupError,
    ProfilerShutdownError,
    ProfilerTickError,
    ProfilerStackCaptureError,
    isProfilerError,
    assertTimerId,
    PROFILER_ERROR_CODES,
} from '../errors';

describe('ProfilerErrorBase', () => {
    it('should create an error with the correct name', () => {
        const error = new ProfilerErrorBase('test message');
        expect(error.name).toBe('ProfilerErrorBase');
        expect(error.code).toBe('PROFILER_ERROR');
        expect(error.message).toBe('test message');
    });

    it('should preserve cause via native Error.cause', () => {
        const cause = new Error('root cause');
        const error = new ProfilerErrorBase('wrapped', { cause });
        expect(error.cause).toBe(cause);
    });

    it('should serialize to JSON', () => {
        const error = new ProfilerStartupError('startup failed');
        const json = error.toJSON();
        expect(json.name).toBe('ProfilerStartupError');
        expect(json.code).toBe('PROFILER_STARTUP');
        expect(json.message).toBe('startup failed');
    });
});

describe('Error subclasses', () => {
    it('should create ProfilerStartupError with default message', () => {
        const error = new ProfilerStartupError();
        expect(error.message).toBe('Failed to initialize profiler');
    });

    it('should create ProfilerShutdownError with default message', () => {
        const error = new ProfilerShutdownError();
        expect(error.message).toBe('Failed to shutdown profiler');
    });

    it('should create ProfilerTickError with default message', () => {
        const error = new ProfilerTickError();
        expect(error.message).toBe('Profiler tick failed');
    });

    it('should create ProfilerStackCaptureError with default message', () => {
        const error = new ProfilerStackCaptureError();
        expect(error.message).toBe('Failed to capture stack trace');
    });

    it('should narrow correctly with isProfilerError', () => {
        const error = new ProfilerTickError();
        expect(isProfilerError(error)).toBe(true);
        expect(isProfilerError(new Error('generic'))).toBe(false);
    });
});

describe('assertTimerId', () => {
    it('should pass for valid timer IDs', () => {
        expect(() => assertTimerId(1)).not.toThrow();
        expect(() => assertTimerId(2_147_483_647)).not.toThrow();
    });

    it('should throw for invalid timer IDs', () => {
        expect(() => assertTimerId(0)).toThrow('Invalid timer ID');
        expect(() => assertTimerId(-1)).toThrow('Invalid timer ID');
        expect(() => assertTimerId(2_147_483_648)).toThrow('Invalid timer ID');
        expect(() => assertTimerId('abc' as unknown as number)).toThrow('Invalid timer ID');
    });
});

describe('PROFILER_ERROR_CODES', () => {
    it('should have expected error code constants', () => {
        expect(PROFILER_ERROR_CODES.STARTUP).toBe('PROFILER_STARTUP');
        expect(PROFILER_ERROR_CODES.SHUTDOWN).toBe('PROFILER_SHUTDOWN');
        expect(PROFILER_ERROR_CODES.TICK).toBe('PROFILER_TICK_ERROR');
        expect(PROFILER_ERROR_CODES.STACK_CAPTURE).toBe('PROFILER_STACK_CAPTURE_ERROR');
    });
});
