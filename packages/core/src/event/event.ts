import { PriorityQueue } from '@axrone/utility';

interface PerformanceTimer {
    readonly now: () => number;
}

class FallbackPerformanceTimer implements PerformanceTimer {
    readonly now = (): number => Date.now();
}

class PerformanceProvider {
    private static instance?: PerformanceTimer;

    static getInstance(): PerformanceTimer {
        if (!PerformanceProvider.instance) {
            PerformanceProvider.instance = PerformanceProvider.createTimer();
        }
        return PerformanceProvider.instance;
    }

    private static createTimer(): PerformanceTimer {
        if (typeof window !== 'undefined' && window.performance && 'now' in window.performance) {
            return window.performance as PerformanceTimer;
        }

        if (typeof global !== 'undefined' && global.performance && 'now' in global.performance) {
            return global.performance as PerformanceTimer;
        }

        try {
            const perfHooks = require('perf_hooks');
            if (perfHooks?.performance && 'now' in perfHooks.performance) {
                return perfHooks.performance as PerformanceTimer;
            }
        } catch {}

        return new FallbackPerformanceTimer();
    }

    static reset(): void {
        PerformanceProvider.instance = undefined;
    }
}

const performance = PerformanceProvider.getInstance();

export type EventCallback<T> = (data: T) => void | Promise<void>;
export type UnsubscribeFn = () => boolean;
export type EventKey<T> = string & keyof T;
export type EventMap = Record<string, any>;
export type EventPriority = 'high' | 'normal' | 'low';

export type ExtractEventData<
    TEventMap extends EventMap,
    TEventKey extends keyof TEventMap,
> = TEventMap[TEventKey];

export type EventNames<T extends EventMap> = keyof T & string;

export type OptionalData<T> = T extends undefined ? T | void : T;

export function isValidEventName(eventName: unknown): eventName is string {
    return typeof eventName === 'string' && eventName.length > 0;
}

export function isValidCallback(callback: unknown): callback is EventCallback<any> {
    return typeof callback === 'function';
}

export function isValidPriority(priority: unknown): priority is EventPriority {
    return typeof priority === 'string' && ['high', 'normal', 'low'].includes(priority);
}

export const PRIORITY_VALUES: Record<EventPriority, number> = {
    high: 0,
    normal: 1,
    low: 2,
} as const;

export const DEFAULT_PRIORITY: EventPriority = 'normal';

export interface EventOptions {
    readonly captureRejections?: boolean;
    readonly maxListeners?: number;
    readonly weakReferences?: boolean;
    readonly immediateDispatch?: boolean;
    readonly concurrencyLimit?: number;
    readonly bufferSize?: number;
    readonly gcIntervalMs?: number;
}

export const DEFAULT_OPTIONS: Required<EventOptions> = Object.freeze({
    captureRejections: false,
    maxListeners: 10,
    weakReferences: false,
    immediateDispatch: true,
    concurrencyLimit: Infinity,
    bufferSize: 1000,
    gcIntervalMs: 60000,
} as const);

export const MEMORY_USAGE_SYMBOLS = Object.freeze({
    staticSubscriptions: Symbol('staticSubscriptions'),
    subscriptionMaps: Symbol('subscriptionMaps'),
    priorityQueues: Symbol('priorityQueues'),
    eventBuffer: Symbol('eventBuffer'),
} as const);

export abstract class BaseError extends Error {
    override readonly name: string;

    constructor(name: string, message: string, options?: ErrorOptions) {
        super(message, options);
        this.name = name;
        Object.setPrototypeOf(this, new.target.prototype);
        (Error as any).captureStackTrace?.(this, this.constructor);
    }
}

export class EventError extends BaseError {
    constructor(message: string, options?: ErrorOptions) {
        super('EventError', message, options);
    }
}

export class EventNotFoundError extends EventError {
    readonly eventName: string;

    constructor(eventName: string) {
        super(`Event "${eventName}" not found`);
        this.eventName = eventName;
    }
}

export class EventQueueFullError extends EventError {
    readonly eventName: string;

    constructor(eventName: string, bufferSize: number) {
        super(`Event queue for "${eventName}" is full (${bufferSize} items)`);
        this.eventName = eventName;
    }
}

export class EventHandlerError extends EventError {
    readonly originalError: unknown;
    readonly eventName: string;

    constructor(eventName: string, originalError: unknown) {
        const message =
            originalError instanceof Error ? originalError.message : String(originalError);
        super(`Handler error for "${eventName}": ${message}`);
        this.eventName = eventName;
        this.originalError = originalError;

        if (originalError instanceof Error && originalError.stack) {
            this.stack = `${this.stack}\nCaused by: ${originalError.stack}`;
        }
    }
}

export interface Subscription<T = any> {
    readonly id: symbol;
    readonly event: string;
    readonly callback: EventCallback<T>;
    readonly once: boolean;
    readonly priority: EventPriority;
    readonly createdAt: number;
    lastExecuted?: number;
    executionCount: number;
}

export interface SubscriptionOptions {
    readonly once?: boolean;
    readonly priority?: EventPriority;
}

export interface EventMetrics {
    readonly emit: {
        readonly count: number;
        readonly timing: {
            readonly avg: number;
            readonly max: number;
            readonly min: number;
            readonly total: number;
        };
    };
    readonly execution: {
        readonly count: number;
        readonly errors: number;
        readonly timing: {
            readonly avg: number;
            readonly max: number;
            readonly min: number;
            readonly total: number;
        };
    };
}

export interface QueuedEvent<T = any> {
    readonly id: number;
    readonly event: string;
    readonly data: T;
    readonly timestamp: number;
    readonly priority: EventPriority;
}

export interface IEventSubscriber<T extends EventMap = EventMap> {
    on<K extends EventKey<T>>(
        event: K,
        callback: EventCallback<T[K]>,
        options?: SubscriptionOptions
    ): UnsubscribeFn;

    once<K extends EventKey<T>>(
        event: K,
        callback: EventCallback<T[K]>,
        options?: Omit<SubscriptionOptions, 'once'>
    ): UnsubscribeFn;

    off<K extends EventKey<T>>(event: K, callback?: EventCallback<T[K]>): boolean;

    offById(subscriptionId: symbol): boolean;

    pipe<K extends EventKey<T>>(
        event: K,
        emitter: IEventPublisher<any>,
        targetEvent?: string
    ): UnsubscribeFn;
}

export interface IEventPublisher<T extends EventMap = EventMap> {
    emit<K extends EventKey<T>>(
        event: K,
        data: T[K],
        options?: { priority?: EventPriority }
    ): Promise<boolean>;

    emitSync<K extends EventKey<T>>(
        event: K,
        data: T[K],
        options?: { priority?: EventPriority }
    ): boolean;

    emitBatch<K extends EventKey<T>>(
        events: Array<{ event: K; data: T[K]; priority?: EventPriority }>
    ): Promise<boolean[]>;
}

export interface IEventBuffer<T extends EventMap = EventMap> {
    getQueuedEvents<K extends EventKey<T>>(event?: K): ReadonlyArray<QueuedEvent>;

    getPendingCount<K extends EventKey<T>>(event?: K): number;

    getBufferSize(): number;

    clearBuffer<K extends EventKey<T>>(event?: K): number;

    pause(): void;

    resume(): void;

    isPaused(): boolean;
}

