import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { ContinuousSampler } from '../sampler';

describe('ContinuousSampler', () => {
    let sampler: ContinuousSampler;
    let tickFn: ReturnType<typeof vi.fn>;

    beforeEach(() => {
        vi.useFakeTimers();
        tickFn = vi.fn();
    });

    afterEach(async () => {
        if (sampler) await sampler[Symbol.asyncDispose]();
        vi.useRealTimers();
    });

    describe('construction', () => {
        it('should create with default options', () => {
            sampler = new ContinuousSampler({ onTick: tickFn });
            expect(sampler.getTickCount()).toBe(0);
            expect(sampler.getLastDurationMs()).toBe(0);
        });

        it('should accept custom options', () => {
            sampler = new ContinuousSampler({
                onTick: tickFn,
                intervalMs: 50,
                minIntervalMs: 5,
                maxIntervalMs: 200,
                targetFrameBudgetMs: 33,
                adaptSampling: false,
            });
            const state = sampler.getState();
            expect(state.intervalMs).toBe(50);
            expect(state.minIntervalMs).toBe(5);
            expect(state.maxIntervalMs).toBe(200);
            expect(state.targetFrameBudgetMs).toBe(33);
            expect(state.adaptationEnabled).toBe(false);
        });
    });

    describe('start() and stop()', () => {
        it('should start sampling', () => {
            sampler = new ContinuousSampler({ onTick: tickFn });
            sampler.start();
            vi.advanceTimersByTime(20);
            expect(tickFn).toHaveBeenCalled();
        });

        it('should pass a bigint timestamp to the callback', () => {
            sampler = new ContinuousSampler({ onTick: tickFn });
            sampler.start();
            vi.advanceTimersByTime(10);
            const arg = tickFn.mock.calls[0][0];
            expect(typeof arg).toBe('bigint');
            expect(arg).toBeGreaterThan(0n);
        });

        it('should stop sampling', () => {
            sampler = new ContinuousSampler({ onTick: tickFn });
            sampler.start();
            vi.advanceTimersByTime(20);
            sampler.stop();
            const callCount = tickFn.mock.calls.length;
            vi.advanceTimersByTime(200);
            expect(tickFn.mock.calls.length).toBe(callCount);
        });

        it('should be idempotent on repeated start', () => {
            sampler = new ContinuousSampler({ onTick: tickFn });
            sampler.start();
            sampler.start();
            vi.advanceTimersByTime(10);
            expect(tickFn).toHaveBeenCalled();
        });

        it('should be safe to stop when not started', () => {
            sampler = new ContinuousSampler({ onTick: tickFn });
            sampler.stop();
        });
    });

    describe('configuration', () => {
        it('setInterval should update the interval', () => {
            sampler = new ContinuousSampler({ onTick: tickFn });
            sampler.setInterval(25);
            expect(sampler.getState().intervalMs).toBe(25);
        });

        it('setInterval should floor to minimum of 1ms', () => {
            sampler = new ContinuousSampler({ onTick: tickFn });
            sampler.setInterval(0);
            expect(sampler.getState().intervalMs).toBe(1);
        });

        it('setAdaptation should toggle adaptation', () => {
            sampler = new ContinuousSampler({ onTick: tickFn });
            sampler.setAdaptation(false);
            expect(sampler.getState().adaptationEnabled).toBe(false);
        });

        it('setTargetFrameBudget should update budget', () => {
            sampler = new ContinuousSampler({ onTick: tickFn });
            sampler.setTargetFrameBudget(33);
            expect(sampler.getState().targetFrameBudgetMs).toBe(33);
        });
    });

    describe('getState()', () => {
        it('should return frozen state snapshot', () => {
            sampler = new ContinuousSampler({ onTick: tickFn });
            const state = sampler.getState();
            expect(state.intervalMs).toBe(10);
            expect(state.tickCounter).toBe(0);
            expect(Object.isFrozen(state)).toBe(true);
        });

        it('should reflect tick count after sampling', () => {
            sampler = new ContinuousSampler({ onTick: tickFn });
            sampler.start();
            vi.advanceTimersByTime(20);
            sampler.stop();
            expect(sampler.getTickCount()).toBeGreaterThanOrEqual(1);
        });
    });

    describe('async dispose', () => {
        it('should stop on dispose', async () => {
            sampler = new ContinuousSampler({ onTick: tickFn });
            sampler.start();
            await sampler[Symbol.asyncDispose]();
            const count = tickFn.mock.calls.length;
            vi.advanceTimersByTime(200);
            expect(tickFn.mock.calls.length).toBe(count);
        });

        it('should allow multiple dispose calls', async () => {
            sampler = new ContinuousSampler({ onTick: tickFn });
            await sampler[Symbol.asyncDispose]();
            await sampler[Symbol.asyncDispose]();
        });
    });

    describe('adaptation and state accumulation', () => {
        it('setTargetFrameBudget should floor to minimum of 1', () => {
            sampler = new ContinuousSampler({ onTick: tickFn });
            sampler.setTargetFrameBudget(0);
            expect(sampler.getState().targetFrameBudgetMs).toBe(1);
        });

        it('setTargetFrameBudget should accept negative values as 1', () => {
            sampler = new ContinuousSampler({ onTick: tickFn });
            sampler.setTargetFrameBudget(-10);
            expect(sampler.getState().targetFrameBudgetMs).toBe(1);
        });

        it('should accumulate time after ticks', () => {
            sampler = new ContinuousSampler({ onTick: tickFn });
            sampler.start();
            vi.advanceTimersByTime(30);
            sampler.stop();
            const state = sampler.getState();
            expect(state.accumulatedTimeNs).toBeGreaterThan(0n);
        });

        it('getLastDurationMs should return >= 0 after a tick', () => {
            sampler = new ContinuousSampler({ onTick: tickFn });
            sampler.start();
            vi.advanceTimersByTime(15);
            sampler.stop();
            expect(sampler.getLastDurationMs()).toBeGreaterThanOrEqual(0);
        });

        it('should track lastTickTimestampNs after first tick', () => {
            sampler = new ContinuousSampler({ onTick: tickFn });
            sampler.start();
            vi.advanceTimersByTime(15);
            sampler.stop();
            const state = sampler.getState();
            expect(state.lastTickTimestampNs).not.toBeNull();
        });
    });
});
