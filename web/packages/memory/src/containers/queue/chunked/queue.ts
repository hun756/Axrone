import { err, ok, type Result } from '@axrone/utility';
import {
    QueueAbortError,
    QueueClosedError,
    QueueEmptyError,
    QueueFullError,
    QueueUnderflowError,
    assertNever,
} from './errors';
import { CHUNK_SIZE, ChunkPool, type MemoryChunk } from './chunk';
import {
    type Batch,
    type IQueue,
    type IQueueMetrics,
    LifecycleState,
    NULL_METRICS,
    OverflowStrategy,
    type QueueCapacity,
    type QueueSize,
    UNBOUNDED_CAPACITY,
    isBatch,
    Symbol_asyncDispose,
    Symbol_dispose,
    toQueueCapacity,
    toQueueSize,
} from './types';
import {
    ConsumerWaitNode,
    DrainWaitNode,
    IntrusiveQueue,
    ProducerWaitNode,
} from './wait-queue';

export class Queue<T> implements IQueue<T> {
    private _chunkHead: MemoryChunk<T>;
    private _chunkTail: MemoryChunk<T>;
    private readonly _allocator: ChunkPool<T>;
    private _elementCount: number;
    private readonly _limit: QueueCapacity;
    private readonly _overflowStrategy: OverflowStrategy;
    private readonly _telemetry: IQueueMetrics;

    private _lifecycle: LifecycleState;
    private _terminalFault: Error | undefined;

    private readonly _consumers: IntrusiveQueue<ConsumerWaitNode<T>>;
    private readonly _producers: IntrusiveQueue<ProducerWaitNode<T>>;
    private readonly _drainers: IntrusiveQueue<DrainWaitNode>;

    constructor(
        capacity: QueueCapacity = UNBOUNDED_CAPACITY,
        strategy: OverflowStrategy = OverflowStrategy.Reject,
        metrics: IQueueMetrics = NULL_METRICS
    ) {
        this._allocator = new ChunkPool<T>();
        const genesisChunk = this._allocator.acquire();
        this._chunkHead = genesisChunk;
        this._chunkTail = genesisChunk;
        this._elementCount = 0;
        this._limit = capacity;
        this._overflowStrategy = strategy;
        this._telemetry = metrics;

        this._lifecycle = LifecycleState.Active;
        this._terminalFault = undefined;

        this._consumers = new IntrusiveQueue<ConsumerWaitNode<T>>();
        this._producers = new IntrusiveQueue<ProducerWaitNode<T>>();
        this._drainers = new IntrusiveQueue<DrainWaitNode>();
    }

    public get size(): QueueSize {
        return toQueueSize(this._elementCount);
    }

    public get capacity(): QueueCapacity {
        return this._limit;
    }

    public get isEmpty(): boolean {
        return this._elementCount === 0;
    }

    public get isFull(): boolean {
        return this._elementCount >= this._limit;
    }

    public get isClosed(): boolean {
        return this._lifecycle !== LifecycleState.Active;
    }

    public get state(): LifecycleState {
        return this._lifecycle;
    }

    public peek(): T | undefined {
        if (this._elementCount === 0) {
            return undefined;
        }
        return this._chunkHead.peek();
    }

    public enqueue(item: NoInfer<T>): Result<void, QueueFullError | QueueClosedError> {
        if (this._lifecycle !== LifecycleState.Active) {
            return err(this._closedError());
        }

        const consumer = this._consumers.shift();
        if (consumer !== null) {
            consumer.detach();
            consumer.resolve(ok(item));
            this._telemetry.onDequeue?.(toQueueSize(this._elementCount));
            return ok();
        }

        if (this._elementCount >= this._limit) {
            switch (this._overflowStrategy) {
                case OverflowStrategy.Reject:
                    return err(QueueFullError.INSTANCE);
                case OverflowStrategy.DropTail:
                    this._telemetry.onDrop?.(OverflowStrategy.DropTail);
                    return ok();
                case OverflowStrategy.DropHead: {
                    this._telemetry.onDrop?.(OverflowStrategy.DropHead);
                    this.executeShift();
                    this.executePush(item);
                    this._telemetry.onEnqueue?.(toQueueSize(this._elementCount));
                    return ok();
                }
                case OverflowStrategy.Block:
                    return err(QueueFullError.INSTANCE);
                default:
                    return assertNever(this._overflowStrategy);
            }
        }

        this.executePush(item);
        this._telemetry.onEnqueue?.(toQueueSize(this._elementCount));
        return ok();
    }

