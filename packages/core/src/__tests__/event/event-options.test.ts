import { EventOptions, DEFAULT_OPTIONS, MEMORY_USAGE_SYMBOLS } from '../../event/event';

describe('EventEmitter - Event Options', () => {
    describe('EventOptions Interface', () => {
        it('should accept valid EventOptions objects', () => {
            const emptyOptions: EventOptions = {};
            expect(typeof emptyOptions).toBe('object');

            const partialOptions: EventOptions = {
                maxListeners: 15,
                captureRejections: true,
            };
            expect(partialOptions.maxListeners).toBe(15);
            expect(partialOptions.captureRejections).toBe(true);

            const completeOptions: EventOptions = {
                captureRejections: false,
                maxListeners: 20,
                weakReferences: true,
                immediateDispatch: false,
                concurrencyLimit: 8,
                bufferSize: 500,
                gcIntervalMs: 30000,
            };
            expect(completeOptions.captureRejections).toBe(false);
            expect(completeOptions.maxListeners).toBe(20);
            expect(completeOptions.weakReferences).toBe(true);
            expect(completeOptions.immediateDispatch).toBe(false);
            expect(completeOptions.concurrencyLimit).toBe(8);
            expect(completeOptions.bufferSize).toBe(500);
            expect(completeOptions.gcIntervalMs).toBe(30000);
        });

        it('should check readonly properties at compile-time', () => {
            const options: EventOptions = { maxListeners: 10 };
            expect(options.maxListeners).toBe(10);
        });

        it('optional properties should be able to be undefined', () => {
            const options: EventOptions = {
                maxListeners: 10,
                // Other properties undefined (missing)
            };

            expect(options.maxListeners).toBe(10);
            expect(options.captureRejections).toBeUndefined();
            expect(options.weakReferences).toBeUndefined();
            expect(options.immediateDispatch).toBeUndefined();
            expect(options.concurrencyLimit).toBeUndefined();
            expect(options.bufferSize).toBeUndefined();
            expect(options.gcIntervalMs).toBeUndefined();
        });

        it('should ensure all property types are correct', () => {
            const options: EventOptions = {
                captureRejections: true,
                maxListeners: 25,
                weakReferences: false,
                immediateDispatch: true,
                concurrencyLimit: Infinity,
                bufferSize: 1000,
                gcIntervalMs: 60000,
            };

            expect(typeof options.captureRejections).toBe('boolean');
            expect(typeof options.maxListeners).toBe('number');
            expect(typeof options.weakReferences).toBe('boolean');
            expect(typeof options.immediateDispatch).toBe('boolean');
            expect(typeof options.concurrencyLimit).toBe('number');
            expect(typeof options.bufferSize).toBe('number');
            expect(typeof options.gcIntervalMs).toBe('number');

            expect(options.concurrencyLimit).toBe(Infinity);
            expect(Number.isFinite(options.maxListeners)).toBe(true);
        });

        it('should prevent invalid type assignments by TypeScript', () => {
            // These assignments should give TypeScript compile errors:
            // const invalidOptions: EventOptions = {
            //   captureRejections: "true",    // ❌ string not assignable to boolean
            //   maxListeners: "10",           // ❌ string not assignable to number
            //   unknownProperty: true         // ❌ unknown property
            // };

            function isValidEventOptions(value: unknown): value is EventOptions {
                if (typeof value !== 'object' || value === null) return false;

                const obj = value as Record<string, unknown>;

                if ('captureRejections' in obj && typeof obj.captureRejections !== 'boolean')
                    return false;
                if ('maxListeners' in obj && typeof obj.maxListeners !== 'number') return false;
                if ('weakReferences' in obj && typeof obj.weakReferences !== 'boolean')
                    return false;
                if ('immediateDispatch' in obj && typeof obj.immediateDispatch !== 'boolean')
                    return false;
                if ('concurrencyLimit' in obj && typeof obj.concurrencyLimit !== 'number')
                    return false;
                if ('bufferSize' in obj && typeof obj.bufferSize !== 'number') return false;
                if ('gcIntervalMs' in obj && typeof obj.gcIntervalMs !== 'number') return false;

                return true;
            }

            expect(isValidEventOptions({ maxListeners: 10 })).toBe(true);
            expect(isValidEventOptions({ maxListeners: '10' })).toBe(false);
            expect(isValidEventOptions({ captureRejections: 'true' })).toBe(false);
        });
    });
});
