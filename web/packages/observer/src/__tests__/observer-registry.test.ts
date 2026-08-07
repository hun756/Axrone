import { afterEach, describe, expect, it, vi } from 'vitest';
import {
    Subject,
    ObserverRegistry,
    createSubject,
} from '@axrone/observer';

describe('ObserverRegistry', () => {
    let registry: ObserverRegistry;

    afterEach(() => {
        registry?.dispose();
    });

    describe('register', () => {
        it('should register an observer and return an ObserverId', () => {
            registry = new ObserverRegistry();
            const subject = new Subject<string>();
            const cb = vi.fn();
            const id = registry.register(subject, cb);
            expect(typeof id).toBe('symbol');
            subject.dispose();
        });

        it('should track the observer in subject map', () => {
            registry = new ObserverRegistry();
            const subject = new Subject<string>();
            const id = registry.register(subject, vi.fn());
            const observers = registry.getObserversForSubject(subject.id);
            expect(observers).toHaveLength(1);
            expect(observers[0].id).toBe(id);
            subject.dispose();
        });

        it('should throw if registry is disposed', () => {
            registry = new ObserverRegistry();
            registry.dispose();
            const subject = new Subject<string>();
            expect(() => registry.register(subject, vi.fn())).toThrow('disposed');
            subject.dispose();
        });

        it('should register with options (priority, debounce)', () => {
            registry = new ObserverRegistry();
            const subject = new Subject<string>();
            const id = registry.register(subject, vi.fn(), {
                priority: 'high',
                debounceMs: 100,
            });
            const obs = registry.getObserver(id);
            expect(obs).toBeDefined();
            expect(obs!.priority).toBe(0); // high = 0
            expect(obs!.isDebounced).toBe(true);
            subject.dispose();
        });
    });

    describe('unregister', () => {
        it('should remove an observer by id', () => {
            registry = new ObserverRegistry();
            const subject = new Subject<string>();
            const id = registry.register(subject, vi.fn());
            expect(registry.unregister(id)).toBe(true);
            expect(registry.getObserver(id)).toBeUndefined();
            expect(registry.getObserversForSubject(subject.id)).toHaveLength(0);
            subject.dispose();
        });

        it('should return false for unknown observer id', () => {
            registry = new ObserverRegistry();
            expect(registry.unregister(Symbol('unknown'))).toBe(false);
        });
    });

    describe('unregisterByCallback', () => {
        it('should remove observer by callback reference', () => {
            registry = new ObserverRegistry();
            const subject = new Subject<string>();
            const cb = vi.fn();
            registry.register(subject, cb);
            expect(registry.unregisterByCallback(subject, cb)).toBe(true);
            expect(registry.getObserversForSubject(subject.id)).toHaveLength(0);
            subject.dispose();
        });

        it('should return false if callback not found', () => {
            registry = new ObserverRegistry();
            const subject = new Subject<string>();
            expect(registry.unregisterByCallback(subject, vi.fn())).toBe(false);
            subject.dispose();
        });

        it('should return false for subject with no observers', () => {
            registry = new ObserverRegistry();
            const subject = new Subject<string>();
            const unknownSubject = new Subject<string>();
            registry.register(subject, vi.fn());
            expect(registry.unregisterByCallback(unknownSubject, vi.fn())).toBe(false);
            subject.dispose();
            unknownSubject.dispose();
        });
    });

    describe('getObserver', () => {
        it('should return subscription for registered observer', () => {
            registry = new ObserverRegistry();
            const subject = new Subject<string>();
            const id = registry.register(subject, vi.fn());
            const obs = registry.getObserver(id);
            expect(obs).toBeDefined();
            expect(obs!.isActive).toBe(true);
            expect(obs!.subject).toBe(subject);
            subject.dispose();
        });

        it('should return undefined for unknown id', () => {
            registry = new ObserverRegistry();
            expect(registry.getObserver(Symbol('x'))).toBeUndefined();
        });
    });

    describe('getObserversForSubject', () => {
        it('should return empty array for unknown subject', () => {
            registry = new ObserverRegistry();
            expect(registry.getObserversForSubject(Symbol('x'))).toEqual([]);
        });

        it('should return all observers for a subject', () => {
            registry = new ObserverRegistry();
            const subject = new Subject<string>();
            registry.register(subject, vi.fn());
            registry.register(subject, vi.fn());
            expect(registry.getObserversForSubject(subject.id)).toHaveLength(2);
            subject.dispose();
        });
    });

    describe('getActiveObserverCount', () => {
        it('should count only active observers', () => {
            registry = new ObserverRegistry();
            const subject = new Subject<string>();
            const id1 = registry.register(subject, vi.fn());
            registry.register(subject, vi.fn());
            expect(registry.getActiveObserverCount()).toBe(2);
            registry.unregister(id1);
            expect(registry.getActiveObserverCount()).toBe(1);
            subject.dispose();
        });
    });

    describe('getSubjectCount', () => {
        it('should return unique subject count', () => {
            registry = new ObserverRegistry();
            const s1 = new Subject<string>();
            const s2 = new Subject<string>();
            registry.register(s1, vi.fn());
            registry.register(s2, vi.fn());
            registry.register(s1, vi.fn());
            expect(registry.getSubjectCount()).toBe(2);
            s1.dispose();
            s2.dispose();
        });
    });

    describe('clear', () => {
        it('should remove all observers', () => {
            registry = new ObserverRegistry();
            const s1 = new Subject<string>();
            const s2 = new Subject<string>();
            registry.register(s1, vi.fn());
            registry.register(s2, vi.fn());
            registry.clear();
            expect(registry.getActiveObserverCount()).toBe(0);
            expect(registry.getSubjectCount()).toBe(0);
            s1.dispose();
            s2.dispose();
        });
    });

    describe('dispose', () => {
        it('should be idempotent', () => {
            registry = new ObserverRegistry();
            registry.dispose();
            registry.dispose(); // no error
        });

        it('should throw on register after dispose', () => {
            registry = new ObserverRegistry();
            registry.dispose();
            expect(() => registry.register(new Subject(), vi.fn())).toThrow();
        });
    });

    describe('getMemoryUsage', () => {
        it('should return observer and subject counts', () => {
            registry = new ObserverRegistry();
            const subject = new Subject<string>();
            registry.register(subject, vi.fn());
            const usage = registry.getMemoryUsage();
            expect(typeof usage).toBe('object');
            const keys = Object.keys(usage);
            expect(keys.length).toBeGreaterThan(0);
            subject.dispose();
        });
    });

    describe('query methods', () => {
        it('getObserversByPriority should filter by priority', () => {
            registry = new ObserverRegistry();
            const subject = new Subject<string>();
            registry.register(subject, vi.fn(), { priority: 'high' });
            registry.register(subject, vi.fn(), { priority: 'low' });
            registry.register(subject, vi.fn(), { priority: 'high' });

            const highObservers = registry.getObserversByPriority(0);
            expect(highObservers).toHaveLength(2);
            const lowObservers = registry.getObserversByPriority(2);
            expect(lowObservers).toHaveLength(1);
            subject.dispose();
        });

        it('getObserversWithFilters should return observers with filter option', () => {
            registry = new ObserverRegistry();
            const subject = new Subject<number>();
            registry.register(subject, vi.fn(), { filter: (d: number) => d > 0 });
            registry.register(subject, vi.fn());

            const filtered = registry.getObserversWithFilters();
            expect(filtered).toHaveLength(1);
            subject.dispose();
        });

        it('getObserversWithTransforms should return observers with transform option', () => {
            registry = new ObserverRegistry();
            const subject = new Subject<number>();
            registry.register(subject, vi.fn(), { transform: (d: number) => d * 2 });
            registry.register(subject, vi.fn());

            const transformed = registry.getObserversWithTransforms();
            expect(transformed).toHaveLength(1);
            subject.dispose();
        });

        it('getDebounceObservers should return debounced observers', () => {
            registry = new ObserverRegistry();
            const subject = new Subject<string>();
            registry.register(subject, vi.fn(), { debounceMs: 100 });
            registry.register(subject, vi.fn());

            const debounced = registry.getDebounceObservers();
            expect(debounced).toHaveLength(1);
            subject.dispose();
        });

        it('getThrottledObservers should return throttled observers', () => {
            registry = new ObserverRegistry();
            const subject = new Subject<string>();
            registry.register(subject, vi.fn(), { throttleMs: 100 });
            registry.register(subject, vi.fn());

            const throttled = registry.getThrottledObservers();
            expect(throttled).toHaveLength(1);
            subject.dispose();
        });
    });

    describe('validateRegistry', () => {
        it('should report healthy registry', () => {
            registry = new ObserverRegistry();
            const subject = new Subject<string>();
            registry.register(subject, vi.fn());
            const health = registry.validateRegistry();
            expect(health.isHealthy).toBe(true);
            expect(health.issues).toHaveLength(0);
            expect(health.totalObservers).toBe(1);
            expect(health.activeObservers).toBe(1);
            expect(health.inactiveObservers).toBe(0);
            subject.dispose();
        });
    });
});