    public dequeue(): Result<T, QueueEmptyError | QueueClosedError> {
        if (this._elementCount === 0) {
            if (this._lifecycle !== LifecycleState.Active) {
                return err(this._closedError());
            }
            return err(QueueEmptyError.INSTANCE);
        }

        const item = this.executeShift();
        this.dispatchPendingProducer();
        this._telemetry.onDequeue?.(toQueueSize(this._elementCount));

        if (this._elementCount === 0) {
            this.evaluateDrainState();
        }

        return ok(item);
    }

    public dequeueBatch<const N extends number>(
        count: N
    ): Result<Batch<T, N>, QueueUnderflowError | QueueClosedError> {
        if (count <= 0) {
            const empty: readonly T[] = [];
            if (isBatch(empty, count)) {
                return ok(empty);
            }
            return err(new QueueUnderflowError(count, this._elementCount));
        }

        if (this._elementCount < count) {
            if (this._lifecycle !== LifecycleState.Active) {
                return err(this._closedError());
            }
            return err(new QueueUnderflowError(count, this._elementCount));
        }

        const batch = new Array<T>(count);
        for (let i = 0; i < count; i++) {
            batch[i] = this.executeShift();
            this.dispatchPendingProducer();
        }

        this._telemetry.onDequeue?.(toQueueSize(this._elementCount));

        if (this._elementCount === 0) {
            this.evaluateDrainState();
        }

        if (isBatch(batch, count)) {
            return ok(batch);
        }

        return err(new QueueUnderflowError(count, batch.length));
    }

    public enqueueAsync(
        item: NoInfer<T>,
        signal?: AbortSignal | undefined
    ): Promise<Result<void, QueueFullError | QueueClosedError | QueueAbortError>> {
        if (signal?.aborted) {
            return Promise.resolve(err(new QueueAbortError(signal.reason)));
        }

        if (this._lifecycle !== LifecycleState.Active) {
            return Promise.resolve(err(this._closedError()));
        }

        const consumer = this._consumers.shift();
        if (consumer !== null) {
            consumer.detach();
            consumer.resolve(ok(item));
            this._telemetry.onDequeue?.(toQueueSize(this._elementCount));
            return Promise.resolve(ok());
        }

        if (this._elementCount >= this._limit) {
            if (this._overflowStrategy === OverflowStrategy.Block) {
                this._telemetry.onSuspendProducer?.();
                return new Promise<Result<void, QueueFullError | QueueClosedError | QueueAbortError>>(
                    (resolve) => {
                        const waitNode = new ProducerWaitNode<T>(item, resolve, signal);

                        if (signal !== undefined) {
                            const onAbort = (): void => {
                                this._producers.remove(waitNode);
                                waitNode.detach();
                                waitNode.resolve(err(new QueueAbortError(signal.reason)));
                            };
                            waitNode.listener = onAbort;
                            signal.addEventListener('abort', onAbort, { once: true });
                        }

                        this._producers.append(waitNode);
                    }
                );
            }

            return Promise.resolve(this.enqueue(item));
        }

        this.executePush(item);
        this._telemetry.onEnqueue?.(toQueueSize(this._elementCount));
        return Promise.resolve(ok());
    }

