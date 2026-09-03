import { performance } from './internal/performance';
import {
    normalizeMaxListeners,
    normalizeConcurrency,
    normalizeBufferSize,
    normalizeGcInterval,
    normalizeBufferOverflow,
} from './internal/normalize';
import { isPromiseLike, toError, rethrowAsync, pipeToEmitter } from './internal/utils';
import {
    EventCallback,
    EventPriority,
    EventMap,
    EventKey,
    UnsubscribeFn,
    EventOptions,
    DEFAULT_OPTIONS,
    DEFAULT_PRIORITY,
    PRIORITY_VALUES,
    EventDispatchItem,
    EventDispatchResult,
} from './definition';
import { EventHandlerError, EventQueueFullError } from './errors';
import {
    IEventSubscriber,
    IEventPublisher,
    IEventObserver,
    IEventBuffer,
    SubscriptionOptions,
    Subscription,
    QueuedEvent,
    EventMetrics,
} from './interfaces';
import { EventScheduler, TaskPriority } from './event-scheduler';
import { EVENT_EMITTER_TAP, EventTap, EventTapContext } from './internals';

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

type InternalCallback<T> = EventCallback<T> | WeakRef<EventCallback<T>>;

interface InternalSubscription<T = unknown> {
    readonly id: symbol;
    readonly event: string;
    readonly once: boolean;
    readonly priority: EventPriority;
    readonly createdAt: number;
    readonly weak: boolean;
    readonly callback: InternalCallback<T>;
    readonly unregisterToken?: object;
    lastExecuted?: number;
    executionCount: number;
    disposed: boolean;
}

interface ListenerBucket {
    readonly high: InternalSubscription<any>[];
    readonly normal: InternalSubscription<any>[];
    readonly low: InternalSubscription<any>[];
    size: number;
}

interface BufferedBucket {
    readonly high: QueuedEvent<any>[];
    readonly normal: QueuedEvent<any>[];
    readonly low: QueuedEvent<any>[];
    size: number;
}

interface TimingAccumulator {
    count: number;
    total: number;
    min: number;
    max: number;
}

type EmitMode = 'sync' | 'async' | 'buffered';

interface MetricsAccumulator {
    emit: {
        timing: TimingAccumulator;
        sync: TimingAccumulator;
        async: TimingAccumulator;
        buffered: TimingAccumulator;
    };
    execution: TimingAccumulator & { errors: number };
}

const PRIORITY_TO_TASK_PRIORITY = Object.freeze({
    high: TaskPriority.HIGH,
    normal: TaskPriority.NORMAL,
    low: TaskPriority.LOW,
} satisfies Readonly<Record<EventPriority, TaskPriority>>);

const MAX_EMIT_DEPTH = 32;

function normalizeOptions(options: EventOptions): Required<EventOptions> {
    return {
        captureRejections:
            typeof options.captureRejections === 'boolean'
                ? options.captureRejections
                : DEFAULT_OPTIONS.captureRejections,
        maxListeners: normalizeMaxListeners(options.maxListeners, DEFAULT_OPTIONS.maxListeners),
        weakReferences:
            typeof options.weakReferences === 'boolean'
                ? options.weakReferences
                : DEFAULT_OPTIONS.weakReferences,
        concurrencyLimit: normalizeConcurrency(
            options.concurrencyLimit,
            DEFAULT_OPTIONS.concurrencyLimit
        ),
        bufferSize: normalizeBufferSize(options.bufferSize, DEFAULT_OPTIONS.bufferSize),
        gcIntervalMs: normalizeGcInterval(options.gcIntervalMs, DEFAULT_OPTIONS.gcIntervalMs),
        bufferOverflow: normalizeBufferOverflow(options.bufferOverflow, DEFAULT_OPTIONS.bufferOverflow),
        metrics: typeof options.metrics === 'boolean' ? options.metrics : DEFAULT_OPTIONS.metrics,
    };
}

function createListenerBucket(): ListenerBucket {
    return {
        high: [],
        normal: [],
        low: [],
        size: 0,
    };
}

function createBufferedBucket(): BufferedBucket {
    return {
        high: [],
        normal: [],
        low: [],
        size: 0,
    };
}

function createTimingAccumulator(): TimingAccumulator {
    return {
        count: 0,
        total: 0,
        min: Number.POSITIVE_INFINITY,
        max: 0,
    };
}

function createMetricsAccumulator(): MetricsAccumulator {
    return {
        emit: {
            timing: createTimingAccumulator(),
            sync: createTimingAccumulator(),
            async: createTimingAccumulator(),
            buffered: createTimingAccumulator(),
        },
        execution: {
            ...createTimingAccumulator(),
            errors: 0,
        },
    };
}

function snapshotTiming(timing: TimingAccumulator): EventMetrics['emit']['timing'] {
    if (timing.count === 0) {
        return {
            avg: 0,
            max: 0,
            min: 0,
            total: 0,
        };
    }

    return {
        avg: timing.total / timing.count,
        max: timing.max,
        min: Number.isFinite(timing.min) ? timing.min : 0,
        total: timing.total,
    };
}

function determineDominantEmitMode(emit: MetricsAccumulator['emit']): EmitMode {
    const syncCount = emit.sync.count;
    const asyncCount = emit.async.count;
    const bufferedCount = emit.buffered.count;

    if (syncCount >= asyncCount && syncCount >= bufferedCount) {
        return 'sync';
    }
    if (asyncCount >= bufferedCount) {
        return 'async';
    }
    return 'buffered';
}

export class EventEmitter<T extends EventMap = EventMap> implements IEventEmitter<T> {
    #events = new Map<string, ListenerBucket>();
    #subscriptionIndex = new Map<symbol, InternalSubscription<any>>();
    #options: Required<EventOptions>;
    #metrics = new Map<string, MetricsAccumulator>();
    #scheduler: EventScheduler | null = null;
    #buffer = new Map<string, BufferedBucket>();
    #bufferedEventId = 0;
    #bufferedEventCount = 0;
    #isPaused = false;
    #isDisposed = false;
    #gcIntervalId?: ReturnType<typeof setInterval>;
    #weakRegistry?: FinalizationRegistry<symbol>;
    #tapListeners = new Set<EventTap>();
    #bufferProcessing: Promise<void> | null = null;
    #emitDepth = new Map<string, number>();
    #warnedEvents = new Set<string>();

