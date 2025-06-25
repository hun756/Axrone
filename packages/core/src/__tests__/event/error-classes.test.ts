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

describe('EventNotFoundError', () => {
    it('should store event name and generate correct message', () => {
        const error = new EventNotFoundError('user:login');

        expect(error.eventName).toBe('user:login');
        expect(error.message).toBe('Event "user:login" not found');
    });

    it('should handle special characters in event names', () => {
        const specialEventName = 'user:login@domain.com#123';
        const error = new EventNotFoundError(specialEventName);

        expect(error.eventName).toBe(specialEventName);
        expect(error.message).toContain(specialEventName);
    });
});

describe('EventQueueFullError', () => {
    it('should store event name and buffer size', () => {
        const error = new EventQueueFullError('high-priority', 1000);

        expect(error.eventName).toBe('high-priority');
        expect(error.message).toBe('Event queue for "high-priority" is full (1000 items)');
    });

    it('should handle edge case buffer sizes', () => {
        const errorZero = new EventQueueFullError('test', 0);
        const errorLarge = new EventQueueFullError('test', Number.MAX_SAFE_INTEGER);

        expect(errorZero.message).toContain('(0 items)');
        expect(errorLarge.message).toContain(`(${Number.MAX_SAFE_INTEGER} items)`);
    });
});

describe('EventHandlerError', () => {
    it('should wrap Error objects correctly', () => {
        const originalError = new TypeError('Invalid argument');
        const error = new EventHandlerError('user:update', originalError);

        expect(error.eventName).toBe('user:update');
        expect(error.originalError).toBe(originalError);
        expect(error.message).toBe('Handler error for "user:update": Invalid argument');
    });

    it('should handle non-Error objects', () => {
        const primitiveError = 'String error';
        const objectError = { code: 500, message: 'Server error' };
        const nullError = null;

        expect(new EventHandlerError('test', primitiveError).message).toContain('String error');
        expect(new EventHandlerError('test', objectError).message).toContain('[object Object]');
        expect(new EventHandlerError('test', nullError).message).toContain('null');
    });

    it('should chain stack traces when original error has stack', () => {
        const originalError = new Error('Original error');
        const error = new EventHandlerError('test-event', originalError);

        expect(error.stack).toContain('Caused by:');
        expect(error.stack).toContain(originalError.stack);
    });

    it('should handle errors without stack traces', () => {
        const errorWithoutStack = new Error('No stack');
        delete errorWithoutStack.stack;

        const error = new EventHandlerError('test', errorWithoutStack);
        expect(error.stack).not.toContain('Caused by:');
    });
});