export interface IEventObserver<T extends EventMap = EventMap> {
    has<K extends EventKey<T>>(event: K): boolean;

    listenerCount<K extends EventKey<T>>(event: K): number;

    maxListeners: number;

    listenerCountAll(): number;

    eventNames(): EventKey<T>[];

    getSubscriptions<K extends EventKey<T>>(event: K): ReadonlyArray<Subscription<T[K]>>;

    hasSubscription(subscriptionId: symbol): boolean;

    getMetrics<K extends EventKey<T>>(event: K): EventMetrics;

    getMemoryUsage(): Record<string, number>;
}

export interface IEventEmitter<T extends EventMap = EventMap>
    extends IEventSubscriber<T>,
        IEventPublisher<T>,
        IEventObserver<T>,
        IEventBuffer<T> {
    removeAllListeners<K extends EventKey<T>>(event?: K): this;

    batchSubscribe<K extends EventKey<T>>(
        event: K,
        callbacks: ReadonlyArray<EventCallback<T[K]>>,
        options?: SubscriptionOptions
    ): ReadonlyArray<symbol>;

    batchUnsubscribe(subscriptionIds: ReadonlyArray<symbol>): number;

    resetMaxListeners(): void;

    drain(): Promise<void>;

    flush<K extends EventKey<T>>(event: K): Promise<void>;

    resetMetrics<K extends EventKey<T>>(event?: K): void;

    dispose(): void;
}

export class EventScheduler {
    #concurrencyLimit: number;
    #activeCount = 0;
    #pendingPromises = new Set<Promise<void>>();
    #queue: Array<() => void> = [];

    constructor(concurrencyLimit: number = Infinity) {
        this.#concurrencyLimit = concurrencyLimit;
    }

    get activeCount(): number {
        return this.#activeCount;
    }

    get pendingCount(): number {
        return this.#queue.length;
    }

    schedule<T>(fn: () => Promise<T>): Promise<T> {
        if (this.#concurrencyLimit === Infinity) {
            const promise = fn();
            this.#pendingPromises.add(promise as Promise<any>);
            promise.finally(() => {
                this.#pendingPromises.delete(promise as Promise<any>);
            });
            return promise;
        }

        return new Promise<T>((resolve, reject) => {
            const execute = async (): Promise<void> => {
                this.#activeCount++;
                try {
                    const result = await fn();
                    resolve(result);
                } catch (error) {
                    reject(error);
                } finally {
                    this.#activeCount--;
                    this.#processQueue();
                }
            };

            if (this.#activeCount < this.#concurrencyLimit) {
                execute();
            } else {
                this.#queue.push(execute);
            }
        });
    }

