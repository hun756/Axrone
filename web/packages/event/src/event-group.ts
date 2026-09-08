import { EventMap, EventKey, EventCallback, UnsubscribeFn } from './definition';
import { IEventEmitter, EventEmitter } from './event-emitter';
import { SubscriptionOptions, Subscription } from './interfaces';
import { ForwardingEmitter } from './internal/forwarding-emitter';

interface TrackedSubscription {
    readonly event: string;
    readonly callback: EventCallback<any>;
}

export class EventGroup<T extends EventMap> extends ForwardingEmitter<T> {
    readonly #emitter: IEventEmitter<T>;
    readonly #ownsEmitter: boolean;
    readonly #subscriptions: Set<symbol> = new Set();
    readonly #tracked = new Map<symbol, TrackedSubscription>();
    #disposed = false;

    constructor(baseEmitter?: IEventEmitter<T>) {
        super();
        this.#emitter = baseEmitter ?? new EventEmitter<T>();
        this.#ownsEmitter = baseEmitter === undefined;
    }

    protected get target(): IEventEmitter<T> {
        return this.#emitter;
    }

    on<K extends EventKey<T>>(
        event: K,
        callback: EventCallback<T[K]>,
        options?: SubscriptionOptions
    ): UnsubscribeFn {
        const [subscriptionId] = this.#emitter.batchSubscribe(event, [callback], options);

        if (!subscriptionId) {
            return () => false;
        }

        this.#trackSubscription(subscriptionId, String(event), callback);

        return () => this.#unsubscribeTracked(subscriptionId);
    }

    once<K extends EventKey<T>>(
        event: K,
        callback: EventCallback<T[K]>,
        options?: Omit<SubscriptionOptions, 'once'>
    ): UnsubscribeFn {
        let subscriptionId: symbol | undefined;
        const wrappedCallback: EventCallback<T[K]> = (data) => {
            if (subscriptionId) {
                this.#untrackSubscription(subscriptionId);
            }
            return callback(data);
        };

        const [trackedId] = this.#emitter.batchSubscribe(event, [wrappedCallback], {
            ...options,
            once: true,
        });

        if (!trackedId) {
            return () => false;
        }

        subscriptionId = trackedId;
        this.#trackSubscription(trackedId, String(event), callback);

        return () => this.#unsubscribeTracked(trackedId);
    }

    off<K extends EventKey<T>>(event: K, callback?: EventCallback<T[K]>): boolean {
        const ids = this.#collectSubscriptionIds(String(event), callback);

        if (ids.length === 0) {
            return false;
        }

        let removed = false;

        for (const subscriptionId of ids) {
            removed = this.#unsubscribeTracked(subscriptionId) || removed;
        }

        return removed;
    }

    offById(subscriptionId: symbol): boolean {
        return this.#unsubscribeTracked(subscriptionId);
    }

    has<K extends EventKey<T>>(event: K): boolean {
        return this.listenerCount(event) > 0;
    }

    listenerCount<K extends EventKey<T>>(event: K): number {
        return this.#collectSubscriptionIds(String(event)).length;
    }

    listenerCountAll(): number {
        this.#pruneStaleSubscriptions();
        return this.#subscriptions.size;
    }

    eventNames(): EventKey<T>[] {
        this.#pruneStaleSubscriptions();

        const names = new Set<EventKey<T>>();

        for (const tracked of this.#tracked.values()) {
            names.add(tracked.event as EventKey<T>);
        }

        return Array.from(names);
    }

    getSubscriptions<K extends EventKey<T>>(event: K): ReadonlyArray<Subscription<T[K]>> {
        const activeIds = new Set(this.#collectSubscriptionIds(String(event)));
        if (activeIds.size === 0) {
            return [];
        }

        return this.#emitter.getSubscriptions(event).flatMap((subscription) => {
            if (!activeIds.has(subscription.id)) {
                return [];
            }

            const tracked = this.#tracked.get(subscription.id);

            return [
                {
                    ...subscription,
                    callback: (tracked?.callback ?? subscription.callback) as EventCallback<T[K]>,
                },
            ];
        });
    }

    hasSubscription(subscriptionId: symbol): boolean {
        this.#pruneStaleSubscriptions();
        return this.#subscriptions.has(subscriptionId);
    }

    removeAllListeners<K extends EventKey<T>>(event?: K): this {
        const ids = this.#collectSubscriptionIds(event ? String(event) : undefined);

        for (const subscriptionId of ids) {
            this.#unsubscribeTracked(subscriptionId);
        }

        return this;
    }

    batchSubscribe<K extends EventKey<T>>(
        event: K,
        callbacks: ReadonlyArray<EventCallback<T[K]>>,
        options?: SubscriptionOptions
    ): ReadonlyArray<symbol> {
        const ids = this.#emitter.batchSubscribe(event, callbacks, options);

        for (let index = 0; index < ids.length; index++) {
            const id = ids[index]!;
            const callback = callbacks[index];

            if (callback) {
                this.#trackSubscription(id, String(event), callback);
            }
        }

        return ids;
    }

    batchUnsubscribe(subscriptionIds: ReadonlyArray<symbol>): number {
        let count = 0;

        for (const subscriptionId of subscriptionIds) {
            if (this.#unsubscribeTracked(subscriptionId)) {
                count += 1;
            }
        }

        return count;
    }

    dispose(): void {
        if (this.#disposed) return;

        this.removeAllListeners();

        if (this.#ownsEmitter) {
            this.#emitter.dispose();
        }

        this.#subscriptions.clear();
        this.#tracked.clear();
        this.#disposed = true;
    }

    #trackSubscription(id: symbol, event: string, callback: EventCallback<any>): void {
        this.#subscriptions.add(id);
        this.#tracked.set(id, { event, callback });
    }

    #untrackSubscription(id: symbol): void {
        this.#subscriptions.delete(id);
        this.#tracked.delete(id);
    }

    #unsubscribeTracked(id: symbol): boolean {
        const wasTracked = this.#subscriptions.has(id);
        const removed = wasTracked ? this.#emitter.offById(id) : false;
        this.#untrackSubscription(id);
        return removed;
    }

    #pruneStaleSubscriptions(): void {
        for (const id of this.#subscriptions) {
            if (!this.#emitter.hasSubscription(id)) {
                this.#untrackSubscription(id);
            }
        }
    }

    #collectSubscriptionIds(
        event?: string,
        callback?: EventCallback<any>
    ): symbol[] {
        this.#pruneStaleSubscriptions();

        const ids: symbol[] = [];

        for (const [id, tracked] of this.#tracked.entries()) {
            if (event !== undefined && tracked.event !== event) {
                continue;
            }

            if (callback !== undefined && tracked.callback !== callback) {
                continue;
            }

            ids.push(id);
        }

        return ids;
    }
}
