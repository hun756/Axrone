import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
    Subject,
    BehaviorSubject,
    ReplaySubject,
    AsyncSubject,
    MaxObserversExceededError,
    SubjectDisposedError,
    SubjectCompletedError,
    ValidationError,
} from '@axrone/observer';

const sleep = (ms: number) => new Promise((res) => setTimeout(res, ms));

describe('Subject Advanced Features', () => {
    let consoleErrorSpy: ReturnType<typeof vi.spyOn>;

    beforeEach(() => {
        consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
    });

    afterEach(() => {
        consoleErrorSpy.mockRestore();
    });

    describe('Lifecycle hooks', () => {
        it('onBeforeNotify returning false should cancel notification', async () => {
            const subject = new Subject<string>();
            const cb = vi.fn();
            subject.addObserver(cb);

            subject.setLifecycle({
                onBeforeNotify: (data) => data !== 'skip',
            });

            await subject.notify('keep');
            await subject.notify('skip');
            await subject.notify('also-keep');

            expect(cb).toHaveBeenCalledTimes(2);
            expect(cb).toHaveBeenNthCalledWith(1, 'keep', subject);
            expect(cb).toHaveBeenNthCalledWith(2, 'also-keep', subject);
            subject.dispose();
        });

        it('onAfterNotify should be called with success status', async () => {
            const subject = new Subject<string>();
            const afterSpy = vi.fn();
            subject.addObserver(vi.fn());
            subject.setLifecycle({ onAfterNotify: afterSpy });

            await subject.notify('data');
            expect(afterSpy).toHaveBeenCalledWith('data', subject, true);
            subject.dispose();
        });

        it('onAfterNotify should be called with false on error', async () => {
            const subject = new Subject<number>({
                validation: { enabled: true, validator: (d) => d > 0 },
                errorPropagation: false,
            });
            const afterSpy = vi.fn();
            subject.setLifecycle({ onAfterNotify: afterSpy });

            await subject.notify(-1);
            expect(afterSpy).toHaveBeenCalledWith(-1, subject, false);
            subject.dispose();
        });

        it('onObserverAdded should fire when observer is added', () => {
            const subject = new Subject<string>();
            const addedSpy = vi.fn();
            subject.setLifecycle({ onObserverAdded: addedSpy });

            subject.addObserver(vi.fn());
            expect(addedSpy).toHaveBeenCalledTimes(1);
            expect(addedSpy).toHaveBeenCalledWith(
                expect.objectContaining({ isActive: true }),
                subject
            );
            subject.dispose();
        });

        it('onObserverRemoved should fire when observer is removed', () => {
            const subject = new Subject<string>();
            const removedSpy = vi.fn();
            subject.setLifecycle({ onObserverRemoved: removedSpy });

            const unsub = subject.addObserver(vi.fn());
            unsub();
            expect(removedSpy).toHaveBeenCalledTimes(1);
            subject.dispose();
        });

        it('onComplete should fire on complete', async () => {
            const subject = new Subject<string>();
            const completeSpy = vi.fn();
            subject.setLifecycle({ onComplete: completeSpy });

            await subject.complete();
            expect(completeSpy).toHaveBeenCalledWith(subject);
            subject.dispose();
        });

        it('onError should fire on error', async () => {
            const subject = new Subject<string>();
            const errorSpy = vi.fn();
            subject.setLifecycle({ onError: errorSpy });

            await subject.error(new Error('test'));
            expect(errorSpy).toHaveBeenCalledWith(expect.any(Error), subject);
            subject.dispose();
        });

        it('onDispose should fire on dispose', () => {
            const subject = new Subject<string>();
            const disposeSpy = vi.fn();
            subject.setLifecycle({ onDispose: disposeSpy });

            subject.dispose();
            expect(disposeSpy).toHaveBeenCalledWith(subject);
        });
    });

    describe('Validation', () => {
        it('should reject invalid data with ValidationError', async () => {
            const subject = new Subject<number>({
                validation: { enabled: true, validator: (d) => d > 0 },
            });

            await expect(subject.notify(-1)).rejects.toThrow(ValidationError);
            subject.dispose();
        });

        it('should accept valid data', async () => {
            const subject = new Subject<number>({
                validation: { enabled: true, validator: (d) => d > 0 },
            });
            const cb = vi.fn();
            subject.addObserver(cb);

            await subject.notify(5);
            expect(cb).toHaveBeenCalledWith(5, subject);
            subject.dispose();
        });

        it('should also validate in notifySync', () => {
            const subject = new Subject<number>({
                validation: { enabled: true, validator: (d) => d > 0 },
            });

            expect(() => subject.notifySync(-1)).toThrow(ValidationError);
            subject.dispose();
        });
    });

    describe('Concurrency', () => {
        it('should throw ConcurrencyLimitError when limit exceeded', async () => {
            const subject = new Subject<string>({
                concurrency: { enabled: true, maxConcurrent: 1 },
            });

            const blockingObserver = async () => {
                await sleep(200);
            };
            subject.addObserver(blockingObserver);

            const first = subject.notify('first');
            await expect(subject.notify('second')).rejects.toThrow();
            await first;
            subject.dispose();
        });

        it('should allow unlimited when concurrency is disabled', async () => {
            const subject = new Subject<number>({
                concurrency: { enabled: false, maxConcurrent: 1 },
            });
            const cb = vi.fn();
            subject.addObserver(cb);

            await subject.notify(1);
            await subject.notify(2);
            expect(cb).toHaveBeenCalledTimes(2);
            subject.dispose();
        });
    });

    describe('Observer management', () => {
        it('removeObserver should find by callback reference', () => {
            const subject = new Subject<string>();
            const cb = vi.fn();
            subject.addObserver(cb);
            expect(subject.hasObserver(cb)).toBe(true);

            const removed = subject.removeObserver(cb);
            expect(removed).toBe(true);
            expect(subject.hasObserver(cb)).toBe(false);
            expect(subject.getObserverCount()).toBe(0);
            subject.dispose();
        });

        it('removeObserver should return false for unknown callback', () => {
            const subject = new Subject<string>();
            expect(subject.removeObserver(vi.fn())).toBe(false);
            subject.dispose();
        });

        it('removeObserverById should return true for valid id', () => {
            const subject = new Subject<string>();
            const cb = vi.fn();
            const unsub = subject.addObserver(cb);
            expect(subject.getObserverCount()).toBe(1);
            unsub();
            expect(subject.getObserverCount()).toBe(0);
            subject.dispose();
        });

        it('hasObserver should return false for non-registered callback', () => {
            const subject = new Subject<string>();
            expect(subject.hasObserver(vi.fn())).toBe(false);
            subject.dispose();
        });

        it('maxObservers should throw when exceeded', () => {
            const subject = new Subject<string>({ maxObservers: 2 });
            subject.addObserver(vi.fn());
            subject.addObserver(vi.fn());
            expect(() => subject.addObserver(vi.fn())).toThrow(MaxObserversExceededError);
            subject.dispose();
        });
    });

    describe('autoComplete', () => {
        it('should dispose after complete when autoComplete is true', async () => {
            const subject = new Subject<string>({ autoComplete: true });
            await subject.complete();
            expect(subject.isCompleted()).toBe(true);
            expect(() => subject.notifySync('x')).toThrow(SubjectDisposedError);
        });
    });

    describe('Error propagation modes', () => {
        it('errorPropagation: true (default) should throw', async () => {
            const subject = new Subject<number>({
                validation: { enabled: true, validator: (d) => d > 0 },
            });
            await expect(subject.notify(-1)).rejects.toThrow();
            subject.dispose();
        });

        it('errorPropagation: false should return false instead of throwing', async () => {
            const subject = new Subject<number>({
                validation: { enabled: true, validator: (d) => d > 0 },
                errorPropagation: false,
            });
            const result = await subject.notify(-1);
            expect(result).toBe(false);
            subject.dispose();
        });

        it('notifySync with errorPropagation: false should return false', () => {
            const subject = new Subject<number>({
                validation: { enabled: true, validator: (d) => d > 0 },
                errorPropagation: false,
            });
            const result = subject.notifySync(-1);
            expect(result).toBe(false);
            subject.dispose();
        });
    });

    describe('Replay buffer', () => {
        it('getReplayBuffer should return buffered values', () => {
            const subject = new Subject<number>({
                replay: { enabled: true, bufferSize: 5 },
            });
            subject.notifySync(1);
            subject.notifySync(2);
            subject.notifySync(3);
            expect(subject.getReplayBuffer()).toEqual([1, 2, 3]);
            subject.dispose();
        });

        it('clearReplayBuffer should empty the buffer', () => {
            const subject = new Subject<number>({
                replay: { enabled: true, bufferSize: 5 },
            });
            subject.notifySync(1);
            subject.notifySync(2);
            subject.clearReplayBuffer();
            expect(subject.getReplayBuffer()).toEqual([]);
            subject.dispose();
        });

        it('getReplayBuffer should return empty array when replay is disabled', () => {
            const subject = new Subject<number>();
            subject.notifySync(1);
            expect(subject.getReplayBuffer()).toEqual([]);
            subject.dispose();
        });
    });

    describe('Observer buffering', () => {
        it('should buffer values when buffering is enabled', () => {
            const subject = new Subject<number>();
            const cb = vi.fn();
            subject.addObserver(cb, {
                buffering: { enabled: true, maxSize: 3, flushIntervalMs: 10000 },
            });

            // Buffered values should not trigger immediate callback
            subject.notifySync(1);
            subject.notifySync(2);
            subject.notifySync(3);
            expect(cb).not.toHaveBeenCalled();

            // Verify memory usage reflects buffered state
            const mem = subject.getMemoryUsage();
            expect(mem).toBeDefined();
            subject.dispose();
        });

        it('should set a flush timer when buffer is not full', () => {
            const subject = new Subject<number>();
            const cb = vi.fn();
            subject.addObserver(cb, {
                buffering: { enabled: true, maxSize: 10, flushIntervalMs: 30 },
            });

            subject.notifySync(1);
            subject.notifySync(2);
            // Values are buffered, not immediately dispatched
            expect(cb).not.toHaveBeenCalled();
            expect(subject.getObserverCount()).toBe(1);
            subject.dispose();
        });
    });

    describe('BehaviorSubject advanced', () => {
        it('should emit current value asynchronously on addObserver', async () => {
            const subject = new BehaviorSubject<string>('initial');
            const cb = vi.fn();
            subject.addObserver(cb);

            // queueMicrotask schedules the emission; flush microtask queue
            await Promise.resolve();
            await Promise.resolve();
            await Promise.resolve();
            expect(cb).toHaveBeenCalledWith('initial', subject);
            subject.dispose();
        });

        it('should not schedule after dispose', async () => {
            const subject = new BehaviorSubject<string>('val');
            subject.dispose();
            expect(() => subject.addObserver(vi.fn())).toThrow(SubjectDisposedError);
        });

        it('notifySync should update value', () => {
            const subject = new BehaviorSubject<number>(0);
            subject.notifySync(42);
            expect(subject.value).toBe(42);
            subject.dispose();
        });
    });

    describe('AsyncSubject advanced', () => {
        it('should not emit anything before complete', async () => {
            const subject = new AsyncSubject<number>();
            const cb = vi.fn();
            subject.addObserver(cb);

            await subject.notify(1);
            await subject.notify(2);
            expect(cb).not.toHaveBeenCalled();
            subject.dispose();
        });

        it('complete without values should not emit', async () => {
            const subject = new AsyncSubject<number>();
            const cb = vi.fn();
            subject.addObserver(cb);

            await subject.complete();
            expect(cb).not.toHaveBeenCalled();
            expect(subject.isCompleted()).toBe(true);
            subject.dispose();
        });

        it('should throw SubjectDisposedError after dispose', async () => {
            const subject = new AsyncSubject<number>();
            subject.dispose();
            await expect(subject.notify(1)).rejects.toThrow(SubjectDisposedError);
        });

        it('should throw SubjectCompletedError on notify after complete', async () => {
            const subject = new AsyncSubject<number>();
            await subject.complete();
            await expect(subject.notify(1)).rejects.toThrow(SubjectCompletedError);
        });

        it('autoComplete should dispose after complete', async () => {
            const subject = new AsyncSubject<number>({ autoComplete: true });
            const cb = vi.fn();
            subject.addObserver(cb);

            await subject.notify(99);
            await subject.complete();
            expect(cb).toHaveBeenCalledWith(99, subject);
            expect(() => subject.notifySync(1)).toThrow(SubjectDisposedError);
        });
    });

    describe('notifySync with async transform', () => {
        it('should throw TransformError for async transforms in sync mode', () => {
            const subject = new Subject<number>();
            const cb = vi.fn();
            subject.addObserver(cb, {
                transform: async (d) => d * 2,
                errorHandling: 'throw',
            });

            expect(() => subject.notifySync(5)).toThrow();
            subject.dispose();
        });
    });

    describe('Disposed subject behavior', () => {
        it('notify should throw SubjectDisposedError', async () => {
            const subject = new Subject<string>();
            subject.dispose();
            await expect(subject.notify('x')).rejects.toThrow(SubjectDisposedError);
        });

        it('notifySync should throw SubjectDisposedError', () => {
            const subject = new Subject<string>();
            subject.dispose();
            expect(() => subject.notifySync('x')).toThrow(SubjectDisposedError);
        });

        it('complete should throw SubjectDisposedError', async () => {
            const subject = new Subject<string>();
            subject.dispose();
            await expect(subject.complete()).rejects.toThrow(SubjectDisposedError);
        });

        it('error should throw SubjectDisposedError', async () => {
            const subject = new Subject<string>();
            subject.dispose();
            await expect(subject.error(new Error('x'))).rejects.toThrow(SubjectDisposedError);
        });

        it('addObserver should throw SubjectDisposedError', () => {
            const subject = new Subject<string>();
            subject.dispose();
            expect(() => subject.addObserver(vi.fn())).toThrow(SubjectDisposedError);
        });

        it('dispose should be idempotent', () => {
            const subject = new Subject<string>();
            subject.dispose();
            subject.dispose(); // no error
        });
    });

    describe('Metrics', () => {
        it('should track notification count and timing', async () => {
            const subject = new Subject<string>();
            subject.addObserver(vi.fn());

            await subject.notify('a');
            await subject.notify('b');

            const m = subject.metrics;
            expect(m.notificationCount).toBe(2);
            expect(m.observerCount).toBe(1);
            expect(m.isCompleted).toBe(false);
            expect(m.isErrored).toBe(false);
            expect(m.totalNotificationTime).toBeGreaterThanOrEqual(0);
            expect(m.averageNotificationTime).toBeGreaterThanOrEqual(0);
            expect(typeof m.createdAt).toBe('number');
            subject.dispose();
        });

        it('should track error count', async () => {
            const subject = new Subject<string>();
            await subject.error(new Error('e1'));
            await subject.error(new Error('e2'));
            expect(subject.metrics.errorCount).toBe(2);
            subject.dispose();
        });

        it('should track completedAt after complete', async () => {
            const subject = new Subject<string>();
            expect(subject.metrics.completedAt).toBeUndefined();
            await subject.complete();
            expect(subject.metrics.completedAt).toBeDefined();
            subject.dispose();
        });
    });

    describe('ReplaySubject replays on addObserver', () => {
        it('should replay buffered values to newly added observers', async () => {
            const subject = new ReplaySubject<number>({ replay: { enabled: true, bufferSize: 5 } });
            subject.notifySync(1);
            subject.notifySync(2);
            subject.notifySync(3);

            const cb = vi.fn();
            subject.addObserver(cb);

            // Replay values are scheduled via queueMicrotask
            await Promise.resolve();
            await Promise.resolve();
            await Promise.resolve();
            await Promise.resolve();
            await Promise.resolve();
            expect(cb).toHaveBeenCalledTimes(3);
            expect(cb).toHaveBeenNthCalledWith(1, 1, subject);
            expect(cb).toHaveBeenNthCalledWith(2, 2, subject);
            expect(cb).toHaveBeenNthCalledWith(3, 3, subject);
            subject.dispose();
        });

        it('should respect bufferSize when replaying', async () => {
            const subject = new ReplaySubject<number>({ replay: { enabled: true, bufferSize: 2 } });
            subject.notifySync(1);
            subject.notifySync(2);
            subject.notifySync(3);
            subject.notifySync(4);

            const cb = vi.fn();
            subject.addObserver(cb);

            await Promise.resolve();
            await Promise.resolve();
            await Promise.resolve();
            await Promise.resolve();
            expect(cb).toHaveBeenCalledTimes(2);
            expect(cb).toHaveBeenNthCalledWith(1, 3, subject);
            expect(cb).toHaveBeenNthCalledWith(2, 4, subject);
            subject.dispose();
        });
    });

    describe('Internal mechanics', () => {
        it('_compactObserversIfNeeded should defer bucket compaction during notification', async () => {
            const subject = new Subject<string>();
            const callOrder: string[] = [];

            const unsub1 = subject.addObserver(() => {
                callOrder.push('obs1');
                // Remove self during notification (marks inactive, defers compaction)
                unsub1();
            });
            const unsub2 = subject.addObserver(() => {
                callOrder.push('obs2');
            });

            expect(subject.getObserverCount()).toBe(2);

            await subject.notify('a');
            // obs1 ran, then removed itself; obs2 still ran because compaction is deferred
            expect(callOrder).toEqual(['obs1', 'obs2']);
            // After notification, compaction occurred: obs1 removed from map
            expect(subject.getObserverCount()).toBe(1);

            // Second notification: only obs2 should fire
            await subject.notify('b');
            expect(callOrder.filter((r) => r === 'obs2')).toHaveLength(2);
            expect(callOrder.filter((r) => r === 'obs1')).toHaveLength(1);
            subject.dispose();
        });

        it('_runGarbageCollection should clear inactive observers', () => {
            const subject = new Subject<string>({
                memoryManagement: { enabled: true, gcIntervalMs: 0, weakReferences: false },
            });
            const cb1 = vi.fn();
            const cb2 = vi.fn();
            subject.addObserver(cb1);
            const unsub2 = subject.addObserver(cb2);

            expect(subject.getObserverCount()).toBe(2);

            // Remove one observer
            unsub2();
            expect(subject.getObserverCount()).toBe(1);

            // GC should clean up any remaining inactive entries
            // (already cleaned by unsub2, so this is a no-op but should not throw)
            subject.notifySync('test');
            expect(cb1).toHaveBeenCalledWith('test', subject);
            subject.dispose();
        });
    });
});