    #processQueue(): void {
        if (this.#queue.length > 0 && this.#activeCount < this.#concurrencyLimit) {
            const nextTask = this.#queue.shift();
            if (nextTask) {
                nextTask();
            }
        }
    }

    async drain(): Promise<void> {
        while (this.#activeCount > 0 || this.#queue.length > 0) {
            await new Promise((resolve) => setTimeout(resolve, 1));
        }

        if (this.#concurrencyLimit === Infinity && this.#pendingPromises.size > 0) {
            await Promise.allSettled(Array.from(this.#pendingPromises));
        }
    }
}

export class EventEmitter<T extends EventMap = EventMap> implements IEventEmitter<T> {
    #subscriptions = new Map<string, Map<symbol, Subscription<any>>>();
    #options: Required<EventOptions>;
    #staticSubscriptionStorage = new Map<symbol, Subscription>();
    #weakSubscriptionStorage?: WeakMap<object, symbol[]>;
    #metrics = new Map<
        string,
        {
            emit: {
                count: number;
                timing: number[];
            };
            execution: {
                count: number;
                errors: number;
                timing: number[];
            };
        }
    >();
    #scheduler: EventScheduler;
    #eventQueues = new Map<string, PriorityQueue<QueuedEvent, number>>();
    #eventIdCounter = 0;
    #isPaused = false;
    #gcIntervalId?: ReturnType<typeof setInterval>;
    #lastGcTime = Date.now();

    constructor(options: EventOptions = {}) {
        this.#options = { ...DEFAULT_OPTIONS, ...options };

        if (this.#options.weakReferences) {
            this.#weakSubscriptionStorage = new WeakMap();
        }

        this.#scheduler = new EventScheduler(this.#options.concurrencyLimit);

        if (this.#options.gcIntervalMs > 0) {
            this.#startGc();
        }
    }

    get maxListeners(): number {
        return this.#options.maxListeners;
    }

    set maxListeners(value: number) {
        if (value < 0 || !Number.isInteger(value)) {
            throw new TypeError('maxListeners must be a non-negative integer');
        }
        this.#options = { ...this.#options, maxListeners: value };
    }

    resetMaxListeners(): void {
        this.#options = {
            ...this.#options,
            maxListeners: DEFAULT_OPTIONS.maxListeners,
        };
    }

    public on<K extends EventKey<T>>(
        event: K,
        callback: EventCallback<T[K]>,
        options: SubscriptionOptions = {}
    ): UnsubscribeFn {
        return this.#addListener(event, callback, {
            once: false,
            priority: 'normal',
            ...options,
        });
    }

    public once<K extends EventKey<T>>(
        event: K,
        callback: EventCallback<T[K]>,
        options: Omit<SubscriptionOptions, 'once'> = {}
    ): UnsubscribeFn {
        return this.#addListener(event, callback, {
            once: true,
            priority: 'normal',
            ...options,
        });
    }

    public pipe<K extends EventKey<T>>(
        event: K,
        emitter: IEventPublisher<any>,
        targetEvent?: string
    ): UnsubscribeFn {
        const actualTargetEvent = targetEvent || event;
        return this.on(event, (data) => {
            void emitter.emit(actualTargetEvent as any, data);
        });
    }

    public off<K extends EventKey<T>>(event: K, callback?: EventCallback<T[K]>): boolean {
        if (!this.#subscriptions.has(event)) {
            return false;
        }

        const subscriptionMap = this.#subscriptions.get(event)!;

        if (!callback) {
            for (const id of subscriptionMap.keys()) {
                this.#staticSubscriptionStorage.delete(id);
            }
            this.#subscriptions.delete(event);
            return subscriptionMap.size > 0;
        }

        let found = false;
        for (const [id, subscription] of subscriptionMap.entries()) {
            if (subscription.callback === callback) {
                subscriptionMap.delete(id);
                this.#staticSubscriptionStorage.delete(id);
                found = true;
            }
        }

        if (subscriptionMap.size === 0) {
            this.#subscriptions.delete(event);
        }

        return found;
    }

    public offById(subscriptionId: symbol): boolean {
        const subscription = this.#staticSubscriptionStorage.get(subscriptionId);
        if (!subscription) {
            return false;
        }

        const { event } = subscription;
        const subscriptionMap = this.#subscriptions.get(event);

        if (!subscriptionMap) {
            this.#staticSubscriptionStorage.delete(subscriptionId);
            return false;
        }

        const result = subscriptionMap.delete(subscriptionId);
        this.#staticSubscriptionStorage.delete(subscriptionId);

        if (subscriptionMap.size === 0) {
            this.#subscriptions.delete(event);
        }

        return result;
    }

    async #handleError(error: Error): Promise<void> {
        const errorEvent = 'error' as EventKey<T>;

        if (this.has(errorEvent)) {
            try {
                await this.emit(errorEvent, error as T[typeof errorEvent]);
            } catch (innerError) {
                console.error('Error in error handler:', innerError);
            }
        } else {
            throw error;
        }
    }

    #handleErrorSync(error: Error): void {
        const errorEvent = 'error' as EventKey<T>;

        if (this.has(errorEvent)) {
            try {
                this.emitSync(errorEvent, error as T[typeof errorEvent]);
            } catch (innerError) {
                console.error('Error in error handler:', innerError);
            }
        } else {
            throw error;
        }
    }

    public async emit<K extends EventKey<T>>(
        event: K,
        data: T[K],
        options: { priority?: EventPriority } = {}
    ): Promise<boolean> {
        const priority = options.priority || 'normal';
        const startTime = performance.now();

        try {
            if (this.#isPaused) {
                this.#addToQueue(event, data, priority);
                this.#updateMetrics(event, 'emit', 0);
                return true;
            }

            const subscriptionMap = this.#subscriptions.get(event);
            if (!subscriptionMap || subscriptionMap.size === 0) {
                this.#updateMetrics(event, 'emit', performance.now() - startTime);
                return false;
            }

            const subscriptions = [...subscriptionMap.values()].sort(
                (a, b) => PRIORITY_VALUES[a.priority] - PRIORITY_VALUES[b.priority]
            );

            const onceSubscriptions = subscriptions.filter((s) => s.once);
            for (const subscription of onceSubscriptions) {
                this.offById(subscription.id);
            }

            const executionPromises = subscriptions.map((subscription) =>
                this.#scheduler.schedule(async () => {
                    const execStartTime = performance.now();
                    subscription.executionCount++;
                    subscription.lastExecuted = Date.now();
                    const { callback } = subscription;

                    try {
                        await callback(data);
                        this.#updateMetrics(event, 'execution', performance.now() - execStartTime);
                    } catch (error) {
                        this.#updateMetrics(
                            event,
                            'execution',
                            performance.now() - execStartTime,
                            true
                        );

                        const shouldCaptureRejections = this.#options.captureRejections === true;

                        if (shouldCaptureRejections) {
                            try {
                                await this.#handleError(new EventHandlerError(event, error));
                                return;
                            } catch (handlerError) {
                                console.error('Failed to handle error:', handlerError);
                                return;
                            }
                        } else {
                            throw new EventHandlerError(event, error);
                        }
                    }
                })
            );

            if (this.#options.captureRejections === true) {
                await Promise.allSettled(executionPromises);
            } else {
                await Promise.all(executionPromises);
            }

            this.#updateMetrics(event, 'emit', performance.now() - startTime);
            return true;
        } catch (error) {
            this.#updateMetrics(event, 'emit', performance.now() - startTime);
            throw error;
        }
    }

    public emitSync<K extends EventKey<T>>(
        event: K,
        data: T[K],
        options: { priority?: EventPriority } = {}
    ): boolean {
        const startTime = performance.now();

        try {
            if (this.#isPaused) {
                this.#addToQueue(event, data, options.priority || 'normal');
                this.#updateMetrics(event, 'emit', 0);
                return true;
            }

            const subscriptionMap = this.#subscriptions.get(event);
            if (!subscriptionMap || subscriptionMap.size === 0) {
                this.#updateMetrics(event, 'emit', performance.now() - startTime);
                return false;
            }

            const subscriptions = [...subscriptionMap.values()].sort(
                (a, b) => PRIORITY_VALUES[a.priority] - PRIORITY_VALUES[b.priority]
            );

            const onceSubscriptions = subscriptions.filter((s) => s.once);
            for (const subscription of onceSubscriptions) {
                this.offById(subscription.id);
            }

            let hadAsyncCallbacks = false;

            for (const subscription of subscriptions) {
                const execStartTime = performance.now();
                subscription.executionCount++;
                subscription.lastExecuted = Date.now();
                const { callback } = subscription;

                try {
                    const result = callback(data);
                    if (result instanceof Promise) {
                        hadAsyncCallbacks = true;
                        result
                            .catch((error) => {
                                this.#updateMetrics(
                                    event,
                                    'execution',
                                    performance.now() - execStartTime,
                                    true
                                );

                                const shouldCaptureRejections =
                                    this.#options.captureRejections === true;

                                if (shouldCaptureRejections) {
                                    this.#handleErrorSync(new EventHandlerError(event, error));
                                } else {
                                    queueMicrotask(() => {
                                        throw new EventHandlerError(event, error);
                                    });
                                }
                            })
                            .then(() => {
                                this.#updateMetrics(
                                    event,
                                    'execution',
                                    performance.now() - execStartTime
                                );
                            });
                    } else {
                        this.#updateMetrics(event, 'execution', performance.now() - execStartTime);
                    }
                } catch (error) {
                    this.#updateMetrics(
                        event,
                        'execution',
                        performance.now() - execStartTime,
                        true
                    );

                    const shouldCaptureRejections = this.#options.captureRejections === true;

                    if (shouldCaptureRejections) {
                        this.#handleErrorSync(new EventHandlerError(event, error));
                    } else {
                        throw new EventHandlerError(event, error);
                    }
                }
            }

            if (hadAsyncCallbacks) {
                console.warn(
                    `EventEmitter: Event "${String(
                        event
                    )}" was emitted synchronously but had async listeners. Consider using emit() instead.`
                );
            }

            this.#updateMetrics(event, 'emit', performance.now() - startTime);
            return true;
        } catch (error) {
            this.#updateMetrics(event, 'emit', performance.now() - startTime);
            throw error;
        }
    }

    public async emitBatch<K extends EventKey<T>>(
        events: Array<{ event: K; data: T[K]; priority?: EventPriority }>
    ): Promise<boolean[]> {
        if (events.length === 0) return [];

        const results: Promise<boolean>[] = [];

        for (const { event, data, priority } of events) {
            results.push(this.emit(event, data, { priority }));
        }

        return Promise.all(results);
    }

    public has<K extends EventKey<T>>(event: K): boolean {
        const subscriptionMap = this.#subscriptions.get(event);
        return !!subscriptionMap && subscriptionMap.size > 0;
    }

    public hasSubscription(subscriptionId: symbol): boolean {
        return this.#staticSubscriptionStorage.has(subscriptionId);
    }

    public listenerCount<K extends EventKey<T>>(event: K): number {
        const subscriptionMap = this.#subscriptions.get(event);
        return subscriptionMap ? subscriptionMap.size : 0;
    }

    public listenerCountAll(): number {
        let count = 0;
        for (const subscriptionMap of this.#subscriptions.values()) {
            count += subscriptionMap.size;
        }
        return count;
    }

    public eventNames(): EventKey<T>[] {
        return Array.from(this.#subscriptions.keys()) as EventKey<T>[];
    }

    public getSubscriptions<K extends EventKey<T>>(event: K): ReadonlyArray<Subscription<T[K]>> {
        const subscriptionMap = this.#subscriptions.get(event);
        if (!subscriptionMap) {
            return [];
        }
        return Array.from(subscriptionMap.values()) as Subscription<T[K]>[];
    }

    public removeAllListeners<K extends EventKey<T>>(event?: K): this {
        if (event) {
            const subscriptionMap = this.#subscriptions.get(event);
            if (subscriptionMap) {
                for (const id of subscriptionMap.keys()) {
                    this.#staticSubscriptionStorage.delete(id);
                }
                this.#subscriptions.delete(event);
            }
        } else {
            this.#staticSubscriptionStorage.clear();
            this.#subscriptions.clear();
            if (this.#weakSubscriptionStorage) {
                this.#weakSubscriptionStorage = new WeakMap();
            }
        }
        return this;
    }

    public batchSubscribe<K extends EventKey<T>>(
        event: K,
        callbacks: ReadonlyArray<EventCallback<T[K]>>,
        options: SubscriptionOptions = {}
    ): ReadonlyArray<symbol> {
        const subscriptionIds: symbol[] = [];

        for (const callback of callbacks) {
            const unsubscribe = this.on(event, callback, options);
            const subscription = this.getSubscriptions(event).find((s) => s.callback === callback);
            if (subscription) {
                subscriptionIds.push(subscription.id);
            }
        }

        return subscriptionIds;
    }

    public batchUnsubscribe(subscriptionIds: ReadonlyArray<symbol>): number {
        let count = 0;
        for (const id of subscriptionIds) {
            if (this.offById(id)) {
                count++;
            }
        }
        return count;
    }

    public getQueuedEvents<K extends EventKey<T>>(event?: K): ReadonlyArray<QueuedEvent> {
        if (event) {
            const queue = this.#eventQueues.get(event);
            return queue ? queue.toArray() : [];
        }

        const allEvents: QueuedEvent[] = [];
        for (const queue of this.#eventQueues.values()) {
            allEvents.push(...queue.toArray());
        }

        return allEvents.sort((a, b) => {
            const priorityDiff = PRIORITY_VALUES[a.priority] - PRIORITY_VALUES[b.priority];
            if (priorityDiff !== 0) return priorityDiff;
            return a.timestamp - b.timestamp;
        });
    }

    public getPendingCount<K extends EventKey<T>>(event?: K): number {
        if (event) {
            const queue = this.#eventQueues.get(event);
            return queue ? queue.size : 0;
        }

        let total = 0;
        for (const queue of this.#eventQueues.values()) {
            total += queue.size;
        }
        return total;
    }

    public getBufferSize(): number {
        return this.#options.bufferSize;
    }

    public clearBuffer<K extends EventKey<T>>(event?: K): number {
        if (event) {
            const queue = this.#eventQueues.get(event);
            if (!queue) return 0;
            const size = queue.size;
            queue.clear();
            return size;
        }

        let total = 0;
        for (const [eventName, queue] of this.#eventQueues.entries()) {
            total += queue.size;
            queue.clear();
        }
        this.#eventQueues.clear();
        return total;
    }

    public pause(): void {
        this.#isPaused = true;
    }

    public resume(): void {
        if (!this.#isPaused) return;
        this.#isPaused = false;
        this.#processQueues();
    }

    public isPaused(): boolean {
        return this.#isPaused;
    }

    public async drain(): Promise<void> {
        await this.#scheduler.drain();

        if (!this.#isPaused) {
            await this.#processQueues();
        }
    }

    public async flush<K extends EventKey<T>>(event: K): Promise<void> {
        if (!this.#eventQueues.has(event)) return;

        const queue = this.#eventQueues.get(event)!;
        const queuedEvents = queue.toArray();
        queue.clear();

        const wasPaused = this.#isPaused;
        this.#isPaused = false;

        try {
            for (const queuedEvent of queuedEvents) {
                await this.emit(event, queuedEvent.data as T[K], {
                    priority: queuedEvent.priority,
                });
            }
        } finally {
            this.#isPaused = wasPaused;
        }
    }

    public getMetrics<K extends EventKey<T>>(event: K): EventMetrics {
        const metrics = this.#metrics.get(event) || {
            emit: { count: 0, timing: [] },
            execution: { count: 0, errors: 0, timing: [] },
        };

        const emitTimings = metrics.emit.timing;
        const executionTimings = metrics.execution.timing;

        return {
            emit: {
                count: metrics.emit.count,
                timing: {
                    avg: emitTimings.length
                        ? emitTimings.reduce((a, b) => a + b, 0) / emitTimings.length
                        : 0,
                    max: emitTimings.length ? Math.max(...emitTimings) : 0,
                    min: emitTimings.length ? Math.min(...emitTimings) : 0,
                    total: emitTimings.reduce((a, b) => a + b, 0),
                },
            },
            execution: {
                count: metrics.execution.count,
                errors: metrics.execution.errors,
                timing: {
                    avg: executionTimings.length
                        ? executionTimings.reduce((a, b) => a + b, 0) / executionTimings.length
                        : 0,
                    max: executionTimings.length ? Math.max(...executionTimings) : 0,
                    min: executionTimings.length ? Math.min(...executionTimings) : 0,
                    total: executionTimings.reduce((a, b) => a + b, 0),
                },
            },
        };
    }

    public resetMetrics<K extends EventKey<T>>(event?: K): void {
        if (event) {
            this.#metrics.delete(event);
        } else {
            this.#metrics.clear();
        }
    }

    public getMemoryUsage(): Record<string, number> {
        const calcSize = (obj: any): number => {
            if (obj === null || obj === undefined) return 0;

            let bytes = 0;

            if (typeof obj === 'object') {
                if (obj instanceof Map) {
                    bytes = 64;
                    for (const [key, value] of obj.entries()) {
                        bytes += calcSize(key) + calcSize(value);
                    }
                } else if (obj instanceof Set) {
                    bytes = 40;
                    for (const item of obj) {
                        bytes += calcSize(item);
                    }
                } else if (obj instanceof Array) {
                    bytes = 40 + 8 * obj.length;
                    for (const item of obj) {
                        bytes += calcSize(item);
                    }
                } else if (obj instanceof PriorityQueue) {
                    bytes = 48;
                    bytes += calcSize(obj.toArray());
                } else {
                    bytes = 40;
                    for (const key in obj) {
                        if (Object.prototype.hasOwnProperty.call(obj, key)) {
                            bytes += calcSize(key) + calcSize(obj[key]);
                        }
                    }
                }
            } else if (typeof obj === 'string') {
                bytes = 2 * obj.length + 24;
            } else if (typeof obj === 'number') {
                bytes = 8;
            } else if (typeof obj === 'boolean') {
                bytes = 4;
            } else if (typeof obj === 'symbol') {
                bytes = 16;
            }

            return bytes;
        };

        return {
            [MEMORY_USAGE_SYMBOLS.staticSubscriptions]: calcSize(this.#staticSubscriptionStorage),
            [MEMORY_USAGE_SYMBOLS.subscriptionMaps]: calcSize(this.#subscriptions),
            [MEMORY_USAGE_SYMBOLS.priorityQueues]: calcSize(this.#eventQueues),
            [MEMORY_USAGE_SYMBOLS.eventBuffer]: Array.from(this.#eventQueues.values()).reduce(
                (total, queue) => total + queue.size,
                0
            ),
            total:
                calcSize(this.#staticSubscriptionStorage) +
                calcSize(this.#subscriptions) +
                calcSize(this.#eventQueues) +
                calcSize(this.#metrics),
        };
    }

    #addListener<K extends EventKey<T>>(
        event: K,
        callback: EventCallback<T[K]>,
        options: Required<SubscriptionOptions>
    ): UnsubscribeFn {
        if (!this.#subscriptions.has(event)) {
            this.#subscriptions.set(event, new Map());
        }

        const subscriptionMap = this.#subscriptions.get(event)!;

        if (
            this.#options.maxListeners !== Infinity &&
            subscriptionMap.size >= this.#options.maxListeners
        ) {
            console.warn(
                `MaxListenersExceededWarning: Possible memory leak detected. ${
                    subscriptionMap.size
                } listeners added to event "${String(event)}".`
            );
        }

        const id = Symbol();
        const subscription: Subscription<T[K]> = {
            id,
            event,
            callback,
            once: options.once,
            priority: options.priority,
            executionCount: 0,
            createdAt: Date.now(),
        };

        subscriptionMap.set(id, subscription as Subscription);
        this.#staticSubscriptionStorage.set(id, subscription as Subscription);

        if (this.#weakSubscriptionStorage && typeof callback === 'object') {
            const existingIds = this.#weakSubscriptionStorage.get(callback) || [];
            this.#weakSubscriptionStorage.set(callback, [...existingIds, id]);
        }

        return () => this.offById(id);
    }

    #addToQueue<K extends EventKey<T>>(event: K, data: T[K], priority: EventPriority): void {
        if (!this.#eventQueues.has(event)) {
            this.#eventQueues.set(
                event,
                PriorityQueue.withComparator<QueuedEvent, number>((a, b) => a - b)
            );
        }

        const queue = this.#eventQueues.get(event)!;

        if (queue.size >= this.#options.bufferSize) {
            throw new EventQueueFullError(event, this.#options.bufferSize);
        }

        const eventId = this.#eventIdCounter++;
        const queuedEvent: QueuedEvent = {
            id: eventId,
            event,
            data,
            timestamp: Date.now(),
            priority,
        };

        const priorityValue = PRIORITY_VALUES[priority] * 1000000000 + Date.now();
        queue.enqueue(queuedEvent, priorityValue);
    }

    async #processQueues(): Promise<void> {
        if (this.#isPaused) return;

        const allEvents = this.getQueuedEvents();

        this.clearBuffer();

        for (const queuedEvent of allEvents) {
            await this.emit(queuedEvent.event as EventKey<T>, queuedEvent.data as T[EventKey<T>], {
                priority: queuedEvent.priority,
            });
        }
    }

    #updateMetrics<K extends EventKey<T>>(
        event: K,
        type: 'emit' | 'execution',
        duration: number,
        isError = false
    ): void {
        if (!this.#metrics.has(event)) {
            this.#metrics.set(event, {
                emit: { count: 0, timing: [] },
                execution: { count: 0, errors: 0, timing: [] },
            });
        }

        const metrics = this.#metrics.get(event)!;

        if (type === 'emit') {
            metrics.emit.count++;
            metrics.emit.timing.push(duration);

            if (metrics.emit.timing.length > 100) {
                metrics.emit.timing = metrics.emit.timing.slice(-100);
            }
        } else {
            metrics.execution.count++;
            if (isError) {
                metrics.execution.errors++;
            }
            metrics.execution.timing.push(duration);

            if (metrics.execution.timing.length > 100) {
                metrics.execution.timing = metrics.execution.timing.slice(-100);
            }
        }
    }

    #startGc(): void {
        if (this.#gcIntervalId) {
            clearInterval(this.#gcIntervalId);
        }

        this.#gcIntervalId = setInterval(() => {
            this.#runGc();
        }, this.#options.gcIntervalMs);

        if (
            typeof this.#gcIntervalId === 'object' &&
            this.#gcIntervalId !== null &&
            'unref' in this.#gcIntervalId
        ) {
            (this.#gcIntervalId as any).unref();
        }
    }

    #runGc(): void {
        this.#lastGcTime = Date.now();

        if (this.#options.weakReferences) {
            return;
        }

        const existingEvents = new Set(this.eventNames());
        for (const event of this.#metrics.keys()) {
            if (!existingEvents.has(event as any)) {
                this.#metrics.delete(event);
            }
        }

        for (const [event, queue] of this.#eventQueues.entries()) {
            if (queue.size === 0) {
                this.#eventQueues.delete(event);
            }
        }
    }

    dispose(): void {
        if (this.#gcIntervalId) {
            clearInterval(this.#gcIntervalId);
        }

        this.removeAllListeners();
        this.clearBuffer();
        this.#metrics.clear();
    }
}

export type EventMapOf<E> = E extends IEventEmitter<infer M> ? M : EventMap;

export type FilteredEventMap<M extends EventMap, K extends keyof M> = Pick<M, K & string>;

export type NamespacedEventMap<P extends string, M extends EventMap> = {
    [K in keyof M as `${P}:${string & K}`]: M[K];
};

export type MergedEventMap<Maps extends EventMap[]> = Maps extends [infer First, ...infer Rest]
    ? First extends EventMap
        ? Rest extends EventMap[]
            ? First & MergedEventMap<Rest>
            : First
        : {}
    : {};

export type EventTransformer<SrcMap extends EventMap, DestMap extends EventMap> = {
    [K in keyof SrcMap]?: (data: SrcMap[K]) => DestMap[keyof DestMap];
};

export type ExcludeEventsMap<M extends EventMap, K extends keyof M> = Pick<M, Exclude<keyof M, K>>;

export function createEmitter<T extends EventMap = EventMap>(
    options?: EventOptions
): IEventEmitter<T> {
    return new EventEmitter<T>(options);
}

export function createTypedEmitter<T extends EventMap>(): IEventEmitter<T> {
    return new EventEmitter<T>();
}

export function isEventEmitter(value: unknown): value is IEventEmitter {
    return (
        value !== null &&
        typeof value === 'object' &&
        typeof (value as any).on === 'function' &&
        typeof (value as any).emit === 'function' &&
        typeof (value as any).off === 'function'
    );
}

export function filterEvents<T extends EventMap, K extends keyof T & string>(
    source: IEventEmitter<T>,
    allowedEvents: ReadonlyArray<K>,
    options?: {
        passthroughErrors?: boolean;
    }
): IEventEmitter<FilteredEventMap<T, K>> {
    const target = new EventEmitter<FilteredEventMap<T, K>>();
    const unsubscribers: UnsubscribeFn[] = [];
    const allowedEventsSet = new Set(allowedEvents);

    if (options?.passthroughErrors && !allowedEventsSet.has('error' as any)) {
        allowedEventsSet.add('error' as any);
    }

    for (const event of allowedEventsSet) {
        unsubscribers.push(source.on(event, (data) => void target.emit(event, data)));
    }

    const originalEmit = target.emit.bind(target);
    target.emit = async function <E extends keyof FilteredEventMap<T, K> & string>(
        event: E,
        data: FilteredEventMap<T, K>[E],
        options?: { priority?: EventPriority }
    ): Promise<boolean> {
        if (!allowedEventsSet.has(event as K)) {
            return false;
        }
        return originalEmit(event, data, options);
    };

    const originalEmitSync = target.emitSync.bind(target);
    target.emitSync = function <E extends keyof FilteredEventMap<T, K> & string>(
        event: E,
        data: FilteredEventMap<T, K>[E],
        options?: { priority?: EventPriority }
    ): boolean {
        if (!allowedEventsSet.has(event as K)) {
            return false;
        }
        return originalEmitSync(event, data, options);
    };

    const originalRemoveAllListeners = target.removeAllListeners.bind(target);
    target.removeAllListeners = function <E extends keyof FilteredEventMap<T, K> & string>(
        event?: E
    ): EventEmitter<FilteredEventMap<T, K>> {
        if (event === undefined) {
            unsubscribers.forEach((unsub) => unsub());
        }
        return originalRemoveAllListeners(event) as EventEmitter<FilteredEventMap<T, K>>;
    };

    (target as any).dispose = () => {
        unsubscribers.forEach((unsub) => unsub());
    };

    return target;
}

export function excludeEvents<T extends EventMap, K extends keyof T & string>(
    source: IEventEmitter<T>,
    excludedEvents: ReadonlyArray<K>
): IEventEmitter<ExcludeEventsMap<T, K>> {
    const target = new EventEmitter<ExcludeEventsMap<T, K>>();
    const excludedEventsSet = new Set(excludedEvents);
    const unsubscribers: UnsubscribeFn[] = [];
    const forwardedEvents = new Set<string>();

    const setupForwarding = (event: string) => {
        if (!excludedEventsSet.has(event as any) && !forwardedEvents.has(event)) {
            forwardedEvents.add(event);
            unsubscribers.push(
                source.on(event as any, (data) => void target.emit(event as any, data))
            );
        }
    };

    source.eventNames().forEach(setupForwarding);

    const originalTargetOn = target.on.bind(target);
    target.on = function <E extends keyof ExcludeEventsMap<T, K> & string>(
        event: E,
        callback: EventCallback<ExcludeEventsMap<T, K>[E]>,
        options?: { priority?: EventPriority }
    ): UnsubscribeFn {
        setupForwarding(event);
        return originalTargetOn(event, callback, options);
    };

    (target as any).dispose = () => {
        unsubscribers.forEach((unsub) => unsub());
    };

    return target as IEventEmitter<ExcludeEventsMap<T, K>>;
}

export function createEventProxy<SrcMap extends EventMap, DestMap extends EventMap>(
    source: IEventEmitter<SrcMap>,
    target: IEventEmitter<DestMap>,
    mapping: Readonly<Partial<Record<keyof SrcMap & string, keyof DestMap & string>>>,
    transformers?: EventTransformer<SrcMap, DestMap>,
    options?: {
        preservePriority?: boolean;
        bidirectional?: boolean;
    }
): UnsubscribeFn {
    const unsubscribers: UnsubscribeFn[] = [];
    const proxyingEvents = new Set<string>();

    const currentPriorities = new Map<string, EventPriority>();

    if (options?.preservePriority) {
        const originalEmit = source.emit.bind(source);
        source.emit = async function <K extends keyof SrcMap & string>(
            event: K,
            data: SrcMap[K],
            emitOptions?: { priority?: EventPriority }
        ): Promise<boolean> {
            const priority = emitOptions?.priority || 'normal';
            currentPriorities.set(event, priority);
            try {
                return await originalEmit(event, data, emitOptions);
            } finally {
                setTimeout(() => currentPriorities.delete(event), 0);
            }
        };
    }

    for (const sourceEvent of Object.keys(mapping) as Array<keyof SrcMap & string>) {
        const targetEvent = mapping[sourceEvent]!;

        unsubscribers.push(
            source.on(sourceEvent, (data: SrcMap[typeof sourceEvent]) => {
                const proxyKey = `src->${sourceEvent}->${targetEvent}`;
                if (proxyingEvents.has(proxyKey)) {
                    return;
                }

                proxyingEvents.add(proxyKey);
                try {
                    const priority: EventPriority | undefined = options?.preservePriority
                        ? currentPriorities.get(sourceEvent) || 'normal'
                        : undefined;

                    if (transformers && sourceEvent in transformers) {
                        const transform = transformers[sourceEvent]!;
                        const transformedData = transform(data);
                        void target.emit(targetEvent, transformedData as any, { priority });
                        return;
                    }

                    void target.emit(targetEvent, data as any, { priority });
                } finally {
                    proxyingEvents.delete(proxyKey);
                }
            })
        );
    }

    if (options?.bidirectional) {
        const reverseMapping: Record<string, string> = {};
        for (const [src, dest] of Object.entries(mapping)) {
            if (dest) {
                reverseMapping[dest] = src;
            }
        }

        const reverseTransformers: EventTransformer<DestMap, SrcMap> = {};

        for (const targetEvent of Object.keys(reverseMapping) as Array<keyof DestMap & string>) {
            const sourceEvent = reverseMapping[targetEvent] as keyof SrcMap & string;

            unsubscribers.push(
                target.on(targetEvent, (data: DestMap[typeof targetEvent]) => {
                    const proxyKey = `dest->${targetEvent}->${sourceEvent}`;
                    if (proxyingEvents.has(proxyKey)) {
                        return;
                    }

                    proxyingEvents.add(proxyKey);
                    try {
                        const priority: EventPriority | undefined = options?.preservePriority
                            ? 'normal'
                            : undefined;

                        if (reverseTransformers && targetEvent in reverseTransformers) {
                            const transform = reverseTransformers[targetEvent]!;
                            const transformedData = transform(data);
                            void source.emit(sourceEvent, transformedData as any, {
                                priority,
                            });
                            return;
                        }

                        void source.emit(sourceEvent, data as any, { priority });
                    } finally {
                        proxyingEvents.delete(proxyKey);
                    }
                })
            );
        }
    }

    return () => {
        let result = true;
        for (const unsub of unsubscribers) {
            if (!unsub()) {
                result = false;
            }
        }
        return result;
    };
}

export function mergeEmitters<T extends ReadonlyArray<IEventEmitter<any>>>(
    ...emitters: T
): IEventEmitter<MergedEventMap<[...{ [K in keyof T]: EventMapOf<T[K]> }]>> {
    const merged = new EventEmitter<any>();
    const unsubscribers: UnsubscribeFn[] = [];

    for (const emitter of emitters) {
        const originalEmit = emitter.emit.bind(emitter);

        (emitter as any).emit = async function (event: any, data: any, options?: any) {
            const result = await originalEmit(event, data, options);
            void merged.emit(event, data, options);
            return result;
        };

        (emitter as any)._originalEmit = originalEmit;
    }

    const originalRemoveAllListeners = merged.removeAllListeners.bind(merged);
    merged.removeAllListeners = function <E extends string>(event?: E) {
        if (event === undefined) {
            unsubscribers.forEach((unsub) => unsub());
        }
        return originalRemoveAllListeners(event);
    };

    (merged as any).dispose = () => {
        unsubscribers.forEach((unsub) => unsub());

        for (const emitter of emitters) {
            if ((emitter as any)._originalEmit) {
                (emitter as any).emit = (emitter as any)._originalEmit;
                delete (emitter as any)._originalEmit;
            }
        }
    };

    return merged as unknown as IEventEmitter<
        MergedEventMap<[...{ [K in keyof T]: EventMapOf<T[K]> }]>
    >;
}

export function namespaceEvents<Prefix extends string, T extends EventMap>(
    prefix: Prefix,
    source: IEventEmitter<T> = new EventEmitter<T>()
): IEventEmitter<NamespacedEventMap<Prefix, T>> {
    const namespaced = new EventEmitter<NamespacedEventMap<Prefix, T>>();
    const unsubscribers: UnsubscribeFn[] = [];

    const resolveSourceEvent = <K extends keyof NamespacedEventMap<Prefix, T> & string>(
        event: K
    ): keyof T & string => {
        const prefixStr = `${prefix}:`;
        if (!event.startsWith(prefixStr)) {
            throw new EventError(`Event "${event}" must start with namespace "${prefixStr}"`);
        }
        return event.slice(prefixStr.length) as keyof T & string;
    };

    const createNamespacedEvent = <K extends keyof T & string>(
        event: K
    ): keyof NamespacedEventMap<Prefix, T> & string => {
        return `${prefix}:${event}` as any;
    };

    const originalOn = namespaced.on.bind(namespaced);
    namespaced.on = function <K extends keyof NamespacedEventMap<Prefix, T> & string>(
        event: K,
        callback: EventCallback<NamespacedEventMap<Prefix, T>[K]>,
        options?: SubscriptionOptions
    ): UnsubscribeFn {
        const sourceEvent = resolveSourceEvent(event);
        return source.on(sourceEvent, callback as any, options);
    };

    const originalEmit = namespaced.emit.bind(namespaced);
    namespaced.emit = function <K extends keyof NamespacedEventMap<Prefix, T> & string>(
        event: K,
        data: NamespacedEventMap<Prefix, T>[K],
        options?: { priority?: EventPriority }
    ): Promise<boolean> {
        const sourceEvent = resolveSourceEvent(event);
        return source.emit(sourceEvent, data as any, options);
    };

    for (const event of source.eventNames()) {
        const namespacedEvent = createNamespacedEvent(event) as keyof NamespacedEventMap<
            Prefix,
            T
        > &
            string;
        unsubscribers.push(
            source.on(event as any, (data: any) => {
                void namespaced.emit(namespacedEvent, data);
            })
        );
    }

    (namespaced as any).dispose = () => {
        unsubscribers.forEach((unsub) => unsub());
    };

    return namespaced;
}

export class TypedEventRegistry<T extends EventMap> {
    readonly #registry = new Map<keyof T & string, symbol>();
    readonly #symbolToEvent = new Map<symbol, keyof T & string>();

    register<K extends keyof T & string>(event: K): symbol {
        if (this.#registry.has(event)) {
            return this.#registry.get(event)!;
        }
        const symbol = Symbol(event);
        this.#registry.set(event, symbol);
        this.#symbolToEvent.set(symbol, event);
        return symbol;
    }

    getSymbol<K extends keyof T & string>(event: K): symbol | undefined {
        return this.#registry.get(event);
    }

    getEvent(symbol: symbol): (keyof T & string) | undefined {
        return this.#symbolToEvent.get(symbol);
    }

    has<K extends keyof T & string>(event: K): boolean {
        return this.#registry.has(event);
    }

    hasSymbol(symbol: symbol): boolean {
        return this.#symbolToEvent.has(symbol);
    }

    events(): Array<keyof T & string> {
        return Array.from(this.#registry.keys());
    }

    symbols(): Array<symbol> {
        return Array.from(this.#symbolToEvent.keys());
    }

    entries(): Array<[keyof T & string, symbol]> {
        return Array.from(this.#registry.entries());
    }

    clear(): void {
        this.#registry.clear();
        this.#symbolToEvent.clear();
    }
}

export class EventGroup<T extends EventMap> implements IEventEmitter<T> {
    readonly #emitter: IEventEmitter<T>;
    readonly #subscriptions: Set<symbol> = new Set();

    constructor(baseEmitter?: IEventEmitter<T>) {
        this.#emitter = baseEmitter || new EventEmitter<T>();
    }

    get maxListeners(): number {
        return this.#emitter.maxListeners;
    }

    set maxListeners(value: number) {
        this.#emitter.maxListeners = value;
    }

    on<K extends EventKey<T>>(
        event: K,
        callback: EventCallback<T[K]>,
        options?: SubscriptionOptions
    ): UnsubscribeFn {
        const unsubscribe = this.#emitter.on(event, callback, options);
        const subscription = this.#emitter
            .getSubscriptions(event)
            .find((s) => s.callback === callback);

        if (subscription) {
            this.#subscriptions.add(subscription.id);
        }

        return () => {
            const result = unsubscribe();
            if (subscription) {
                this.#subscriptions.delete(subscription.id);
            }
            return result;
        };
    }

    once<K extends EventKey<T>>(
        event: K,
        callback: EventCallback<T[K]>,
        options?: Omit<SubscriptionOptions, 'once'>
    ): UnsubscribeFn {
        const unsubscribe = this.#emitter.once(event, callback, options);
        const subscription = this.#emitter
            .getSubscriptions(event)
            .find((s) => s.callback === callback);

        if (subscription) {
            this.#subscriptions.add(subscription.id);
        }

        const wrappedUnsubscribe = () => {
            const result = unsubscribe();
            if (subscription) {
                this.#subscriptions.delete(subscription.id);
            }
            return result;
        };

        const wrappedCallback: EventCallback<T[K]> = (data) => {
            if (subscription) {
                this.#subscriptions.delete(subscription.id);
            }
            return callback(data);
        };

        return wrappedUnsubscribe;
    }

    off<K extends EventKey<T>>(event: K, callback?: EventCallback<T[K]>): boolean {
        if (callback) {
            const subscription = this.#emitter
                .getSubscriptions(event)
                .find((s) => s.callback === callback);
            if (subscription) {
                this.#subscriptions.delete(subscription.id);
            }
        } else {
            for (const subscription of this.#emitter.getSubscriptions(event)) {
                this.#subscriptions.delete(subscription.id);
            }
        }

        return this.#emitter.off(event, callback);
    }

    offById(subscriptionId: symbol): boolean {
        const result = this.#emitter.offById(subscriptionId);
        if (result) {
            this.#subscriptions.delete(subscriptionId);
        }
        return result;
    }

    pipe<K extends EventKey<T>>(
        event: K,
        emitter: IEventPublisher<any>,
        targetEvent?: string
    ): UnsubscribeFn {
        return this.on(
            event,
            (data) => void emitter.emit((targetEvent as any) || (event as any), data)
        );
    }

    emit<K extends EventKey<T>>(
        event: K,
        data: T[K],
        options?: { priority?: EventPriority }
    ): Promise<boolean> {
        return this.#emitter.emit(event, data, options);
    }

    emitSync<K extends EventKey<T>>(
        event: K,
        data: T[K],
        options?: { priority?: EventPriority }
    ): boolean {
        return this.#emitter.emitSync(event, data, options);
    }

    emitBatch<K extends EventKey<T>>(
        events: Array<{ event: K; data: T[K]; priority?: EventPriority }>
    ): Promise<boolean[]> {
        return this.#emitter.emitBatch(events);
    }

    has<K extends EventKey<T>>(event: K): boolean {
        return this.#emitter.has(event);
    }

    listenerCount<K extends EventKey<T>>(event: K): number {
        return this.#emitter.listenerCount(event);
    }

    listenerCountAll(): number {
        return this.#emitter.listenerCountAll();
    }

    eventNames(): EventKey<T>[] {
        return this.#emitter.eventNames();
    }

    getSubscriptions<K extends EventKey<T>>(event: K): ReadonlyArray<Subscription<T[K]>> {
        return this.#emitter.getSubscriptions(event).filter((s) => this.#subscriptions.has(s.id));
    }

    hasSubscription(subscriptionId: symbol): boolean {
        return (
            this.#subscriptions.has(subscriptionId) && this.#emitter.hasSubscription(subscriptionId)
        );
    }

    getMetrics<K extends EventKey<T>>(event: K): EventMetrics {
        return this.#emitter.getMetrics(event);
    }

    getMemoryUsage(): Record<string, number> {
        return this.#emitter.getMemoryUsage();
    }

    getQueuedEvents<K extends EventKey<T>>(event?: K): ReadonlyArray<QueuedEvent> {
        return this.#emitter.getQueuedEvents(event);
    }

    getPendingCount<K extends EventKey<T>>(event?: K): number {
        return this.#emitter.getPendingCount(event);
    }

    getBufferSize(): number {
        return this.#emitter.getBufferSize();
    }

    clearBuffer<K extends EventKey<T>>(event?: K): number {
        return this.#emitter.clearBuffer(event);
    }

    pause(): void {
        this.#emitter.pause();
    }

    resume(): void {
        this.#emitter.resume();
    }

    isPaused(): boolean {
        return this.#emitter.isPaused();
    }

    removeAllListeners<K extends EventKey<T>>(event?: K): this {
        if (event) {
            for (const subscription of this.#emitter.getSubscriptions(event)) {
                this.#subscriptions.delete(subscription.id);
            }
        } else {
            this.#subscriptions.clear();
        }

        this.#emitter.removeAllListeners(event);
        return this;
    }

    batchSubscribe<K extends EventKey<T>>(
        event: K,
        callbacks: ReadonlyArray<EventCallback<T[K]>>,
        options?: SubscriptionOptions
    ): ReadonlyArray<symbol> {
        const ids = this.#emitter.batchSubscribe(event, callbacks, options);

        for (const id of ids) {
            this.#subscriptions.add(id);
        }

        return ids;
    }

    batchUnsubscribe(subscriptionIds: ReadonlyArray<symbol>): number {
        const count = this.#emitter.batchUnsubscribe(subscriptionIds);

        for (const id of subscriptionIds) {
            this.#subscriptions.delete(id);
        }

        return count;
    }

    resetMaxListeners(): void {
        this.#emitter.resetMaxListeners();
    }

    async drain(): Promise<void> {
        return this.#emitter.drain();
    }

    async flush<K extends EventKey<T>>(event: K): Promise<void> {
        return this.#emitter.flush(event);
    }

    resetMetrics<K extends EventKey<T>>(event?: K): void {
        this.#emitter.resetMetrics(event);
    }

    dispose(): void {
        for (const id of this.#subscriptions) {
            this.#emitter.offById(id);
        }
        this.#subscriptions.clear();
    }
}

export function createHooks<T extends EventMap>(): {
    on: <K extends EventKey<T>>(
        event: K,
        callback: EventCallback<T[K]>,
        options?: SubscriptionOptions
    ) => UnsubscribeFn;
    once: <K extends EventKey<T>>(
        event: K,
        callback: EventCallback<T[K]>,
        options?: Omit<SubscriptionOptions, 'once'>
    ) => UnsubscribeFn;
    off: <K extends EventKey<T>>(event: K, callback?: EventCallback<T[K]>) => boolean;
    emit: <K extends EventKey<T>>(
        event: K,
        data: T[K],
        options?: { priority?: EventPriority }
    ) => Promise<boolean>;
    emitSync: <K extends EventKey<T>>(
        event: K,
        data: T[K],
        options?: { priority?: EventPriority }
    ) => boolean;
    useEmitter: () => IEventEmitter<T>;
} {
    const emitter = new EventEmitter<T>();

    return {
        on: <K extends EventKey<T>>(
            event: K,
            callback: EventCallback<T[K]>,
            options?: SubscriptionOptions
        ) => emitter.on(event, callback, options),
        once: <K extends EventKey<T>>(
            event: K,
            callback: EventCallback<T[K]>,
            options?: Omit<SubscriptionOptions, 'once'>
        ) => emitter.once(event, callback, options),
        off: <K extends EventKey<T>>(event: K, callback?: EventCallback<T[K]>) =>
            emitter.off(event, callback),
        emit: <K extends EventKey<T>>(
            event: K,
            data: T[K],
            options?: { priority?: EventPriority }
        ) => emitter.emit(event, data, options),
        emitSync: <K extends EventKey<T>>(
            event: K,
            data: T[K],
            options?: { priority?: EventPriority }
        ) => emitter.emitSync(event, data, options),
        useEmitter: () => emitter,
    };
}

export const EventUtils = {
    createKey: <T>(name: string): EventKey<{ [key: string]: T }> => name as any,

    toAsync: <T, R>(fn: (data: T) => R): ((data: T) => Promise<R>) => {
        return async (data: T) => fn(data);
    },

    debounce: <T>(callback: EventCallback<T>, wait: number): EventCallback<T> => {
        let timeout: ReturnType<typeof setTimeout> | null = null;
        let lastData: T;

        return (data: T) => {
            lastData = data;

            if (timeout !== null) {
                clearTimeout(timeout);
            }

            timeout = setTimeout(() => {
                timeout = null;
                callback(lastData);
            }, wait);
        };
    },

    throttle: <T>(callback: EventCallback<T>, limit: number): EventCallback<T> => {
        let inThrottle = false;
        let lastResult: Promise<void> | void;

        return (data: T) => {
            if (!inThrottle) {
                inThrottle = true;
                lastResult = callback(data);

                setTimeout(() => {
                    inThrottle = false;
                }, limit);
            }

            return lastResult instanceof Promise ? lastResult : Promise.resolve(lastResult);
        };
    },

    rateLimit: <T>(
        callback: EventCallback<T>,
        maxCalls: number,
        timeWindow: number
    ): EventCallback<T> => {
        const calls: number[] = [];

        return (data: T) => {
            const now = Date.now();

            while (calls.length > 0 && calls[0] <= now - timeWindow) {
                calls.shift();
            }

            if (calls.length < maxCalls) {
                calls.push(now);
                return callback(data);
            }

            return Promise.resolve();
        };
    },

    once: <T>(callback: EventCallback<T>): EventCallback<T> => {
        let called = false;
        let result: any;

        return (data: T) => {
            if (!called) {
                called = true;
                result = callback(data);
            }
            return result;
        };
    },

    compose: <T>(...callbacks: EventCallback<T>[]): EventCallback<T> => {
        return async (data: T) => {
            for (const callback of callbacks) {
                await callback(data);
            }
        };
    },

    filter: <T>(predicate: (data: T) => boolean, callback: EventCallback<T>): EventCallback<T> => {
        return (data: T) => {
            if (predicate(data)) {
                return callback(data);
            }
        };
    },

    map: <T, U>(transform: (data: T) => U, callback: EventCallback<U>): EventCallback<T> => {
        return (data: T) => {
            const transformed = transform(data);
            return callback(transformed);
        };
    },

    catchErrors: <T>(
        callback: EventCallback<T>,
        errorHandler: (error: unknown, data: T) => void
    ): EventCallback<T> => {
        return async (data: T) => {
            try {
                await callback(data);
            } catch (error) {
                errorHandler(error, data);
            }
        };
    },
};
