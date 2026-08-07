import { describe, it, expect } from 'vitest';
import {
    PROFILER_VERSION,
    // Errors
    ProfilerErrorBase,
    ProfilerStartupError,
    ProfilerShutdownError,
    ProfilerTickError,
    ProfilerStackCaptureError,
    isProfilerError,
    tryCatch,
    tryCatchAsync,
    fromThrowable,
    fromThrowableAsync,
    fromPromise,
    assertTimerId,
    PROFILER_ERROR_CODES,
    // Clock
    TIME_CONVERSION,
    ClockError,
    ClockNotRunningError,
    ClockAlreadyRunningError,
    ClockOverflowError,
    ClockSkewError,
    ClockResolutionError,
    isClockError,
    MonotonicClock,
    WallClock,
    VirtualClock,
    Stopwatch,
    // Core
    ContinuousSampler,
    StackCaptureEngine,
    LogBoundedHistogram,
    MemoryTracker,
    FlameGraphBuilder,
    FlameGraphNode,
    TimelineRecorder,
    // Export
    PerfettoSerializer,
    // Main
    ProfileTimerTree,
    Profiler,
    profile,
    CORE_PROFILE_ID,
} from '../index';

describe('Public API surface', () => {
    it('should export PROFILER_VERSION as a string', () => {
        expect(typeof PROFILER_VERSION).toBe('string');
        expect(PROFILER_VERSION).toBe('0.1.0');
    });

    it('should export all error classes', () => {
        expect(ProfilerErrorBase).toBeDefined();
        expect(ProfilerStartupError).toBeDefined();
        expect(ProfilerShutdownError).toBeDefined();
        expect(ProfilerTickError).toBeDefined();
        expect(ProfilerStackCaptureError).toBeDefined();
        expect(typeof isProfilerError).toBe('function');
        expect(typeof tryCatch).toBe('function');
        expect(typeof tryCatchAsync).toBe('function');
        expect(typeof fromThrowable).toBe('function');
        expect(typeof fromThrowableAsync).toBe('function');
        expect(typeof fromPromise).toBe('function');
        expect(typeof assertTimerId).toBe('function');
        expect(PROFILER_ERROR_CODES).toBeDefined();
    });

    it('should export all clock classes and constants', () => {
        expect(TIME_CONVERSION).toBeDefined();
        expect(ClockError).toBeDefined();
        expect(ClockNotRunningError).toBeDefined();
        expect(ClockAlreadyRunningError).toBeDefined();
        expect(ClockOverflowError).toBeDefined();
        expect(ClockSkewError).toBeDefined();
        expect(ClockResolutionError).toBeDefined();
        expect(typeof isClockError).toBe('function');
        expect(MonotonicClock).toBeDefined();
        expect(WallClock).toBeDefined();
        expect(VirtualClock).toBeDefined();
        expect(Stopwatch).toBeDefined();
    });

    it('should export core module classes', () => {
        expect(ContinuousSampler).toBeDefined();
        expect(StackCaptureEngine).toBeDefined();
        expect(LogBoundedHistogram).toBeDefined();
        expect(MemoryTracker).toBeDefined();
        expect(FlameGraphBuilder).toBeDefined();
        expect(FlameGraphNode).toBeDefined();
        expect(TimelineRecorder).toBeDefined();
    });

    it('should export PerfettoSerializer', () => {
        expect(PerfettoSerializer).toBeDefined();
    });

    it('should export main profiler orchestration symbols', () => {
        expect(ProfileTimerTree).toBeDefined();
        expect(Profiler).toBeDefined();
        expect(typeof profile).toBe('function');
        expect(CORE_PROFILE_ID).toBe(0);
    });

    it('should allow constructing key classes from barrel import', () => {
        const clock = new MonotonicClock({ clockId: 'barrel-test' });
        expect(clock.clockId).toBe('barrel-test');
        clock[Symbol.asyncDispose]();

        const builder = new FlameGraphBuilder();
        expect(builder.getRoots()).toHaveLength(0);
        builder[Symbol.asyncDispose]();

        const profiler = new Profiler();
        expect(profiler).toBeDefined();
        profiler.dispose();
    });
});
