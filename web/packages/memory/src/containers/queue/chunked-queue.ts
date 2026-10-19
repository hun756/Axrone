import { type Brand, err, ok, type Result } from '@axrone/utility';

const Symbol_dispose: typeof Symbol.dispose =
    (Symbol as { dispose?: typeof Symbol.dispose }).dispose ??
    (Symbol.for('Symbol.dispose') as never);

const Symbol_asyncDispose: typeof Symbol.asyncDispose =
    (Symbol as { asyncDispose?: typeof Symbol.asyncDispose }).asyncDispose ??
    (Symbol.for('Symbol.asyncDispose') as never);

export type QueueCapacity = Brand<number, 'QueueCapacity'>;
export type QueueSize = Brand<number, 'QueueSize'>;

export const UNBOUNDED_CAPACITY: QueueCapacity = Number.MAX_SAFE_INTEGER as QueueCapacity;

export type Batch<T, N extends number, Acc extends readonly T[] = []> = number extends N
    ? readonly T[]
    : Acc['length'] extends N
      ? Acc
      : Batch<T, N, readonly [...Acc, T]>;

export function isBatch<T, N extends number>(items: readonly T[], count: N): items is Batch<T, N> {
    return items.length === count;
}

export type QueueErrorCode =
    | 'QUEUE_EMPTY'
    | 'QUEUE_FULL'
    | 'QUEUE_CLOSED'
    | 'QUEUE_ABORTED'
    | 'QUEUE_UNDERFLOW'
    | 'QUEUE_INVALID_CAPACITY';

export abstract class QueueDomainError extends Error {
    public abstract readonly code: QueueErrorCode;

    constructor(message: string) {
        super(message);
        this.name = new.target.name;
        Object.setPrototypeOf(this, new.target.prototype);
    }
}

export class QueueEmptyError extends QueueDomainError {
    public override readonly code = 'QUEUE_EMPTY';
    public static readonly INSTANCE = new QueueEmptyError();

    constructor(message: string = 'Queue is empty.') {
        super(message);
    }
}

export class QueueFullError extends QueueDomainError {
    public override readonly code = 'QUEUE_FULL';
    public static readonly INSTANCE = new QueueFullError();

    constructor(message: string = 'Queue is full.') {
        super(message);
    }
}

export class QueueClosedError extends QueueDomainError {
    public override readonly code = 'QUEUE_CLOSED';
    public static readonly INSTANCE = new QueueClosedError();

    constructor(message: string = 'Queue is closed.') {
        super(message);
    }
}

export class QueueAbortError extends QueueDomainError {
    public override readonly code = 'QUEUE_ABORTED';
    public static readonly INSTANCE = new QueueAbortError();

    constructor(message: string = 'Queue operation aborted.') {
        super(message);
    }
}

export class QueueUnderflowError extends QueueDomainError {
    public override readonly code = 'QUEUE_UNDERFLOW';

    constructor(requested: number, actual: number) {
        super(`Requested ${requested} items, but only ${actual} available.`);
    }
}

export class QueueInvalidCapacityError extends QueueDomainError {
    public override readonly code = 'QUEUE_INVALID_CAPACITY';

    constructor(value: number) {
        super(`Specified capacity ${value} is invalid. Expected positive safe integer.`);
    }
}

function throwInvalidCapacity(value: number): never {
    throw new QueueInvalidCapacityError(value);
}

function throwCorruptedSlot(): never {
    throw new Error('Queue memory chunk slot corruption detected.');
}

export function assertNever(x: never, message: string = 'Unreachable branch'): never {
    throw new Error(`${message}: ${String(x)}`);
}

export function isValidQueueCapacity(value: number): value is QueueCapacity {
    return Number.isSafeInteger(value) && value > 0;
}

export function isValidQueueSize(value: number): value is QueueSize {
    return Number.isSafeInteger(value) && value >= 0;
}

export function toQueueCapacity(value: number): QueueCapacity {
    if (!isValidQueueCapacity(value)) {
        throwInvalidCapacity(value);
    }
    return value;
}

export function toQueueSize(value: number): QueueSize {
    if (!isValidQueueSize(value)) {
        throw new RangeError(`Specified size ${value} is invalid. Expected non-negative safe integer.`);
    }
    return value;
}

export const OverflowStrategy = {
    Reject: 'REJECT',
    DropHead: 'DROP_HEAD',
    DropTail: 'DROP_TAIL',
    Block: 'BLOCK',
} as const;

export type OverflowStrategy = (typeof OverflowStrategy)[keyof typeof OverflowStrategy];

