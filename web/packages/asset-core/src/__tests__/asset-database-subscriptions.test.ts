import { describe, expect, it, vi } from 'vitest';
import {
    AssetDatabase,
    AssetDisposedError,
    type AssetChangeEvent,
    type AssetImporter,
    type AssetSchema,
} from '@axrone/asset-core';

interface TestSchema extends AssetSchema {
    text: string;
}

const textImporter: AssetImporter<TestSchema, { kind: 'text'; data: string; uri?: string }, 'text'> = {
    id: 'test.text',
    sourceKinds: ['text'],
    import: ({ source }) => ({
        primary: {
            kind: 'text',
            data: source.data,
        },
    }),
};

describe('Asset Database Subscriptions', () => {
    describe('subscribe', () => {
        it('returns subscription with isDisposed', () => {
            const database = new AssetDatabase<TestSchema>();
            const subscription = database.subscribe(() => {});

            expect(subscription.isDisposed).toBe(false);
            subscription.dispose();
            expect(subscription.isDisposed).toBe(true);
        });

        it('listener receives upsert events', async () => {
            const database = new AssetDatabase<TestSchema>();
            const listener = vi.fn();
            database.subscribe(listener);

            const record = database.upsert({
                kind: 'text',
                stableKey: 'test.txt',
                data: 'hello',
            });

            await vi.waitFor(() => {
                expect(listener).toHaveBeenCalledTimes(1);
            });

            const event = listener.mock.calls[0][0] as AssetChangeEvent<TestSchema>;
            expect(event.type).toBe('upsert');
            if (event.type === 'upsert') {
                expect(event.asset.id).toBe(record.id);
                expect(event.asset.data).toBe('hello');
                expect(event.previous).toBeUndefined();
            }
        });

        it('listener receives delete events', async () => {
            const database = new AssetDatabase<TestSchema>();
            const listener = vi.fn();

            const record = database.upsert({
                kind: 'text',
                stableKey: 'test.txt',
                data: 'hello',
            });

            // Wait for upsert event to flush before subscribing
            await new Promise((resolve) => setTimeout(resolve, 0));

            database.subscribe(listener);

            database.delete(record.reference);

            await vi.waitFor(() => {
                expect(listener).toHaveBeenCalledTimes(1);
            });

            const event = listener.mock.calls[0][0] as AssetChangeEvent<TestSchema>;
            // delete() uses batch internally, but since there's only one delete event,
            // it should be delivered as a leaf event
            expect(event.type).toBe('delete');
            if (event.type === 'delete') {
                expect(event.asset.id).toBe(record.id);
            }
        });

        it('listener receives import events', async () => {
            const database = new AssetDatabase<TestSchema>({
                importers: [textImporter],
            });
            const listener = vi.fn();
            database.subscribe(listener);

            await database.import({
                kind: 'text',
                data: 'hello',
                uri: 'test.txt',
            });

            await vi.waitFor(() => {
                expect(listener).toHaveBeenCalledTimes(1);
            });

            const event = listener.mock.calls[0][0] as AssetChangeEvent<TestSchema>;
            // import() emits both upsert events (from _applyWrites) and an import event
            // in the same batch, so we expect a batch with 2 events
            expect(event.type).toBe('batch');
            if (event.type === 'batch') {
                expect(event.events).toHaveLength(2);
                expect(event.events[0].type).toBe('upsert');
                expect(event.events[1].type).toBe('import');
                if (event.events[1].type === 'import') {
                    expect(event.events[1].receipt.primary.data).toBe('hello');
                }
            }
        });

        it('dispose subscription stops events', async () => {
            const database = new AssetDatabase<TestSchema>();
            const listener = vi.fn();
            const subscription = database.subscribe(listener);

            database.upsert({
                kind: 'text',
                stableKey: 'test1.txt',
                data: 'first',
            });

            await vi.waitFor(() => {
                expect(listener).toHaveBeenCalledTimes(1);
            });

            subscription.dispose();

            database.upsert({
                kind: 'text',
                stableKey: 'test2.txt',
                data: 'second',
            });

            await new Promise((resolve) => setTimeout(resolve, 10));
            expect(listener).toHaveBeenCalledTimes(1);
        });

        it('double dispose is no-op', () => {
            const database = new AssetDatabase<TestSchema>();
            const subscription = database.subscribe(() => {});

            subscription.dispose();
            subscription.dispose();
            expect(subscription.isDisposed).toBe(true);
        });
    });

    describe('Event batching', () => {
        it('multiple upserts in upsertMany produce single batch event', async () => {
            const database = new AssetDatabase<TestSchema>();
            const listener = vi.fn();
            database.subscribe(listener);

            database.upsertMany([
                { kind: 'text', stableKey: 'test1.txt', data: 'first' },
                { kind: 'text', stableKey: 'test2.txt', data: 'second' },
                { kind: 'text', stableKey: 'test3.txt', data: 'third' },
            ]);

            await vi.waitFor(() => {
                expect(listener).toHaveBeenCalledTimes(1);
            });

            const event = listener.mock.calls[0][0] as AssetChangeEvent<TestSchema>;
            expect(event.type).toBe('batch');
            if (event.type === 'batch') {
                expect(event.events).toHaveLength(3);
                expect(event.events[0].type).toBe('upsert');
                expect(event.events[1].type).toBe('upsert');
                expect(event.events[2].type).toBe('upsert');
            }
        });

        it('single upsert produces leaf event (not batch)', async () => {
            const database = new AssetDatabase<TestSchema>();
            const listener = vi.fn();
            database.subscribe(listener);

            database.upsert({
                kind: 'text',
                stableKey: 'test.txt',
                data: 'hello',
            });

            await vi.waitFor(() => {
                expect(listener).toHaveBeenCalledTimes(1);
            });

            const event = listener.mock.calls[0][0] as AssetChangeEvent<TestSchema>;
            expect(event.type).toBe('upsert');
        });
    });

    describe('subscribe on disposed database', () => {
        it('throws AssetDisposedError', () => {
            const database = new AssetDatabase<TestSchema>();
            database.dispose();

            expect(() => database.subscribe(() => {})).toThrow(AssetDisposedError);
        });
    });

    describe('Listener error isolation', () => {
        it('throwing listener does not prevent other listeners', async () => {
            const database = new AssetDatabase<TestSchema>();
            const listener1 = vi.fn(() => {
                throw new Error('Listener 1 failed');
            });
            const listener2 = vi.fn();

            // Mock queueMicrotask to catch the rethrown error
            const originalQueueMicrotask = globalThis.queueMicrotask;
            const errors: Error[] = [];
            globalThis.queueMicrotask = vi.fn((callback) => {
                try {
                    callback();
                } catch (error) {
                    errors.push(error as Error);
                }
            }) as typeof queueMicrotask;

            database.subscribe(listener1);
            database.subscribe(listener2);

            database.upsert({
                kind: 'text',
                stableKey: 'test.txt',
                data: 'hello',
            });

            // Both listeners should be called despite listener1 throwing
            await vi.waitFor(() => {
                expect(listener1).toHaveBeenCalledTimes(1);
                expect(listener2).toHaveBeenCalledTimes(1);
            });

            // Wait for the error rethrow microtask to execute
            await new Promise((resolve) => setTimeout(resolve, 10));

            // Restore original queueMicrotask
            globalThis.queueMicrotask = originalQueueMicrotask;

            // Verify that an error was indeed thrown
            expect(errors).toHaveLength(1);
            expect(errors[0].message).toBe('Listener 1 failed');
        });
    });
});