    public dequeueAsync(
        signal?: AbortSignal | undefined
    ): Promise<Result<T, QueueClosedError | QueueAbortError>> {
        if (signal?.aborted) {
            return Promise.resolve(err(new QueueAbortError(signal.reason)));
        }

        if (this._elementCount > 0) {
            const item = this.executeShift();
            this.dispatchPendingProducer();
            this._telemetry.onDequeue?.(toQueueSize(this._elementCount));

            if (this._elementCount === 0) {
                this.evaluateDrainState();
            }
            return Promise.resolve(ok(item));
        }

        if (this._lifecycle !== LifecycleState.Active) {
            return Promise.resolve(err(this._closedError()));
        }

        this._telemetry.onSuspendConsumer?.();
        return new Promise<Result<T, QueueClosedError | QueueAbortError>>((resolve) => {
            const waitNode = new ConsumerWaitNode<T>(resolve, signal);

            if (signal !== undefined) {
                const onAbort = (): void => {
                    this._consumers.remove(waitNode);
                    waitNode.detach();
                    waitNode.resolve(err(new QueueAbortError(signal.reason)));
                };
                waitNode.listener = onAbort;
                signal.addEventListener('abort', onAbort, { once: true });
            }

            this._consumers.append(waitNode);
        });
    }

    public async *[Symbol.asyncIterator](): AsyncIterator<T> {
        while (true) {
            const result = await this.dequeueAsync();
            if (result.isOk()) {
                yield result.get();
            } else {
                const error = result.err()!;
                if (error instanceof QueueClosedError) {
                    if (this._terminalFault !== undefined) {
                        throw this._terminalFault;
                    }
                    return;
                }
                throw error;
            }
        }
    }

    public close(error?: Error | undefined): void {
        if (this._lifecycle !== LifecycleState.Active) {
            return;
        }

        this._terminalFault = error;
        const isDrained = this._elementCount === 0;
        this._lifecycle = isDrained ? LifecycleState.Drained : LifecycleState.Closing;

        const closedError = this._closedError();
        while (this._producers.length > 0) {
            const producer = this._producers.shift()!;
            producer.detach();
            producer.resolve(err(closedError));
        }

        if (isDrained) {
            this.flushConsumerNodes();
            this.flushDrainWaiters();
        }
    }

    public drain(signal?: AbortSignal | undefined): Promise<void> {
        if (this._elementCount === 0) {
            return Promise.resolve();
        }

        if (signal?.aborted) {
            return Promise.reject(new QueueAbortError(signal.reason));
        }

        return new Promise<void>((resolve, reject) => {
            const drainNode = new DrainWaitNode(resolve, reject, signal);

            if (signal !== undefined) {
                const onAbort = (): void => {
                    this._drainers.remove(drainNode);
                    drainNode.detach();
                    drainNode.reject(new QueueAbortError(signal.reason));
                };
                drainNode.listener = onAbort;
                signal.addEventListener('abort', onAbort, { once: true });
            }

            this._drainers.append(drainNode);
        });
    }

    public [Symbol_dispose](): void {
        this.close();
        this.disposeMemoryTopology();
        this._lifecycle = LifecycleState.Terminated;
        this.flushConsumerNodes();
        this.flushDrainWaiters();
    }

    public async [Symbol_asyncDispose](): Promise<void> {
        this.close();
        try {
            await this.drain();
        } catch {
            // Aborted or faulted drains are intentionally swallowed on disposal.
        } finally {
            this.disposeMemoryTopology();
            this._lifecycle = LifecycleState.Terminated;
            this.flushConsumerNodes();
            this.flushDrainWaiters();
        }
    }

    private _closedError(): QueueClosedError {
        return this._terminalFault !== undefined
            ? new QueueClosedError(this._terminalFault.message)
            : QueueClosedError.INSTANCE;
    }

    private executePush(item: T): void {
        let tail = this._chunkTail;
        if (tail.writeHead === CHUNK_SIZE) {
            const nextChunk = this._allocator.acquire();
            tail.next = nextChunk;
            this._chunkTail = nextChunk;
            tail = nextChunk;
        }
        tail.write(item);
        this._elementCount++;
    }

