import {
    GameLoopConfigurationError,
    GameLoopSystemError,
    isGameLoopSystem,
} from '@axrone/game-loop';
import {
    GameLoopSystemRunner,
    type GameLoopSystemRunnerRuntime,
} from '../game-loop-system-runner';
import type { GameLoopSystem } from '@axrone/game-loop';

interface TestState {
    value: number;
}

const createRuntime = (overrides: Partial<GameLoopSystemRunnerRuntime<TestState>> = {}): GameLoopSystemRunnerRuntime<TestState> => ({
    getState: vi.fn(() => ({ value: 0 })),
    getFrame: vi.fn(() => 1),
    getElapsed: vi.fn(() => 100),
    isRunning: vi.fn(() => true),
    pause: vi.fn(),
    stop: vi.fn(),
    safeNow: vi.fn(() => Date.now()),
    ...overrides,
});

const createRunner = (options: Partial<{
    errorPolicy: 'throw' | 'pause' | 'stop' | 'continue';
    retryAttempts: number;
    onError: (error: GameLoopSystemError, failure: Readonly<Parameters<NonNullable<Parameters<typeof createRunner>[0]>['onError']>>[1]) => 'throw' | 'pause' | 'stop' | 'continue' | void;
    shouldRetry: (error: unknown, context: Readonly<Parameters<NonNullable<Parameters<typeof createRunner>[0]>['shouldRetry']>>[1]) => boolean;
}> = {}) =>
    new GameLoopSystemRunner<TestState>({
        errorPolicy: options.errorPolicy ?? 'continue',
        retryAttempts: options.retryAttempts ?? 0,
        resolveMessage: (descriptor) => descriptor.code,
        onError: options.onError as GameLoopSystemRunner<TestState> extends GameLoopSystemRunner<infer S> ? import('@axrone/game-loop').GameLoopOptions<S>['onError'] : never,
        shouldRetry: options.shouldRetry as never,
    });

const baseContext = {
    phase: 'update' as const,
    loop: {} as any,
    state: { value: 0 },
    frame: 1,
    now: 100,
    elapsed: 100,
    delta: 16,
    unscaledDelta: 16,
    accumulator: 0,
    fixedDelta: 16.67,
    timeScale: 1,
};

describe('isGameLoopSystem', () => {
    it('returns false for non-objects', () => {
        expect(isGameLoopSystem(null)).toBe(false);
        expect(isGameLoopSystem(undefined)).toBe(false);
        expect(isGameLoopSystem(42)).toBe(false);
        expect(isGameLoopSystem('string')).toBe(false);
    });

    it('returns false for empty object', () => {
        expect(isGameLoopSystem({})).toBe(false);
    });

    it('returns false for empty or whitespace-only id', () => {
        expect(isGameLoopSystem({ id: '' })).toBe(false);
        expect(isGameLoopSystem({ id: '   ' })).toBe(false);
    });

    it('returns false for non-finite priority', () => {
        expect(isGameLoopSystem({ id: 'x', priority: 'high' })).toBe(false);
        expect(isGameLoopSystem({ id: 'x', priority: NaN })).toBe(false);
        expect(isGameLoopSystem({ id: 'x', priority: Infinity })).toBe(false);
    });

    it('returns false for non-boolean enabled', () => {
        expect(isGameLoopSystem({ id: 'x', enabled: 'yes' })).toBe(false);
        expect(isGameLoopSystem({ id: 'x', enabled: 1 })).toBe(false);
    });

    it('returns false for non-function hooks', () => {
        expect(isGameLoopSystem({ id: 'x', update: 'notFn' })).toBe(false);
        expect(isGameLoopSystem({ id: 'x', render: 42 })).toBe(false);
    });

    it('returns true for minimal valid system (id only)', () => {
        expect(isGameLoopSystem({ id: 'minimal' })).toBe(true);
    });

    it('returns true for system with valid hooks', () => {
        expect(
            isGameLoopSystem({
                id: 'full',
                priority: 5,
                enabled: true,
                beforeUpdate: () => {},
                fixedUpdate: () => {},
                update: () => {},
                render: () => {},
                afterFrame: () => {},
                dispose: () => {},
            })
        ).toBe(true);
    });
});

