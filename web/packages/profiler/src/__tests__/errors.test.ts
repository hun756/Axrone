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
    tryCatch,
    tryCatchAsync,
    fromThrowable,
    fromThrowableAsync,
    fromPromise,
    assertOk,
} from '../errors';
import { Ok, Err } from '@axrone/utility';

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

describe('tryCatch', () => {
    it('should return Ok when fn succeeds', () => {
        const result = tryCatch(() => 42);
        expect(result.isOk()).toBe(true);
        expect(result.unwrap()).toBe(42);
    });

    it('should return Err when fn throws', () => {
        const result = tryCatch(() => {
            throw new Error('boom');
        });
        expect(result.isErr()).toBe(true);
        const err = result.expectErr('should be err');
        expect(err).toBeInstanceOf(ProfilerErrorBase);
        expect(err.message).toBe('boom');
    });

    it('should use custom onError mapper', () => {
        const result = tryCatch(
            () => {
                throw new Error('mapped');
            },
            (e) => new ProfilerTickError((e as Error).message)
        );
        expect(result.isErr()).toBe(true);
        expect(result.expectErr('should be err')).toBeInstanceOf(ProfilerTickError);
        expect(result.expectErr('should be err').message).toBe('mapped');
    });

    it('should wrap non-Error throws as ProfilerErrorBase', () => {
        const result = tryCatch(() => {
            throw 'string-error';
        });
        expect(result.isErr()).toBe(true);
        expect(result.expectErr('should be err')).toBeInstanceOf(ProfilerErrorBase);
    });
});

describe('tryCatchAsync', () => {
    it('should return Ok when async fn resolves', async () => {
        const result = await tryCatchAsync(async () => 'hello');
        expect(result.isOk()).toBe(true);
        expect(result.unwrap()).toBe('hello');
    });

    it('should return Err when async fn rejects', async () => {
        const result = await tryCatchAsync(async () => {
            throw new Error('async-boom');
        });
        expect(result.isErr()).toBe(true);
        expect(result.expectErr('should be err').message).toBe('async-boom');
    });

    it('should use custom onError mapper for async', async () => {
        const result = await tryCatchAsync(
            async () => {
                throw new Error('async-mapped');
            },
            (e) => new ProfilerStartupError((e as Error).message)
        );
        expect(result.isErr()).toBe(true);
        expect(result.expectErr('should be err')).toBeInstanceOf(ProfilerStartupError);
    });
});

describe('fromThrowable', () => {
    it('should return a wrapped function that yields Ok', () => {
        const wrapped = fromThrowable((x: number) => x * 2);
        const result = wrapped(5);
        expect(result.isOk()).toBe(true);
        expect(result.unwrap()).toBe(10);
    });

    it('should return Err when wrapped function throws', () => {
        const wrapped = fromThrowable(() => {
            throw new Error('wrap-fail');
        });
        const result = wrapped();
        expect(result.isErr()).toBe(true);
        expect(result.expectErr('should be err').message).toBe('wrap-fail');
    });

    it('should use custom onError mapper', () => {
        const wrapped = fromThrowable(
            () => {
                throw new Error('custom');
            },
            (e) => new ProfilerShutdownError((e as Error).message)
        );
        const result = wrapped();
        expect(result.expectErr('should be err')).toBeInstanceOf(ProfilerShutdownError);
    });

    it('should forward arguments to the wrapped function', () => {
        const wrapped = fromThrowable((a: number, b: string) => `${a}-${b}`);
        const result = wrapped(1, 'test');
        expect(result.unwrap()).toBe('1-test');
    });
});

describe('fromThrowableAsync', () => {
    it('should return a wrapped async function that yields Ok', async () => {
        const wrapped = fromThrowableAsync(async (x: number) => x + 1);
        const result = await wrapped(10);
        expect(result.isOk()).toBe(true);
        expect(result.unwrap()).toBe(11);
    });

    it('should return Err when wrapped async function rejects', async () => {
        const wrapped = fromThrowableAsync(async () => {
            throw new Error('async-wrap-fail');
        });
        const result = await wrapped();
        expect(result.isErr()).toBe(true);
        expect(result.expectErr('should be err').message).toBe('async-wrap-fail');
    });

    it('should use custom onError mapper for async', async () => {
        const wrapped = fromThrowableAsync(
            async () => {
                throw new Error('async-custom');
            },
            (e) => new ProfilerStackCaptureError((e as Error).message)
        );
        const result = await wrapped();
        expect(result.expectErr('should be err')).toBeInstanceOf(ProfilerStackCaptureError);
    });
});

describe('fromPromise', () => {
    it('should return Ok when promise resolves', async () => {
        const result = await fromPromise(Promise.resolve(99));
        expect(result.isOk()).toBe(true);
        expect(result.unwrap()).toBe(99);
    });

    it('should return Err when promise rejects', async () => {
        const result = await fromPromise(Promise.reject(new Error('promise-fail')));
        expect(result.isErr()).toBe(true);
        expect(result.expectErr('should be err').message).toBe('promise-fail');
    });

    it('should use custom onError mapper', async () => {
        const result = await fromPromise(
            Promise.reject(new Error('promise-mapped')),
            (e) => new ProfilerTickError((e as Error).message)
        );
        expect(result.expectErr('should be err')).toBeInstanceOf(ProfilerTickError);
        expect(result.expectErr('should be err').message).toBe('promise-mapped');
    });
});

describe('assertOk', () => {
    it('should not throw for Ok results', () => {
        const result = Ok.of(42);
        expect(() => assertOk(result)).not.toThrow();
    });

    it('should throw the error for Err results', () => {
        const result = Err.of(new ProfilerTickError('assert-fail'));
        expect(() => assertOk(result)).toThrow(ProfilerTickError);
    });
});
