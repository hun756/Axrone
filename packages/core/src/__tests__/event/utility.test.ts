import {
    createEmitter,
    createTypedEmitter,
    EventMap,
    excludeEvents,
    filterEvents,
    isEventEmitter,
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

describe('EventEmitter - Adım 8: Advanced Features', () => {
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
});
