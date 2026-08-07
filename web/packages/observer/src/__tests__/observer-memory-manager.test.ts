import { afterEach, describe, expect, it, vi } from 'vitest';
import { Subject, MemoryManager } from '@axrone/observer';

describe('MemoryManager', () => {
    let manager: MemoryManager;

    afterEach(() => {
        manager?.dispose();
    });

    describe('trackSubject / untrackSubject', () => {
        it('should track a subject', () => {
            manager = new MemoryManager({ gcIntervalMs: 0 });
            const subject = new Subject<string>();
            manager.trackSubject(subject);
            const tracked = manager.getTrackedSubjects();
            expect(tracked).toHaveLength(1);
            expect(tracked[0]).toBe(subject.id);
            subject.dispose();
        });

        it('should untrack a subject', () => {
            manager = new MemoryManager({ gcIntervalMs: 0 });
            const subject = new Subject<string>();
            manager.trackSubject(subject);
            manager.untrackSubject(subject.id);
            expect(manager.getTrackedSubjects()).toHaveLength(0);
            subject.dispose();
        });

        it('should not track when tracking is disabled', () => {
            manager = new MemoryManager({ enableTracking: false, gcIntervalMs: 0 });
            const subject = new Subject<string>();
            manager.trackSubject(subject);
            expect(manager.getTrackedSubjects()).toHaveLength(0);
            subject.dispose();
        });

        it('should not track after dispose', () => {
            manager = new MemoryManager({ gcIntervalMs: 0 });
            manager.dispose();
            const subject = new Subject<string>();
            manager.trackSubject(subject);
            expect(manager.getTrackedSubjects()).toHaveLength(0);
            subject.dispose();
        });
    });

    describe('trackObserver / untrackObserver', () => {
        it('should track an observer', () => {
            manager = new MemoryManager({ gcIntervalMs: 0 });
            const subject = new Subject<string>();
            const obsId = Symbol('TestObs');
            const mockObserver = {
                id: obsId,
                callback: () => {},
                options: {} as any,
                createdAt: Date.now(),
                executionCount: 0,
                lastExecuted: undefined,
                isActive: true,
                subject,
                priority: 1,
                isDebounced: false,
                isThrottled: false,
                hasFilter: false,
                hasTransform: false,
                bufferSize: 0,
                replayEnabled: false,
            };
            manager.trackObserver(mockObserver);
            expect(manager.getTrackedObservers()).toHaveLength(1);
            expect(manager.getTrackedObservers()[0]).toBe(obsId);
            subject.dispose();
        });

        it('should untrack an observer', () => {
            manager = new MemoryManager({ gcIntervalMs: 0 });
            const subject = new Subject<string>();
            const obsId = Symbol('TestObs');
            const mockObserver = {
                id: obsId,
                callback: () => {},
                options: {} as any,
                createdAt: Date.now(),
                executionCount: 0,
                lastExecuted: undefined,
                isActive: true,
                subject,
                priority: 1,
                isDebounced: false,
                isThrottled: false,
                hasFilter: false,
                hasTransform: false,
                bufferSize: 0,
                replayEnabled: false,
            };
            manager.trackObserver(mockObserver);
            manager.untrackObserver(obsId);
            expect(manager.getTrackedObservers()).toHaveLength(0);
            subject.dispose();
        });
    });

    describe('getMemoryUsage', () => {
        it('should return usage stats', () => {
            manager = new MemoryManager({ gcIntervalMs: 0 });
            const subject = new Subject<string>();
            manager.trackSubject(subject);
            const usage = manager.getMemoryUsage();
            expect(usage.subjects).toBe(1);
            expect(typeof usage.observers).toBe('number');
            expect(typeof usage.replayBuffers).toBe('number');
            expect(typeof usage.observerBuffers).toBe('number');
            expect(typeof usage.totalMemoryBytes).toBe('number');
            expect(usage.totalMemoryBytes).toBeGreaterThan(0);
            subject.dispose();
        });

        it('should return zero counts for empty manager', () => {
            manager = new MemoryManager({ gcIntervalMs: 0 });
            const usage = manager.getMemoryUsage();
            expect(usage.subjects).toBe(0);
            expect(usage.observers).toBe(0);
            expect(usage.totalMemoryBytes).toBe(0);
        });
    });

    describe('runGarbageCollection', () => {
        it('should return zeros after dispose', async () => {
            manager = new MemoryManager({ gcIntervalMs: 0 });
            manager.dispose();
            const result = await manager.runGarbageCollection();
            expect(result.subjectsCleared).toBe(0);
            expect(result.observersCleared).toBe(0);
            expect(result.memoryFreed).toBe(0);
        });

        it('should clear inactive observers', async () => {
            manager = new MemoryManager({ gcIntervalMs: 0 });
            const subject = new Subject<string>();
            const obsId = Symbol('InactiveObs');
            const mockObserver = {
                id: obsId,
                callback: () => {},
                options: {} as any,
                createdAt: Date.now(),
                executionCount: 0,
                lastExecuted: undefined,
                isActive: false,
                subject,
                priority: 1,
                isDebounced: false,
                isThrottled: false,
                hasFilter: false,
                hasTransform: false,
                bufferSize: 0,
                replayEnabled: false,
            };
            manager.trackObserver(mockObserver);
            expect(manager.getTrackedObservers()).toHaveLength(1);

            const result = await manager.runGarbageCollection();
            expect(result.observersCleared).toBe(1);
            expect(manager.getTrackedObservers()).toHaveLength(0);
            subject.dispose();
        });
    });

    describe('dispose', () => {
        it('should clear all tracked items', () => {
            manager = new MemoryManager({ gcIntervalMs: 0 });
            const subject = new Subject<string>();
            manager.trackSubject(subject);
            manager.dispose();
            expect(manager.getTrackedSubjects()).toHaveLength(0);
            expect(manager.getTrackedObservers()).toHaveLength(0);
            subject.dispose();
        });

        it('should be idempotent', () => {
            manager = new MemoryManager({ gcIntervalMs: 0 });
            manager.dispose();
            manager.dispose(); // no error
        });
    });

    describe('getSubjectAccessTime / getObserverAccessTime', () => {
        it('should return access time for tracked subject', () => {
            manager = new MemoryManager({ gcIntervalMs: 0 });
            const subject = new Subject<string>();
            manager.trackSubject(subject);
            const time = manager.getSubjectAccessTime(subject.id);
            expect(typeof time).toBe('number');
            expect(time).toBeGreaterThan(0);
            subject.dispose();
        });

        it('should return undefined for untracked subject', () => {
            manager = new MemoryManager({ gcIntervalMs: 0 });
            expect(manager.getSubjectAccessTime(Symbol('x'))).toBeUndefined();
        });

        it('should return undefined for untracked observer', () => {
            manager = new MemoryManager({ gcIntervalMs: 0 });
            expect(manager.getObserverAccessTime(Symbol('x'))).toBeUndefined();
        });
    });

    describe('getHealthMetrics', () => {
        it('should return health metrics', () => {
            manager = new MemoryManager({ gcIntervalMs: 0 });
            const subject = new Subject<string>();
            manager.trackSubject(subject);
            const health = manager.getHealthMetrics();
            expect(typeof health.deadSubjects).toBe('number');
            expect(typeof health.deadObservers).toBe('number');
            expect(typeof health.inactiveSubjects).toBe('number');
            expect(typeof health.inactiveObservers).toBe('number');
            expect(typeof health.totalTracked).toBe('number');
            expect(['low', 'medium', 'high']).toContain(health.memoryPressure);
            expect(health.totalTracked).toBe(1);
            subject.dispose();
        });

        it('should report low memory pressure for empty manager', () => {
            manager = new MemoryManager({ gcIntervalMs: 0 });
            const health = manager.getHealthMetrics();
            expect(health.memoryPressure).toBe('low');
            expect(health.totalTracked).toBe(0);
        });
    });
});
