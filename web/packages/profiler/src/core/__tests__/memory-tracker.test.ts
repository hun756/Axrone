import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { MemoryTracker } from '../memory-tracker';

describe('MemoryTracker', () => {
    let tracker: MemoryTracker;

    afterEach(async () => {
        if (tracker) await tracker[Symbol.asyncDispose]();
    });

    describe('construction', () => {
        it('should create with default options', () => {
            tracker = new MemoryTracker();
            expect(tracker.getTotalSamples()).toBe(0);
            expect(tracker.getProviderName()).toBeTruthy();
        });

        it('should accept onMemoryChange callback', () => {
            const cb = vi.fn();
            tracker = new MemoryTracker({ onMemoryChange: cb });
            expect(tracker.getTotalSamples()).toBe(0);
        });
    });

    describe('provider selection', () => {
        it('should select a provider on construction', () => {
            tracker = new MemoryTracker();
            const name = tracker.getProviderName();
            expect(['chromium', 'nodejs', 'performance-api', 'unknown']).toContain(name);
        });
    });

    describe('start/stop tracking', () => {
        it('should start tracking', () => {
            tracker = new MemoryTracker();
            tracker.startTracking({ intervalMs: 10 });
            expect(tracker.getTotalSamples()).toBe(0);
        });

        it('should be idempotent on repeated start', () => {
            tracker = new MemoryTracker();
            tracker.startTracking();
            tracker.startTracking();
        });

        it('should stop tracking', () => {
            tracker = new MemoryTracker();
            tracker.startTracking();
            tracker.stopTracking();
        });

        it('should stop safely if never started', () => {
            tracker = new MemoryTracker();
            tracker.stopTracking();
        });

        it('should track samples after starting', () => {
            vi.useFakeTimers();
            tracker = new MemoryTracker();
            tracker.startTracking({ intervalMs: 10 });
            vi.advanceTimersByTime(50);
            expect(tracker.getTotalSamples()).toBeGreaterThanOrEqual(1);
            tracker.stopTracking();
            vi.useRealTimers();
        });

        it('should invoke onMemoryChange callback', () => {
            vi.useFakeTimers();
            const cb = vi.fn();
            tracker = new MemoryTracker({ onMemoryChange: cb });
            tracker.startTracking({ intervalMs: 10 });
            vi.advanceTimersByTime(30);
            expect(cb).toHaveBeenCalled();
            tracker.stopTracking();
            vi.useRealTimers();
        });
    });

    describe('async dispose', () => {
        it('should stop tracking on dispose', async () => {
            vi.useFakeTimers();
            tracker = new MemoryTracker();
            tracker.startTracking({ intervalMs: 10 });
            await tracker[Symbol.asyncDispose]();
            const samplesAfter = tracker.getTotalSamples();
            vi.advanceTimersByTime(100);
            expect(tracker.getTotalSamples()).toBe(samplesAfter);
            vi.useRealTimers();
        });

        it('should allow multiple dispose calls', async () => {
            tracker = new MemoryTracker();
            await tracker[Symbol.asyncDispose]();
            await tracker[Symbol.asyncDispose]();
        });
    });
});
