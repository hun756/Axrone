import {
    createEmitter,
    createEventProxy,
    createTypedEmitter,
    EventMap,
    excludeEvents,
    filterEvents,
    isEventEmitter,
    mergeEmitters,
} from '../../event/event';

interface TestEvents extends EventMap {
    'test:event': { id: string; data: any };
    'test:filtered': { value: number };
    'test:excluded': { message: string };
}

interface TargetEvents extends EventMap {
    'target:mapped': { transformed: boolean };
    'target:direct': { forwarded: boolean };
}

describe('EventEmitter - Features', () => {
    describe('Factory Functions', () => {
        it('should create emitters with correct configurations', () => {
            const emitter1 = createEmitter({ maxListeners: 15 });
            const emitter2 = createTypedEmitter<TestEvents>();

            expect(emitter1.maxListeners).toBe(15);
            expect(emitter2.maxListeners).toBe(10); // default

            emitter1.dispose();
            emitter2.dispose();
        });

        it('should correctly identify EventEmitter instances', () => {
            const emitter = createEmitter();
            const notEmitter = { on: 'not a function', emit: null };
            const partialEmitter = { on: () => {}, emit: () => {} };

            expect(isEventEmitter(emitter)).toBe(true);
            expect(isEventEmitter(notEmitter)).toBe(false);
            expect(isEventEmitter(partialEmitter)).toBe(false);
            expect(isEventEmitter(null)).toBe(false);
            expect(isEventEmitter(undefined)).toBe(false);

            emitter.dispose();
        });

        it('should handle edge cases in type guard', () => {
            const edgeCases = [
                {},
                { on: null, emit: () => {}, off: () => {} },
                { on: () => {}, emit: undefined, off: () => {} },
                { on: () => {}, emit: () => {} }, // missing off
                'string',
                123,
                [],
            ];

            edgeCases.forEach((testCase) => {
                expect(isEventEmitter(testCase)).toBe(false);
            });
        });
    });

    describe('Event Filtering', () => {
        let sourceEmitter: ReturnType<typeof createTypedEmitter<TestEvents>>;

        beforeEach(() => {
            sourceEmitter = createTypedEmitter<TestEvents>();
        });

        afterEach(() => {
            sourceEmitter.dispose();
        });

        it('should filter events correctly', async () => {
            const filtered = filterEvents(sourceEmitter, ['test:event']);
            let filteredCount = 0;
            let sourceCount = 0;

            filtered.on('test:event', () => {
                filteredCount++;
            });
            sourceEmitter.on('test:filtered', () => {
                sourceCount++;
            });

            await sourceEmitter.emit('test:event', { id: 'test', data: {} });
            await sourceEmitter.emit('test:filtered', { value: 42 });

            expect(filteredCount).toBe(1);
            expect(sourceCount).toBe(1);

            (filtered as any).dispose();
        });

        it('should reject non-allowed events in filtered emitter', async () => {
            const filtered = filterEvents(sourceEmitter, ['test:event']);

            const result = await filtered.emit('test:filtered' as any, { value: 42 });
            expect(result).toBe(false);

            (filtered as any).dispose();
        });

        it('should passthrough errors when configured', async () => {
            const filtered = filterEvents(sourceEmitter, ['test:event'], {
                passthroughErrors: true,
            });
            let errorPassed = false;

            filtered.on('error' as any, () => {
                errorPassed = true;
            });
            await sourceEmitter.emit('error' as any, new Error('test'));

            expect(errorPassed).toBe(true);

            (filtered as any).dispose();
        });

        it('should exclude specified events', async () => {
            const excluded = excludeEvents(sourceEmitter, ['test:excluded']);
            let includedCount = 0;
            let excludedCount = 0;

            excluded.on('test:event', () => {
                includedCount++;
            });
            sourceEmitter.on('test:excluded', () => {
                excludedCount++;
            });

            await sourceEmitter.emit('test:event', { id: 'test', data: {} });
            await sourceEmitter.emit('test:excluded', { message: 'excluded' });

            expect(includedCount).toBe(1);
            expect(excludedCount).toBe(1);

            (excluded as any).dispose();
        });
    });

    describe('Event Proxy', () => {
        let sourceEmitter: ReturnType<typeof createTypedEmitter<TestEvents>>;
        let targetEmitter: ReturnType<typeof createTypedEmitter<TargetEvents>>;

        beforeEach(() => {
            sourceEmitter = createTypedEmitter<TestEvents>();
            targetEmitter = createTypedEmitter<TargetEvents>();
        });

        afterEach(() => {
            sourceEmitter.dispose();
            targetEmitter.dispose();
        });

        it('should proxy events with mapping', async () => {
            let targetReceived = false;

            targetEmitter.on('target:mapped', () => {
                targetReceived = true;
            });

            const unsubscribe = createEventProxy(sourceEmitter, targetEmitter, {
                'test:event': 'target:mapped',
            });

            await sourceEmitter.emit('test:event', { id: 'test', data: {} });
            expect(targetReceived).toBe(true);

            unsubscribe();
        });

        it('should transform data with transformers', async () => {
            let transformedData: any = null;

            targetEmitter.on('target:mapped', (data) => {
                transformedData = data;
            });

            const unsubscribe = createEventProxy(
                sourceEmitter,
                targetEmitter,
                { 'test:event': 'target:mapped' },
                {
                    'test:event': (data) => ({ transformed: true, original: data }),
                }
            );

            await sourceEmitter.emit('test:event', { id: 'test', data: { value: 42 } });

            expect(transformedData).toEqual({
                transformed: true,
                original: { id: 'test', data: { value: 42 } },
            });

            unsubscribe();
        });

        it('should handle bidirectional proxying', async () => {
            let sourceReceived = false;
            let targetReceived = false;

            sourceEmitter.on('test:event', () => {
                sourceReceived = true;
            });
            targetEmitter.on('target:mapped', () => {
                targetReceived = true;
            });

            const unsubscribe = createEventProxy(
                sourceEmitter,
                targetEmitter,
                { 'test:event': 'target:mapped' },
                undefined,
                { bidirectional: true }
            );

            await sourceEmitter.emit('test:event', { id: 'test', data: {} });
            expect(targetReceived).toBe(true);

            await targetEmitter.emit('target:mapped', { transformed: true });
            expect(sourceReceived).toBe(true);

            unsubscribe();
        });

        it('should preserve priority when configured', async () => {
            const targetEmitter = createTypedEmitter<TargetEvents>();
            let receivedPriority: any = null;

            const originalEmit = targetEmitter.emit.bind(targetEmitter);
            targetEmitter.emit = async function (event, data, options) {
                receivedPriority = options?.priority;
                return originalEmit(event, data, options);
            };

            const unsubscribe = createEventProxy(
                sourceEmitter,
                targetEmitter,
                { 'test:event': 'target:mapped' },
                undefined,
                { preservePriority: true }
            );

            await sourceEmitter.emit('test:event', { id: 'test', data: {} }, { priority: 'high' });
            expect(receivedPriority).toBe('high');

            unsubscribe();
            targetEmitter.dispose();
        });
    });

    describe('Emitter Merging', () => {
        it('should merge multiple emitters correctly', async () => {
            const emitter1 = createTypedEmitter<TestEvents>();
            const emitter2 = createTypedEmitter<TargetEvents>();
            const merged = mergeEmitters(emitter1, emitter2);

            let event1Count = 0;
            let event2Count = 0;

            merged.on('test:event' as any, () => {
                event1Count++;
            });
            merged.on('target:mapped' as any, () => {
                event2Count++;
            });

            await emitter1.emit('test:event', { id: 'test', data: {} });
            await emitter2.emit('target:mapped', { transformed: true });

            expect(event1Count).toBe(1);
            expect(event2Count).toBe(1);

            emitter1.dispose();
            emitter2.dispose();
            (merged as any).dispose();
        });

        it('should forward error events from source emitters', async () => {
            const emitter1 = createTypedEmitter<TestEvents>();
            const emitter2 = createTypedEmitter<TargetEvents>();
            const merged = mergeEmitters(emitter1, emitter2);

            let errorForwarded = false;

            merged.on('error' as any, () => {
                errorForwarded = true;
            });

            emitter1.on('error' as any, () => {});
            await emitter1.emit('error' as any, new Error('test'));

            expect(errorForwarded).toBe(true);

            emitter1.dispose();
            emitter2.dispose();
            (merged as any).dispose();
        });
    });
});
