import { describe, it, expect } from 'vitest';
import { SingletonError, SingletonErrorCode } from '../../singleton/singleton-errors';

describe('SingletonError', () => {
    describe('constructor', () => {
        it('sets message, code, key, timestamp, details', () => {
            const err = new SingletonError('test message', SingletonErrorCode.DISPOSED, 'my-key', {
                extra: true,
            });
            expect(err).toBeInstanceOf(Error);
            expect(err).toBeInstanceOf(SingletonError);
            expect(err.name).toBe('SingletonError');
            expect(err.message).toBe('Singleton [my-key]: test message');
            expect(err.code).toBe(SingletonErrorCode.DISPOSED);
            expect(err.key).toBe('my-key');
            expect(err.timestamp).toBeGreaterThan(0);
            expect(err.details).toEqual({ extra: true });
        });

        it('omits key from message when not provided', () => {
            const err = new SingletonError('test', SingletonErrorCode.INVALID_OPERATION);
            expect(err.message).toBe('Singleton: test');
            expect(err.key).toBeUndefined();
        });

        it('has correct prototype chain', () => {
            const err = new SingletonError('test', SingletonErrorCode.DISPOSED);
            expect(err instanceof Error).toBe(true);
            expect(err instanceof SingletonError).toBe(true);
        });
    });

    describe('static factories', () => {
        it('initializationFailed without cause', () => {
            const err = SingletonError.initializationFailed('svc');
            expect(err.code).toBe(SingletonErrorCode.INITIALIZATION_FAILED);
            expect(err.key).toBe('svc');
            expect(err.message).toContain('Initialization failed');
            expect(err.details).toBeUndefined();
        });

        it('initializationFailed with cause', () => {
            const cause = new Error('root');
            const err = SingletonError.initializationFailed('svc', cause);
            expect(err.message).toContain('root');
            expect(err.details).toEqual({ cause: 'root', stack: cause.stack });
        });

        it('alreadyInitialized', () => {
            const err = SingletonError.alreadyInitialized('k');
            expect(err.code).toBe(SingletonErrorCode.ALREADY_INITIALIZED);
            expect(err.key).toBe('k');
            expect(err.message).toContain('already initialized');
        });

        it('notInitialized', () => {
            const err = SingletonError.notInitialized('k');
            expect(err.code).toBe(SingletonErrorCode.NOT_INITIALIZED);
            expect(err.key).toBe('k');
        });

        it('disposed', () => {
            const err = SingletonError.disposed('k');
            expect(err.code).toBe(SingletonErrorCode.DISPOSED);
            expect(err.message).toContain('disposed');
        });

        it('alreadyRegistered', () => {
            const err = SingletonError.alreadyRegistered('dup-key');
            expect(err.code).toBe(SingletonErrorCode.ALREADY_REGISTERED);
            expect(err.key).toBe('dup-key');
            expect(err.message).toContain('already registered');
        });

        it('notFound', () => {
            const err = SingletonError.notFound('missing');
            expect(err.code).toBe(SingletonErrorCode.NOT_FOUND);
            expect(err.key).toBe('missing');
        });

        it('circularDependency formats cycle path', () => {
            const err = SingletonError.circularDependency(['A', 'B', 'C']);
            expect(err.code).toBe(SingletonErrorCode.CIRCULAR_DEPENDENCY);
            expect(err.message).toContain('A -> B -> C');
            expect(err.details).toEqual({ cycle: ['A', 'B', 'C'] });
        });

        it('disposeFailed', () => {
            const cause = new Error('cleanup failed');
            const err = SingletonError.disposeFailed('svc', cause);
            expect(err.code).toBe(SingletonErrorCode.DISPOSE_FAILED);
            expect(err.message).toContain('cleanup failed');
            expect(err.details).toEqual({ cause: 'cleanup failed', stack: cause.stack });
        });

        it('timeout includes timeoutMs in details', () => {
            const err = SingletonError.timeout('svc', 5000);
            expect(err.code).toBe(SingletonErrorCode.TIMEOUT);
            expect(err.message).toContain('5000ms');
            expect(err.details).toEqual({ timeoutMs: 5000 });
        });

        it('maxRetriesExceeded without lastError', () => {
            const err = SingletonError.maxRetriesExceeded('svc', 3);
            expect(err.code).toBe(SingletonErrorCode.MAX_RETRIES_EXCEEDED);
            expect(err.message).toContain('3');
            expect(err.details).toEqual({ retryCount: 3, lastError: undefined });
        });

        it('maxRetriesExceeded with lastError', () => {
            const last = new Error('final');
            const err = SingletonError.maxRetriesExceeded('svc', 3, last);
            expect(err.message).toContain('final');
            expect(err.details).toEqual({ retryCount: 3, lastError: 'final' });
        });

        it('invalidState formats expected states', () => {
            const err = SingletonError.invalidState('svc', 'disposed', ['resolved', 'computing']);
            expect(err.code).toBe(SingletonErrorCode.INVALID_STATE);
            expect(err.message).toContain('resolved, computing');
            expect(err.message).toContain('disposed');
        });

        it('scopeDisposed', () => {
            const err = SingletonError.scopeDisposed('root');
            expect(err.code).toBe(SingletonErrorCode.SCOPE_DISPOSED);
            expect(err.message).toContain('root');
            expect(err.details).toEqual({ scopeId: 'root' });
        });

        it('invalidOperation', () => {
            const err = SingletonError.invalidOperation('cannot do that', 'k');
            expect(err.code).toBe(SingletonErrorCode.INVALID_OPERATION);
            expect(err.message).toBe('Singleton [k]: cannot do that');
            expect(err.key).toBe('k');
        });
    });
});
