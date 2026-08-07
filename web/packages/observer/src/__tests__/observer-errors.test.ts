import { describe, expect, it } from 'vitest';
import {
    BaseObserverError,
    ObserverError,
    SubjectError,
    ObserverNotFoundError,
    SubjectCompletedError,
    SubjectDisposedError,
    MaxObserversExceededError,
    ObserverExecutionError,
    ValidationError,
    ConcurrencyLimitError,
    FilterError,
    TransformError,
} from '@axrone/observer';
import { createObserverId, createSubjectId, createNotificationData } from '@axrone/observer';

describe('Observer Error Classes', () => {
    describe('BaseObserverError', () => {
        it('should have correct name, code, timestamp, and message', () => {
            const error = new BaseObserverError('test message', 'TEST_CODE');
            expect(error).toBeInstanceOf(Error);
            expect(error).toBeInstanceOf(BaseObserverError);
            expect(error.name).toBe('BaseObserverError');
            expect(error.message).toBe('test message');
            expect(error.code).toBe('TEST_CODE');
            expect(typeof error.timestamp).toBe('number');
            expect(error.timestamp).toBeLessThanOrEqual(Date.now());
        });

        it('should default code to OBSERVER_ERROR', () => {
            const error = new BaseObserverError('msg');
            expect(error.code).toBe('OBSERVER_ERROR');
        });

        it('should maintain prototype chain after construction', () => {
            const error = new BaseObserverError('test');
            expect(error instanceof BaseObserverError).toBe(true);
            expect(error instanceof Error).toBe(true);
        });
    });

    describe('ObserverError', () => {
        it('should extend BaseObserverError', () => {
            const error = new ObserverError('observer issue', 'CUSTOM_CODE');
            expect(error).toBeInstanceOf(BaseObserverError);
            expect(error).toBeInstanceOf(ObserverError);
            expect(error).toBeInstanceOf(Error);
            expect(error.name).toBe('ObserverError');
            expect(error.code).toBe('CUSTOM_CODE');
        });
    });

    describe('SubjectError', () => {
        it('should carry subjectId', () => {
            const sid = createSubjectId('TestSubject');
            const error = new SubjectError('subject issue', sid, 'SUBJECT_CUSTOM');
            expect(error).toBeInstanceOf(BaseObserverError);
            expect(error).toBeInstanceOf(SubjectError);
            expect(error.name).toBe('SubjectError');
            expect(error.subjectId).toBe(sid);
            expect(error.code).toBe('SUBJECT_CUSTOM');
        });

        it('should default code to SUBJECT_ERROR', () => {
            const error = new SubjectError('msg');
            expect(error.code).toBe('SUBJECT_ERROR');
            expect(error.subjectId).toBeUndefined();
        });
    });

    describe('ObserverNotFoundError', () => {
        it('should include observerId in message when provided', () => {
            const oid = createObserverId('TestObs');
            const error = new ObserverNotFoundError(oid);
            expect(error).toBeInstanceOf(ObserverError);
            expect(error.name).toBe('ObserverNotFoundError');
            expect(error.observerId).toBe(oid);
            expect(error.code).toBe('OBSERVER_NOT_FOUND');
            expect(error.message).toContain(String(oid));
        });

        it('should handle missing observerId', () => {
            const error = new ObserverNotFoundError();
            expect(error.observerId).toBeUndefined();
            expect(error.message).toBe('Observer not found');
        });
    });

    describe('SubjectCompletedError', () => {
        it('should have SUBJECT_COMPLETED code', () => {
            const sid = createSubjectId('S');
            const error = new SubjectCompletedError(sid);
            expect(error).toBeInstanceOf(SubjectError);
            expect(error.code).toBe('SUBJECT_COMPLETED');
            expect(error.subjectId).toBe(sid);
            expect(error.message).toBe('Cannot operate on completed subject');
        });
    });

    describe('SubjectDisposedError', () => {
        it('should have SUBJECT_DISPOSED code', () => {
            const sid = createSubjectId('S');
            const error = new SubjectDisposedError(sid);
            expect(error).toBeInstanceOf(SubjectError);
            expect(error.code).toBe('SUBJECT_DISPOSED');
            expect(error.subjectId).toBe(sid);
            expect(error.message).toBe('Cannot operate on disposed subject');
        });
    });

    describe('MaxObserversExceededError', () => {
        it('should carry max and current counts', () => {
            const sid = createSubjectId('S');
            const error = new MaxObserversExceededError(10, 10, sid);
            expect(error).toBeInstanceOf(SubjectError);
            expect(error.code).toBe('MAX_OBSERVERS_EXCEEDED');
            expect(error.maxObservers).toBe(10);
            expect(error.currentCount).toBe(10);
            expect(error.subjectId).toBe(sid);
            expect(error.message).toContain('10');
        });
    });

    describe('ObserverExecutionError', () => {
        it('should carry observerId, originalError, notificationData, and cause', () => {
            const oid = createObserverId('O');
            const original = new Error('boom');
            const nd = createNotificationData(createSubjectId('S'), 'update', 'data');
            const error = new ObserverExecutionError(oid, original, nd);
            expect(error).toBeInstanceOf(ObserverError);
            expect(error.name).toBe('ObserverExecutionError');
            expect(error.code).toBe('OBSERVER_EXECUTION_ERROR');
            expect(error.observerId).toBe(oid);
            expect(error.originalError).toBe(original);
            expect(error.notificationData).toBe(nd);
            expect((error as any).cause).toBe(original);
            expect(error.message).toContain('boom');
        });
    });

    describe('ValidationError', () => {
        it('should carry invalidData and subjectId', () => {
            const sid = createSubjectId('S');
            const error = new ValidationError('bad data', { foo: 1 }, sid);
            expect(error).toBeInstanceOf(SubjectError);
            expect(error.name).toBe('ValidationError');
            expect(error.code).toBe('VALIDATION_ERROR');
            expect(error.invalidData).toEqual({ foo: 1 });
            expect(error.subjectId).toBe(sid);
        });
    });

    describe('ConcurrencyLimitError', () => {
        it('should carry limit and current counts', () => {
            const sid = createSubjectId('S');
            const error = new ConcurrencyLimitError(5, 6, sid);
            expect(error).toBeInstanceOf(SubjectError);
            expect(error.code).toBe('CONCURRENCY_LIMIT_ERROR');
            expect(error.limit).toBe(5);
            expect(error.current).toBe(6);
            expect(error.message).toContain('5');
            expect(error.message).toContain('6');
        });
    });

    describe('FilterError', () => {
        it('should carry filterFunction, originalError, and cause', () => {
            const fn = () => true;
            const original = new Error('filter broke');
            const error = new FilterError(original, fn);
            expect(error).toBeInstanceOf(ObserverError);
            expect(error.name).toBe('FilterError');
            expect(error.code).toBe('FILTER_ERROR');
            expect(error.filterFunction).toBe(fn);
            expect(error.originalError).toBe(original);
            expect((error as any).cause).toBe(original);
            expect(error.message).toContain('filter broke');
        });

        it('should normalize non-Error originals', () => {
            const error = new FilterError('string error', () => false);
            expect(error.originalError).toBeInstanceOf(Error);
            expect(error.originalError.message).toBe('string error');
        });
    });

    describe('TransformError', () => {
        it('should carry transformFunction, originalError, inputData, and cause', () => {
            const fn = (x: number) => x * 2;
            const original = new Error('transform broke');
            const error = new TransformError(original, fn, 42);
            expect(error).toBeInstanceOf(ObserverError);
            expect(error.name).toBe('TransformError');
            expect(error.code).toBe('TRANSFORM_ERROR');
            expect(error.transformFunction).toBe(fn);
            expect(error.originalError).toBe(original);
            expect(error.inputData).toBe(42);
            expect((error as any).cause).toBe(original);
            expect(error.message).toContain('transform broke');
        });

        it('should normalize non-Error originals', () => {
            const error = new TransformError('string error', () => 0, null);
            expect(error.originalError).toBeInstanceOf(Error);
            expect(error.originalError.message).toBe('string error');
            expect(error.inputData).toBeNull();
        });
    });

    describe('Error inheritance chains', () => {
        it('ObserverError -> BaseObserverError -> Error', () => {
            const error = new ObserverError('test');
            expect(error instanceof ObserverError).toBe(true);
            expect(error instanceof BaseObserverError).toBe(true);
            expect(error instanceof Error).toBe(true);
        });

        it('SubjectCompletedError -> SubjectError -> BaseObserverError -> Error', () => {
            const error = new SubjectCompletedError();
            expect(error instanceof SubjectCompletedError).toBe(true);
            expect(error instanceof SubjectError).toBe(true);
            expect(error instanceof BaseObserverError).toBe(true);
            expect(error instanceof Error).toBe(true);
        });

        it('ObserverExecutionError -> ObserverError -> BaseObserverError -> Error', () => {
            const nd = createNotificationData(createSubjectId(), 'update', null);
            const error = new ObserverExecutionError(createObserverId(), new Error('x'), nd);
            expect(error instanceof ObserverExecutionError).toBe(true);
            expect(error instanceof ObserverError).toBe(true);
            expect(error instanceof BaseObserverError).toBe(true);
            expect(error instanceof Error).toBe(true);
        });
    });
});
