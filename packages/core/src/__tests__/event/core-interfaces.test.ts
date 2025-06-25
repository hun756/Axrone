import {
    IEventSubscriber,
    IEventPublisher,
    IEventBuffer,
    IEventObserver,
    IEventEmitter,
    EventMap,
    EventCallback,
    SubscriptionOptions,
    EventMetrics,
    QueuedEvent,
} from '../../event/event';

interface TestEvents extends EventMap {
    'test:event': { id: string; data: any };
    'test:error': { error: Error };
    'test:batch': { index: number };
}

describe('EventEmitter - Core Interfaces', () => {
    describe('Interface Type Contracts', () => {
        it('should enforce correct generic constraints', () => {
            interface ValidEvents extends EventMap {
                'valid:event': { data: string };
            }

            interface InvalidEvents {
                notAnEvent: string;
            }

            const validSubscriber: IEventSubscriber<ValidEvents> = {} as any;
            const validPublisher: IEventPublisher<ValidEvents> = {} as any;
            const validObserver: IEventObserver<ValidEvents> = {} as any;

            expect(validSubscriber).toBeDefined();
            expect(validPublisher).toBeDefined();
            expect(validObserver).toBeDefined();
        });

        it('should support interface composition in IEventEmitter', () => {
            const emitter: IEventEmitter<TestEvents> = {} as any;

            expect(typeof emitter.on).toBe('undefined');
            expect(typeof emitter.once).toBe('undefined');
            expect(typeof emitter.off).toBe('undefined');
            expect(typeof emitter.offById).toBe('undefined');
            expect(typeof emitter.pipe).toBe('undefined');

            expect(typeof emitter.emit).toBe('undefined');
            expect(typeof emitter.emitSync).toBe('undefined');
            expect(typeof emitter.emitBatch).toBe('undefined');

            expect(typeof emitter.has).toBe('undefined');
            expect(typeof emitter.listenerCount).toBe('undefined');
            expect(typeof emitter.getMetrics).toBe('undefined');

            expect(typeof emitter.pause).toBe('undefined');
            expect(typeof emitter.resume).toBe('undefined');
            expect(typeof emitter.getQueuedEvents).toBe('undefined');

            expect(typeof emitter.removeAllListeners).toBe('undefined');
            expect(typeof emitter.batchSubscribe).toBe('undefined');
            expect(typeof emitter.drain).toBe('undefined');
        });

        it('should handle EventKey type constraints correctly', () => {
            type TestEventKey = keyof TestEvents & string;

            const validKeys: TestEventKey[] = ['test:event', 'test:error', 'test:batch'];

            validKeys.forEach((key) => {
                expect(typeof key).toBe('string');
                expect(key.includes(':')).toBe(true);
            });
        });
    });

    describe('IEventSubscriber Contract', () => {
        let mockSubscriber: jest.Mocked<IEventSubscriber<TestEvents>>;

        beforeEach(() => {
            mockSubscriber = {
                on: jest.fn(),
                once: jest.fn(),
                off: jest.fn(),
                offById: jest.fn(),
                pipe: jest.fn(),
            };
        });

        it('should handle subscription lifecycle correctly', () => {
            const mockUnsubscribe = jest.fn().mockReturnValue(true);
            mockSubscriber.on.mockReturnValue(mockUnsubscribe);

            const callback: EventCallback<TestEvents['test:event']> = jest.fn();
            const unsubscribe = mockSubscriber.on('test:event', callback);

            expect(mockSubscriber.on).toHaveBeenCalledWith('test:event', callback);
            expect(typeof unsubscribe).toBe('function');

            const result = unsubscribe();
            expect(result).toBe(true);
            expect(mockUnsubscribe).toHaveBeenCalled();
        });

        it('should support subscription options correctly', () => {
            const callback: EventCallback<TestEvents['test:event']> = jest.fn();
            const options: SubscriptionOptions = { priority: 'high' };

            mockSubscriber.on('test:event', callback, options);
            expect(mockSubscriber.on).toHaveBeenCalledWith('test:event', callback, options);

            const onceOptions: Omit<SubscriptionOptions, 'once'> = { priority: 'low' };
            mockSubscriber.once('test:event', callback, onceOptions);
            expect(mockSubscriber.once).toHaveBeenCalledWith('test:event', callback, onceOptions);
        });

        it('should handle off operations with different signatures', () => {
            const callback: EventCallback<TestEvents['test:event']> = jest.fn();

            mockSubscriber.off.mockReturnValue(true);

            let result = mockSubscriber.off('test:event', callback);
            expect(mockSubscriber.off).toHaveBeenCalledWith('test:event', callback);
            expect(result).toBe(true);

            result = mockSubscriber.off('test:event');
            expect(mockSubscriber.off).toHaveBeenCalledWith('test:event');
        });

        it('should handle piping operations', () => {
            const targetPublisher: IEventPublisher<any> = {
                emit: jest.fn(),
                emitSync: jest.fn(),
                emitBatch: jest.fn(),
            };

            const mockUnsubscribe = jest.fn().mockReturnValue(true);
            mockSubscriber.pipe.mockReturnValue(mockUnsubscribe);

            let unsubscribe = mockSubscriber.pipe('test:event', targetPublisher);
            expect(mockSubscriber.pipe).toHaveBeenCalledWith('test:event', targetPublisher);

            unsubscribe = mockSubscriber.pipe('test:event', targetPublisher, 'target:event');
            expect(mockSubscriber.pipe).toHaveBeenCalledWith(
                'test:event',
                targetPublisher,
                'target:event'
            );
        });
    });

    describe('IEventPublisher Contract', () => {
        let mockPublisher: jest.Mocked<IEventPublisher<TestEvents>>;

        beforeEach(() => {
            mockPublisher = {
                emit: jest.fn(),
                emitSync: jest.fn(),
                emitBatch: jest.fn(),
            };
        });

        it('should handle async emit operations', async () => {
            mockPublisher.emit.mockResolvedValue(true);

            const data: TestEvents['test:event'] = { id: 'test', data: { value: 42 } };
            const result = await mockPublisher.emit('test:event', data);

            expect(mockPublisher.emit).toHaveBeenCalledWith('test:event', data);
            expect(result).toBe(true);
        });

        it('should handle emit with priority options', async () => {
            mockPublisher.emit.mockResolvedValue(true);

            const data: TestEvents['test:event'] = { id: 'test', data: {} };
            const options = { priority: 'high' as const };

            await mockPublisher.emit('test:event', data, options);
            expect(mockPublisher.emit).toHaveBeenCalledWith('test:event', data, options);
        });

        it('should handle sync emit operations', () => {
            mockPublisher.emitSync.mockReturnValue(true);

            const data: TestEvents['test:error'] = { error: new Error('Test error') };
            const result = mockPublisher.emitSync('test:error', data);

            expect(mockPublisher.emitSync).toHaveBeenCalledWith('test:error', data);
            expect(result).toBe(true);
        });

        it('should handle batch emit operations', async () => {
            mockPublisher.emitBatch.mockResolvedValue([true, false, true]);

            const events = [
                { event: 'test:batch' as const, data: { index: 1 } },
                { event: 'test:batch' as const, data: { index: 2 }, priority: 'high' as const },
                { event: 'test:batch' as const, data: { index: 3 }, priority: 'low' as const },
            ];

            const results = await mockPublisher.emitBatch(events);

            expect(mockPublisher.emitBatch).toHaveBeenCalledWith(events);
            expect(results).toEqual([true, false, true]);
        });

        it('should handle emit failures gracefully', async () => {
            const error = new Error('Emit failed');
            mockPublisher.emit.mockRejectedValue(error);

            await expect(
                mockPublisher.emit('test:event', { id: 'test', data: {} })
            ).rejects.toThrow('Emit failed');
        });
    });
});
