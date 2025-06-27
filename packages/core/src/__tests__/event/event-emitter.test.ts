import { EventEmitter, EventHandlerError, EventMap } from '../../event/event';

interface TestEvents extends EventMap {
    'test:event': { id: string; data: any };
    'test:async': { delay: number };
    'test:error': { shouldFail: boolean };
    'test:priority': { level: string };
    'test:batch': { index: number };
}

describe('EventEmitter - Main Implementation', () => {
    describe('Core Event Operations', () => {
        let emitter: EventEmitter<TestEvents>;

        beforeEach(() => {
            emitter = new EventEmitter<TestEvents>();
        });

        afterEach(() => {
            emitter.dispose();
        });

        it('should handle basic subscription and emission workflow', async () => {
            let callbackExecuted = false;
            let receivedData: any = null;

            const unsubscribe = emitter.on('test:event', (data) => {
                callbackExecuted = true;
                receivedData = data;
            });

            const testData = { id: 'test1', data: { value: 42 } };
            const result = await emitter.emit('test:event', testData);

            expect(result).toBe(true);
            expect(callbackExecuted).toBe(true);
            expect(receivedData).toEqual(testData);

            unsubscribe();
        });

        it('should handle once subscriptions correctly', async () => {
            let executionCount = 0;

            emitter.once('test:event', () => {
                executionCount++;
            });

            await emitter.emit('test:event', { id: 'test1', data: {} });
            expect(executionCount).toBe(1);

            await emitter.emit('test:event', { id: 'test2', data: {} });
            expect(executionCount).toBe(1);

            expect(emitter.listenerCount('test:event')).toBe(0);
        });

        it('should handle async and sync callbacks correctly', async () => {
            const executionOrder: string[] = [];

            emitter.on('test:async', async (data) => {
                await new Promise((resolve) => setTimeout(resolve, data.delay));
                executionOrder.push('async');
            });

            emitter.on('test:async', (data) => {
                executionOrder.push('sync');
            });

            await emitter.emit('test:async', { delay: 50 });

            expect(executionOrder).toContain('async');
            expect(executionOrder).toContain('sync');
        });

        it('should return false when no listeners exist', async () => {
            const result = await emitter.emit('test:event', { id: 'test', data: {} });
            expect(result).toBe(false);
        });

        it('should handle subscription removal correctly', () => {
            const callback1 = jest.fn();
            const callback2 = jest.fn();

            emitter.on('test:event', callback1);
            emitter.on('test:event', callback2);

            expect(emitter.listenerCount('test:event')).toBe(2);

            const removed = emitter.off('test:event', callback1);
            expect(removed).toBe(true);
            expect(emitter.listenerCount('test:event')).toBe(1);

            const removedAll = emitter.off('test:event');
            expect(removedAll).toBe(true);
            expect(emitter.listenerCount('test:event')).toBe(0);
        });
    });

    // PRIORITY SYSTEM TESTS
    describe('Priority System', () => {
        let emitter: EventEmitter<TestEvents>;

        beforeEach(() => {
            emitter = new EventEmitter<TestEvents>({ concurrencyLimit: 1 });
        });

        afterEach(() => {
            emitter.dispose();
        });

        it('should execute handlers in priority order', async () => {
            const executionOrder: string[] = [];

            emitter.on(
                'test:priority',
                () => {
                    executionOrder.push('low');
                },
                { priority: 'low' }
            );

            emitter.on(
                'test:priority',
                () => {
                    executionOrder.push('high');
                },
                { priority: 'high' }
            );

            emitter.on(
                'test:priority',
                () => {
                    executionOrder.push('normal');
                },
                { priority: 'normal' }
            );

            await emitter.emit('test:priority', { level: 'test' });

            expect(executionOrder).toEqual(['high', 'normal', 'low']);
        });

        it('should handle priority-based emission options', async () => {
            emitter.pause();

            const events = [
                emitter.emit('test:priority', { level: 'low' }, { priority: 'low' }),
                emitter.emit('test:priority', { level: 'high' }, { priority: 'high' }),
                emitter.emit('test:priority', { level: 'normal' }, { priority: 'normal' }),
            ];

            const queuedEvents = emitter.getQueuedEvents();
            expect(queuedEvents).toHaveLength(3);

            expect(queuedEvents[0].priority).toBe('high');
            expect(queuedEvents[1].priority).toBe('normal');
            expect(queuedEvents[2].priority).toBe('low');

            emitter.resume();
            await Promise.all(events);
        });
    });

    // ERROR HANDLING TESTS
    describe('Error Handling', () => {
        it('should handle handler errors with captureRejections enabled', async () => {
            const emitter = new EventEmitter<TestEvents>({ captureRejections: true });
            let errorCaught = false;

            emitter.on('error' as any, (error: Error) => {
                errorCaught = true;
                expect(error).toBeInstanceOf(EventHandlerError);
            });

            emitter.on('test:error', (data) => {
                if (data.shouldFail) {
                    throw new Error('Handler intentionally failed');
                }
            });

            await emitter.emit('test:error', { shouldFail: true });
            expect(errorCaught).toBe(true);

            emitter.dispose();
        });

        it('should propagate errors when captureRejections is disabled', async () => {
            const emitter = new EventEmitter<TestEvents>({ captureRejections: false });

            emitter.on('test:error', () => {
                throw new Error('Handler error');
            });

            await expect(emitter.emit('test:error', { shouldFail: true })).rejects.toThrow(
                EventHandlerError
            );

            emitter.dispose();
        });

        it('should handle async handler errors correctly', async () => {
            const emitter = new EventEmitter<TestEvents>({ captureRejections: true });
            let errorHandled = false;

            emitter.on('error' as any, () => {
                errorHandled = true;
            });

            emitter.on('test:error', async () => {
                await new Promise((resolve) => setTimeout(resolve, 10));
                throw new Error('Async handler error');
            });

            await emitter.emit('test:error', { shouldFail: true });
            expect(errorHandled).toBe(true);

            emitter.dispose();
        });
    });
});
