import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { ProfileTimerTree, Profiler, profile, CORE_PROFILE_ID } from '../profile';
import type { FlameGraphNode } from '../core/flame-graph';
import { Ok, Err } from '@axrone/utility';
import { ProfilerStartupError } from '../errors';

describe('ProfileTimerTree', () => {
    let tree: ProfileTimerTree;

    beforeEach(() => {
        vi.useFakeTimers();
        tree = new ProfileTimerTree();
    });

    afterEach(() => {
        tree.dispose();
        vi.useRealTimers();
    });

    describe('timer management', () => {
        it('should add a timer and return a valid ID', () => {
            const id = tree.addTimer('test-timer');
            expect(typeof id).toBe('number');
            expect(id).toBeGreaterThan(0);
        });

        it('should end a timer and record metrics', () => {
            vi.advanceTimersByTime(100);
            const id = tree.addTimer('quick');
            vi.advanceTimersByTime(50);
            tree.endTimer(id);
            const stats = tree.getSampledMetrics();
            expect(stats.totalDurationMs as bigint).toBeGreaterThan(0n);
        });

        it('should return undefined for unknown timer metrics', () => {
            const metrics = tree.getSampledMetrics(99999);
            expect(metrics).toBeUndefined();
        });

        it('should pause and resume a timer', () => {
            const id = tree.addTimer('interrupted');
            vi.advanceTimersByTime(50);
            tree.pauseTimer(id);
            vi.advanceTimersByTime(50);
            tree.resumeTimer(id);
            vi.advanceTimersByTime(50);
            tree.endTimer(id);
            const hierarchy = tree.getHierarchy();
            expect(hierarchy.length).toBe(1);
            const elapsedNs = hierarchy[0].getDurationNs();
            expect(elapsedNs).toBeGreaterThan(0n);
        });

        it('should cancel a timer', () => {
            const id = tree.addTimer('cancel-me');
            tree.cancelTimer(id);
            tree.endTimer(id);
            expect(tree.getActiveTimerCount()).toBe(0);
        });

        it('should handle undefined timer ID gracefully', () => {
            tree.endTimer(undefined);
            tree.pauseTimer(undefined);
            tree.resumeTimer(undefined);
            tree.cancelTimer(undefined);
        });
    });

    describe('addMarkerWithCallback', () => {
        it('should execute callback and record metrics', () => {
            const cb = vi.fn();
            const markerId = tree.addMarkerWithCallback(cb);
            expect(typeof markerId).toBe('number');
            expect(cb).toHaveBeenCalledTimes(1);
        });
    });

    describe('getHierarchy and iteration', () => {
        it('should return flame graph hierarchy', () => {
            const id = tree.addTimer('root');
            vi.advanceTimersByTime(10);
            tree.endTimer(id);
            const hierarchy = tree.getHierarchy();
            expect(hierarchy.length).toBe(1);
            expect(hierarchy[0].name).toBe('root');
        });

        it('should be iterable', () => {
            const id = tree.addTimer('iterable-test');
            vi.advanceTimersByTime(10);
            tree.endTimer(id);
            const items: FlameGraphNode[] = [];
            for (const node of tree) {
                items.push(node);
            }
            expect(items.length).toBe(1);
        });
    });

    describe('overhead metrics', () => {
        it('should track overhead', () => {
            tree.addTimer('overhead-test');
            const overhead = tree.getOverheadMetrics();
            expect(overhead.timerCount).toBe(1);
            expect(overhead.totalOverheadNs).toBeGreaterThanOrEqual(0n);
        });
    });

    describe('dispose and clear', () => {
        it('should clear all timers', () => {
            tree.addTimer('a');
            tree.addTimer('b');
            tree.clear();
            expect(tree.getActiveTimerCount()).toBe(0);
        });

        it('should cleanup on dispose', () => {
            tree.addTimer('a');
            tree.dispose();
        });
    });
});

