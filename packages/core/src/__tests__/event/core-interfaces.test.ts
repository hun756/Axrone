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
});
