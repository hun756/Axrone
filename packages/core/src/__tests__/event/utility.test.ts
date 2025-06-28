import { createEmitter, createTypedEmitter, EventMap, isEventEmitter } from '../../event/event';

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
});
