import {
    GameLoopError,
    GameLoopConfigurationError,
    GameLoopDisposedError,
    GameLoopSchedulerError,
    GameLoopSnapshotError,
    GameLoopSystemError,
} from '@axrone/game-loop';

describe('GameLoop Error Classes', () => {
    describe('GameLoopError (base)', () => {
        it('extends Error with correct name, code, and message', () => {
            const error = new GameLoopError('GameLoopError', 'loop.disposed', 'loop disposed');

            expect(error).toBeInstanceOf(Error);
            expect(error).toBeInstanceOf(GameLoopError);
            expect(error.name).toBe('GameLoopError');
            expect(error.code).toBe('loop.disposed');
            expect(error.message).toBe('loop disposed');
        });

        it('propagates ErrorOptions cause', () => {
            const cause = new TypeError('root cause');
            const error = new GameLoopError('GameLoopError', 'loop.disposed', 'failed', {
                cause,
            });

            expect(error.cause).toBe(cause);
        });

        it('supports Error.captureStackTrace when available', () => {
            const original = (Error as Error & { captureStackTrace?: Function }).captureStackTrace;
            let captured = false;

            (Error as Error & { captureStackTrace?: Function }).captureStackTrace = (
                target: object
            ) => {
                captured = true;
                expect(target).toBeInstanceOf(GameLoopError);
            };

            try {
                new GameLoopError('GameLoopError', 'loop.disposed', 'test');
                expect(captured).toBe(true);
            } finally {
                (Error as Error & { captureStackTrace?: Function }).captureStackTrace = original;
            }
        });

        it('does not throw when captureStackTrace is unavailable', () => {
            const original = (Error as Error & { captureStackTrace?: Function }).captureStackTrace;
            (Error as Error & { captureStackTrace?: Function }).captureStackTrace = undefined;

            try {
                expect(() => new GameLoopError('GameLoopError', 'loop.disposed', 'test')).not.toThrow();
            } finally {
                (Error as Error & { captureStackTrace?: Function }).captureStackTrace = original;
            }
        });
    });

    describe('GameLoopConfigurationError', () => {
        it('extends GameLoopError with validation code', () => {
            const error = new GameLoopConfigurationError(
                'loop.invalid-fixed-delta',
                'fixedDelta must be positive'
            );

            expect(error).toBeInstanceOf(Error);
            expect(error).toBeInstanceOf(GameLoopError);
            expect(error).toBeInstanceOf(GameLoopConfigurationError);
            expect(error.name).toBe('GameLoopConfigurationError');
            expect(error.code).toBe('loop.invalid-fixed-delta');
            expect(error.message).toBe('fixedDelta must be positive');
        });
    });

    describe('GameLoopDisposedError', () => {
        it('always uses loop.disposed code', () => {
            const error = new GameLoopDisposedError('loop is disposed');

            expect(error).toBeInstanceOf(GameLoopError);
            expect(error).toBeInstanceOf(GameLoopDisposedError);
            expect(error.name).toBe('GameLoopDisposedError');
            expect(error.code).toBe('loop.disposed');
            expect(error.message).toBe('loop is disposed');
        });
    });

    describe('GameLoopSchedulerError', () => {
        it('stores request-failed code', () => {
            const error = new GameLoopSchedulerError(
                'loop.scheduler.request-failed',
                'request failed'
            );

            expect(error).toBeInstanceOf(GameLoopError);
            expect(error).toBeInstanceOf(GameLoopSchedulerError);
            expect(error.name).toBe('GameLoopSchedulerError');
            expect(error.code).toBe('loop.scheduler.request-failed');
        });

        it('stores cancel-failed code', () => {
            const error = new GameLoopSchedulerError(
                'loop.scheduler.cancel-failed',
                'cancel failed'
            );

            expect(error.code).toBe('loop.scheduler.cancel-failed');
        });
    });

    describe('GameLoopSnapshotError', () => {
        it('always uses loop.snapshot.invalid code', () => {
            const error = new GameLoopSnapshotError('invalid snapshot');

            expect(error).toBeInstanceOf(GameLoopError);
            expect(error).toBeInstanceOf(GameLoopSnapshotError);
            expect(error.name).toBe('GameLoopSnapshotError');
            expect(error.code).toBe('loop.snapshot.invalid');
            expect(error.message).toBe('invalid snapshot');
        });
    });

    describe('GameLoopSystemError', () => {
        it('stores systemId, phase, and attempt', () => {
            const error = new GameLoopSystemError(
                'system failed',
                'physics',
                'update',
                3
            );

            expect(error).toBeInstanceOf(GameLoopError);
            expect(error).toBeInstanceOf(GameLoopSystemError);
            expect(error.name).toBe('GameLoopSystemError');
            expect(error.code).toBe('loop.system.failed');
            expect(error.systemId).toBe('physics');
            expect(error.phase).toBe('update');
            expect(error.attempt).toBe(3);
            expect(error.message).toBe('system failed');
        });

        it('propagates cause through ErrorOptions', () => {
            const original = new RangeError('out of range');
            const error = new GameLoopSystemError('failed', 'sys', 'render', 1, {
                cause: original,
            });

            expect(error.cause).toBe(original);
        });

        it('accepts dispose as phase', () => {
            const error = new GameLoopSystemError('dispose failed', 'sys', 'dispose', 1);

            expect(error.phase).toBe('dispose');
        });
    });

    describe('prototype chain integrity', () => {
        it('all subclasses pass instanceof checks through the full chain', () => {
            const errors = [
                new GameLoopConfigurationError('loop.invalid-fixed-delta', 'msg'),
                new GameLoopDisposedError('msg'),
                new GameLoopSchedulerError('loop.scheduler.request-failed', 'msg'),
                new GameLoopSnapshotError('msg'),
                new GameLoopSystemError('msg', 'sys', 'update', 1),
            ];

            for (const error of errors) {
                expect(error).toBeInstanceOf(Error);
                expect(error).toBeInstanceOf(GameLoopError);
            }
        });
    });
});