export const LifecycleState = {
    Active: 'ACTIVE',
    Closing: 'CLOSING',
    Drained: 'DRAINED',
    Terminated: 'TERMINATED',
} as const;

export type LifecycleState = (typeof LifecycleState)[keyof typeof LifecycleState];

export interface IQueueMetrics {
    readonly onEnqueue?: ((size: QueueSize) => void) | undefined;
    readonly onDequeue?: ((size: QueueSize) => void) | undefined;
    readonly onDrop?: ((strategy: OverflowStrategy) => void) | undefined;
    readonly onSuspendConsumer?: (() => void) | undefined;
    readonly onSuspendProducer?: (() => void) | undefined;
}

export const NULL_METRICS: IQueueMetrics = Object.freeze({});

export interface IQueueSource<out T> {
    readonly size: QueueSize;
    readonly capacity: QueueCapacity;
    readonly isEmpty: boolean;
    readonly isClosed: boolean;
    peek(): T | undefined;
    dequeue(): Result<T, QueueEmptyError | QueueClosedError>;
    dequeueBatch<const N extends number>(
        count: N
    ): Result<Batch<T, N>, QueueUnderflowError | QueueClosedError>;
}

export interface IQueueSink<in T> {
    readonly size: QueueSize;
    readonly capacity: QueueCapacity;
    readonly isFull: boolean;
    readonly isClosed: boolean;
    enqueue(item: NoInfer<T>): Result<void, QueueFullError | QueueClosedError>;
}

export interface IAsyncQueueSource<out T> {
    dequeueAsync(
        signal?: AbortSignal | undefined
    ): Promise<Result<T, QueueClosedError | QueueAbortError>>;
    [Symbol.asyncIterator](): AsyncIterator<T>;
}

export interface IAsyncQueueSink<in T> {
    enqueueAsync(
        item: NoInfer<T>,
        signal?: AbortSignal | undefined
    ): Promise<Result<void, QueueFullError | QueueClosedError | QueueAbortError>>;
}

export interface IQueueLifecycle {
    readonly state: LifecycleState;
    close(error?: Error | undefined): void;
    drain(signal?: AbortSignal | undefined): Promise<void>;
    [Symbol_dispose](): void;
    [Symbol_asyncDispose](): Promise<void>;
}

export interface IQueue<T>
    extends IQueueSource<T>,
        IQueueSink<T>,
        IAsyncQueueSource<T>,
        IAsyncQueueSink<T>,
        IQueueLifecycle {}

const CHUNK_SIZE = 512;
const CHUNK_POOL_LIMIT = 64;
const EMPTY_SLOT: unique symbol = Symbol('EMPTY_SLOT');
type EmptySlot = typeof EMPTY_SLOT;

function assertItemSlot<V>(value: V | EmptySlot): asserts value is V {
    if (value === EMPTY_SLOT) {
        throwCorruptedSlot();
    }
}

class MemoryChunk<T> {
    public next: MemoryChunk<T> | null = null;
    public readHead: number = 0;
    public writeHead: number = 0;
    private readonly _slots: (T | EmptySlot)[];

    constructor() {
        this._slots = new Array<T | EmptySlot>(CHUNK_SIZE).fill(EMPTY_SLOT);
    }

    public write(item: T): void {
        this._slots[this.writeHead] = item;
        this.writeHead++;
    }

    public read(): T {
        const index = this.readHead;
        const value = this._slots[index];
        this._slots[index] = EMPTY_SLOT;
        this.readHead = index + 1;
        assertItemSlot(value);
        return value;
    }

    public peek(): T | undefined {
        const value = this._slots[this.readHead];
        if (value === undefined || value === EMPTY_SLOT) {
            return undefined;
        }
        return value;
    }

    public reset(): void {
        this.next = null;
        this.readHead = 0;
        this.writeHead = 0;
        this._slots.fill(EMPTY_SLOT);
    }
}

class ChunkPool<T> {
    private _head: MemoryChunk<T> | null = null;
    private _size: number = 0;

    public acquire(): MemoryChunk<T> {
        const chunk = this._head;
        if (chunk !== null) {
            this._head = chunk.next;
            chunk.next = null;
            this._size--;
            return chunk;
        }
        return new MemoryChunk<T>();
    }

    public release(chunk: MemoryChunk<T>): void {
        if (this._size < CHUNK_POOL_LIMIT) {
            chunk.reset();
            chunk.next = this._head;
            this._head = chunk;
            this._size++;
        } else {
            chunk.reset();
        }
    }

    public clear(): void {
        let cursor = this._head;
        while (cursor !== null) {
            const next = cursor.next;
            cursor.reset();
            cursor = next;
        }
        this._head = null;
        this._size = 0;
    }
}

