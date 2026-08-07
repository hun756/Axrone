import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
    Subject,
    chain,
    group,
    connect,
    pipe,
    merge,
    combineLatest,
    filter,
    map,
    debounce,
    throttle,
    ObserverChain,
    SubjectGroup,
    ObserverConnection,
} from '@axrone/observer';

const sleep = (ms: number) => new Promise((res) => setTimeout(res, ms));

describe('Observer Operators', () => {
    describe('ObserverChain', () => {
        describe('filter', () => {
            it('should pass values matching predicate', async () => {
                const subject = new Subject<number>();
                const cb = vi.fn();
                chain(subject).filter((d) => d > 5).subscribe(cb);
                await subject.notify(3);
                await subject.notify(7);
                await subject.notify(10);
                expect(cb).toHaveBeenCalledTimes(2);
                expect(cb).toHaveBeenNthCalledWith(1, 7, subject);
                expect(cb).toHaveBeenNthCalledWith(2, 10, subject);
                subject.dispose();
            });
        });

        describe('map', () => {
            it('should transform values', async () => {
                const subject = new Subject<number>();
                const cb = vi.fn();
                chain(subject).map((d) => d * 3).subscribe(cb);
                await subject.notify(2);
                await subject.notify(5);
                expect(cb).toHaveBeenCalledTimes(2);
                expect(cb).toHaveBeenNthCalledWith(1, 6, subject);
                expect(cb).toHaveBeenNthCalledWith(2, 15, subject);
                subject.dispose();
            });
        });

        describe('debounce', () => {
            it('should debounce rapid emissions', async () => {
                vi.useFakeTimers();
                const subject = new Subject<number>();
                const cb = vi.fn();
                chain(subject).debounce(50).subscribe(cb);

                subject.notifySync(1);
                subject.notifySync(2);
                subject.notifySync(3);
                expect(cb).not.toHaveBeenCalled();

                vi.advanceTimersByTime(60);
                expect(cb).toHaveBeenCalledTimes(1);
                expect(cb).toHaveBeenCalledWith(3, subject);
                subject.dispose();
                vi.useRealTimers();
            });
        });

        describe('throttle', () => {
            it('should throttle emissions', () => {
                vi.useFakeTimers();
                const subject = new Subject<number>();
                const cb = vi.fn();
                chain(subject).throttle(100).subscribe(cb);

                subject.notifySync(1);
                expect(cb).toHaveBeenCalledTimes(1);

                subject.notifySync(2);
                subject.notifySync(3);
                expect(cb).toHaveBeenCalledTimes(1);

                vi.advanceTimersByTime(110);
                subject.notifySync(4);
                expect(cb).toHaveBeenCalledTimes(2);
                subject.dispose();
                vi.useRealTimers();
            });
        });

        describe('buffer', () => {
            it('should flush when buffer reaches maxSize', () => {
                const subject = new Subject<number>();
                const cb = vi.fn();
                chain(subject).buffer(3, 10000).subscribe(cb);

                subject.notifySync(1);
                subject.notifySync(2);
                expect(cb).not.toHaveBeenCalled();

                subject.notifySync(3);
                expect(cb).toHaveBeenCalledTimes(1);
                expect(cb).toHaveBeenCalledWith([1, 2, 3], subject);
                subject.dispose();
            });

            it('should flush on timer', async () => {
                vi.useFakeTimers();
                const subject = new Subject<number>();
                const cb = vi.fn();
                chain(subject).buffer(100, 50).subscribe(cb);

                subject.notifySync(1);
                subject.notifySync(2);
                expect(cb).not.toHaveBeenCalled();

                vi.advanceTimersByTime(60);
                expect(cb).toHaveBeenCalledTimes(1);
                expect(cb).toHaveBeenCalledWith([1, 2], subject);
                subject.dispose();
                vi.useRealTimers();
            });
        });

        describe('take', () => {
            it('should limit emissions to count', () => {
                const subject = new Subject<number>();
                const cb = vi.fn();
                chain(subject).take(2).subscribe(cb);

                subject.notifySync(1);
                subject.notifySync(2);
                subject.notifySync(3);
                expect(cb).toHaveBeenCalledTimes(2);
                expect(cb).toHaveBeenNthCalledWith(1, 1, subject);
                expect(cb).toHaveBeenNthCalledWith(2, 2, subject);
                subject.dispose();
            });
        });

        describe('takeUntil', () => {
            it('should stop when predicate matches', () => {
                const subject = new Subject<number>();
                const cb = vi.fn();
                chain(subject).takeUntil((d) => d === 3).subscribe(cb);

                subject.notifySync(1);
                subject.notifySync(2);
                subject.notifySync(3);
                subject.notifySync(4);
                expect(cb).toHaveBeenCalledTimes(2);
                subject.dispose();
            });
        });

        describe('chaining multiple operations', () => {
            it('should compose filter -> map -> take', () => {
                const subject = new Subject<number>();
                const cb = vi.fn();
                chain(subject)
                    .filter((d) => d > 2)
                    .map((d) => d * 10)
                    .take(2)
                    .subscribe(cb);

                subject.notifySync(1); // filtered out
                subject.notifySync(3); // -> 30
                subject.notifySync(4); // -> 40, take limit reached
                subject.notifySync(5); // stopped
                expect(cb).toHaveBeenCalledTimes(2);
                expect(cb).toHaveBeenNthCalledWith(1, 30, subject);
                expect(cb).toHaveBeenNthCalledWith(2, 40, subject);
                subject.dispose();
            });
        });

        describe('subscribe UnobserveFn', () => {
            it('should stop receiving emissions after unsubscribe', () => {
                const subject = new Subject<number>();
                const cb = vi.fn();
                const unsub = chain(subject).subscribe(cb);

                subject.notifySync(1);
                expect(cb).toHaveBeenCalledTimes(1);

                unsub();
                subject.notifySync(2);
                expect(cb).toHaveBeenCalledTimes(1);
                subject.dispose();
            });
        });
    });

    describe('SubjectGroup', () => {
        describe('add / remove / removeById', () => {
            it('should manage subjects', () => {
                const g = group(new Subject<string>(), new Subject<string>());
                expect(g.subjects).toHaveLength(2);

                const s3 = new Subject<string>();
                g.add(s3);
                expect(g.subjects).toHaveLength(3);

                g.remove(s3);
                expect(g.subjects).toHaveLength(2);

                const remaining = g.subjects[0];
                g.removeById(remaining.id);
                expect(g.subjects).toHaveLength(1);
            });
        });

        describe('notifyAll / notifyAllSync', () => {
            it('should notify all subjects asynchronously', async () => {
                const s1 = new Subject<string>();
                const s2 = new Subject<string>();
                const g = group(s1, s2);
                const cb1 = vi.fn();
                const cb2 = vi.fn();
                s1.addObserver(cb1);
                s2.addObserver(cb2);

                const results = await g.notifyAll('hello');
                expect(results).toEqual([true, true]);
                expect(cb1).toHaveBeenCalledWith('hello', s1);
                expect(cb2).toHaveBeenCalledWith('hello', s2);
                s1.dispose();
                s2.dispose();
            });

            it('should notify all subjects synchronously', () => {
                const s1 = new Subject<string>();
                const s2 = new Subject<string>();
                const g = group(s1, s2);
                const cb1 = vi.fn();
                const cb2 = vi.fn();
                s1.addObserver(cb1);
                s2.addObserver(cb2);

                const results = g.notifyAllSync('hello');
                expect(results).toEqual([true, true]);
                expect(cb1).toHaveBeenCalledWith('hello', s1);
                s1.dispose();
                s2.dispose();
            });
        });

        describe('completeAll', () => {
            it('should complete all subjects', async () => {
                const s1 = new Subject<string>();
                const s2 = new Subject<string>();
                const g = group(s1, s2);

                await g.completeAll();
                expect(s1.isCompleted()).toBe(true);
                expect(s2.isCompleted()).toBe(true);
                s1.dispose();
                s2.dispose();
            });
        });

        describe('disposeAll', () => {
            it('should dispose all subjects and clear group', () => {
                const s1 = new Subject<string>();
                const s2 = new Subject<string>();
                const g = group(s1, s2);

                g.disposeAll();
                expect(() => s1.notifySync('x')).toThrow();
                expect(() => s2.notifySync('x')).toThrow();
                expect(g.subjects).toHaveLength(0);
            });
        });

        describe('addObserver', () => {
            it('should add observer to all subjects and return unsubscribe array', () => {
                const s1 = new Subject<string>();
                const s2 = new Subject<string>();
                const g = group(s1, s2);
                const cb = vi.fn();

                const unsubs = g.addObserver(cb);
                expect(unsubs).toHaveLength(2);

                s1.notifySync('a');
                expect(cb).toHaveBeenCalledTimes(1);

                unsubs.forEach((u) => u());
                s1.notifySync('b');
                expect(cb).toHaveBeenCalledTimes(1);
                s1.dispose();
                s2.dispose();
            });
        });

        describe('merge', () => {
            it('should create a subject that receives from all sources', async () => {
                const s1 = new Subject<number>();
                const s2 = new Subject<number>();
                const g = group(s1, s2);
                const merged = g.merge();
                const cb = vi.fn();
                merged.addObserver(cb);

                await s1.notify(1);
                await s2.notify(2);
                expect(cb).toHaveBeenCalledTimes(2);
                expect(cb).toHaveBeenNthCalledWith(1, 1, expect.anything());
                expect(cb).toHaveBeenNthCalledWith(2, 2, expect.anything());
                merged.dispose();
                s1.dispose();
                s2.dispose();
            });
        });

        describe('combineLatest', () => {
            it('should emit arrays after all subjects have emitted', async () => {
                const s1 = new Subject<number>();
                const s2 = new Subject<number>();
                const g = group(s1, s2);
                const combined = g.combineLatest();
                const cb = vi.fn();
                combined.addObserver(cb);

                await s1.notify(1);
                expect(cb).not.toHaveBeenCalled();

                await s2.notify(10);
                expect(cb).toHaveBeenCalledTimes(1);
                expect(cb).toHaveBeenCalledWith([1, 10], expect.anything());

                await s1.notify(2);
                expect(cb).toHaveBeenCalledTimes(2);
                expect(cb).toHaveBeenCalledWith([2, 10], expect.anything());
                combined.dispose();
                s1.dispose();
                s2.dispose();
            });
        });
    });

    describe('ObserverConnection', () => {
        describe('connect / disconnect', () => {
            it('should forward notifications from source to target', async () => {
                const source = new Subject<number>();
                const target = new Subject<number>();
                const conn = connect(source, target);
                const cb = vi.fn();
                target.addObserver(cb);

                conn.connect();
                expect(conn.isConnected).toBe(true);

                await source.notify(42);
                expect(cb).toHaveBeenCalledWith(42, target);

                conn.disconnect();
                expect(conn.isConnected).toBe(false);

                await source.notify(99);
                expect(cb).toHaveBeenCalledTimes(1);
                source.dispose();
                target.dispose();
            });

            it('should be idempotent for connect and disconnect', () => {
                const source = new Subject<number>();
                const target = new Subject<number>();
                const conn = connect(source, target);

                conn.connect();
                conn.connect(); // no error
                expect(conn.isConnected).toBe(true);

                conn.disconnect();
                conn.disconnect(); // no error
                expect(conn.isConnected).toBe(false);
                source.dispose();
                target.dispose();
            });
        });

        describe('transform', () => {
            it('should apply synchronous transform', async () => {
                const source = new Subject<number>();
                const target = new Subject<string>();
                const conn = connect(source, target, (d) => String(d * 2));
                const cb = vi.fn();
                target.addObserver(cb);

                conn.connect();
                await source.notify(5);
                expect(cb).toHaveBeenCalledWith('10', target);
                conn.disconnect();
                source.dispose();
                target.dispose();
            });

            it('should apply async transform', async () => {
                const source = new Subject<number>();
                const target = new Subject<string>();
                const conn = connect(source, target, async (d) => `val:${d}`);
                const cb = vi.fn();
                target.addObserver(cb);

                conn.connect();
                await source.notify(7);
                await sleep(10);
                expect(cb).toHaveBeenCalledWith('val:7', target);
                conn.disconnect();
                source.dispose();
                target.dispose();
            });
        });

        describe('error forwarding', () => {
            it('should forward transform errors to target.error', async () => {
                const source = new Subject<number>();
                const target = new Subject<string>();
                const conn = connect(source, target, () => {
                    throw new Error('transform fail');
                });
                target.addObserver(() => {});

                conn.connect();
                await source.notify(1);
                await sleep(10);
                expect(target.isErrored()).toBe(true);
                expect(target.getLastError()?.message).toBe('transform fail');
                conn.disconnect();
                source.dispose();
                target.dispose();
            });
        });

        describe('dispose', () => {
            it('should disconnect on dispose', () => {
                const source = new Subject<number>();
                const target = new Subject<number>();
                const conn = connect(source, target);
                conn.connect();
                expect(conn.isConnected).toBe(true);

                conn.dispose();
                expect(conn.isConnected).toBe(false);
                source.dispose();
                target.dispose();
            });
        });
    });

    describe('Standalone operators', () => {
        describe('pipe', () => {
            it('should transform and forward values', async () => {
                const source = new Subject<number>();
                const target = pipe(source, (d) => d * 10);
                const cb = vi.fn();
                target.addObserver(cb);

                await source.notify(3);
                expect(cb).toHaveBeenCalledWith(30, target);
                target.dispose();
                source.dispose();
            });

            it('should forward errors to target', async () => {
                const source = new Subject<number>();
                const target = pipe(source, () => {
                    throw new Error('pipe fail');
                });
                target.addObserver(() => {});

                await source.notify(1);
                await sleep(10);
                expect(target.isErrored()).toBe(true);
                target.dispose();
                source.dispose();
            });
        });

        describe('merge', () => {
            it('should merge multiple subjects into one', async () => {
                const s1 = new Subject<number>();
                const s2 = new Subject<number>();
                const merged = merge(s1, s2);
                const cb = vi.fn();
                merged.addObserver(cb);

                await s1.notify(1);
                await s2.notify(2);
                expect(cb).toHaveBeenCalledTimes(2);
                merged.dispose();
                s1.dispose();
                s2.dispose();
            });
        });

        describe('combineLatest', () => {
            it('should emit combined arrays', async () => {
                const s1 = new Subject<string>();
                const s2 = new Subject<string>();
                const combined = combineLatest(s1, s2);
                const cb = vi.fn();
                combined.addObserver(cb);

                await s1.notify('a');
                expect(cb).not.toHaveBeenCalled();
                await s2.notify('b');
                expect(cb).toHaveBeenCalledWith(['a', 'b'], combined);
                combined.dispose();
                s1.dispose();
                s2.dispose();
            });
        });

        describe('filter (standalone)', () => {
            it('should forward only matching values', async () => {
                const source = new Subject<number>();
                const filtered = filter(source, (d) => d % 2 === 0);
                const cb = vi.fn();
                filtered.addObserver(cb);

                await source.notify(1);
                await source.notify(2);
                await source.notify(3);
                await source.notify(4);
                expect(cb).toHaveBeenCalledTimes(2);
                expect(cb).toHaveBeenNthCalledWith(1, 2, filtered);
                expect(cb).toHaveBeenNthCalledWith(2, 4, filtered);
                filtered.dispose();
                source.dispose();
            });
        });

        describe('map (standalone)', () => {
            it('should transform values', async () => {
                const source = new Subject<number>();
                const mapped = map(source, (d) => d + '!');
                const cb = vi.fn();
                mapped.addObserver(cb);

                await source.notify(42);
                expect(cb).toHaveBeenCalledWith('42!', mapped);
                mapped.dispose();
                source.dispose();
            });
        });

        describe('debounce (standalone)', () => {
            it('should debounce emissions', async () => {
                vi.useFakeTimers();
                const source = new Subject<number>();
                const debounced = debounce(source, 50);
                const cb = vi.fn();
                debounced.addObserver(cb);

                source.notifySync(1);
                source.notifySync(2);
                source.notifySync(3);
                expect(cb).not.toHaveBeenCalled();

                vi.advanceTimersByTime(60);
                expect(cb).toHaveBeenCalledTimes(1);
                expect(cb).toHaveBeenCalledWith(3, debounced);
                debounced.dispose();
                source.dispose();
                vi.useRealTimers();
            });

            it('should clean up timer on dispose', () => {
                vi.useFakeTimers();
                const source = new Subject<number>();
                const debounced = debounce(source, 50);
                const cb = vi.fn();
                debounced.addObserver(cb);

                source.notifySync(1);
                debounced.dispose();
                vi.advanceTimersByTime(100);
                expect(cb).not.toHaveBeenCalled();
                source.dispose();
                vi.useRealTimers();
            });
        });

        describe('throttle (standalone)', () => {
            it('should throttle emissions', () => {
                vi.useFakeTimers();
                const source = new Subject<number>();
                const throttled = throttle(source, 100);
                const cb = vi.fn();
                throttled.addObserver(cb);

                source.notifySync(1);
                expect(cb).toHaveBeenCalledTimes(1);

                source.notifySync(2);
                expect(cb).toHaveBeenCalledTimes(1);

                vi.advanceTimersByTime(110);
                source.notifySync(3);
                expect(cb).toHaveBeenCalledTimes(2);
                throttled.dispose();
                source.dispose();
                vi.useRealTimers();
            });
        });
    });
});
