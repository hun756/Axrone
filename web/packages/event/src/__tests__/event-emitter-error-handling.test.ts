import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { EventEmitter, EventHandlerError, type EventMap } from '@axrone/event';

interface TestEvents extends EventMap {
    'test:event': { value: number };
    'test:error': { shouldFail: boolean; message?: string };
    'test:async-error': { delay?: number; shouldFail: boolean };
    error: Error | EventHandlerError;
}

describe('EventEmitter - Error Handling', () => {
    describe('Synchronous Error Handling with captureRejections', () => {
        let emitter: EventEmitter<TestEvents>;

        afterEach(() => {
            emitter?.dispose();
        });

        it('should route sync handler errors to error event when captureRejections is true', async () => {
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

            await new Promise((resolve) => setTimeout(resolve, 50));

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

            await expect(
                emitter.emit('test:error', { shouldFail: true })
            ).rejects.toThrow(EventHandlerError);
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

    describe('Asynchronous Error Handling', () => {
        let emitter: EventEmitter<TestEvents>;

        afterEach(() => {
            emitter?.dispose();
        });

        it('should route async handler errors to error event with captureRejections enabled', async () => {
            emitter = new EventEmitter<TestEvents>({ captureRejections: true });
            let errorHandled = false;
            let caughtError: EventHandlerError | null = null;

            emitter.on('error', (error) => {
                errorHandled = true;
                if (error instanceof EventHandlerError) {
                    caughtError = error;
                }
            });

            emitter.on('test:async-error', async (data) => {
                await new Promise((resolve) => setTimeout(resolve, data.delay || 10));
                if (data.shouldFail) {
                    throw new Error('Async handler error');
                }
            });

            await emitter.emit('test:async-error', { shouldFail: true, delay: 10 });

            await new Promise((resolve) => setTimeout(resolve, 50));

            expect(errorHandled).toBe(true);
            expect(caughtError).toBeInstanceOf(EventHandlerError);
            expect(caughtError!.eventName).toBe('test:async-error');
        });

        it('should propagate async errors when captureRejections is disabled', async () => {
            emitter = new EventEmitter<TestEvents>({ captureRejections: false });

            emitter.on('test:async-error', async (data) => {
                await new Promise((resolve) => setTimeout(resolve, 10));
                if (data.shouldFail) {
                    throw new Error('Async handler error');
                }
            });

            await expect(
                emitter.emit('test:async-error', { shouldFail: true })
            ).rejects.toThrow(EventHandlerError);
        });

        it('should handle Promise rejection in async handlers', async () => {
            emitter = new EventEmitter<TestEvents>({ captureRejections: true });
            let errorHandled = false;

            emitter.on('error', () => {
                errorHandled = true;
            });

            emitter.on('test:async-error', () => {
                return Promise.reject(new Error('Promise rejection'));
            });

            await emitter.emit('test:async-error', { shouldFail: true });

            await new Promise((resolve) => setTimeout(resolve, 50));

            expect(errorHandled).toBe(true);
        });

        it('should warn when emitSync encounters async callbacks', async () => {
            emitter = new EventEmitter<TestEvents>({ captureRejections: true });
            const consoleSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
            let errorHandled = false;

            emitter.on('error', () => {
                errorHandled = true;
            });

            emitter.on('test:async-error', async (data) => {
                await new Promise((resolve) => setTimeout(resolve, 10));
                if (data.shouldFail) {
                    throw new Error('Async error in emitSync');
                }
            });

            emitter.emitSync('test:async-error', { shouldFail: true });

            expect(consoleSpy).toHaveBeenCalledWith(
                expect.stringContaining('emitted synchronously but had async listeners')
            );

            await new Promise((resolve) => setTimeout(resolve, 50));
            expect(errorHandled).toBe(true);

            consoleSpy.mockRestore();
        });
    });

    describe('Multiple Handlers Error Scenarios', () => {
        let emitter: EventEmitter<TestEvents>;

        afterEach(() => {
            emitter?.dispose();
        });

        it('should handle errors from multiple handlers independently', async () => {
            emitter = new EventEmitter<TestEvents>({ captureRejections: true });
            let errorCount = 0;
            const errors: EventHandlerError[] = [];

            emitter.on('error', (error) => {
                errorCount++;
                if (error instanceof EventHandlerError) {
                    errors.push(error);
                }
            });

            emitter.on('test:error', () => {
                throw new Error('Handler 1 error');
            });

            emitter.on('test:error', () => {
                // Successful handler - no throw
            });

            emitter.on('test:error', () => {
                throw new Error('Handler 3 error');
            });

            await emitter.emit('test:error', { shouldFail: true });

            await new Promise((resolve) => setTimeout(resolve, 50));

            expect(errorCount).toBe(2);
            expect(errors).toHaveLength(2);
            expect(errors.every((e) => e instanceof EventHandlerError)).toBe(true);
        });

        it('should handle mixed sync and async handler errors', async () => {
            emitter = new EventEmitter<TestEvents>({ captureRejections: true });
            let errorCount = 0;

            emitter.on('error', () => {
                errorCount++;
            });

            emitter.on('test:error', () => {
                throw new Error('Sync error');
            });

            emitter.on('test:error', async () => {
                await new Promise((resolve) => setTimeout(resolve, 10));
                throw new Error('Async error');
            });

            await emitter.emit('test:error', { shouldFail: true });

            await new Promise((resolve) => setTimeout(resolve, 50));

            expect(errorCount).toBe(2);
        });
    });

    describe('Error Event Re-throw Paths', () => {
        let emitter: EventEmitter<TestEvents>;

        afterEach(() => {
            emitter?.dispose();
        });

        it('should re-throw when error event itself fails with captureRejections async', async () => {
            emitter = new EventEmitter<TestEvents>({ captureRejections: true });

            emitter.on('error', () => {
                throw new Error('Error handler itself failed');
            });

            emitter.on('test:error', () => {
                throw new Error('Original error');
            });

            await expect(
                emitter.emit('test:error', { shouldFail: true })
            ).rejects.toThrow();
        });

        it('should re-throw when error event itself fails with captureRejections sync', () => {
            emitter = new EventEmitter<TestEvents>({ captureRejections: true });

            emitter.on('error', () => {
                throw new Error('Error handler itself failed');
            });

            emitter.on('test:error', () => {
                throw new Error('Original error');
            });

            expect(() => {
                emitter.emitSync('test:error', { shouldFail: true });
            }).toThrow();
        });

        it('should re-throw when no error listener exists with captureRejections async', async () => {
            emitter = new EventEmitter<TestEvents>({ captureRejections: true });

            emitter.on('test:error', () => {
                throw new Error('No error listener');
            });

            await expect(
                emitter.emit('test:error', { shouldFail: true })
            ).rejects.toThrow(EventHandlerError);
        });

        it('should re-throw when no error listener exists with captureRejections sync', () => {
            emitter = new EventEmitter<TestEvents>({ captureRejections: true });

            emitter.on('test:error', () => {
                throw new Error('No error listener');
            });

            expect(() => {
                emitter.emitSync('test:error', { shouldFail: true });
            }).toThrow(EventHandlerError);
        });
    });

    describe('Error Metric Tracking', () => {
        let emitter: EventEmitter<TestEvents>;

        afterEach(() => {
            emitter?.dispose();
        });

        it('should track error metrics for sync handler failures with captureRejections', async () => {
            emitter = new EventEmitter<TestEvents>({ captureRejections: true });

            emitter.on('error', () => {});

            emitter.on('test:error', () => {
                throw new Error('Test error');
            });

            emitter.on('test:error', () => {
                // Successful handler
            });

            await emitter.emit('test:error', { shouldFail: true });

            const metrics = emitter.getMetrics('test:error');

            expect(metrics.execution.count).toBe(2);
            expect(metrics.execution.errors).toBe(1);
        });

        it('should track error metrics for async handler failures', async () => {
            emitter = new EventEmitter<TestEvents>({ captureRejections: true });

            emitter.on('error', () => {});

            emitter.on('test:async-error', async () => {
                await new Promise((resolve) => setTimeout(resolve, 5));
                throw new Error('Async failure');
            });

            emitter.on('test:async-error', async () => {
                await new Promise((resolve) => setTimeout(resolve, 5));
                // Successful async handler
            });

            await emitter.emit('test:async-error', { shouldFail: true });

            await new Promise((resolve) => setTimeout(resolve, 50));

            const metrics = emitter.getMetrics('test:async-error');

            expect(metrics.execution.count).toBe(2);
            expect(metrics.execution.errors).toBe(1);
        });
    });

    describe('Non-Error Object Rejection Handling', () => {
        let emitter: EventEmitter<TestEvents>;

        afterEach(() => {
            emitter?.dispose();
        });

        it('should wrap string rejections in EventHandlerError', async () => {
            emitter = new EventEmitter<TestEvents>({ captureRejections: false });

            emitter.on('test:error', () => {
                throw 'string error';
            });

            try {
                await emitter.emit('test:error', { shouldFail: true });
                expect.unreachable('Should have thrown');
            } catch (error) {
                expect(error).toBeInstanceOf(EventHandlerError);
                const handlerError = error as EventHandlerError;
                expect(handlerError.originalError).toBe('string error');
            }
        });

        it('should wrap non-Error sync throws in EventHandlerError with correct message', () => {
            emitter = new EventEmitter<TestEvents>({ captureRejections: false });

            emitter.on('test:error', () => {
                throw 42;
            });

            try {
                emitter.emitSync('test:error', { shouldFail: true });
                expect.unreachable('Should have thrown');
            } catch (error) {
                expect(error).toBeInstanceOf(EventHandlerError);
                const handlerError = error as EventHandlerError;
                expect(handlerError.message).toContain('42');
            }
        });
    });
});