    private executeShift(): T {
        const head = this._chunkHead;
        const value = head.read();
        this._elementCount--;

        if (this._elementCount === 0) {
            head.readHead = 0;
            head.writeHead = 0;
        } else if (head.readHead === CHUNK_SIZE) {
            const forward = head.next;
            if (forward !== null) {
                this._chunkHead = forward;
                this._allocator.release(head);
            }
        }

        return value;
    }

    private dispatchPendingProducer(): void {
        const producer = this._producers.shift();
        if (producer !== null) {
            producer.detach();
            this.executePush(producer.element);
            this._telemetry.onEnqueue?.(toQueueSize(this._elementCount));
            producer.resolve(ok());
        }
    }

    private evaluateDrainState(): void {
        if (this._lifecycle === LifecycleState.Closing) {
            this._lifecycle = LifecycleState.Drained;
            this.flushConsumerNodes();
        }
        this.flushDrainWaiters();
    }

    private flushConsumerNodes(): void {
        const closedError = this._closedError();
        while (this._consumers.length > 0) {
            const consumer = this._consumers.shift()!;
            consumer.detach();
            consumer.resolve(err(closedError));
        }
    }

    private flushDrainWaiters(): void {
        while (this._drainers.length > 0) {
            const drainer = this._drainers.shift()!;
            drainer.detach();
            drainer.resolve();
        }
    }

    private disposeMemoryTopology(): void {
        let cursor: MemoryChunk<T> | null = this._chunkHead;
        while (cursor !== null) {
            const next: MemoryChunk<T> | null = cursor.next;
            cursor.reset();
            cursor = next;
        }
        this._chunkTail = this._chunkHead;
        this._elementCount = 0;
        this._allocator.clear();
    }
}

export type TypestateUnconfigured = { readonly __tag: 'UNCONFIGURED' };
export type TypestateBounded = { readonly __tag: 'BOUNDED' };
export type TypestateConfigured = { readonly __tag: 'CONFIGURED' };

export class QueueBuilder<T, S extends { readonly __tag: string } = TypestateUnconfigured> {
    private readonly _capacity: QueueCapacity;
    private readonly _strategy: OverflowStrategy;
    private readonly _metrics: IQueueMetrics;

    private constructor(capacity: QueueCapacity, strategy: OverflowStrategy, metrics: IQueueMetrics) {
        this._capacity = capacity;
        this._strategy = strategy;
        this._metrics = metrics;
    }

    public static create<T>(): QueueBuilder<T, TypestateUnconfigured> {
        return new QueueBuilder<T, TypestateUnconfigured>(
            UNBOUNDED_CAPACITY,
            OverflowStrategy.Reject,
            NULL_METRICS
        );
    }

    public unbounded(
        this: QueueBuilder<T, TypestateUnconfigured>
    ): QueueBuilder<T, TypestateConfigured> {
        return new QueueBuilder<T, TypestateConfigured>(
            UNBOUNDED_CAPACITY,
            OverflowStrategy.Reject,
            this._metrics
        );
    }

    public bounded(
        this: QueueBuilder<T, TypestateUnconfigured>,
        capacity: number
    ): QueueBuilder<T, TypestateBounded> {
        return new QueueBuilder<T, TypestateBounded>(
            toQueueCapacity(capacity),
            this._strategy,
            this._metrics
        );
    }

    public withOverflowStrategy(
        this: QueueBuilder<T, TypestateBounded>,
        strategy: OverflowStrategy
    ): QueueBuilder<T, TypestateConfigured> {
        return new QueueBuilder<T, TypestateConfigured>(this._capacity, strategy, this._metrics);
    }

    public withMetrics<const M extends IQueueMetrics>(
        this: QueueBuilder<T, S>,
        sink: M
    ): QueueBuilder<T, S> {
        return new QueueBuilder<T, S>(this._capacity, this._strategy, sink);
    }

    public build(this: QueueBuilder<T, TypestateConfigured>): Queue<T> {
        return new Queue<T>(this._capacity, this._strategy, this._metrics);
    }
}
