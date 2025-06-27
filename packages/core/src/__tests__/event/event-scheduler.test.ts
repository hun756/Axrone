import { EventScheduler } from '../../event/event';

describe('EventScheduler', () => {
    describe('Concurrency Control', () => {
        it('should respect concurrency limits', async () => {
            const scheduler = new EventScheduler(2);
            let activeCount = 0;
            let maxConcurrent = 0;

            const createTask = (id: number) => async () => {
                activeCount++;
                maxConcurrent = Math.max(maxConcurrent, activeCount);

                await new Promise((resolve) => setTimeout(resolve, 100));

                activeCount--;
                return id;
            };

            const promises = Array.from({ length: 5 }, (_, i) =>
                scheduler.schedule(createTask(i + 1))
            );

            const results = await Promise.all(promises);

            expect(results).toEqual([1, 2, 3, 4, 5]);
            expect(maxConcurrent).toBe(2);
            expect(scheduler.activeCount).toBe(0);
            expect(scheduler.pendingCount).toBe(0);
        });

        it('should handle unlimited concurrency correctly', async () => {
            const scheduler = new EventScheduler();
            let activeCount = 0;
            let maxConcurrent = 0;

            const createTask = (id: number) => async () => {
                activeCount++;
                maxConcurrent = Math.max(maxConcurrent, activeCount);

                await new Promise((resolve) => setTimeout(resolve, 50));

                activeCount--;
                return id;
            };

            const promises = Array.from({ length: 10 }, (_, i) =>
                scheduler.schedule(createTask(i + 1))
            );

            const results = await Promise.all(promises);

            expect(results).toHaveLength(10);
            expect(maxConcurrent).toBe(10);
        });

        it('should queue tasks when at capacity', async () => {
            const scheduler = new EventScheduler(1);
            let executionOrder: number[] = [];

            const createTask = (id: number, duration: number) => async () => {
                executionOrder.push(id);
                await new Promise((resolve) => setTimeout(resolve, duration));
                return id;
            };

            const task1 = scheduler.schedule(createTask(1, 100));
            const task2 = scheduler.schedule(createTask(2, 50));
            const task3 = scheduler.schedule(createTask(3, 30));

            expect(scheduler.activeCount).toBe(1);
            expect(scheduler.pendingCount).toBe(2);

            await Promise.all([task1, task2, task3]);

            expect(executionOrder).toEqual([1, 2, 3]);
            expect(scheduler.activeCount).toBe(0);
            expect(scheduler.pendingCount).toBe(0);
        });
    });

    describe('Error Handling and Resilience', () => {
        it('should handle task failures without affecting other tasks', async () => {
            const scheduler = new EventScheduler(2);
            const results: any[] = [];

            const tasks = [
                scheduler.schedule(async () => {
                    await new Promise((resolve) => setTimeout(resolve, 50));
                    return 'success1';
                }),
                scheduler.schedule(async () => {
                    await new Promise((resolve) => setTimeout(resolve, 30));
                    throw new Error('Task failed');
                }),
                scheduler.schedule(async () => {
                    await new Promise((resolve) => setTimeout(resolve, 70));
                    return 'success2';
                }),
            ];

            const settled = await Promise.allSettled(tasks);

            expect(settled[0].status).toBe('fulfilled');
            expect((settled[0] as PromiseFulfilledResult<string>).value).toBe('success1');

            expect(settled[1].status).toBe('rejected');
            expect((settled[1] as PromiseRejectedResult).reason.message).toBe('Task failed');

            expect(settled[2].status).toBe('fulfilled');
            expect((settled[2] as PromiseFulfilledResult<string>).value).toBe('success2');

            expect(scheduler.activeCount).toBe(0);
        });

        it('should continue processing queue after task failure', async () => {
            const scheduler = new EventScheduler(1);
            let processedTasks = 0;

            const createTask = (shouldFail: boolean) => async () => {
                processedTasks++;
                await new Promise((resolve) => setTimeout(resolve, 50));

                if (shouldFail) {
                    throw new Error('Task failed');
                }
                return 'success';
            };

            const tasks = [
                scheduler.schedule(createTask(false)),
                scheduler.schedule(createTask(true)),
                scheduler.schedule(createTask(false)),
            ];

            const results = await Promise.allSettled(tasks);

            expect(processedTasks).toBe(3);
            expect(results[0].status).toBe('fulfilled');
            expect(results[1].status).toBe('rejected');
            expect(results[2].status).toBe('fulfilled');
        });
    });

    describe('Drain and Cleanup Operations', () => {
        it('should drain all pending operations', async () => {
            const scheduler = new EventScheduler(1);
            let completedTasks = 0;

            const tasks = Array.from({ length: 5 }, (_, i) =>
                scheduler.schedule(async () => {
                    await new Promise((resolve) => setTimeout(resolve, 50));
                    completedTasks++;
                    return i;
                })
            );

            await scheduler.drain();

            expect(completedTasks).toBe(5);
            expect(scheduler.activeCount).toBe(0);
            expect(scheduler.pendingCount).toBe(0);
        });

        it('should handle drain when no pending operations', async () => {
            const scheduler = new EventScheduler(2);

            await expect(scheduler.drain()).resolves.toBeUndefined();

            expect(scheduler.activeCount).toBe(0);
            expect(scheduler.pendingCount).toBe(0);
        });
    });

    describe('Memory Management', () => {
        it('should not leak memory with many completed tasks', async () => {
            const scheduler = new EventScheduler(5);

            for (let batch = 0; batch < 10; batch++) {
                const tasks = Array.from({ length: 100 }, () =>
                    scheduler.schedule(async () => {
                        await new Promise((resolve) => setTimeout(resolve, 1));
                        return Math.random();
                    })
                );

                await Promise.all(tasks);
            }

            expect(scheduler.activeCount).toBe(0);
            expect(scheduler.pendingCount).toBe(0);
        });

        it('should handle rapid scheduling and completion cycles', async () => {
            const scheduler = new EventScheduler(3);
            let completedCount = 0;

            const promises = [];
            for (let i = 0; i < 50; i++) {
                promises.push(
                    scheduler.schedule(async () => {
                        await new Promise((resolve) => setTimeout(resolve, Math.random() * 10));
                        completedCount++;
                        return i;
                    })
                );
            }

            await Promise.all(promises);

            expect(completedCount).toBe(50);
            expect(scheduler.activeCount).toBe(0);
            expect(scheduler.pendingCount).toBe(0);
        });
    });

    describe('Timing and Performance', () => {
        it('should not significantly delay task execution in unlimited mode', async () => {
            const scheduler = new EventScheduler();

            const startTime = Date.now();

            const tasks = Array.from({ length: 100 }, () =>
                scheduler.schedule(async () => {
                    return Date.now();
                })
            );

            const results = await Promise.all(tasks);
            const endTime = Date.now();

            expect(endTime - startTime).toBeLessThan(100);
            expect(results).toHaveLength(100);
        });

        it('should maintain consistent scheduling behavior under load', async () => {
            const scheduler = new EventScheduler(5);
            const taskDurations: number[] = [];

            const tasks = Array.from({ length: 20 }, (_, i) =>
                scheduler.schedule(async () => {
                    const start = Date.now();
                    await new Promise((resolve) => setTimeout(resolve, 100));
                    const duration = Date.now() - start;
                    taskDurations.push(duration);
                    return i;
                })
            );

            await Promise.all(tasks);

            taskDurations.forEach((duration) => {
                expect(duration).toBeGreaterThan(80);
                expect(duration).toBeLessThan(150);
            });
        });
    });
});