interface ILinkedListNode<N> {
    prev: N | null;
    next: N | null;
}

class IntrusiveQueue<N extends ILinkedListNode<N>> {
    public head: N | null = null;
    public tail: N | null = null;
    public length: number = 0;

    public append(node: N): void {
        if (this.tail === null) {
            this.head = node;
            this.tail = node;
            node.prev = null;
            node.next = null;
        } else {
            node.prev = this.tail;
            node.next = null;
            this.tail.next = node;
            this.tail = node;
        }
        this.length++;
    }

    public remove(node: N): void {
        if (node.prev !== null) {
            node.prev.next = node.next;
        } else if (this.head === node) {
            this.head = node.next;
        }

        if (node.next !== null) {
            node.next.prev = node.prev;
        } else if (this.tail === node) {
            this.tail = node.prev;
        }

        node.prev = null;
        node.next = null;
        this.length--;
    }

    public shift(): N | null {
        const first = this.head;
        if (first === null) {
            return null;
        }
        this.remove(first);
        return first;
    }
}

class ConsumerWaitNode<T> implements ILinkedListNode<ConsumerWaitNode<T>> {
    public prev: ConsumerWaitNode<T> | null = null;
    public next: ConsumerWaitNode<T> | null = null;
    public readonly signal: AbortSignal | undefined;
    public listener: (() => void) | null = null;
    public readonly resolve: (result: Result<T, QueueClosedError | QueueAbortError>) => void;

    constructor(
        resolve: (result: Result<T, QueueClosedError | QueueAbortError>) => void,
        signal: AbortSignal | undefined
    ) {
        this.resolve = resolve;
        this.signal = signal;
    }

    public detach(): void {
        this.prev = null;
        this.next = null;
        if (this.signal !== undefined && this.listener !== null) {
            this.signal.removeEventListener('abort', this.listener);
            this.listener = null;
        }
    }
}

class ProducerWaitNode<T> implements ILinkedListNode<ProducerWaitNode<T>> {
    public prev: ProducerWaitNode<T> | null = null;
    public next: ProducerWaitNode<T> | null = null;
    public readonly element: T;
    public readonly signal: AbortSignal | undefined;
    public listener: (() => void) | null = null;
    public readonly resolve: (
        result: Result<void, QueueFullError | QueueClosedError | QueueAbortError>
    ) => void;

    constructor(
        element: T,
        resolve: (result: Result<void, QueueFullError | QueueClosedError | QueueAbortError>) => void,
        signal: AbortSignal | undefined
    ) {
        this.element = element;
        this.resolve = resolve;
        this.signal = signal;
    }

    public detach(): void {
        this.prev = null;
        this.next = null;
        if (this.signal !== undefined && this.listener !== null) {
            this.signal.removeEventListener('abort', this.listener);
            this.listener = null;
        }
    }
}

class DrainWaitNode implements ILinkedListNode<DrainWaitNode> {
    public prev: DrainWaitNode | null = null;
    public next: DrainWaitNode | null = null;
    public readonly signal: AbortSignal | undefined;
    public listener: (() => void) | null = null;
    public readonly resolve: () => void;
    public readonly reject: (reason: QueueAbortError) => void;

    constructor(
        resolve: () => void,
        reject: (reason: QueueAbortError) => void,
        signal: AbortSignal | undefined
    ) {
        this.resolve = resolve;
        this.reject = reject;
        this.signal = signal;
    }

    public detach(): void {
        this.prev = null;
        this.next = null;
        if (this.signal !== undefined && this.listener !== null) {
            this.signal.removeEventListener('abort', this.listener);
            this.listener = null;
        }
    }
}

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
            return Promise.resolve(err(new QueueAbortError(abortMessage(signal))));
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
                                waitNode.resolve(err(new QueueAbortError(abortMessage(signal))));
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
            return Promise.resolve(err(new QueueAbortError(abortMessage(signal))));
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
                    waitNode.resolve(err(new QueueAbortError(abortMessage(signal))));
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
            return Promise.reject(new QueueAbortError(abortMessage(signal)));
        }

        return new Promise<void>((resolve, reject) => {
            const drainNode = new DrainWaitNode(resolve, reject, signal);

            if (signal !== undefined) {
                const onAbort = (): void => {
                    this._drainers.remove(drainNode);
                    drainNode.detach();
                    drainNode.reject(new QueueAbortError(abortMessage(signal)));
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

function abortMessage(signal: AbortSignal): string {
    return signal.reason !== undefined ? String(signal.reason) : 'Queue operation aborted.';
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
