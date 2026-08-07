import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
    Subject,
    ObserverUtils,
    isObservableSubject,
    isObserver,
    createSubject,
} from '@axrone/observer';

const sleep = (ms: number) => new Promise((res) => setTimeout(res, ms));

describe('ObserverUtils', () => {
    describe('createTypedSubject', () => {
        it('should create subjects on property access', async () => {
            interface MyState {
                name: string;
                age: number;
            }
            const typed = ObserverUtils.createTypedSubject<MyState>();
            const nameCb = vi.fn();
            const ageCb = vi.fn();

            typed.name.addObserver(nameCb);
            typed.age.addObserver(ageCb);

            await typed.name.notify('Alice');
            await typed.age.notify(30);

            expect(nameCb).toHaveBeenCalledWith('Alice', expect.anything());
            expect(ageCb).toHaveBeenCalledWith(30, expect.anything());
        });

        it('should cache subjects for the same key', () => {
            const typed = ObserverUtils.createTypedSubject<{ x: number }>();
            const first = typed.x;
            const second = typed.x;
            expect(first).toBe(second);
        });
    });

    describe('fromPromise', () => {
        it('should notify value and complete on resolve', async () => {
            const promise = Promise.resolve(42);
            const subject = ObserverUtils.fromPromise(promise);
            const cb = vi.fn();
            subject.addObserver(cb);

            await sleep(20);
            expect(cb).toHaveBeenCalledWith(42, subject);
            expect(subject.isCompleted()).toBe(true);
            subject.dispose();
        });

        it('should error on reject', async () => {
            const promise = Promise.reject(new Error('fail'));
            const subject = ObserverUtils.fromPromise(promise);
            subject.addObserver(() => {});

            await sleep(20);
            expect(subject.isErrored()).toBe(true);
            expect(subject.getLastError()?.message).toBe('fail');
            subject.dispose();
        });

        it('should wrap non-Error rejections', async () => {
            const promise = Promise.reject('string error');
            const subject = ObserverUtils.fromPromise(promise);
            subject.addObserver(() => {});

            await sleep(20);
            expect(subject.isErrored()).toBe(true);
            expect(subject.getLastError()?.message).toBe('string error');
            subject.dispose();
        });
    });

    describe('fromArray', () => {
        it('should emit all values and complete in sync mode', () => {
            // In sync mode, fromArray notifies all values then completes immediately.
            // Observers cannot be added after completion, so we verify the subject state.
            const subject = ObserverUtils.fromArray([1, 2, 3], 0);
            expect(subject.isCompleted()).toBe(true);
            subject.dispose();
        });

        it('should emit values asynchronously with interval', async () => {
            vi.useFakeTimers();
            const subject = ObserverUtils.fromArray([10, 20, 30], 50);
            const cb = vi.fn();
            subject.addObserver(cb);

            vi.advanceTimersByTime(50);
            await Promise.resolve();
            expect(cb).toHaveBeenCalledTimes(1);
            expect(cb).toHaveBeenCalledWith(10, subject);

            vi.advanceTimersByTime(50);
            await Promise.resolve();
            expect(cb).toHaveBeenCalledTimes(2);
            expect(cb).toHaveBeenCalledWith(20, subject);

            vi.advanceTimersByTime(50);
            await Promise.resolve();
            expect(cb).toHaveBeenCalledTimes(3);
            expect(cb).toHaveBeenCalledWith(30, subject);

            subject.dispose();
            vi.useRealTimers();
        });

        it('should clear interval on dispose', () => {
            vi.useFakeTimers();
            const subject = ObserverUtils.fromArray([1, 2, 3], 50);
            const cb = vi.fn();
            subject.addObserver(cb);

            vi.advanceTimersByTime(50);
            subject.dispose();
            vi.advanceTimersByTime(200);
            expect(cb).toHaveBeenCalledTimes(1);
            vi.useRealTimers();
        });
    });

    describe('fromEvent', () => {
        it('should forward DOM events', () => {
            const target = new EventTarget();
            const subject = ObserverUtils.fromEvent<MouseEvent>(target, 'click');
            const cb = vi.fn();
            subject.addObserver(cb);

            const event = new Event('click');
            target.dispatchEvent(event);

            expect(cb).toHaveBeenCalledWith(event, subject);
            subject.dispose();
        });

        it('should remove listener on dispose', () => {
            const target = new EventTarget();
            const subject = ObserverUtils.fromEvent(target, 'click');
            const cb = vi.fn();
            subject.addObserver(cb);

            subject.dispose();
            target.dispatchEvent(new Event('click'));
            expect(cb).not.toHaveBeenCalled();
        });
    });

    describe('interval', () => {
        it('should emit incrementing numbers', () => {
            vi.useFakeTimers();
            const subject = ObserverUtils.interval(100);
            const cb = vi.fn();
            subject.addObserver(cb);

            vi.advanceTimersByTime(100);
            expect(cb).toHaveBeenCalledTimes(1);
            expect(cb).toHaveBeenCalledWith(0, subject);

            vi.advanceTimersByTime(100);
            expect(cb).toHaveBeenCalledTimes(2);
            expect(cb).toHaveBeenCalledWith(1, subject);

            vi.advanceTimersByTime(100);
            expect(cb).toHaveBeenCalledTimes(3);
            expect(cb).toHaveBeenCalledWith(2, subject);

            subject.dispose();
            vi.useRealTimers();
        });

        it('should stop on dispose', () => {
            vi.useFakeTimers();
            const subject = ObserverUtils.interval(50);
            const cb = vi.fn();
            subject.addObserver(cb);

            vi.advanceTimersByTime(50);
            subject.dispose();
            vi.advanceTimersByTime(200);
            expect(cb).toHaveBeenCalledTimes(1);
            vi.useRealTimers();
        });
    });

    describe('timer', () => {
        it('should emit after delay without interval', async () => {
            const subject = ObserverUtils.timer(30);
            const cb = vi.fn();
            subject.addObserver(cb);

            await sleep(60);
            expect(cb).toHaveBeenCalledWith(0, subject);
            expect(subject.isCompleted()).toBe(true);

            subject.dispose();
        });

        it('should emit repeatedly with interval after delay', () => {
            vi.useFakeTimers();
            const subject = ObserverUtils.timer(50, 100);
            const cb = vi.fn();
            subject.addObserver(cb);

            vi.advanceTimersByTime(50);
            expect(cb).toHaveBeenCalledTimes(1);
            expect(cb).toHaveBeenCalledWith(0, subject);

            vi.advanceTimersByTime(100);
            expect(cb).toHaveBeenCalledTimes(2);
            expect(cb).toHaveBeenCalledWith(1, subject);

            subject.dispose();
            vi.useRealTimers();
        });

        it('should clear both timeout and interval on dispose', () => {
            vi.useFakeTimers();
            const subject = ObserverUtils.timer(50, 100);
            const cb = vi.fn();
            subject.addObserver(cb);

            vi.advanceTimersByTime(50);
            subject.dispose();
            vi.advanceTimersByTime(300);
            expect(cb).toHaveBeenCalledTimes(1);
            vi.useRealTimers();
        });
    });

    describe('defer', () => {
        it('should lazily create source on first addObserver', async () => {
            const source = createSubject<number>();
            const factoryFn = vi.fn(() => source);

            const deferred = ObserverUtils.defer(factoryFn);
            expect(factoryFn).not.toHaveBeenCalled();

            const cb = vi.fn();
            deferred.addObserver(cb);

            expect(factoryFn).toHaveBeenCalledTimes(1);

            await source.notify(42);
            await sleep(20);
            expect(cb).toHaveBeenCalledWith(42, expect.anything());
            deferred.dispose();
            source.dispose();
        });

        it('should not call factory again for subsequent observers', () => {
            const factoryFn = vi.fn(() => createSubject<number>());
            const deferred = ObserverUtils.defer(factoryFn);

            deferred.addObserver(vi.fn());
            deferred.addObserver(vi.fn());
            expect(factoryFn).toHaveBeenCalledTimes(1);
            deferred.dispose();
        });

        it('should clean up source on dispose', () => {
            const source = createSubject<number>();
            const disposeSpy = vi.spyOn(source, 'dispose');
            const deferred = ObserverUtils.defer(() => source);

            deferred.addObserver(vi.fn());
            deferred.dispose();
            expect(disposeSpy).toHaveBeenCalled();
        });
    });
});

