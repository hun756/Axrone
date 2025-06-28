import { EventEmitter, EventHandlerError, EventMap } from '../../event/event';

interface TestEvents extends EventMap {
    'test:event': { id: string; data: any };
    'test:async': { delay: number };
    'test:error': { shouldFail: boolean; message?: string };
    'test:async-error': { delay?: number; shouldFail: boolean };
    'test:priority': { level: string };
    'test:batch': { index: number };
    'test:success': { data: string };
    error: Error | EventHandlerError; // Error event'ini düzgün tanımlayalım
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
    describe('EventEmitter - Error Handling Tests', () => {
        let emitter: EventEmitter<TestEvents>;

        afterEach(() => {
            if (emitter) {
                emitter.dispose();
            }
        });

        describe('Synchronous Error Handling', () => {
            it('should handle synchronous handler errors with captureRejections enabled', async () => {
                emitter = new EventEmitter<TestEvents>({ captureRejections: true });
                let errorCaught = false;
                let caughtError: EventHandlerError | null = null;

                emitter.on('error', (error) => {
                    errorCaught = true;
                    if (error instanceof EventHandlerError) {
                        caughtError = error;
                    }
                });

                emitter.on('test:error', (data) => {
                    if (data.shouldFail) {
                        throw new Error(data.message || 'Handler intentionally failed');
                    }
                });

                await emitter.emit('test:error', { shouldFail: true, message: 'Test error' });

                await new Promise((resolve) => setTimeout(resolve, 10));

                expect(errorCaught).toBe(true);
                expect(caughtError).toBeInstanceOf(EventHandlerError);
                expect(caughtError!.eventName).toBe('test:error');
                expect(caughtError!.originalError).toBeInstanceOf(Error);
            });

            it('should propagate errors when captureRejections is disabled', async () => {
                emitter = new EventEmitter<TestEvents>({ captureRejections: false });

                emitter.on('test:error', (data) => {
                    if (data.shouldFail) {
                        throw new Error('Handler error');
                    }
                });

                await expect(emitter.emit('test:error', { shouldFail: true })).rejects.toThrow(
                    EventHandlerError
                );
            });

            it('should handle errors in emitSync with captureRejections enabled', () => {
                emitter = new EventEmitter<TestEvents>({ captureRejections: true });
                let errorCaught = false;

                emitter.on('error', () => {
                    errorCaught = true;
                });

                emitter.on('test:error', (data) => {
                    if (data.shouldFail) {
                        throw new Error('Sync handler error');
                    }
                });

                expect(() => {
                    emitter.emitSync('test:error', { shouldFail: true });
                }).not.toThrow();

                expect(errorCaught).toBe(true);
            });

            it('should propagate errors in emitSync when captureRejections is disabled', () => {
                emitter = new EventEmitter<TestEvents>({ captureRejections: false });

                emitter.on('test:error', (data) => {
                    if (data.shouldFail) {
                        throw new Error('Sync handler error');
                    }
                });

                expect(() => {
                    emitter.emitSync('test:error', { shouldFail: true });
                }).toThrow(EventHandlerError);
            });
        });
    });
});
