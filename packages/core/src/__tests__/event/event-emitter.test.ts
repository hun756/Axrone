import { EventEmitter, EventMap } from '../../event/event';

interface TestEvents extends EventMap {
    'test:event': { id: string; data: any };
    'test:async': { delay: number };
    'test:error': { shouldFail: boolean };
    'test:priority': { level: string };
    'test:batch': { index: number };
}

describe('EventEmitter - Adım 7: Main Implementation', () => {
    describe('Core Event Operations', () => {
        let emitter: EventEmitter<TestEvents>;

        beforeEach(() => {
            emitter = new EventEmitter<TestEvents>();
        });

        afterEach(() => {
            emitter.dispose();
        });

        it('should handle basic subscription and emission workflow', async () => {
            let callbackExecuted = false;
            let receivedData: any = null;

            const unsubscribe = emitter.on('test:event', (data) => {
                callbackExecuted = true;
                receivedData = data;
            });

            const testData = { id: 'test1', data: { value: 42 } };
            const result = await emitter.emit('test:event', testData);

            expect(result).toBe(true);
            expect(callbackExecuted).toBe(true);
            expect(receivedData).toEqual(testData);

            unsubscribe();
        });

        it('should handle once subscriptions correctly', async () => {
            let executionCount = 0;

            emitter.once('test:event', () => {
                executionCount++;
            });

            await emitter.emit('test:event', { id: 'test1', data: {} });
            expect(executionCount).toBe(1);

            await emitter.emit('test:event', { id: 'test2', data: {} });
            expect(executionCount).toBe(1);

            expect(emitter.listenerCount('test:event')).toBe(0);
        });

        it('should handle async and sync callbacks correctly', async () => {
            const executionOrder: string[] = [];

            emitter.on('test:async', async (data) => {
                await new Promise((resolve) => setTimeout(resolve, data.delay));
                executionOrder.push('async');
            });

            emitter.on('test:async', (data) => {
                executionOrder.push('sync');
            });

            await emitter.emit('test:async', { delay: 50 });

            expect(executionOrder).toContain('async');
            expect(executionOrder).toContain('sync');
        });

        it('should return false when no listeners exist', async () => {
            const result = await emitter.emit('test:event', { id: 'test', data: {} });
            expect(result).toBe(false);
        });

        it('should handle subscription removal correctly', () => {
            const callback1 = jest.fn();
            const callback2 = jest.fn();

            emitter.on('test:event', callback1);
            emitter.on('test:event', callback2);

            expect(emitter.listenerCount('test:event')).toBe(2);

            const removed = emitter.off('test:event', callback1);
            expect(removed).toBe(true);
            expect(emitter.listenerCount('test:event')).toBe(1);

            const removedAll = emitter.off('test:event');
            expect(removedAll).toBe(true);
            expect(emitter.listenerCount('test:event')).toBe(0);
        });
    });
});
