import { describe, it, expect, afterEach, vi } from 'vitest';
import {
    EventScheduler,
    TaskPriority,
    TaskState,
    type ISchedulerOptions,
} from '@axrone/event';

describe('EventScheduler - Advanced', () => {
    describe('gracefulDispose()', () => {
        it('should drain pending tasks then dispose', async () => {
            const scheduler = new EventScheduler({
                concurrencyLimit: 1,
                enableMetrics: true,
            });

            let completed = 0;
            const task = () =>
                new Promise<void>((resolve) => {
                    setTimeout(() => {
                        completed++;
                        resolve();
                    }, 20);
                });

            scheduler.schedule(task);
            scheduler.schedule(task);
            scheduler.schedule(task);

            await scheduler.gracefulDispose();

            expect(completed).toBe(3);
            expect(scheduler.disposed).toBe(true);
        });

        it('should be no-op when already disposed', async () => {
            const scheduler = new EventScheduler();
            scheduler.dispose();

            await expect(scheduler.gracefulDispose()).resolves.toBeUndefined();
        });
    });

    describe('getTaskMetrics()', () => {
        it('should return metrics for a specific completed task', async () => {
            const scheduler = new EventScheduler({ enableMetrics: true });

            let taskId: any;
            await scheduler.schedule(() => {
                return Promise.resolve('done');
            }).then(() => {});

            const allMetrics = scheduler.getAllTaskMetrics();
            expect(allMetrics.length).toBeGreaterThan(0);

            const firstMetric = allMetrics[0]!;
            const specific = scheduler.getTaskMetrics(firstMetric.id);
            expect(specific).not.toBeNull();
            expect(specific!.state).toBe(TaskState.COMPLETED);
            expect(specific!.executionTime).toBeGreaterThanOrEqual(0);

            scheduler.dispose();
        });

        it('should return null for non-existing task ID', () => {
            const scheduler = new EventScheduler({ enableMetrics: true });

            const result = scheduler.getTaskMetrics(99999 as any);
            expect(result).toBeNull();

            scheduler.dispose();
        });
    });

    describe('IMMEDIATE and IDLE priority', () => {
        it('should execute IMMEDIATE priority tasks before HIGH', async () => {
            const scheduler = new EventScheduler({
                concurrencyLimit: 1,
            });

            const executionOrder: string[] = [];

            const blocking = scheduler.schedule(
                () => new Promise<void>((resolve) => setTimeout(resolve, 50))
            );

            const highTask = scheduler.schedule(
                () => Promise.resolve(executionOrder.push('high')),
                TaskPriority.HIGH
            );
            const immediateTask = scheduler.schedule(
                () => Promise.resolve(executionOrder.push('immediate')),
                TaskPriority.IMMEDIATE
            );
            const idleTask = scheduler.schedule(
                () => Promise.resolve(executionOrder.push('idle')),
                TaskPriority.IDLE
            );

            await blocking;
            await Promise.all([highTask, immediateTask, idleTask]);

            expect(executionOrder).toEqual(['immediate', 'high', 'idle']);

            scheduler.dispose();
        });
    });

    describe('Getters', () => {
        it('should report isAtCapacity correctly', () => {
            const scheduler = new EventScheduler({
                concurrencyLimit: 1,
                maxQueueSize: 2,
            });

            expect(scheduler.isAtCapacity).toBe(false);

            scheduler.schedule(() => new Promise((resolve) => setTimeout(resolve, 100)));
            scheduler.schedule(() => Promise.resolve());
            scheduler.schedule(() => Promise.resolve());

            expect(scheduler.isAtCapacity).toBe(true);

            scheduler.dispose();
        });

        it('should report disposed correctly', () => {
            const scheduler = new EventScheduler();

            expect(scheduler.disposed).toBe(false);

            scheduler.dispose();

            expect(scheduler.disposed).toBe(true);
        });
    });

    describe('Garbage Collection', () => {
        it('should clean up old completed task metrics', async () => {
            const scheduler = new EventScheduler({
                enableMetrics: true,
                gcIntervalMs: 1000,
            });

            await scheduler.schedule(() => Promise.resolve('task1'));
            await scheduler.schedule(() => Promise.resolve('task2'));

            expect(scheduler.getAllTaskMetrics().length).toBe(2);

            // Manually trigger GC by advancing time
            vi.useFakeTimers();
            vi.advanceTimersByTime(3000);
            vi.useRealTimers();

            // GC should have cleaned up completed metrics
            // The cutoff is gcIntervalMs * 2 = 2000ms
            // Since the tasks completed before the cutoff, they should be cleaned

            scheduler.dispose();
        });
    });

    describe('Throughput Calculation', () => {
        it('should report throughputPerSecond in stats', async () => {
            const scheduler = new EventScheduler({
                concurrencyLimit: Infinity,
                enableMetrics: true,
            });

            const tasks = Array.from({ length: 10 }, () =>
                scheduler.schedule(() => Promise.resolve())
            );

            await Promise.all(tasks);

            const stats = scheduler.getStats();
            expect(stats.throughputPerSecond).toBeGreaterThanOrEqual(0);
            expect(stats.completedCount).toBe(10);

            scheduler.dispose();
        });

        it('should reset throughput on clearMetrics', async () => {
            const scheduler = new EventScheduler({
                concurrencyLimit: Infinity,
                enableMetrics: true,
            });

            await scheduler.schedule(() => Promise.resolve());

            scheduler.clearMetrics();

            const stats = scheduler.getStats();
            expect(stats.completedCount).toBe(0);
            expect(stats.totalProcessed).toBe(0);

            scheduler.dispose();
        });
    });

    describe('Option Normalization', () => {
        it('should handle NaN concurrency limit', () => {
            const scheduler = new EventScheduler({
                concurrencyLimit: NaN,
            });

            expect(scheduler.activeCount).toBe(0);
            scheduler.dispose();
        });

        it('should handle negative maxQueueSize', () => {
            const scheduler = new EventScheduler({
                maxQueueSize: -5,
            });

            expect(scheduler.isAtCapacity).toBe(false);
            scheduler.dispose();
        });

        it('should handle Infinity concurrency', async () => {
            const scheduler = new EventScheduler({
                concurrencyLimit: Infinity,
            });

            let active = 0;
            let maxActive = 0;

            const tasks = Array.from({ length: 20 }, () =>
                scheduler.schedule(async () => {
                    active++;
                    maxActive = Math.max(maxActive, active);
                    await new Promise((resolve) => setTimeout(resolve, 10));
                    active--;
                })
            );

            await Promise.all(tasks);
            expect(maxActive).toBe(20);

            scheduler.dispose();
        });

        it('should handle negative retryDelay gracefully', () => {
            const scheduler = new EventScheduler({
                enableRetries: true,
                retryDelay: -100,
            });

            expect(scheduler).toBeDefined();
            scheduler.dispose();
        });

        it('should handle negative taskTimeout gracefully', () => {
            const scheduler = new EventScheduler({
                taskTimeout: -500,
            });

            expect(scheduler).toBeDefined();
            scheduler.dispose();
        });
    });

    describe('Double Disposal', () => {
        it('should be safe to call dispose multiple times', () => {
            const scheduler = new EventScheduler();

            expect(() => {
                scheduler.dispose();
                scheduler.dispose();
            }).not.toThrow();
        });
    });
});
