import {
    BaseError,
    EventError,
    EventNotFoundError,
    EventQueueFullError,
    EventHandlerError,
} from '../../event/event';

describe('EventEmitter - Error Classes', () => {
    describe('Error Inheritance', () => {
        it('should maintain proper inheritance chain', () => {
            const eventError = new EventError('test');
            const notFoundError = new EventNotFoundError('test-event');
            const queueError = new EventQueueFullError('test-event', 100);
            const handlerError = new EventHandlerError('test-event', new Error('test'));

            expect(eventError instanceof Error).toBe(true);
            expect(eventError instanceof BaseError).toBe(true);
            expect(eventError instanceof EventError).toBe(true);

            expect(notFoundError instanceof Error).toBe(true);
            expect(notFoundError instanceof BaseError).toBe(true);
            expect(notFoundError instanceof EventError).toBe(true);
            expect(notFoundError instanceof EventNotFoundError).toBe(true);

            expect(queueError instanceof EventError).toBe(true);
            expect(handlerError instanceof EventError).toBe(true);
        });

        it('should have correct error names', () => {
            expect(new EventError('test').name).toBe('EventError');
            expect(new EventNotFoundError('test').name).toBe('EventError');
            expect(new EventQueueFullError('test', 100).name).toBe('EventError');
            expect(new EventHandlerError('test', new Error()).name).toBe('EventError');
        });

        it('should support Error.captureStackTrace when available', () => {
            const originalCaptureStackTrace = (Error as any).captureStackTrace;
            let captureStackTraceCalled = false;

            (Error as any).captureStackTrace = () => {
                captureStackTraceCalled = true;
            };

            new EventError('test');
            expect(captureStackTraceCalled).toBe(true);

            (Error as any).captureStackTrace = originalCaptureStackTrace;
        });
    });
});
