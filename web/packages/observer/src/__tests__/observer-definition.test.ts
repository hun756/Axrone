import { describe, expect, it } from 'vitest';
import {
    createObserverId,
    createSubjectId,
    createNotificationData,
    isValidObserver,
    isValidPriority,
    isValidNotificationType,
    mergeObserverOptions,
    mergeSubjectOptions,
    normalizeObserverOptions,
    normalizeSubjectOptions,
    DEFAULT_OBSERVER_OPTIONS,
    DEFAULT_SUBJECT_OPTIONS,
    PRIORITY_VALUES,
    OBSERVER_MEMORY_SYMBOLS,
} from '@axrone/observer';

describe('Observer Definition Utilities', () => {
    describe('createObserverId', () => {
        it('should return a symbol', () => {
            const id = createObserverId();
            expect(typeof id).toBe('symbol');
        });

        it('should return unique symbols on each call', () => {
            const a = createObserverId();
            const b = createObserverId();
            expect(a).not.toBe(b);
        });

        it('should accept a custom description', () => {
            const id = createObserverId('MyObserver');
            expect(id.description).toBe('MyObserver');
        });

        it('should default description to "Observer"', () => {
            const id = createObserverId();
            expect(id.description).toBe('Observer');
        });
    });

    describe('createSubjectId', () => {
        it('should return a symbol', () => {
            const id = createSubjectId();
            expect(typeof id).toBe('symbol');
        });

        it('should return unique symbols on each call', () => {
            const a = createSubjectId();
            const b = createSubjectId();
            expect(a).not.toBe(b);
        });

        it('should accept a custom description', () => {
            const id = createSubjectId('MySubject');
            expect(id.description).toBe('MySubject');
        });

        it('should default description to "Subject"', () => {
            const id = createSubjectId();
            expect(id.description).toBe('Subject');
        });
    });

    describe('createNotificationData', () => {
        it('should create notification with all fields', () => {
            const sid = createSubjectId('S');
            const nd = createNotificationData(sid, 'update', 'hello', 12345);
            expect(nd.source).toBe(sid);
            expect(nd.type).toBe('update');
            expect(nd.data).toBe('hello');
            expect(nd.timestamp).toBe(12345);
        });

        it('should default timestamp to Date.now()', () => {
            const before = Date.now();
            const nd = createNotificationData(createSubjectId(), 'update', null);
            const after = Date.now();
            expect(nd.timestamp).toBeGreaterThanOrEqual(before);
            expect(nd.timestamp).toBeLessThanOrEqual(after);
        });

        it('should support all notification types', () => {
            const sid = createSubjectId();
            expect(createNotificationData(sid, 'complete', null).type).toBe('complete');
            expect(createNotificationData(sid, 'error', null).type).toBe('error');
            expect(createNotificationData(sid, 'lifecycle', null).type).toBe('lifecycle');
        });
    });

    describe('isValidObserver', () => {
        it('should return true for functions', () => {
            expect(isValidObserver(() => {})).toBe(true);
            expect(isValidObserver(function test() {})).toBe(true);
            expect(isValidObserver(async () => {})).toBe(true);
        });

        it('should return false for non-functions', () => {
            expect(isValidObserver(null)).toBe(false);
            expect(isValidObserver(undefined)).toBe(false);
            expect(isValidObserver(42)).toBe(false);
            expect(isValidObserver('string')).toBe(false);
            expect(isValidObserver({})).toBe(false);
            expect(isValidObserver([])).toBe(false);
        });
    });

    describe('isValidPriority', () => {
        it('should return true for valid priorities', () => {
            expect(isValidPriority('high')).toBe(true);
            expect(isValidPriority('normal')).toBe(true);
            expect(isValidPriority('low')).toBe(true);
        });

        it('should return false for invalid priorities', () => {
            expect(isValidPriority('urgent')).toBe(false);
            expect(isValidPriority('')).toBe(false);
            expect(isValidPriority(null)).toBe(false);
            expect(isValidPriority(0)).toBe(false);
            expect(isValidPriority(undefined)).toBe(false);
        });
    });

    describe('isValidNotificationType', () => {
        it('should return true for valid types', () => {
            expect(isValidNotificationType('update')).toBe(true);
            expect(isValidNotificationType('complete')).toBe(true);
            expect(isValidNotificationType('error')).toBe(true);
            expect(isValidNotificationType('lifecycle')).toBe(true);
        });

        it('should return false for invalid types', () => {
            expect(isValidNotificationType('invalid')).toBe(false);
            expect(isValidNotificationType('')).toBe(false);
            expect(isValidNotificationType(null)).toBe(false);
            expect(isValidNotificationType(0)).toBe(false);
        });
    });

    describe('DEFAULT_OBSERVER_OPTIONS', () => {
        it('should be frozen', () => {
            expect(Object.isFrozen(DEFAULT_OBSERVER_OPTIONS)).toBe(true);
        });

        it('should have correct default values', () => {
            expect(DEFAULT_OBSERVER_OPTIONS.priority).toBe('normal');
            expect(DEFAULT_OBSERVER_OPTIONS.once).toBe(false);
            expect(DEFAULT_OBSERVER_OPTIONS.debounceMs).toBe(0);
            expect(DEFAULT_OBSERVER_OPTIONS.throttleMs).toBe(0);
            expect(DEFAULT_OBSERVER_OPTIONS.weakReference).toBe(false);
            expect(DEFAULT_OBSERVER_OPTIONS.errorHandling).toBe('throw');
            expect(DEFAULT_OBSERVER_OPTIONS.buffering.enabled).toBe(false);
            expect(DEFAULT_OBSERVER_OPTIONS.buffering.maxSize).toBe(100);
            expect(DEFAULT_OBSERVER_OPTIONS.buffering.flushIntervalMs).toBe(1000);
            expect(DEFAULT_OBSERVER_OPTIONS.replay.enabled).toBe(false);
            expect(DEFAULT_OBSERVER_OPTIONS.replay.bufferSize).toBe(10);
        });

        it('should have frozen nested objects', () => {
            expect(Object.isFrozen(DEFAULT_OBSERVER_OPTIONS.buffering)).toBe(true);
            expect(Object.isFrozen(DEFAULT_OBSERVER_OPTIONS.replay)).toBe(true);
        });
    });

    describe('DEFAULT_SUBJECT_OPTIONS', () => {
        it('should be frozen', () => {
            expect(Object.isFrozen(DEFAULT_SUBJECT_OPTIONS)).toBe(true);
        });

        it('should have correct default values', () => {
            expect(DEFAULT_SUBJECT_OPTIONS.maxObservers).toBe(100);
            expect(DEFAULT_SUBJECT_OPTIONS.autoComplete).toBe(false);
            expect(DEFAULT_SUBJECT_OPTIONS.errorPropagation).toBe(true);
            expect(DEFAULT_SUBJECT_OPTIONS.memoryManagement.enabled).toBe(true);
            expect(DEFAULT_SUBJECT_OPTIONS.memoryManagement.gcIntervalMs).toBe(60000);
            expect(DEFAULT_SUBJECT_OPTIONS.memoryManagement.weakReferences).toBe(false);
            expect(DEFAULT_SUBJECT_OPTIONS.replay.enabled).toBe(false);
            expect(DEFAULT_SUBJECT_OPTIONS.replay.bufferSize).toBe(10);
            expect(DEFAULT_SUBJECT_OPTIONS.concurrency.enabled).toBe(true);
            expect(DEFAULT_SUBJECT_OPTIONS.concurrency.maxConcurrent).toBe(10);
            expect(DEFAULT_SUBJECT_OPTIONS.validation.enabled).toBe(false);
        });
    });

    describe('PRIORITY_VALUES', () => {
        it('should map high=0, normal=1, low=2', () => {
            expect(PRIORITY_VALUES.high).toBe(0);
            expect(PRIORITY_VALUES.normal).toBe(1);
            expect(PRIORITY_VALUES.low).toBe(2);
        });

        it('should be frozen', () => {
            expect(Object.isFrozen(PRIORITY_VALUES)).toBe(true);
        });
    });

    describe('OBSERVER_MEMORY_SYMBOLS', () => {
        it('should have all 6 symbols', () => {
            expect(typeof OBSERVER_MEMORY_SYMBOLS.observerMap).toBe('symbol');
            expect(typeof OBSERVER_MEMORY_SYMBOLS.subjectRegistry).toBe('symbol');
            expect(typeof OBSERVER_MEMORY_SYMBOLS.replayBuffers).toBe('symbol');
            expect(typeof OBSERVER_MEMORY_SYMBOLS.observationQueues).toBe('symbol');
            expect(typeof OBSERVER_MEMORY_SYMBOLS.filterFunctions).toBe('symbol');
            expect(typeof OBSERVER_MEMORY_SYMBOLS.transformFunctions).toBe('symbol');
        });

        it('should be frozen', () => {
            expect(Object.isFrozen(OBSERVER_MEMORY_SYMBOLS)).toBe(true);
        });
    });

    describe('mergeObserverOptions', () => {
        it('should merge base and override', () => {
            const result = mergeObserverOptions(
                { priority: 'high' },
                { once: true }
            );
            expect(result.priority).toBe('high');
            expect(result.once).toBe(true);
        });

        it('should override base with override values', () => {
            const result = mergeObserverOptions(
                { priority: 'high' },
                { priority: 'low' }
            );
            expect(result.priority).toBe('low');
        });

        it('should deep merge buffering options', () => {
            const result = mergeObserverOptions(
                { buffering: { enabled: true, maxSize: 50 } },
                { buffering: { flushIntervalMs: 2000 } }
            );
            expect(result.buffering).toEqual({
                enabled: true,
                maxSize: 50,
                flushIntervalMs: 2000,
            });
        });

        it('should deep merge replay options', () => {
            const result = mergeObserverOptions(
                { replay: { enabled: true } },
                { replay: { bufferSize: 20 } }
            );
            expect(result.replay).toEqual({ enabled: true, bufferSize: 20 });
        });

        it('should handle empty inputs', () => {
            const result = mergeObserverOptions();
            expect(result).toBeDefined();
        });
    });

    describe('mergeSubjectOptions', () => {
        it('should merge base and override', () => {
            const result = mergeSubjectOptions(
                { maxObservers: 50 },
                { autoComplete: true }
            );
            expect(result.maxObservers).toBe(50);
            expect(result.autoComplete).toBe(true);
        });

        it('should deep merge memoryManagement', () => {
            const result = mergeSubjectOptions(
                { memoryManagement: { enabled: true } },
                { memoryManagement: { gcIntervalMs: 5000 } }
            );
            expect(result.memoryManagement).toEqual({
                enabled: true,
                gcIntervalMs: 5000,
            });
        });

        it('should deep merge concurrency', () => {
            const result = mergeSubjectOptions(
                { concurrency: { enabled: true } },
                { concurrency: { maxConcurrent: 20 } }
            );
            expect(result.concurrency).toEqual({ enabled: true, maxConcurrent: 20 });
        });

        it('should deep merge validation', () => {
            const validator = (d: number) => d > 0;
            const result = mergeSubjectOptions(
                { validation: { enabled: true } },
                { validation: { validator } }
            );
            expect(result.validation?.enabled).toBe(true);
            expect(result.validation?.validator).toBe(validator);
        });

        it('should deep merge replay', () => {
            const result = mergeSubjectOptions(
                { replay: { enabled: true } },
                { replay: { bufferSize: 5 } }
            );
            expect(result.replay).toEqual({ enabled: true, bufferSize: 5 });
        });
    });

    describe('normalizeObserverOptions', () => {
        it('should return defaults for empty input', () => {
            const result = normalizeObserverOptions();
            expect(result.priority).toBe('normal');
            expect(result.once).toBe(false);
            expect(result.debounceMs).toBe(0);
            expect(result.throttleMs).toBe(0);
            expect(result.weakReference).toBe(false);
            expect(result.errorHandling).toBe('throw');
            expect(result.buffering.enabled).toBe(false);
            expect(result.buffering.maxSize).toBe(100);
            expect(result.replay.enabled).toBe(false);
            expect(result.replay.bufferSize).toBe(10);
        });

        it('should return frozen result', () => {
            const result = normalizeObserverOptions();
            expect(Object.isFrozen(result)).toBe(true);
            expect(Object.isFrozen(result.buffering)).toBe(true);
            expect(Object.isFrozen(result.replay)).toBe(true);
        });

        it('should fallback invalid priority to default', () => {
            const result = normalizeObserverOptions({ priority: 'invalid' as any });
            expect(result.priority).toBe('normal');
        });

        it('should fallback invalid errorHandling to default', () => {
            const result = normalizeObserverOptions({ errorHandling: 'invalid' as any });
            expect(result.errorHandling).toBe('throw');
        });

        it('should fallback negative debounceMs to 0', () => {
            const result = normalizeObserverOptions({ debounceMs: -5 });
            expect(result.debounceMs).toBe(0);
        });

        it('should fallback non-integer throttleMs to default', () => {
            const result = normalizeObserverOptions({ throttleMs: NaN });
            expect(result.throttleMs).toBe(0);
        });

        it('should preserve valid explicit values', () => {
            const filter = (d: number) => d > 0;
            const transform = (d: number) => d * 2;
            const onError = () => {};
            const result = normalizeObserverOptions({
                priority: 'high',
                once: true,
                filter,
                transform,
                debounceMs: 100,
                throttleMs: 200,
                weakReference: true,
                errorHandling: 'callback',
                onError,
                buffering: { enabled: true, maxSize: 50, flushIntervalMs: 500 },
                replay: { enabled: true, bufferSize: 5 },
            });
            expect(result.priority).toBe('high');
            expect(result.once).toBe(true);
            expect(result.filter).toBe(filter);
            expect(result.transform).toBe(transform);
            expect(result.debounceMs).toBe(100);
            expect(result.throttleMs).toBe(200);
            expect(result.weakReference).toBe(true);
            expect(result.errorHandling).toBe('callback');
            expect(result.onError).toBe(onError);
            expect(result.buffering.enabled).toBe(true);
            expect(result.buffering.maxSize).toBe(50);
            expect(result.buffering.flushIntervalMs).toBe(500);
            expect(result.replay.enabled).toBe(true);
            expect(result.replay.bufferSize).toBe(5);
        });

        it('should fallback invalid buffering maxSize to default', () => {
            const result = normalizeObserverOptions({ buffering: { maxSize: -1 } });
            expect(result.buffering.maxSize).toBe(100);
        });

        it('should fallback non-boolean buffering.enabled to false', () => {
            const result = normalizeObserverOptions({ buffering: { enabled: 'yes' as any } });
            expect(result.buffering.enabled).toBe(false);
        });
    });

    describe('normalizeSubjectOptions', () => {
        it('should return defaults for empty input', () => {
            const result = normalizeSubjectOptions();
            expect(result.maxObservers).toBe(100);
            expect(result.autoComplete).toBe(false);
            expect(result.errorPropagation).toBe(true);
            expect(result.memoryManagement.enabled).toBe(true);
            expect(result.memoryManagement.gcIntervalMs).toBe(60000);
            expect(result.memoryManagement.weakReferences).toBe(false);
            expect(result.replay.enabled).toBe(false);
            expect(result.replay.bufferSize).toBe(10);
            expect(result.concurrency.enabled).toBe(true);
            expect(result.concurrency.maxConcurrent).toBe(10);
            expect(result.validation.enabled).toBe(false);
        });

        it('should return frozen result', () => {
            const result = normalizeSubjectOptions();
            expect(Object.isFrozen(result)).toBe(true);
            expect(Object.isFrozen(result.memoryManagement)).toBe(true);
            expect(Object.isFrozen(result.replay)).toBe(true);
            expect(Object.isFrozen(result.concurrency)).toBe(true);
            expect(Object.isFrozen(result.validation)).toBe(true);
        });

        it('should fallback invalid maxObservers to default', () => {
            const result = normalizeSubjectOptions({ maxObservers: 0 });
            expect(result.maxObservers).toBe(100);
        });

        it('should preserve explicit boolean values', () => {
            const result = normalizeSubjectOptions({
                autoComplete: true,
                errorPropagation: false,
            });
            expect(result.autoComplete).toBe(true);
            expect(result.errorPropagation).toBe(false);
        });

        it('should fallback non-boolean memoryManagement.enabled to default', () => {
            const result = normalizeSubjectOptions({
                memoryManagement: { enabled: 'yes' as any },
            });
            expect(result.memoryManagement.enabled).toBe(true);
        });

        it('should preserve explicit validation validator', () => {
            const validator = (d: any) => typeof d === 'string';
            const result = normalizeSubjectOptions({
                validation: { enabled: true, validator },
            });
            expect(result.validation.enabled).toBe(true);
            expect(result.validation.validator).toBe(validator);
        });
    });
});