describe('Profiler', () => {
    let profiler: Profiler;

    afterEach(async () => {
        if (profiler) await profiler[Symbol.asyncDispose]();
    });

    describe('construction', () => {
        it('should create with default options', () => {
            profiler = new Profiler();
            expect(profiler).toBeDefined();
        });

        it('should auto-start in Continuous mode', () => {
            profiler = new Profiler({ mode: 'Continuous' });
        });

        it('should auto-start with autoStart option', () => {
            profiler = new Profiler({ autoStart: true });
        });

        it('should skip MemoryTracker in OnDemand mode', () => {
            profiler = new Profiler({ mode: 'OnDemand' });
            expect(profiler.getMemoryTracker()).toBeUndefined();
        });

        it('should accept custom timer tree', () => {
            const tree = new ProfileTimerTree();
            profiler = new Profiler({ timerTree: tree });
            tree.dispose();
        });
    });

    describe('timer delegation', () => {
        it('should add and end timers', () => {
            profiler = new Profiler();
            const id = profiler.addTimer('profile-timer');
            profiler.endTimer(id);
        });

        it('should pause, resume, and cancel timers', () => {
            profiler = new Profiler();
            const id = profiler.addTimer('flexible');
            profiler.pauseTimer(id);
            profiler.resumeTimer(id);
            profiler.endTimer(id);
        });

        it('should cancel timers', () => {
            profiler = new Profiler();
            const id = profiler.addTimer('cancel');
            profiler.cancelTimer(id);
        });
    });

    describe('getResult()', () => {
        it('should return a ProfilerResult', () => {
            profiler = new Profiler();
            const id = profiler.addTimer('result-test');
            profiler.endTimer(id);
            const result = profiler.getResult();
            expect(result.sessionId).toBeTruthy();
            expect(result.startedAtMs).toBeGreaterThan(0);
            expect(result.metrics).toBeDefined();
            expect(result.flameGraphs).toBeDefined();
            expect(result.timelineEvents).toBeDefined();
        });
    });

    describe('markers', () => {
        it('should add a marker', () => {
            profiler = new Profiler();
            profiler.addMarker('frame-start');
            const events = profiler.getTimelineRecorder().getEvents();
            expect(events.length).toBe(1);
            expect(events[0].payload.marker).toBe('frame-start');
        });

        it('should execute marker with callback', () => {
            profiler = new Profiler();
            const cb = vi.fn();
            profiler.addMarkerWithCallback(cb);
            expect(cb).toHaveBeenCalledTimes(1);
        });
    });

    describe('start, stop, endFrame, clear', () => {
        it('should start and stop', () => {
            profiler = new Profiler();
            profiler.start();
            profiler.stop();
        });

        it('should call onFrameEnd when set', () => {
            const onEnd = vi.fn();
            profiler = new Profiler({ onFrameEnd: onEnd });
            profiler.endFrame();
            expect(onEnd).toHaveBeenCalledTimes(1);
        });

        it('should clear state', () => {
            profiler = new Profiler();
            const id = profiler.addTimer('to-clear');
            profiler.endTimer(id);
            profiler.clear();
            const result = profiler.getResult();
            expect(result.totalSamples).toBe(0n);
        });
    });

    describe('getHierarchy', () => {
        it('should return flame graph from timer tree', () => {
            profiler = new Profiler();
            const id = profiler.addTimer('hierarchy-test');
            profiler.endTimer(id);
            const hierarchy = profiler.getHierarchy();
            expect(hierarchy.length).toBeGreaterThanOrEqual(1);
        });
    });
});

describe('profile() function', () => {
    it('should return Ok result for successful operations', () => {
        const result = profile(() => 42);
        expect(result.isOk()).toBe(true);
        expect(result.unwrap()).toBe(42);
    });

    it('should return Err result for throwing operations', () => {
        const result = profile(() => {
            throw new Error('fail');
        });
        expect(result.isErr()).toBe(true);
        expect(result.expectErr('should be err')).toBeInstanceOf(ProfilerStartupError);
    });
});

describe('CORE_PROFILE_ID', () => {
    it('should be 0', () => {
        expect(CORE_PROFILE_ID).toBe(0);
    });
});