describe('Type Guards', () => {
    describe('isObservableSubject', () => {
        it('should return true for a valid Subject', () => {
            const subject = new Subject<string>();
            expect(isObservableSubject(subject)).toBe(true);
            subject.dispose();
        });

        it('should return false for null/undefined', () => {
            expect(isObservableSubject(null)).toBe(false);
            expect(isObservableSubject(undefined)).toBe(false);
        });

        it('should return false for primitives', () => {
            expect(isObservableSubject(42)).toBe(false);
            expect(isObservableSubject('string')).toBe(false);
            expect(isObservableSubject(true)).toBe(false);
        });

        it('should return false for partial objects', () => {
            expect(isObservableSubject({})).toBe(false);
            expect(isObservableSubject({ notify: () => {} })).toBe(false);
            expect(isObservableSubject({ notify: () => {}, addObserver: () => {} })).toBe(false);
        });

        it('should return true for duck-typed subject', () => {
            const duck = {
                id: Symbol('test'),
                notify: async () => true,
                addObserver: () => () => false,
                dispose: () => {},
            };
            expect(isObservableSubject(duck)).toBe(true);
        });
    });

    describe('isObserver', () => {
        it('should return false for null/undefined', () => {
            expect(isObserver(null)).toBe(false);
            expect(isObserver(undefined)).toBe(false);
        });

        it('should return false for primitives', () => {
            expect(isObserver(42)).toBe(false);
            expect(isObserver('string')).toBe(false);
        });

        it('should return false for partial objects', () => {
            expect(isObserver({})).toBe(false);
            expect(isObserver({ callback: () => {} })).toBe(false);
            expect(isObserver({ callback: () => {}, id: Symbol() })).toBe(false);
        });

        it('should return true for duck-typed observer', () => {
            const duck = {
                id: Symbol('obs'),
                callback: () => {},
                createdAt: Date.now(),
            };
            expect(isObserver(duck)).toBe(true);
        });
    });
});
