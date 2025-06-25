import {
    Subscription,
    SubscriptionOptions,
    EventMetrics,
    QueuedEvent,
    EventCallback,
    EventPriority,
} from '../../event/event';

describe('EventEmitter - Subscription Interfaces', () => {
    it('should support proper subscription lifecycle', () => {
        let executionCount = 0;
        const callback: EventCallback<string> = () => {
            executionCount++;
        };

        const subscription: Subscription<string> = {
            id: Symbol('test'),
            event: 'test:event',
            callback,
            once: false,
            priority: 'normal',
            createdAt: Date.now(),
            executionCount: 0,
        };

        subscription.callback('test data');
        subscription.executionCount++;
        subscription.lastExecuted = Date.now();

        expect(executionCount).toBe(1);
        expect(subscription.executionCount).toBe(1);
        expect(subscription.lastExecuted).toBeDefined();
        expect(typeof subscription.lastExecuted).toBe('number');
    });

    it('should handle async callbacks correctly', async () => {
        let resolved = false;
        const asyncCallback: EventCallback<number> = async (data) => {
            await new Promise((resolve) => setTimeout(resolve, 1));
            resolved = true;
            return;
        };

        const subscription: Subscription<number> = {
            id: Symbol('async-test'),
            event: 'async:event',
            callback: asyncCallback,
            once: true,
            priority: 'high',
            createdAt: Date.now(),
            executionCount: 0,
        };

        const result = subscription.callback(42);
        expect(result).toBeInstanceOf(Promise);

        await result;
        expect(resolved).toBe(true);
    });

    it('should maintain immutable properties', () => {
        const subscription: Subscription = {
            id: Symbol('immutable'),
            event: 'test',
            callback: () => {},
            once: false,
            priority: 'normal',
            createdAt: Date.now(),
            executionCount: 0,
        };

        // TypeScript should prevent these assignments:
        // subscription.id = Symbol('new'); // ❌
        // subscription.event = 'new'; // ❌
        // subscription.callback = () => {}; // ❌

        // But allow mutable properties:
        subscription.executionCount = 5;
        subscription.lastExecuted = Date.now();

        expect(subscription.executionCount).toBe(5);
        expect(subscription.lastExecuted).toBeDefined();
    });

    describe('SubscriptionOptions', () => {
        it('should support all valid option combinations', () => {
            const validOptions: SubscriptionOptions[] = [
                {},
                { once: true },
                { priority: 'high' },
                { once: false, priority: 'low' },
                { once: true, priority: 'normal' },
            ];

            validOptions.forEach((options) => {
                expect(typeof options).toBe('object');

                if ('once' in options) {
                    expect(typeof options.once).toBe('boolean');
                }

                if ('priority' in options) {
                    expect(['high', 'normal', 'low']).toContain(options.priority);
                }
            });
        });

        it('should work with partial options merging', () => {
            const defaultOptions = { once: false, priority: 'normal' as EventPriority };
            const userOptions: SubscriptionOptions = { priority: 'high' };

            const mergedOptions = { ...defaultOptions, ...userOptions };

            expect(mergedOptions.once).toBe(false); // From default
            expect(mergedOptions.priority).toBe('high'); // From user
        });
    });

    describe('QueuedEvent Interface', () => {
        it('should support priority-based sorting', () => {
            const events: QueuedEvent[] = [
                { id: 1, event: 'low1', data: {}, timestamp: 1000, priority: 'low' },
                { id: 2, event: 'high1', data: {}, timestamp: 2000, priority: 'high' },
                { id: 3, event: 'normal1', data: {}, timestamp: 1500, priority: 'normal' },
                { id: 4, event: 'high2', data: {}, timestamp: 1200, priority: 'high' },
            ];

            const priorityValues = { high: 0, normal: 1, low: 2 };

            const sorted = events.sort((a, b) => {
                const priorityDiff = priorityValues[a.priority] - priorityValues[b.priority];
                if (priorityDiff !== 0) return priorityDiff;
                return a.timestamp - b.timestamp;
            });

            expect(sorted[0].event).toBe('high2');
            expect(sorted[1].event).toBe('high1');
            expect(sorted[2].event).toBe('normal1');
            expect(sorted[3].event).toBe('low1');
        });

        it('should handle large data payloads', () => {
            const largeData = {
                users: new Array(1000).fill(0).map((_, i) => ({ id: i, name: `User${i}` })),
                metadata: { source: 'bulk-import', processed: Date.now() },
            };

            const queuedEvent: QueuedEvent = {
                id: 999,
                event: 'bulk:import',
                data: largeData,
                timestamp: Date.now(),
                priority: 'normal',
            };

            expect(queuedEvent.data.users).toHaveLength(1000);
            expect(queuedEvent.data.metadata.source).toBe('bulk-import');
        });

        it('should maintain event ordering within same priority', () => {
            const events: QueuedEvent[] = [
                { id: 1, event: 'order1', data: {}, timestamp: 1000, priority: 'normal' },
                { id: 2, event: 'order2', data: {}, timestamp: 1001, priority: 'normal' },
                { id: 3, event: 'order3', data: {}, timestamp: 1002, priority: 'normal' },
            ];

            const sorted = events.sort((a, b) => a.timestamp - b.timestamp);

            expect(sorted.map((e) => e.event)).toEqual(['order1', 'order2', 'order3']);
        });
    });
});
