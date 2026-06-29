import { Ok, Err, type Result } from '@axrone/utility';

export class ProfilerErrorBase extends Error {
  public readonly code: string;
  public readonly cause?: unknown;

  constructor(message: string, options?: { cause?: unknown }) {
    super(message);
    this.name = new.target.name;
    this.code = 'PROFILER_ERROR';
    if (options?.cause !== undefined) {
      (this as Record<string, unknown>).cause = options.cause;
    }
  }

  toJSON(): Record<string, unknown> {
    return { name: this.name, message: this.message, code: this.code, cause: this.cause };
  }
}

export class ProfilerStartupError extends ProfilerErrorBase {
  readonly code = 'PROFILER_STARTUP' as const;
  constructor(message?: string) { super(message ?? 'Failed to initialize profiler'); }
}

export class ProfilerShutdownError extends ProfilerErrorBase {
  readonly code = 'PROFILER_SHUTDOWN' as const;
  constructor(message?: string) { super(message ?? 'Failed to shutdown profiler'); }
}

export class ProfilerTickError extends ProfilerErrorBase {
  readonly code = 'PROFILER_TICK_ERROR' as const;
  constructor(message?: string) { super(message ?? 'Profiler tick failed'); }
}

export class ProfilerStackCaptureError extends ProfilerErrorBase {
  readonly code = 'PROFILER_STACK_CAPTURE_ERROR' as const;
  constructor(message?: string) { super(message ?? 'Failed to capture stack trace'); }
}

// ---------------------------------------------------------------------------
// Utilities
// ---------------------------------------------------------------------------

export function isProfilerError(value: unknown): value is ProfilerErrorBase {
  return value instanceof ProfilerErrorBase;
}

export function assertOk<T, E>(result: Result<T, E>): asserts result is Ok<T, E> {
  if (result.isErr()) {
    const err = result as Err<T, E>;
    throw err.get();
  }
}

function asProfilerError(e: unknown): ProfilerErrorBase {
  if (e instanceof ProfilerErrorBase) return e;
  if (e instanceof Error) {
    return new ProfilerErrorBase(e.message, { cause: e });
  }
  return new ProfilerStartupError(typeof e === 'string' ? e : String(e));
}

// ---------------------------------------------------------------------------
// tryCatch helpers
// ---------------------------------------------------------------------------

export function tryCatch<T, E extends ProfilerErrorBase = ProfilerErrorBase>(
  fn: () => T,
  onError?: (error: unknown) => E
): Result<T, E> {
  try {
    return Ok.of(fn());
  } catch (e) {
    return Err.of(onError ? onError(e) : asProfilerError(e) as E);
  }
}

export async function tryCatchAsync<T, E extends ProfilerErrorBase = ProfilerErrorBase>(
  fn: () => Promise<T>,
  onError?: (error: unknown) => E
): Promise<Result<T, E>> {
  try {
    return Ok.of(await fn());
  } catch (e) {
    return Err.of(onError ? onError(e) : asProfilerError(e) as E);
  }
}

export function fromThrowable<T, A extends readonly unknown[]>(
  fn: (...args: A) => T,
  onError?: (error: unknown) => ProfilerErrorBase
): (...args: A) => Result<T, ProfilerErrorBase> {
  return (...args: A) => {
    try {
      return Ok.of(fn(...args));
    } catch (e) {
      return Err.of(onError ? onError(e) : asProfilerError(e));
    }
  };
}

export function fromThrowableAsync<T, A extends readonly unknown[]>(
  fn: (...args: A) => Promise<T>,
  onError?: (error: unknown) => ProfilerErrorBase
): (...args: A) => Promise<Result<T, ProfilerErrorBase>> {
  return async (...args: A) => {
    try {
      return Ok.of(await fn(...args));
    } catch (e) {
      return Err.of(onError ? onError(e) : asProfilerError(e));
    }
  };
}

export function fromPromise<T, E extends ProfilerErrorBase = ProfilerErrorBase>(
  promise: Promise<T>,
  onError?: (error: unknown) => E
): Promise<Result<T, E>> {
  return promise.then(
    (value) => Ok.of(value),
    (error) => Err.of(onError ? onError(error) : asProfilerError(error) as E)
  );
}

export function assertTimerId(timerId: unknown): asserts timerId is number {
  if (typeof timerId !== 'number' || timerId <= 0 || timerId > 2_147_483_647) {
    throw new ProfilerTickError('Invalid timer ID');
  }
}

export const PROFILER_ERROR_CODES = {
  STARTUP: 'PROFILER_STARTUP',
  SHUTDOWN: 'PROFILER_SHUTDOWN',
  TICK: 'PROFILER_TICK_ERROR',
  STACK_CAPTURE: 'PROFILER_STACK_CAPTURE_ERROR',
} as const;
