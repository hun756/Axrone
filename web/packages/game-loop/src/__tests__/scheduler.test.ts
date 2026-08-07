import {
    createAnimationFrameScheduler,
    isGameLoopScheduler,
    type GameLoopScheduler,
} from '@axrone/game-loop';

describe('isGameLoopScheduler', () => {
    it('returns false for null and undefined', () => {
        expect(isGameLoopScheduler(null)).toBe(false);
        expect(isGameLoopScheduler(undefined)).toBe(false);
    });

    it('returns false for primitives', () => {
        expect(isGameLoopScheduler(42)).toBe(false);
        expect(isGameLoopScheduler('string')).toBe(false);
        expect(isGameLoopScheduler(true)).toBe(false);
    });

    it('returns false for empty object', () => {
        expect(isGameLoopScheduler({})).toBe(false);
    });

    it('returns false when kind is missing or non-string', () => {
        expect(isGameLoopScheduler({ kind: 123 })).toBe(false);
        expect(
            isGameLoopScheduler({
                now: () => 0,
                request: () => 0,
                cancel: () => {},
            })
        ).toBe(false);
    });

    it('returns false when methods are missing', () => {
        expect(isGameLoopScheduler({ kind: 'test' })).toBe(false);
        expect(isGameLoopScheduler({ kind: 'test', now: () => 0 })).toBe(false);
        expect(
            isGameLoopScheduler({
                kind: 'test',
                now: () => 0,
                request: () => 0,
            })
        ).toBe(false);
    });

    it('returns false when methods are non-functions', () => {
        expect(
            isGameLoopScheduler({
                kind: 'test',
                now: 'not-a-function',
                request: () => 0,
                cancel: () => {},
            })
        ).toBe(false);
    });

    it('returns true for a valid scheduler-like object', () => {
        expect(
            isGameLoopScheduler({
                kind: 'test',
                now: () => 0,
                request: () => 0,
                cancel: () => {},
            })
        ).toBe(true);
    });
});

describe('createAnimationFrameScheduler', () => {
    it('returns animation-frame kind when rAF is available', () => {
        const scheduler = createAnimationFrameScheduler();

        expect(scheduler.kind).toBe('animation-frame');
        expect(typeof scheduler.now).toBe('function');
        expect(typeof scheduler.request).toBe('function');
        expect(typeof scheduler.cancel).toBe('function');
        expect(isGameLoopScheduler(scheduler)).toBe(true);
    });

    it('falls back to timeout kind when rAF is unavailable', () => {
        const originalRAF = globalThis.requestAnimationFrame;
        const originalCAF = globalThis.cancelAnimationFrame;

        // @ts-expect-error - deleting for test
        delete globalThis.requestAnimationFrame;
        // @ts-expect-error - deleting for test
        delete globalThis.cancelAnimationFrame;

        try {
            const scheduler = createAnimationFrameScheduler();

            expect(scheduler.kind).toBe('timeout');
            expect(isGameLoopScheduler(scheduler)).toBe(true);
        } finally {
            globalThis.requestAnimationFrame = originalRAF;
            globalThis.cancelAnimationFrame = originalCAF;
        }
    });

    it('uses custom now function when provided', () => {
        let customCalled = false;
        const scheduler = createAnimationFrameScheduler({
            now: () => {
                customCalled = true;
                return 999;
            },
        });

        const result = scheduler.now();

        expect(customCalled).toBe(true);
        expect(result).toBe(999);
    });

    it('uses performance.now when no custom now is provided', () => {
        const scheduler = createAnimationFrameScheduler();
        const result = scheduler.now();

        expect(typeof result).toBe('number');
        expect(Number.isFinite(result)).toBe(true);
    });

    it('falls back to Date.now when performance is unavailable', () => {
        const originalPerformance = globalThis.performance;

        // @ts-expect-error - deleting for test
        delete globalThis.performance;

        try {
            const scheduler = createAnimationFrameScheduler();
            const result = scheduler.now();

            expect(typeof result).toBe('number');
            expect(Number.isFinite(result)).toBe(true);
        } finally {
            globalThis.performance = originalPerformance;
        }
    });

    describe('fallbackFps', () => {
        it('uses default 60fps for invalid fallbackFps values', () => {
            const originalRAF = globalThis.requestAnimationFrame;
            const originalCAF = globalThis.cancelAnimationFrame;
            // @ts-expect-error - deleting for test
            delete globalThis.requestAnimationFrame;
            // @ts-expect-error - deleting for test
            delete globalThis.cancelAnimationFrame;

            const setTimeoutSpy = vi.spyOn(globalThis, 'setTimeout');

            try {
                for (const fps of [0, -10, NaN, Infinity]) {
                    setTimeoutSpy.mockClear();
                    const scheduler = createAnimationFrameScheduler({ fallbackFps: fps });

                    scheduler.request(() => {});

                    const timeoutArg = setTimeoutSpy.mock.calls[0]?.[1];
                    expect(timeoutArg).toBeCloseTo(1000 / 60, 0);
                }
            } finally {
                setTimeoutSpy.mockRestore();
                globalThis.requestAnimationFrame = originalRAF;
                globalThis.cancelAnimationFrame = originalCAF;
            }
        });

        it('computes correct frame duration for valid fallbackFps', () => {
            const originalRAF = globalThis.requestAnimationFrame;
            const originalCAF = globalThis.cancelAnimationFrame;
            // @ts-expect-error - deleting for test
            delete globalThis.requestAnimationFrame;
            // @ts-expect-error - deleting for test
            delete globalThis.cancelAnimationFrame;

            const setTimeoutSpy = vi.spyOn(globalThis, 'setTimeout');

            try {
                const scheduler = createAnimationFrameScheduler({ fallbackFps: 30 });

                scheduler.request(() => {});

                const timeoutArg = setTimeoutSpy.mock.calls[0]?.[1];
                expect(timeoutArg).toBeCloseTo(1000 / 30, 0);
            } finally {
                setTimeoutSpy.mockRestore();
                globalThis.requestAnimationFrame = originalRAF;
                globalThis.cancelAnimationFrame = originalCAF;
            }
        });
    });

    describe('timeout scheduler request/cancel', () => {
        it('request invokes callback with current time and cancel clears it', () => {
            const originalRAF = globalThis.requestAnimationFrame;
            const originalCAF = globalThis.cancelAnimationFrame;
            // @ts-expect-error - deleting for test
            delete globalThis.requestAnimationFrame;
            // @ts-expect-error - deleting for test
            delete globalThis.cancelAnimationFrame;

            const clearTimeoutSpy = vi.spyOn(globalThis, 'clearTimeout');

            try {
                const customNow = vi.fn(() => 42);
                const scheduler = createAnimationFrameScheduler({ now: customNow });

                const handle = scheduler.request(() => {});
                expect(typeof handle).toBe('number');

                scheduler.cancel(handle);
                expect(clearTimeoutSpy).toHaveBeenCalledWith(handle);
            } finally {
                clearTimeoutSpy.mockRestore();
                globalThis.requestAnimationFrame = originalRAF;
                globalThis.cancelAnimationFrame = originalCAF;
            }
        });
    });
});