    constructor(options: EventOptions = {}) {
        this.#options = normalizeOptions(options);

        if (
            this.#options.weakReferences &&
            typeof WeakRef === 'function' &&
            typeof FinalizationRegistry === 'function'
        ) {
            this.#weakRegistry = new FinalizationRegistry((subscriptionId) => {
                this.offById(subscriptionId);
            });
        }

        if (this.#options.gcIntervalMs > 0) {
            this.#startGc();
        }
    }

    get maxListeners(): number {
        return this.#options.maxListeners;
    }

    set maxListeners(value: number) {
        if (value !== Infinity && (value < 0 || !Number.isInteger(value))) {
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
        this.#ensureRuntime();

        const id = this.#registerListener(event, callback, {
            once: options.once ?? false,
            priority: options.priority ?? DEFAULT_PRIORITY,
        });

        return () => this.offById(id);
    }

    public once<K extends EventKey<T>>(
        event: K,
        callback: EventCallback<T[K]>,
        options: Omit<SubscriptionOptions, 'once'> = {}
    ): UnsubscribeFn {
        this.#ensureRuntime();

        const id = this.#registerListener(event, callback, {
            once: true,
            priority: options.priority ?? DEFAULT_PRIORITY,
        });

        return () => this.offById(id);
    }

    public pipe<K extends EventKey<T>>(
        event: K,
        emitter: IEventPublisher<any>,
        targetEvent?: string
    ): UnsubscribeFn {
        return pipeToEmitter(
            (callback) => this.on(event, callback),
            emitter,
            targetEvent ?? (event as string)
        );
    }

    public off<K extends EventKey<T>>(event: K, callback?: EventCallback<T[K]>): boolean {
        const eventName = String(event);
        const bucket = this.#events.get(eventName);

        if (!bucket || bucket.size === 0) {
            return false;
        }

        if (!callback) {
            this.#clearBucket(eventName, bucket);
            return true;
        }

        let removed = false;

        for (const priority of ['high', 'normal', 'low'] as const) {
            const records = bucket[priority];

            for (let index = records.length - 1; index >= 0; index--) {
                const record = records[index]!;
                const currentCallback = this.#resolveCallback(record);

                if (currentCallback === callback) {
                    this.#deleteSubscription(record);
                    removed = true;
                }
            }
        }

        return removed;
    }

    public offById(subscriptionId: symbol): boolean {
        const subscription = this.#subscriptionIndex.get(subscriptionId);
        if (!subscription) {
            return false;
        }

        return this.#deleteSubscription(subscription);
    }

    /**
     * Asynchronously dispatch an event to all currently-subscribed listeners.
     *
     * **Dispatch semantics — snapshot model.** The listener set is captured
     * at the moment this method is called, and dispatch iterates only that
     * snapshot. Concretely:
     *
     * - *Snapshot at dispatch start.* The snapshot is built from the
     *   listener bucket in priority order (high → normal → low) before any
     *   handler runs. The set of listeners that will fire is fixed for the
     *   lifetime of this `emit`.
     * - *Mutations during dispatch are deferred.* Subscriptions added via
     *   `on()` / `once()` during this `emit` are stored in the bucket but
     *   are NOT in the snapshot, so they do not fire in this dispatch —
     *   they become visible to the next `emit`.
     * - *`off()` during dispatch.* Removing a listener via `off()` /
     *   `offById()` while this `emit` is in-flight sets the subscription's
     *   `disposed` flag. Both the async fast-path (`concurrencyLimit ===
     *   Infinity`) and the scheduler path re-check this flag before
     *   invocation, so a listener removed during the current dispatch is
     *   skipped for the remainder of this `emit`. The snapshot itself is
     *   not re-read — the bucket, not the snapshot, is the source of truth
     *   for "still subscribed". This is the snapshot model: a fixed list
     *   at start, not a live-growing list.
     * - *`once` removal is post-dispatch.* A `once` listener fires on the
     *   first matching emit after registration and is unregistered only
     *   after its handler returns (or throws). It is NEVER removed before
     *   being invoked in this `emit`, even if `off()` is called on it from
     *   inside its own handler.
     * - *Re-entrant emits complete in full.* A handler that calls `emit()`
     *   for the same event runs a nested, full dispatch that snapshots
     *   and completes before the outer iteration resumes. There is no
     *   implicit short-circuit or skip of the outer iteration.
     * - *Re-entrancy is bounded.* Nested emits for the same event are
     *   capped at `MAX_EMIT_DEPTH` (32). Deeper recursion is dropped and
     *   logged once.
     *
     * **Divergence from `@axrone/observer`.** This snapshot model differs
     * deliberately from `@axrone/observer`'s `Subject`, which iterates a
     * live, growing array and therefore FIRES listeners added
     * mid-dispatch. Use `Subject` for observer-style live fan-out; use
     * `EventEmitter` for stable per-emit fan-out (the common case for
     * game and event systems).
     *
     * @typeParam K - Event key, narrowed against `T`.
     * @param event - The event to dispatch.
     * @param data - The payload delivered to every listener.
     * @param options - Per-emit overrides; `priority` is accepted for
     *   parity but does not change the dispatch order within the
     *   snapshot.
     * @returns A promise that resolves to `true` if at least one listener
     *   was invoked, `false` if the emitter was paused, had no listeners,
     *   exceeded re-entrancy depth, or the snapshot was empty. Rejects
     *   with collected handler errors (an `EventHandlerError` or
     *   `AggregateError`) when `captureRejections` is disabled and at
     *   least one handler threw.
     */
    public async emit<K extends EventKey<T>>(
        event: K,
        data: T[K],
        options: { priority?: EventPriority } = {}
    ): Promise<boolean> {
        this.#ensureRuntime();

        const eventName = String(event);
        const priority = options.priority ?? DEFAULT_PRIORITY;
        const startTime = this.#options.metrics ? performance.now() : 0;

        const currentDepth = this.#emitDepth.get(eventName) ?? 0;
        if (currentDepth >= MAX_EMIT_DEPTH) {
            console.warn(
                `EventEmitter: Re-entrancy depth exceeded for event "${eventName}" (max ${MAX_EMIT_DEPTH}). Dropping emit.`
            );
            return false;
        }

        if (
            !this.#isPaused &&
            !this.#hasListeners(eventName) &&
            this.#tapListeners.size === 0
        ) {
            return false;
        }

        this.#emitDepth.set(eventName, currentDepth + 1);

        try {
            if (this.#isPaused) {
                const tapContext: Omit<EventTapContext, 'phase'> = {
                    event: eventName,
                    data,
                    priority,
                    sync: false,
                };
                this.#emitTaps({ ...tapContext, phase: 'start' });
                try {
                    this.#enqueueBufferedEvent(eventName, data, priority);
                    this.#recordEmitMetric(eventName, 0, 'buffered');
                    this.#emitTaps({ ...tapContext, phase: 'end' });
                    return true;
                } catch (error) {
                    this.#recordEmitMetric(eventName, startTime, 'buffered');
                    this.#emitTaps({ ...tapContext, phase: 'end' });
                    throw error;
                }
            }

            const tapContext: Omit<EventTapContext, 'phase'> = {
                event: eventName,
                data,
                priority,
                sync: false,
            };

            this.#emitTaps({ ...tapContext, phase: 'start' });

        try {
            const snapshot = this.#snapshotListeners(eventName);

            if (snapshot.length === 0) {
                return false;
            }

            // Fast path: when concurrencyLimit is Infinity (the default), bypass
            // the EventScheduler to skip per-listener ITask / Promise / closure /
            // setTimeout(30s) overhead. Handlers run serially via `await`, so any
            // Promises they return are awaited before emit() resolves. Failure
            // isolation (per-iteration try/catch), once-removal post-dispatch,
            // execution metrics, captureRejections semantics (collect-then-process
            // with 'error' event emission or final throw), and #reportAsyncError
            // dispatch for non-captured rejections are all preserved. The
            // scheduler path is retained for finite concurrencyLimit so the
            // configured cap is honoured.
            if (this.#options.concurrencyLimit === Infinity) {
                const errors: EventHandlerError[] = [];
                for (const subscription of snapshot) {
                    if (subscription.disposed) {
                        continue;
                    }

                    const callback = this.#resolveCallback(subscription);
                    if (!callback) {
                        continue;
                    }

                    const execStartTime = performance.now();
                    subscription.executionCount += 1;
                    subscription.lastExecuted = Date.now();
                    let isError = false;
                    try {
                        await (callback as (data: unknown) => unknown)(data);
                    } catch (error) {
                        isError = true;
                        const wrapped =
                            error instanceof EventHandlerError
                                ? error
                                : new EventHandlerError(eventName, error);
                        if (this.#options.captureRejections) {
                            errors.push(wrapped);
                        } else {
                            this.#reportAsyncError(wrapped);
                        }
                    }

                    this.#recordExecutionMetric(
                        eventName,
                        performance.now() - execStartTime,
                        isError
                    );

                    if (subscription.once) {
                        this.#deleteSubscription(subscription);
                    }
                }

                if (errors.length > 0) {
                    const errorEvent = 'error' as EventKey<T>;
                    if (this.has(errorEvent)) {
                        for (const err of errors) {
                            this.emitSync(errorEvent, err as T[typeof errorEvent]);
                        }
                    } else if (errors.length === 1) {
                        throw errors[0];
                    } else {
                        throw new EventHandlerError(
                            eventName,
                            new AggregateError(
                                errors as Error[],
                                `${errors.length} handlers failed`
                            )
                        );
                    }
                }

                return true;
            }

            await this.#dispatchAsync(eventName, data, snapshot);
            return true;
        } catch (error) {
            throw error;
        } finally {
            this.#recordEmitMetric(eventName, startTime, 'async');
            this.#emitTaps({ ...tapContext, phase: 'end' });
            const depth = this.#emitDepth.get(eventName) ?? 1;
            if (depth <= 1) {
                this.#emitDepth.delete(eventName);
            } else {
                this.#emitDepth.set(eventName, depth - 1);
            }
        }
    }

    /**
     * Synchronously dispatch an event to all currently-subscribed listeners.
     *
     * **Dispatch semantics — snapshot model.** The listener set is captured
     * at the moment this method is called, and dispatch iterates only that
     * snapshot. Concretely:
     *
     * - *Snapshot at dispatch start.* The snapshot is built from the
     *   listener bucket in priority order (high → normal → low) before any
     *   handler runs. The set of listeners that will fire is fixed for the
     *   lifetime of this `emitSync`.
     * - *Mutations during dispatch are deferred.* Subscriptions added via
     *   `on()` / `once()` during this `emitSync` are stored in the bucket
     *   but are NOT in the snapshot, so they do not fire in this
     *   dispatch — they become visible to the next emit.
     * - *`off()` during dispatch is a no-op for the current dispatch.*
     *   Unlike the async `emit()` path, the sync fast-path does NOT
     *   re-check the `disposed` flag before invocation. A listener
     *   removed via `off()` / `offById()` while this `emitSync` is
     *   in-flight will STILL be invoked once, because the snapshot — not
     *   the live bucket — determines what fires. (If the snapshot
     *   callback was wrapped in a `WeakRef` and the underlying callback
     *   has been garbage-collected, the listener is skipped — but that
     *   is a separate `WeakRef`-resolution failure, not a removal
     *   effect.)
     * - *`once` removal is post-dispatch.* A `once` listener fires on the
     *   first matching emit after registration and is unregistered only
     *   after its handler returns (or throws). It is NEVER removed before
     *   being invoked in this `emitSync`, even if `off()` is called on it
     *   from inside its own handler.
     * - *Async handlers are warned, not awaited.* If a handler returns a
     *   thenable, this method returns immediately and a warning is
     *   logged to the console; the promise's outcome is observed in the
     *   background and routed through `captureRejections` or
     *   `reportAsyncError`. For awaitable semantics, use `emit()`.
     * - *Re-entrancy is bounded.* Nested `emitSync` calls for the same
     *   event are capped at `MAX_EMIT_DEPTH` (32). Deeper recursion is
     *   dropped and logged once.
     *
     * **Divergence from `@axrone/observer`.** This snapshot model differs
     * deliberately from `@axrone/observer`'s `Subject`, which iterates a
     * live, growing array and therefore FIRES listeners added
     * mid-dispatch. Use `Subject` for observer-style live fan-out; use
     * `EventEmitter` for stable per-emit fan-out (the common case for
     * game and event systems).
     *
     * @typeParam K - Event key, narrowed against `T`.
     * @param event - The event to dispatch.
     * @param data - The payload delivered to every listener.
     * @param options - Per-emit overrides; `priority` is accepted for
     *   parity but does not change the dispatch order within the
     *   snapshot.
     * @returns `true` if at least one listener was invoked (or any
     *   handler returned a thenable, in which case that promise is
     *   still pending), `false` if the emitter was paused, had no
     *   listeners, exceeded re-entrancy depth, or the snapshot was
     *   empty. Throws collected handler errors as `EventHandlerError` /
     *   `AggregateError` when at least one handler threw synchronously.
     */
    public emitSync<K extends EventKey<T>>(
        event: K,
        data: T[K],
        options: { priority?: EventPriority } = {}
    ): boolean {
        this.#ensureRuntime();

        const eventName = String(event);
        const priority = options.priority ?? DEFAULT_PRIORITY;
        const startTime = this.#options.metrics ? performance.now() : 0;

        const currentDepth = this.#emitDepth.get(eventName) ?? 0;
        if (currentDepth >= MAX_EMIT_DEPTH) {
            console.warn(
                `EventEmitter: Re-entrancy depth exceeded for event "${eventName}" (max ${MAX_EMIT_DEPTH}). Dropping emit.`
            );
            return false;
        }

        if (
            !this.#isPaused &&
            !this.#hasListeners(eventName) &&
            this.#tapListeners.size === 0
        ) {
            return false;
        }

        this.#emitDepth.set(eventName, currentDepth + 1);

        try {
            if (this.#isPaused) {
                try {
                    const tapContext: Omit<EventTapContext, 'phase'> = {
                        event: eventName,
                        data,
                        priority,
                        sync: true,
                    };
                    this.#emitTaps({ ...tapContext, phase: 'start' });
                    this.#enqueueBufferedEvent(eventName, data, priority);
                    this.#recordEmitMetric(eventName, 0, 'buffered');
                    this.#emitTaps({ ...tapContext, phase: 'end' });
                    return true;
                } catch (error) {
                    this.#recordEmitMetric(eventName, startTime, 'buffered');
                    this.#emitTaps({ ...tapContext, phase: 'end' });
                    throw error;
                }
            }

            const tapContext: Omit<EventTapContext, 'phase'> = {
                event: eventName,
                data,
                priority,
                sync: true,
            };

        this.#emitTaps({ ...tapContext, phase: 'start' });

        try {
            const snapshot = this.#snapshotListeners(eventName);

            if (snapshot.length === 0) {
                return false;
            }

            let hadAsyncCallbacks = false;
            const errors: Error[] = [];

            for (const subscription of snapshot) {
                const callback = this.#resolveCallback(subscription);

                if (!callback) {
                    continue;
                }

                const execStartTime = performance.now();
                subscription.executionCount++;
                subscription.lastExecuted = Date.now();

                try {
                    const result = callback(data);

                    if (subscription.once) {
                        this.#deleteSubscription(subscription);
                    }

                    if (isPromiseLike<void>(result)) {
                        hadAsyncCallbacks = true;
                        void Promise.resolve(result).then(
                            () => {
                                // Intentionally not recording execution metric here:
                                // the measured time includes await suspension and
                                // scheduler latency, not the handler's actual cost.
                            },
                            (error) => {
                                // Same reason: do not record timing for async
                                // listeners invoked from emitSync. The error is
                                // still surfaced through captureRejections or
                                // reportAsyncError so behavior is unchanged.
                                const wrapped = new EventHandlerError(eventName, error);

                                if (this.#options.captureRejections) {
                                    errors.push(wrapped);
                                } else {
                                    this.#reportAsyncError(wrapped);
                                }
                            }
                        );
                    } else {
                        this.#recordExecutionMetric(
                            eventName,
                            performance.now() - execStartTime,
                            false
                        );
                    }
                } catch (error) {
                    if (subscription.once) {
                        this.#deleteSubscription(subscription);
                    }

                    this.#recordExecutionMetric(
                        eventName,
                        performance.now() - execStartTime,
                        true
                    );

                    const wrapped = new EventHandlerError(eventName, error);
                    errors.push(wrapped);
                }
            }

            if (errors.length > 0) {
                const errorEvent = 'error' as EventKey<T>;

                if (this.has(errorEvent)) {
                    for (const err of errors) {
                        this.emitSync(errorEvent, err as T[typeof errorEvent]);
                    }
                } else if (errors.length === 1) {
                    throw errors[0];
                } else {
                    throw new EventHandlerError(
                        eventName,
                        new AggregateError(errors, `${errors.length} handlers failed`)
                    );
                }
            }

            if (hadAsyncCallbacks) {
                console.warn(
                    `EventEmitter: Event "${eventName}" was emitted synchronously but had async listeners. Consider using emit() instead.`
                );
            }

            return true;
        } catch (error) {
            throw error;
        } finally {
            this.#recordEmitMetric(eventName, startTime, 'sync');
            this.#emitTaps({ ...tapContext, phase: 'end' });
            const depth = this.#emitDepth.get(eventName) ?? 1;
            if (depth <= 1) {
                this.#emitDepth.delete(eventName);
            } else {
                this.#emitDepth.set(eventName, depth - 1);
            }
        }
    }

    /**
     * Emit a batch of events with settle-all, per-item error isolation.
     *
     * Replaces the previous `Promise.all` fail-fast implementation. Now every
     * input gets a corresponding {@link EventDispatchResult} in the output:
     *
     * - `{ success: true }` — the underlying `emit()` resolved to `true`.
     * - `{ success: false }` — the underlying `emit()` resolved to `false`
     *   (no listeners, re-entrancy cap, etc.).
     * - `{ success: false, error }` — the underlying `emit()` rejected; the
     *   error is captured here instead of rejecting the whole batch, so
     *   sibling emissions are not discarded.
     *
     * The returned promise only rejects if the emitter was disposed before
     * the call (mirroring `emit()`'s precondition).
     */
    public async emitBatch(
        events: ReadonlyArray<EventDispatchItem<T>>
    ): Promise<ReadonlyArray<EventDispatchResult>> {
        if (events.length === 0) return [];

        this.#ensureRuntime();

        // #dispatchBatchItem absorbs every per-item error into its result, so
        // the awaited promise can never reject and a plain Promise.all suffices.
        const tasks = new Array<Promise<EventDispatchResult>>(events.length);
        for (let index = 0; index < events.length; index++) {
            const { event, data, priority } = events[index]!;
            tasks[index] = this.#dispatchBatchItem(event, data, priority);
        }

        return Promise.all(tasks);
    }

    async #dispatchBatchItem<K extends EventKey<T>>(
        event: K,
        data: T[K],
        priority: EventPriority | undefined
    ): Promise<EventDispatchResult> {
        try {
            const dispatched = await this.emit(event, data, priority ? { priority } : undefined);
            return { success: dispatched };
        } catch (error) {
            return { success: false, error: toError(error) };
        }
    }

    public has<K extends EventKey<T>>(event: K): boolean {
        const bucket = this.#events.get(String(event));
        return bucket !== undefined && bucket.size > 0;
    }

    public hasSubscription(subscriptionId: symbol): boolean {
        return this.#subscriptionIndex.has(subscriptionId);
    }

    public listenerCount<K extends EventKey<T>>(event: K): number {
        return this.#events.get(String(event))?.size ?? 0;
    }

    public listenerCountAll(): number {
        return this.#subscriptionIndex.size;
    }

    public eventNames(): EventKey<T>[] {
        return Array.from(this.#events.keys()) as EventKey<T>[];
    }

    public getSubscriptions<K extends EventKey<T>>(event: K): ReadonlyArray<Subscription<T[K]>> {
        const bucket = this.#events.get(String(event));
        if (!bucket || bucket.size === 0) {
            return [];
        }

        const subscriptions: Subscription<T[K]>[] = [];
        this.#appendPublicSubscriptions(bucket.high, subscriptions);
        this.#appendPublicSubscriptions(bucket.normal, subscriptions);
        this.#appendPublicSubscriptions(bucket.low, subscriptions);
        return subscriptions;
    }

    public removeAllListeners<K extends EventKey<T>>(event?: K): this {
        if (event) {
            const eventName = String(event);
            const bucket = this.#events.get(eventName);
            if (bucket) {
                this.#clearBucket(eventName, bucket);
            }
        } else {
            for (const [eventName, bucket] of this.#events.entries()) {
                this.#clearBucket(eventName, bucket);
            }
        }
        return this;
    }

    public batchSubscribe<K extends EventKey<T>>(
        event: K,
        callbacks: ReadonlyArray<EventCallback<T[K]>>,
        options: SubscriptionOptions = {}
    ): ReadonlyArray<symbol> {
        if (callbacks.length === 0) {
            return [];
        }

        this.#ensureRuntime();

        const subscriptionIds = new Array<symbol>(callbacks.length);

        for (let index = 0; index < callbacks.length; index++) {
            const callback = callbacks[index]!;
            subscriptionIds[index] = this.#registerListener(event, callback, {
                once: options.once ?? false,
                priority: options.priority ?? DEFAULT_PRIORITY,
            });
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

    public getQueuedEvents<K extends EventKey<T>>(event: K): ReadonlyArray<QueuedEvent<T[K]>>;
    public getQueuedEvents(): ReadonlyArray<QueuedEvent<T[EventKey<T>]>>;
    public getQueuedEvents<K extends EventKey<T>>(event?: K): ReadonlyArray<QueuedEvent<any>> {
        if (event) {
            const bucket = this.#buffer.get(String(event));
            return bucket ? this.#snapshotBufferedBucket(bucket) : [];
        }

        if (this.#bufferedEventCount === 0) {
            return [];
        }

        const allEvents = new Array<QueuedEvent>(this.#bufferedEventCount);
        let offset = 0;

        for (const bucket of this.#buffer.values()) {
            offset = this.#copyBufferedEntries(bucket.high, allEvents, offset);
            offset = this.#copyBufferedEntries(bucket.normal, allEvents, offset);
            offset = this.#copyBufferedEntries(bucket.low, allEvents, offset);
        }

        return allEvents.sort((a, b) => {
            const priorityDiff = PRIORITY_VALUES[a.priority] - PRIORITY_VALUES[b.priority];
            if (priorityDiff !== 0) return priorityDiff;
            return a.id - b.id;
        });
    }

    public getPendingCount<K extends EventKey<T>>(event?: K): number {
        if (event) {
            return this.#buffer.get(String(event))?.size ?? 0;
        }

        return this.#bufferedEventCount;
    }

    public getBufferSize(): number {
        return this.#options.bufferSize;
    }

    public clearBuffer<K extends EventKey<T>>(event?: K): number {
        if (event) {
            const eventName = String(event);
            const bucket = this.#buffer.get(eventName);
            if (!bucket) return 0;
            const size = bucket.size;
            this.#buffer.delete(eventName);
            this.#bufferedEventCount -= size;
            return size;
        }

        const total = this.#bufferedEventCount;
        this.#buffer.clear();
        this.#bufferedEventCount = 0;
        return total;
    }

    public pause(): void {
        this.#ensureRuntime();
        this.#isPaused = true;
    }

    public resume(): void {
        if (!this.#isPaused) return;

        this.#ensureRuntime();
        this.#isPaused = false;

        if (this.#bufferedEventCount === 0 || this.#bufferProcessing) {
            return;
        }

        const processing = this.#processBufferedEvents().finally(() => {
            if (this.#bufferProcessing === processing) {
                this.#bufferProcessing = null;
            }
        });

        this.#bufferProcessing = processing;
    }

    public isPaused(): boolean {
        return this.#isPaused;
    }

    public async drain(options: { maxIterations?: number; timeoutMs?: number } = {}): Promise<void> {
        const maxIterations = options.maxIterations ?? 1000;
        const timeoutMs = options.timeoutMs ?? 30000;
        const startTime = Date.now();
        let iteration = 0;

        for (;;) {
            if (iteration++ >= maxIterations) {
                throw new Error(`EventEmitter.drain() exceeded max iterations (${maxIterations})`);
            }

            if (Date.now() - startTime > timeoutMs) {
                throw new Error(`EventEmitter.drain() timed out after ${timeoutMs}ms`);
            }

            const currentBufferProcessing = this.#bufferProcessing;
            if (currentBufferProcessing) {
                await currentBufferProcessing;
                continue;
            }

            await this.#getScheduler().drain();

            if (
                this.#bufferProcessing === null &&
                this.#getScheduler().activeCount === 0 &&
                this.#getScheduler().queuedCount === 0
            ) {
                break;
            }
        }
    }

    public async flush<K extends EventKey<T>>(event: K): Promise<void> {
        if (this.#bufferProcessing) {
            await this.#bufferProcessing;
        }

        const eventName = String(event);
        const bucket = this.#buffer.get(eventName);
        if (!bucket || bucket.size === 0) return;

        const queuedEvents = this.#snapshotBufferedBucket(bucket);
        this.#buffer.delete(eventName);
        this.#bufferedEventCount -= queuedEvents.length;

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
        const metrics = this.#metrics.get(String(event));

        if (!metrics) {
            return {
                emit: {
                    count: 0,
                    mode: 'sync',
                    timing: snapshotTiming(createTimingAccumulator()),
                },
                execution: {
                    count: 0,
                    errors: 0,
                    timing: snapshotTiming(createTimingAccumulator()),
                },
            };
        }

        return {
            emit: {
                count: metrics.emit.sync.count + metrics.emit.async.count + metrics.emit.buffered.count,
                mode: determineDominantEmitMode(metrics.emit),
                timing: snapshotTiming(metrics.emit.timing),
            },
            execution: {
                count: metrics.execution.count,
                errors: metrics.execution.errors,
                timing: snapshotTiming(metrics.execution),
            },
        };
    }

    public resetMetrics<K extends EventKey<T>>(event?: K): void {
        if (event) {
            this.#metrics.delete(String(event));
        } else {
            this.#metrics.clear();
        }
    }

    public [EVENT_EMITTER_TAP](tap: EventTap): UnsubscribeFn {
        this.#ensureRuntime();
        this.#tapListeners.add(tap);
        return () => this.#tapListeners.delete(tap);
    }

    #registerListener<K extends EventKey<T>>(
        event: K,
        callback: EventCallback<T[K]>,
        options: Required<SubscriptionOptions>
    ): symbol {
        const eventName = String(event);
        let bucket = this.#events.get(eventName);

        if (!bucket) {
            bucket = createListenerBucket();
            this.#events.set(eventName, bucket);
        }

        if (
            this.#options.maxListeners !== Infinity &&
            bucket.size >= this.#options.maxListeners &&
            !this.#warnedEvents.has(eventName) &&
            (globalThis as { __AXRONE_DEBUG__?: boolean }).__AXRONE_DEBUG__ !== false
        ) {
            this.#warnedEvents.add(eventName);
            console.warn(
                `MaxListenersExceededWarning: Possible memory leak detected. ${
                    bucket.size
                } listeners added to event "${eventName}". (Further warnings suppressed.)`
            );
        }

        const id = Symbol(eventName);
        let internalCallback: InternalCallback<T[K]> = callback;
        let unregisterToken: object | undefined;
        let weak = false;

        if (this.#options.weakReferences && this.#weakRegistry) {
            unregisterToken = Object.create(null) as object;
            this.#weakRegistry.register(callback as EventCallback<T[K]> & object, id, unregisterToken);
            internalCallback = new WeakRef(callback as EventCallback<T[K]> & object);
            weak = true;
        }

        const subscription: InternalSubscription<T[K]> = {
            id,
            event: eventName,
            callback: internalCallback,
            once: options.once,
            priority: options.priority,
            executionCount: 0,
            createdAt: Date.now(),
            unregisterToken,
            weak,
            disposed: false,
        };

        bucket[options.priority].push(subscription);
        bucket.size += 1;
        this.#subscriptionIndex.set(id, subscription);

        return id;
    }

    #enqueueBufferedEvent<K extends EventKey<T>>(
        event: K | string,
        data: T[K] | T[EventKey<T>],
        priority: EventPriority
    ): void {
        const eventName = String(event);
        let bucket = this.#buffer.get(eventName);

        if (!bucket) {
            bucket = createBufferedBucket();
            this.#buffer.set(eventName, bucket);
        }

        if (bucket.size >= this.#options.bufferSize) {
            const policy = this.#options.bufferOverflow;

            if (policy === 'throw') {
                throw new EventQueueFullError(eventName, this.#options.bufferSize);
            }

            if (policy === 'drop-newest') {
                return;
            }

            if (policy === 'drop-oldest') {
                this.#dropOldestBufferedEvent(bucket);
            }
        }

        const eventId = ++this.#bufferedEventId;
        const queuedEvent: QueuedEvent = {
            id: eventId,
            event: eventName,
            data,
            timestamp: Date.now(),
            priority,
        };

        bucket[priority].push(queuedEvent);
        bucket.size += 1;
        this.#bufferedEventCount += 1;
    }

    #dropOldestBufferedEvent(bucket: BufferedBucket): void {
        for (const priority of ['high', 'normal', 'low'] as const) {
            const queue = bucket[priority];
            if (queue.length > 0) {
                queue.shift();
                bucket.size -= 1;
                this.#bufferedEventCount -= 1;
                return;
            }
        }
    }

    async #processBufferedEvents(): Promise<void> {
        if (this.#isPaused || this.#bufferedEventCount === 0) {
            return;
        }

        // Snapshot event names up front so we can safely delete emptied
        // buckets from the buffer map while iterating.
        const eventNames = Array.from(this.#buffer.keys());

        // The buffer is already pre-partitioned per priority, so we can walk
        // it directly in high → normal → low order without materializing
        // a flat array or running a JS sort comparator.
        for (const eventName of eventNames) {
            const bucket = this.#buffer.get(eventName);
            if (!bucket) continue;

            for (const priority of ['high', 'normal', 'low'] as const) {
                const queue = bucket[priority];
                const initialLength = queue.length;
                if (initialLength === 0) continue;

                for (let index = 0; index < initialLength; index++) {
                    const queuedEvent = queue[index]!;
                    await this.emit(
                        queuedEvent.event as EventKey<T>,
                        queuedEvent.data as T[EventKey<T>],
                        { priority: queuedEvent.priority }
                    );
                }

                // Drop only the originally-buffered entries. Any entries
                // appended during emit() (e.g. by a listener that called
                // pause() and re-emitted) live past `initialLength` and must
                // remain in the buffer for the next resume().
                queue.splice(0, initialLength);
                this.#bufferedEventCount -= initialLength;
            }

            bucket.size = bucket.high.length + bucket.normal.length + bucket.low.length;

            if (bucket.size === 0) {
                this.#buffer.delete(eventName);
            }
        }
    }

    async #dispatchAsync<K extends EventKey<T>>(
        event: K | string,
        data: T[K] | T[EventKey<T>],
        snapshot: ReadonlyArray<InternalSubscription>
    ): Promise<void> {
        const eventName = String(event);

        if (snapshot.length === 1) {
            let scheduled = this.#scheduleDispatch(eventName, snapshot[0]!, data);

            if (this.#options.captureRejections) {
                scheduled = scheduled.catch((error) => this.#handleCapturedErrorAsync(eventName, error));
            }

            await scheduled;
            return;
        }

        const scheduled = new Array<Promise<void>>(snapshot.length);

        for (let index = 0; index < snapshot.length; index++) {
            let task = this.#scheduleDispatch(eventName, snapshot[index]!, data);

            if (this.#options.captureRejections) {
                task = task.catch((error) => this.#handleCapturedErrorAsync(eventName, error));
            }

            scheduled[index] = task;
        }

        await Promise.all(scheduled);
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
        if (this.#options.weakReferences) {
            for (const subscription of this.#subscriptionIndex.values()) {
                this.#resolveCallback(subscription);
            }
        }

        for (const [eventName, metrics] of this.#metrics.entries()) {
            if (!this.#events.has(eventName) && !this.#buffer.has(eventName)) {
                this.#metrics.delete(eventName);
            }
        }

        for (const [eventName, bucket] of this.#buffer.entries()) {
            if (bucket.size === 0) {
                this.#buffer.delete(eventName);
            }
        }

        this.#scheduler?.runGarbageCollection(this.#options.gcIntervalMs);
    }

    dispose(): void {
        if (this.#gcIntervalId) {
            clearInterval(this.#gcIntervalId);
            this.#gcIntervalId = undefined;
        }

        if (this.#scheduler) {
            this.#scheduler.dispose();
            this.#scheduler = null;
        }

        this.removeAllListeners();
        this.clearBuffer();
        this.#metrics.clear();
        this.#tapListeners.clear();
        this.#bufferProcessing = null;
        this.#isPaused = false;
        this.#isDisposed = true;
    }

    #createScheduler(): EventScheduler {
        return new EventScheduler({
            concurrencyLimit: this.#options.concurrencyLimit,
            gcIntervalMs: 0,
        });
    }

    #getScheduler(): EventScheduler {
        if (this.#scheduler === null) {
            this.#scheduler = this.#createScheduler();
        }
        return this.#scheduler;
    }

    #ensureRuntime(): void {
        if (this.#isDisposed) {
            throw new Error('EventEmitter has been disposed and cannot be reused');
        }

        if (this.#scheduler === null) {
            this.#scheduler = this.#createScheduler();
        }
    }

    get isDisposed(): boolean {
        return this.#isDisposed;
    }

    #scheduleDispatch<K extends EventKey<T>>(
        event: K | string,
        subscription: InternalSubscription,
        data: T[K] | T[EventKey<T>]
    ): Promise<void> {
        const eventName = String(event);

        return this.#getScheduler().schedule(
            async () => {
                if (subscription.disposed) {
                    return;
                }

                const callback = this.#resolveCallback(subscription);

                if (!callback) {
                    return;
                }

                const startTime = this.#options.metrics ? performance.now() : 0;
                subscription.executionCount += 1;
                subscription.lastExecuted = Date.now();

                try {
                    await callback(data as never);
                    this.#recordExecutionMetric(eventName, startTime, false);
                } catch (error) {
                    this.#recordExecutionMetric(eventName, startTime, true);
                    if (subscription.once) {
                        this.#deleteSubscription(subscription);
                    }
                    throw new EventHandlerError(eventName, error);
                }

                if (subscription.once) {
                    this.#deleteSubscription(subscription);
                }
            },
            PRIORITY_TO_TASK_PRIORITY[subscription.priority]
        );
    }

    async #handleCapturedErrorAsync(eventName: string, error: unknown): Promise<void> {
        const wrapped = error instanceof EventHandlerError ? error : new EventHandlerError(eventName, error);

        if (eventName === 'error') {
            throw wrapped;
        }

        const errorEvent = 'error' as EventKey<T>;

        if (!this.has(errorEvent)) {
            throw wrapped;
        }

        await this.emit(errorEvent, wrapped as T[typeof errorEvent]);
    }

    #handleCapturedErrorSync(eventName: string, error: EventHandlerError): void {
        if (eventName === 'error') {
            throw error;
        }

        const errorEvent = 'error' as EventKey<T>;

        if (!this.has(errorEvent)) {
            throw error;
        }

        this.emitSync(errorEvent, error as T[typeof errorEvent]);
    }

    #reportAsyncError(error: unknown): void {
        rethrowAsync(error);
    }

    #hasListeners(eventName: string): boolean {
        const bucket = this.#events.get(eventName);
        if (!bucket) {
            return false;
        }
        return bucket.size > 0;
    }

    #emitTaps(context: EventTapContext): void {
        if (this.#tapListeners.size === 0) {
            return;
        }

        for (const tap of this.#tapListeners) {
            try {
                tap(context);
            } catch (error) {
                this.#reportAsyncError(error);
            }
        }
    }

    #emitTapsFor(
        eventName: string,
        data: unknown,
        priority: EventPriority,
        sync: boolean,
        phase: 'start' | 'end'
    ): void {
        if (this.#tapListeners.size === 0) {
            return;
        }

        for (const tap of this.#tapListeners) {
            try {
                tap({ event: eventName, data, priority, sync, phase });
            } catch (error) {
                this.#reportAsyncError(error);
            }
        }
    }

    #resolveCallback<TData>(subscription: InternalSubscription<TData>): EventCallback<TData> | undefined {
        if (!subscription.weak) {
            return subscription.callback as EventCallback<TData>;
        }

        const callback = (subscription.callback as WeakRef<EventCallback<TData>>).deref();

        if (callback) {
            return callback;
        }

        this.#deleteSubscription(subscription);
        return undefined;
    }

    #deleteSubscription(subscription: InternalSubscription<any>): boolean {
        subscription.disposed = true;
        const bucket = this.#events.get(subscription.event);
        this.#subscriptionIndex.delete(subscription.id);

        if (subscription.unregisterToken && this.#weakRegistry) {
            this.#weakRegistry.unregister(subscription.unregisterToken);
        }

        if (!bucket) {
            return false;
        }

        const records = bucket[subscription.priority];

        for (let index = 0; index < records.length; index++) {
            if (records[index] === subscription) {
                records.splice(index, 1);
                bucket.size -= 1;

                if (bucket.size === 0) {
                    this.#events.delete(subscription.event);
                }

                return true;
            }
        }

        if (bucket.size === 0) {
            this.#events.delete(subscription.event);
        }

        return false;
    }

    #clearBucket(eventName: string, bucket: ListenerBucket): void {
        for (const priority of ['high', 'normal', 'low'] as const) {
            const records = bucket[priority];

            for (let index = 0; index < records.length; index++) {
                const subscription = records[index]!;
                this.#subscriptionIndex.delete(subscription.id);

                if (subscription.unregisterToken && this.#weakRegistry) {
                    this.#weakRegistry.unregister(subscription.unregisterToken);
                }
            }

            records.length = 0;
        }

        bucket.size = 0;
        this.#events.delete(eventName);
    }

    #snapshotListeners(eventName: string): InternalSubscription<any>[] {
        const bucket = this.#events.get(eventName);
        if (!bucket || bucket.size === 0) {
            return [];
        }

        const snapshot = new Array<InternalSubscription<any>>(bucket.size);
        let offset = 0;
        offset = this.#copyLiveSubscriptions(bucket.high, snapshot, offset);
        offset = this.#copyLiveSubscriptions(bucket.normal, snapshot, offset);
        offset = this.#copyLiveSubscriptions(bucket.low, snapshot, offset);

        snapshot.length = offset;
        return snapshot;
    }

    #copyLiveSubscriptions(
        source: InternalSubscription<any>[],
        target: InternalSubscription<any>[],
        offset: number
    ): number {
        for (let index = 0; index < source.length; ) {
            const subscription = source[index]!;

            if (!this.#resolveCallback(subscription)) {
                index += 1;
                continue;
            }

            target[offset] = subscription;
            offset += 1;
            index += 1;
        }

        return offset;
    }

    #removeOnceSubscriptions(snapshot: ReadonlyArray<InternalSubscription<any>>): void {
        for (let index = 0; index < snapshot.length; index++) {
            const subscription = snapshot[index]!;
            if (subscription.once) {
                this.#deleteSubscription(subscription);
            }
        }
    }

    #appendPublicSubscriptions<TData>(
        source: InternalSubscription<any>[],
        target: Subscription<TData>[]
    ): void {
        for (let index = 0; index < source.length; ) {
            const subscription = source[index]!;
            const callback = this.#resolveCallback(subscription);

            if (!callback) {
                continue;
            }

            target.push({
                id: subscription.id,
                event: subscription.event,
                callback,
                once: subscription.once,
                priority: subscription.priority,
                createdAt: subscription.createdAt,
                lastExecuted: subscription.lastExecuted,
                executionCount: subscription.executionCount,
            });
            index += 1;
        }
    }

    #copyBufferedEntries(source: ReadonlyArray<QueuedEvent>, target: QueuedEvent[], offset: number): number {
        for (let index = 0; index < source.length; index++) {
            target[offset] = source[index]!;
            offset += 1;
        }

        return offset;
    }

    #snapshotBufferedBucket(bucket: BufferedBucket): QueuedEvent[] {
        const snapshot = new Array<QueuedEvent>(bucket.size);
        let offset = 0;
        offset = this.#copyBufferedEntries(bucket.high, snapshot, offset);
        offset = this.#copyBufferedEntries(bucket.normal, snapshot, offset);
        this.#copyBufferedEntries(bucket.low, snapshot, offset);
        return snapshot;
    }

    #recordEmitMetric(eventName: string, duration: number, mode: EmitMode): void {
        if (!this.#options.metrics) {
            return;
        }
        const metrics = this.#metrics.get(eventName) ?? createMetricsAccumulator();
        this.#metrics.set(eventName, metrics);
        this.#updateTiming(metrics.emit[mode], duration);
        if (mode !== 'buffered') {
            this.#updateTiming(metrics.emit.timing, duration);
        }
    }

    #recordExecutionMetric(eventName: string, duration: number, isError: boolean): void {
        if (!this.#options.metrics) {
            return;
        }
        const metrics = this.#metrics.get(eventName) ?? createMetricsAccumulator();
        this.#metrics.set(eventName, metrics);
        this.#updateTiming(metrics.execution, duration);

        if (isError) {
            metrics.execution.errors += 1;
        }
    }

    #updateTiming(timing: TimingAccumulator, duration: number): void {
        timing.count += 1;
        timing.total += duration;
        timing.max = Math.max(timing.max, duration);
        timing.min = Math.min(timing.min, duration);
    }
}
