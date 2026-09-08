import {
    EventCallback,
    EventPriority,
    EventMap,
    EventKey,
    UnsubscribeFn,
    EventDispatchItem,
    EventDispatchResult,
} from '../definition';
import { IEventEmitter } from '../event-emitter';
import {
    SubscriptionOptions,
    Subscription,
    QueuedEvent,
    IEventPublisher,
    EventMetrics,
} from '../interfaces';

/**
 * Abstract base class that delegates every member of {@link IEventEmitter}
 * to a wrapped target emitter.
 *
 * Wrappers (e.g. `EventGroup`, the namespace wrapper inside
 * `namespaceEvents`) only need to override the surface that actually changes
 * behaviour. Everything else is forwarded to `target` so adding a new method
 * to {@link IEventEmitter} is automatically picked up by every existing
 * subclass — no third hand-written 30-method copy, no `as unknown as` cast
 * to bridge the new method through the wrapper.
 *
 * Subclasses must expose the wrapped emitter through the protected
 * `target` getter. Any method that needs custom behaviour (subscription
 * tracking, prefix translation, etc.) simply overrides the corresponding
 * member; the rest are inherited unchanged.
 */
export abstract class ForwardingEmitter<T extends EventMap> implements IEventEmitter<T> {
    protected abstract get target(): IEventEmitter<T>;

    get maxListeners(): number {
        return this.target.maxListeners;
    }

    set maxListeners(value: number) {
        this.target.maxListeners = value;
    }

    on<K extends EventKey<T>>(
        event: K,
        callback: EventCallback<T[K]>,
        options?: SubscriptionOptions
    ): UnsubscribeFn {
        return this.target.on(event, callback, options);
    }

    once<K extends EventKey<T>>(
        event: K,
        callback: EventCallback<T[K]>,
        options?: Omit<SubscriptionOptions, 'once'>
    ): UnsubscribeFn {
        return this.target.once(event, callback, options);
    }

    off<K extends EventKey<T>>(event: K, callback?: EventCallback<T[K]>): boolean {
        return this.target.off(event, callback);
    }

    offById(subscriptionId: symbol): boolean {
        return this.target.offById(subscriptionId);
    }

    pipe<K extends EventKey<T>>(
        event: K,
        emitter: IEventPublisher<any>,
        targetEvent?: string
    ): UnsubscribeFn {
        return this.target.pipe(event, emitter, targetEvent);
    }

    emit<K extends EventKey<T>>(
        event: K,
        data: T[K],
        options?: { priority?: EventPriority }
    ): Promise<boolean> {
        return this.target.emit(event, data, options);
    }

    emitSync<K extends EventKey<T>>(
        event: K,
        data: T[K],
        options?: { priority?: EventPriority }
    ): boolean {
        return this.target.emitSync(event, data, options);
    }

    emitBatch(
        events: ReadonlyArray<EventDispatchItem<T>>
    ): Promise<ReadonlyArray<EventDispatchResult>> {
        return this.target.emitBatch(events);
    }

    has<K extends EventKey<T>>(event: K): boolean {
        return this.target.has(event);
    }

    listenerCount<K extends EventKey<T>>(event: K): number {
        return this.target.listenerCount(event);
    }

    listenerCountAll(): number {
        return this.target.listenerCountAll();
    }

    eventNames(): EventKey<T>[] {
        return this.target.eventNames();
    }

    getSubscriptions<K extends EventKey<T>>(event: K): ReadonlyArray<Subscription<T[K]>> {
        return this.target.getSubscriptions(event);
    }

    hasSubscription(subscriptionId: symbol): boolean {
        return this.target.hasSubscription(subscriptionId);
    }

    getMetrics<K extends EventKey<T>>(event: K): EventMetrics {
        return this.target.getMetrics(event);
    }

    getQueuedEvents<K extends EventKey<T>>(event: K): ReadonlyArray<QueuedEvent<T[K]>>;
    getQueuedEvents(): ReadonlyArray<QueuedEvent<T[EventKey<T>]>>;
    getQueuedEvents<K extends EventKey<T>>(event?: K): ReadonlyArray<QueuedEvent<any>> {
        return event !== undefined
            ? this.target.getQueuedEvents(event)
            : this.target.getQueuedEvents();
    }

    getPendingCount<K extends EventKey<T>>(event?: K): number {
        return this.target.getPendingCount(event);
    }

    getBufferSize(): number {
        return this.target.getBufferSize();
    }

    clearBuffer<K extends EventKey<T>>(event?: K): number {
        return this.target.clearBuffer(event);
    }

    pause(): void {
        this.target.pause();
    }

    resume(): void {
        this.target.resume();
    }

    isPaused(): boolean {
        return this.target.isPaused();
    }

    removeAllListeners<K extends EventKey<T>>(event?: K): this {
        this.target.removeAllListeners(event);
        return this;
    }

    batchSubscribe<K extends EventKey<T>>(
        event: K,
        callbacks: ReadonlyArray<EventCallback<T[K]>>,
        options?: SubscriptionOptions
    ): ReadonlyArray<symbol> {
        return this.target.batchSubscribe(event, callbacks, options);
    }

    batchUnsubscribe(subscriptionIds: ReadonlyArray<symbol>): number {
        return this.target.batchUnsubscribe(subscriptionIds);
    }

    resetMaxListeners(): void {
        this.target.resetMaxListeners();
    }

    async drain(): Promise<void> {
        return this.target.drain();
    }

    async flush<K extends EventKey<T>>(event: K): Promise<void> {
        return this.target.flush(event);
    }

    resetMetrics<K extends EventKey<T>>(event?: K): void {
        this.target.resetMetrics(event);
    }

    dispose(): void {
        this.target.dispose();
    }
}