describe('GameLoopSystemRunner', () => {
    describe('addSystem', () => {
        it('adds a valid system and increments systemCount', () => {
            const runner = createRunner();
            expect(runner.systemCount).toBe(0);

            runner.addSystem({ id: 'sys-a' });
            expect(runner.systemCount).toBe(1);

            runner.addSystem({ id: 'sys-b' });
            expect(runner.systemCount).toBe(2);
        });

        it('throws GameLoopConfigurationError for invalid system', () => {
            const runner = createRunner();

            expect(() => runner.addSystem({} as any)).toThrow(GameLoopConfigurationError);
            expect(() => runner.addSystem({ id: '' } as any)).toThrow(GameLoopConfigurationError);
        });

        it('throws GameLoopConfigurationError for duplicate system id', () => {
            const runner = createRunner();
            runner.addSystem({ id: 'dup' });

            expect(() => runner.addSystem({ id: 'dup' })).toThrow(GameLoopConfigurationError);
        });
    });

    describe('hasSystem / getSystem', () => {
        it('hasSystem returns true for registered systems', () => {
            const runner = createRunner();
            runner.addSystem({ id: 'tracked' });

            expect(runner.hasSystem('tracked')).toBe(true);
            expect(runner.hasSystem('unknown')).toBe(false);
        });

        it('getSystem returns the system object or undefined', () => {
            const runner = createRunner();
            const system: GameLoopSystem<TestState> = { id: 'retrievable' };
            runner.addSystem(system);

            expect(runner.getSystem('retrievable')).toBe(system);
            expect(runner.getSystem('missing')).toBeUndefined();
        });
    });

    describe('removeSystem', () => {
        it('removes by string id and calls dispose', () => {
            const runner = createRunner();
            const dispose = vi.fn();
            runner.addSystem({ id: 'removable', dispose });
            const runtime = createRuntime();

            const result = runner.removeSystem('removable', runtime);

            expect(result).toBe(true);
            expect(runner.systemCount).toBe(0);
            expect(dispose).toHaveBeenCalledOnce();
        });

        it('removes by system object reference', () => {
            const runner = createRunner();
            const system: GameLoopSystem<TestState> = { id: 'obj-ref' };
            runner.addSystem(system);
            const runtime = createRuntime();

            const result = runner.removeSystem(system, runtime);

            expect(result).toBe(true);
            expect(runner.systemCount).toBe(0);
        });

        it('returns false for non-existent system', () => {
            const runner = createRunner();
            const runtime = createRuntime();

            expect(runner.removeSystem('ghost', runtime)).toBe(false);
        });

        it('wraps dispose error in GameLoopSystemError and re-throws', () => {
            const runner = createRunner();
            runner.addSystem({
                id: 'bad-dispose',
                dispose() {
                    throw new Error('dispose boom');
                },
            });
            const runtime = createRuntime();

            expect(() => runner.removeSystem('bad-dispose', runtime)).toThrow(GameLoopSystemError);
        });
    });

    describe('clearSystems / disposeAllSystems', () => {
        it('disposes all systems and clears the registry', () => {
            const runner = createRunner();
            const disposeA = vi.fn();
            const disposeB = vi.fn();
            runner.addSystem({ id: 'a', dispose: disposeA });
            runner.addSystem({ id: 'b', dispose: disposeB });
            const runtime = createRuntime();

            runner.clearSystems(runtime);

            expect(runner.systemCount).toBe(0);
            expect(disposeA).toHaveBeenCalledOnce();
            expect(disposeB).toHaveBeenCalledOnce();
        });

        it('disposeAllSystems returns first error without throwing', () => {
            const runner = createRunner();
            runner.addSystem({
                id: 'fail-first',
                dispose() {
                    throw new Error('first');
                },
            });
            runner.addSystem({
                id: 'fail-second',
                dispose() {
                    throw new Error('second');
                },
            });
            const runtime = createRuntime();

            const error = runner.disposeAllSystems(runtime);

            expect(error).toBeInstanceOf(GameLoopSystemError);
            expect(error?.systemId).toBe('fail-first');
            expect(runner.systemCount).toBe(0);
        });

        it('clearSystems throws the first disposal error', () => {
            const runner = createRunner();
            runner.addSystem({
                id: 'exploder',
                dispose() {
                    throw new Error('boom');
                },
            });
            const runtime = createRuntime();

            expect(() => runner.clearSystems(runtime)).toThrow(GameLoopSystemError);
        });
    });

    describe('invokePhase', () => {
        it('calls the correct method for each phase', () => {
            const runner = createRunner();
            const calls: string[] = [];

            runner.addSystem({
                id: 'all-phases',
                beforeUpdate() { calls.push('beforeUpdate'); },
                fixedUpdate() { calls.push('fixedUpdate'); },
                update() { calls.push('update'); },
                render() { calls.push('render'); },
                afterFrame() { calls.push('afterFrame'); },
            });
            const runtime = createRuntime();

            runner.invokePhase('before-update', baseContext as any, runtime);
            runner.invokePhase('fixed-update', baseContext as any, runtime);
            runner.invokePhase('update', baseContext as any, runtime);
            runner.invokePhase('render', baseContext as any, runtime);
            runner.invokePhase('after-frame', baseContext as any, runtime);

            expect(calls).toEqual(['beforeUpdate', 'fixedUpdate', 'update', 'render', 'afterFrame']);
        });

        it('respects priority ordering (higher priority first)', () => {
            const runner = createRunner();
            const order: string[] = [];

            runner.addSystem({
                id: 'low',
                priority: 1,
                update() { order.push('low'); },
            });
            runner.addSystem({
                id: 'high',
                priority: 10,
                update() { order.push('high'); },
            });
            runner.addSystem({
                id: 'mid',
                priority: 5,
                update() { order.push('mid'); },
            });
            const runtime = createRuntime();

            runner.invokePhase('update', baseContext as any, runtime);

            expect(order).toEqual(['high', 'mid', 'low']);
        });

        it('uses insertion order for equal priority', () => {
            const runner = createRunner();
            const order: string[] = [];

            runner.addSystem({ id: 'first', update() { order.push('first'); } });
            runner.addSystem({ id: 'second', update() { order.push('second'); } });
            runner.addSystem({ id: 'third', update() { order.push('third'); } });
            const runtime = createRuntime();

            runner.invokePhase('update', baseContext as any, runtime);

            expect(order).toEqual(['first', 'second', 'third']);
        });

        it('skips systems with enabled === false', () => {
            const runner = createRunner();
            const calls: string[] = [];

            runner.addSystem({
                id: 'disabled',
                enabled: false,
                update() { calls.push('disabled'); },
            });
            runner.addSystem({
                id: 'enabled',
                update() { calls.push('enabled'); },
            });
            const runtime = createRuntime();

            runner.invokePhase('update', baseContext as any, runtime);

            expect(calls).toEqual(['enabled']);
        });

        it('skips systems without the relevant hook', () => {
            const runner = createRunner();
            runner.addSystem({ id: 'no-render' });
            const runtime = createRuntime();

            expect(() => runner.invokePhase('render', baseContext as any, runtime)).not.toThrow();
        });

        it('stops when runtime.isRunning() returns false mid-phase', () => {
            const runner = createRunner();
            const calls: string[] = [];
            let callCount = 0;

            runner.addSystem({
                id: 'pauser',
                priority: 10,
                update() {
                    calls.push('pauser');
                    callCount++;
                },
            });
            runner.addSystem({
                id: 'skipped',
                priority: 1,
                update() {
                    calls.push('skipped');
                },
            });

            const runtime = createRuntime({
                isRunning: vi.fn(() => {
                    // After first system runs, simulate pause
                    return callCount === 0;
                }),
            });

            runner.invokePhase('update', baseContext as any, runtime);

            expect(calls).toEqual(['pauser']);
            expect(callCount).toBe(1);
        });
    });

    describe('error handling', () => {
        it('throw policy wraps error in GameLoopSystemError and throws', () => {
            const runner = createRunner({ errorPolicy: 'throw' });
            runner.addSystem({
                id: 'thrower',
                update() { throw new Error('boom'); },
            });
            const runtime = createRuntime();

            expect(() => runner.invokePhase('update', baseContext as any, runtime)).toThrow(
                GameLoopSystemError
            );
        });

        it('pause policy calls runtime.pause() on failure', () => {
            const runner = createRunner({ errorPolicy: 'pause' });
            runner.addSystem({
                id: 'fail-pause',
                update() { throw new Error('boom'); },
            });
            const runtime = createRuntime();

            runner.invokePhase('update', baseContext as any, runtime);

            expect(runtime.pause).toHaveBeenCalledOnce();
        });

        it('stop policy calls runtime.stop() on failure', () => {
            const runner = createRunner({ errorPolicy: 'stop' });
            runner.addSystem({
                id: 'fail-stop',
                update() { throw new Error('boom'); },
            });
            const runtime = createRuntime();

            runner.invokePhase('update', baseContext as any, runtime);

            expect(runtime.stop).toHaveBeenCalledOnce();
        });

        it('continue policy proceeds to next system after failure', () => {
            const runner = createRunner({ errorPolicy: 'continue' });
            const calls: string[] = [];

            runner.addSystem({
                id: 'fail-continue',
                priority: 10,
                update() { throw new Error('boom'); },
            });
            runner.addSystem({
                id: 'survivor',
                priority: 1,
                update() { calls.push('survivor'); },
            });
            const runtime = createRuntime();

            runner.invokePhase('update', baseContext as any, runtime);

            expect(calls).toEqual(['survivor']);
        });

        it('onError override replaces default policy', () => {
            const runner = createRunner({
                errorPolicy: 'continue',
                onError: () => 'pause' as const,
            });
            runner.addSystem({
                id: 'override',
                update() { throw new Error('boom'); },
            });
            const runtime = createRuntime();

            runner.invokePhase('update', baseContext as any, runtime);

            expect(runtime.pause).toHaveBeenCalledOnce();
        });

        it('onError returning non-policy value falls back to default policy', () => {
            const runner = createRunner({
                errorPolicy: 'stop',
                onError: () => undefined,
            });
            runner.addSystem({
                id: 'fallback',
                update() { throw new Error('boom'); },
            });
            const runtime = createRuntime();

            runner.invokePhase('update', baseContext as any, runtime);

            expect(runtime.stop).toHaveBeenCalledOnce();
        });
    });

    describe('retry', () => {
        it('retries up to N times before failing', () => {
            let attempts = 0;
            const runner = createRunner({
                errorPolicy: 'pause',
                retryAttempts: 2,
            });
            runner.addSystem({
                id: 'retry-system',
                update() {
                    attempts++;
                    throw new Error('persistent failure');
                },
            });
            const runtime = createRuntime();

            runner.invokePhase('update', baseContext as any, runtime);

            expect(attempts).toBe(3); // 1 initial + 2 retries
            expect(runtime.pause).toHaveBeenCalledOnce();
        });

        it('succeeds if retry eventually works', () => {
            let attempts = 0;
            const runner = createRunner({
                errorPolicy: 'throw',
                retryAttempts: 3,
            });
            runner.addSystem({
                id: 'flaky',
                update() {
                    attempts++;
                    if (attempts < 3) throw new Error('transient');
                },
            });
            const runtime = createRuntime();

            runner.invokePhase('update', baseContext as any, runtime);

            expect(attempts).toBe(3);
        });

        it('shouldRetry returning false stops retrying immediately', () => {
            let attempts = 0;
            const runner = createRunner({
                errorPolicy: 'pause',
                retryAttempts: 5,
                shouldRetry: () => false,
            });
            runner.addSystem({
                id: 'no-retry',
                update() {
                    attempts++;
                    throw new Error('boom');
                },
            });
            const runtime = createRuntime();

            runner.invokePhase('update', baseContext as any, runtime);

            expect(attempts).toBe(1);
            expect(runtime.pause).toHaveBeenCalledOnce();
        });

        it('shouldRetry receives error and failure context', () => {
            const shouldRetry = vi.fn(() => false);
            const runner = createRunner({
                errorPolicy: 'continue',
                retryAttempts: 2,
                shouldRetry,
            });
            runner.addSystem({
                id: 'ctx-system',
                update() { throw new Error('contextual'); },
            });
            const runtime = createRuntime();

            runner.invokePhase('update', baseContext as any, runtime);

            expect(shouldRetry).toHaveBeenCalledOnce();
            const [error, context] = shouldRetry.mock.calls[0];
            expect(error).toBeInstanceOf(Error);
            expect((error as Error).message).toBe('contextual');
            expect(context.system.id).toBe('ctx-system');
            expect(context.phase).toBe('update');
            expect(context.attempt).toBe(1);
        });
    });

    describe('sort caching', () => {
        it('re-sorts only after add/remove (dirty flag)', () => {
            const runner = createRunner();
            const order: string[] = [];

            runner.addSystem({ id: 'a', priority: 1, update() { order.push('a'); } });
            runner.addSystem({ id: 'b', priority: 10, update() { order.push('b'); } });
            const runtime = createRuntime();

            // First invocation triggers sort
            runner.invokePhase('update', baseContext as any, runtime);
            expect(order).toEqual(['b', 'a']);

            // Second invocation uses cache (no re-sort needed)
            order.length = 0;
            runner.invokePhase('update', baseContext as any, runtime);
            expect(order).toEqual(['b', 'a']);

            // Adding a new system invalidates cache
            runner.addSystem({ id: 'c', priority: 100, update() { order.push('c'); } });
            order.length = 0;
            runner.invokePhase('update', baseContext as any, runtime);
            expect(order).toEqual(['c', 'b', 'a']);
        });
    });
});
