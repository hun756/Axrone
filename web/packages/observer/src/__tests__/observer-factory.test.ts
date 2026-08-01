import { afterEach, describe, expect, it, vi } from 'vitest';
import {
    ObservableFactory,
    createSubject,
    createBehaviorSubject,
    createReplaySubject,
    createAsyncSubject,
    createObserver,
    createRegistry,
    Subject,
    BehaviorSubject,
    ReplaySubject,
    AsyncSubject,
    ObserverRegistry,
} from '@axrone/observer';

describe('Observer Factory', () => {
    describe('ObservableFactory', () => {
        let factory: ObservableFactory;

        afterEach(() => {
            factory?.dispose();
        });

        it('should create a Subject via createSubject', () => {
            factory = new ObservableFactory();
            const subject = factory.createSubject<number>();
            expect(subject).toBeDefined();
            expect(typeof subject.notify).toBe('function');
            expect(typeof subject.addObserver).toBe('function');
            expect(typeof subject.dispose).toBe('function');
            expect(subject.id).toBeDefined();
        });

        it('should apply default subject options', () => {
            factory = new ObservableFactory({
                defaultSubjectOptions: { maxObservers: 5 },
            });
            const subject = factory.createSubject();
            const obs: Array<() => void> = [];
            for (let i = 0; i < 5; i++) {
                obs.push(subject.addObserver(() => {}));
            }
            expect(() => subject.addObserver(() => {})).toThrow();
            obs.forEach((u) => u());
        });

        it('should create a BehaviorSubject with initial value', () => {
            factory = new ObservableFactory();
            const subject = factory.createBehaviorSubject<string>('hello');
            expect(subject).toBeInstanceOf(BehaviorSubject);
            expect(subject.value).toBe('hello');
        });

        it('should merge default options into BehaviorSubject', () => {
            factory = new ObservableFactory({
                defaultSubjectOptions: { maxObservers: 2 },
            });
            const subject = factory.createBehaviorSubject<number>(0);
            subject.addObserver(() => {});
            subject.addObserver(() => {});
            expect(() => subject.addObserver(() => {})).toThrow();
        });

        it('should create a ReplaySubject with buffer size', () => {
            factory = new ObservableFactory();
            const subject = factory.createReplaySubject<number>(3);
            expect(subject).toBeInstanceOf(ReplaySubject);
            subject.notifySync(1);
            subject.notifySync(2);
            subject.notifySync(3);
            subject.notifySync(4);
            const buf = subject.getReplayBuffer();
            expect(buf).toEqual([2, 3, 4]);
        });

        it('should create an AsyncSubject', () => {
            factory = new ObservableFactory();
            const subject = factory.createAsyncSubject<number>();
            expect(subject).toBeInstanceOf(AsyncSubject);
        });

        it('should create an observer with callback and options', () => {
            factory = new ObservableFactory();
            const cb = vi.fn();
            const observer = factory.createObserver(cb, { priority: 'high' });
            expect(observer.callback).toBe(cb);
            expect(observer.options.priority).toBe('high');
            expect(observer.id).toBeDefined();
            expect(typeof observer.createdAt).toBe('number');
        });

        it('should create a registry', () => {
            factory = new ObservableFactory();
            const registry = factory.createRegistry();
            expect(registry).toBeDefined();
            expect(typeof registry.register).toBe('function');
            expect(typeof registry.unregister).toBe('function');
        });

        it('should return memory usage', () => {
            factory = new ObservableFactory();
            const usage = factory.getMemoryUsage();
            expect(usage).toBeDefined();
            expect(typeof usage.subjects).toBe('number');
            expect(typeof usage.observers).toBe('number');
            expect(typeof usage.totalMemoryBytes).toBe('number');
        });

        it('should run garbage collection', async () => {
            factory = new ObservableFactory();
            const result = await factory.runGarbageCollection();
            expect(result).toBeDefined();
            expect(typeof result.subjectsCleared).toBe('number');
            expect(typeof result.observersCleared).toBe('number');
            expect(typeof result.memoryFreed).toBe('number');
        });

        it('should not dispose external memory manager on factory dispose', () => {
            const externalMm = {
                trackSubject: vi.fn(),
                untrackSubject: vi.fn(),
                trackObserver: vi.fn(),
                untrackObserver: vi.fn(),
                getMemoryUsage: vi.fn().mockReturnValue({
                    subjects: 0, observers: 0, replayBuffers: 0, observerBuffers: 0, totalMemoryBytes: 0,
                }),
                runGarbageCollection: vi.fn().mockResolvedValue({ subjectsCleared: 0, observersCleared: 0, memoryFreed: 0 }),
                dispose: vi.fn(),
            };
            factory = new ObservableFactory({ memoryManager: externalMm });
            factory.dispose();
            expect(externalMm.dispose).not.toHaveBeenCalled();
        });
    });

    describe('Convenience factory functions', () => {
        it('createSubject should return a working subject', async () => {
            const subject = createSubject<string>();
            const cb = vi.fn();
            subject.addObserver(cb);
            await subject.notify('test');
            expect(cb).toHaveBeenCalledWith('test', subject);
            subject.dispose();
        });

        it('createBehaviorSubject should return subject with initial value', () => {
            const subject = createBehaviorSubject<number>(42);
            expect(subject.value).toBe(42);
            subject.dispose();
        });

        it('createReplaySubject should return subject with replay enabled', () => {
            const subject = createReplaySubject<number>(2);
            subject.notifySync(1);
            subject.notifySync(2);
            subject.notifySync(3);
            expect(subject.getReplayBuffer()).toEqual([2, 3]);
            subject.dispose();
        });

        it('createAsyncSubject should return an AsyncSubject', () => {
            const subject = createAsyncSubject<string>();
            expect(subject).toBeInstanceOf(AsyncSubject);
            subject.dispose();
        });

        it('createObserver should return an observer object', () => {
            const cb = vi.fn();
            const observer = createObserver(cb, { once: true });
            expect(observer.callback).toBe(cb);
            expect(observer.options.once).toBe(true);
        });

        it('createRegistry should return an ObserverRegistry', () => {
            const registry = createRegistry();
            expect(registry).toBeDefined();
            expect(typeof registry.register).toBe('function');
        });
    });
});
