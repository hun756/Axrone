import { ProfilerErrorBase } from '../../errors';

export class ClockError extends ProfilerErrorBase {
    readonly code: string = 'CLOCK_ERROR';
    constructor(message?: string, options?: { cause?: unknown }) {
        super(message ?? 'Clock operation failed', options);
    }
}

export class ClockNotRunningError extends ClockError {
    readonly code = 'CLOCK_NOT_RUNNING' as const;
    constructor(message?: string) {
        super(message ?? 'Clock is not running');
    }
}

export class ClockAlreadyRunningError extends ClockError {
    readonly code = 'CLOCK_ALREADY_RUNNING' as const;
    constructor(message?: string) {
        super(message ?? 'Clock is already running');
    }
}

export class ClockOverflowError extends ClockError {
    readonly code = 'CLOCK_OVERFLOW' as const;
    constructor(message?: string) {
        super(message ?? 'Clock timestamp overflow detected');
    }
}

export class ClockSkewError extends ClockError {
    readonly code = 'CLOCK_SKEW' as const;
    constructor(message?: string) {
        super(message ?? 'Clock skew detected');
    }
}

export class ClockResolutionError extends ClockError {
    readonly code = 'CLOCK_RESOLUTION' as const;
    constructor(message?: string) {
        super(message ?? 'Insufficient clock resolution');
    }
}

export function isClockError(value: unknown): value is ClockError {
    return value instanceof ClockError;
}
