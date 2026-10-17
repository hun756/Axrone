declare const __brand: unique symbol;
type Brand<T, B> = T & { readonly [__brand]: B };

type StackCapacity = Brand<number, "StackCapacity">;
type StackId = Brand<string, "StackId">;

function assertStackCapacity(value: number): asserts value is StackCapacity {
    if (!Number.isSafeInteger(value) || value <= 0) {
        throw new RangeError(`Capacity must be a positive safe integer. Received: ${value}`);
    }
}

function assertStackId(value: string): asserts value is StackId {
    if (value.length === 0) {
        throw new RangeError("Stack identifier cannot be empty.");
    }
}

let globalStackSequence = 0;
function generateStackId(): StackId {
    globalStackSequence = (globalStackSequence + 1) | 0;
    const candidate = `stk_${Date.now().toString(36)}_${globalStackSequence.toString(36)}`;
    assertStackId(candidate);
    return candidate;
}

type Result<T, E> =
    | { readonly ok: true; readonly value: T }
    | { readonly ok: false; readonly error: E };

function ok<T>(value: T): Result<T, never> {
    return { ok: true, value };
}

function err<E>(error: E): Result<never, E> {
    return { ok: false, error };
}

const OK_VOID: Result<void, never> = Object.freeze({ ok: true, value: undefined });

const EMPTY_UNDERFLOW: Result<never, StackUnderflowError> = Object.freeze({
    ok: false,
    error: new StackUnderflowError(),
});

class StackUnderflowError extends Error {
    readonly _tag = "StackUnderflowError" as const;
    constructor() {
        super("Stack underflow: cannot pop or peek from an empty stack.");
        this.name = "StackUnderflowError";
    }
}

class StackOverflowError extends Error {
    readonly _tag = "StackOverflowError" as const;
    readonly limit: number;
    constructor(limit: number) {
        super(`Stack overflow: maximum capacity ${limit} reached.`);
        this.name = "StackOverflowError";
        this.limit = limit;
    }
}

class StackDisposedError extends Error {
    readonly _tag = "StackDisposedError" as const;
    readonly id: StackId;
    readonly cause: Error | undefined;
    constructor(id: StackId, cause?: Error | undefined) {
        super(`Stack '${id}' is disposed and cannot process operations.`);
        this.name = "StackDisposedError";
        this.id = id;
        this.cause = cause;
    }
}

class StackAbortError extends Error {
    readonly _tag = "StackAbortError" as const;
    readonly reason: unknown;
    constructor(reason: unknown) {
        super(`Stack operation was aborted: ${String(reason)}`);
        this.name = "StackAbortError";
        this.reason = reason;
    }
}

type StackError =
    | StackUnderflowError
    | StackOverflowError
    | StackDisposedError
    | StackAbortError;

const LIFECYCLE_ACTIVE = 0 as const;
const LIFECYCLE_DISPOSED = 1 as const;
type Lifecycle = typeof LIFECYCLE_ACTIVE | typeof LIFECYCLE_DISPOSED;

interface IStackMetricsSink {
    onPush(): void;
    onPop(): void;
    onChunkAllocated(): void;
    onChunkDeallocated(): void;
}

const MonomorphicNoOpMetricsSink: Readonly<IStackMetricsSink> = Object.freeze({
    onPush(): void {},
    onPop(): void {},
    onChunkAllocated(): void {},
    onChunkDeallocated(): void {},
});

const ITERATOR_DONE: IteratorReturnResult<undefined> = Object.freeze({
    done: true,
    value: undefined,
});

class ChunkedStackIterator<T> implements Iterator<T> {
    private readonly _chunks: readonly (readonly (T | undefined)[])[];
    private _cursor: number;

    constructor(
        chunks: readonly (readonly (T | undefined)[])[],
        size: number
    ) {
        this._chunks = chunks;
        this._cursor = (size - 1) | 0;
    }

    next(): IteratorResult<T> {
        const cursor = this._cursor;
        if (cursor < 0) {
            return ITERATOR_DONE;
        }
        this._cursor = (cursor - 1) | 0;
        const chunkIdx = cursor >>> 6;
        const slotIdx = cursor & 63;
        const chunk = this._chunks[chunkIdx];
        if (chunk === undefined) {
            return ITERATOR_DONE;
        }
        const value = chunk[slotIdx] as T;
        return { done: false, value };
    }
}

const CHUNK_SIZE = 64 as const;
const CHUNK_MASK = 63 as const;
const CHUNK_SHIFT = 6 as const;
const MAX_POOLED_CHUNKS = 4 as const;
const HYSTERESIS_THRESHOLD = 2 as const;

function allocatePackedChunk<T>(): (T | undefined)[] {
    const chunk: (T | undefined)[] = new Array(CHUNK_SIZE);
    for (let i = 0; i < CHUNK_SIZE; i = (i + 1) | 0) {
        chunk[i] = undefined;
    }
    return chunk;
}

export class ChunkedStack<T> {
    readonly id: StackId;
    private _chunks: (T | undefined)[][];
    private _chunkCount: number;
    private readonly _chunkPool: (T | undefined)[][];
    private _size: number;
    private readonly _maxCapacity: number;
    private _lifecycle: Lifecycle;
    private _disposedReason: Error | undefined;
    private readonly _metrics: IStackMetricsSink;

    constructor(
        id: StackId,
        maxCapacity: StackCapacity,
        metrics: IStackMetricsSink
    ) {
        this.id = id;
        this._chunks = [allocatePackedChunk<T>()];
        this._chunkCount = 1;
        this._chunkPool = [];
        this._size = 0;
        this._maxCapacity = maxCapacity;
        this._lifecycle = LIFECYCLE_ACTIVE;
        this._disposedReason = undefined;
        this._metrics = metrics;
    }

    get size(): number {
        return this._size;
    }

    get isEmpty(): boolean {
        return this._size === 0;
    }

    get isDisposed(): boolean {
        return this._lifecycle === LIFECYCLE_DISPOSED;
    }

    push(item: T): void {
        if (this._lifecycle !== LIFECYCLE_ACTIVE) {
            this._coldFailDisposed();
        }
        const size = this._size;
        if (size === this._maxCapacity) {
            this._coldFailOverflow();
        }
        const chunkIdx = size >>> CHUNK_SHIFT;
        const slotIdx = size & CHUNK_MASK;

        if (chunkIdx >= this._chunkCount) {
            this._allocateChunk();
        }

        this._chunks[chunkIdx]![slotIdx] = item;
        this._size = (size + 1) | 0;
        this._metrics.onPush();
    }

    tryPush(item: T): Result<void, StackOverflowError | StackDisposedError> {
        if (this._lifecycle !== LIFECYCLE_ACTIVE) {
            return err(new StackDisposedError(this.id, this._disposedReason));
        }
        const size = this._size;
        if (size === this._maxCapacity) {
            return err(new StackOverflowError(this._maxCapacity));
        }
        const chunkIdx = size >>> CHUNK_SHIFT;
        const slotIdx = size & CHUNK_MASK;

        if (chunkIdx >= this._chunkCount) {
            this._allocateChunk();
        }

        this._chunks[chunkIdx]![slotIdx] = item;
        this._size = (size + 1) | 0;
        this._metrics.onPush();
        return OK_VOID;
    }

    pop(): T {
        if (this._lifecycle !== LIFECYCLE_ACTIVE) {
            this._coldFailDisposed();
        }
        const size = this._size;
        if (size === 0) {
            this._coldFailUnderflow();
        }
        const targetIdx = (size - 1) | 0;
        const chunkIdx = targetIdx >>> CHUNK_SHIFT;
        const slotIdx = targetIdx & CHUNK_MASK;

        const chunk = this._chunks[chunkIdx]!;
        const item = chunk[slotIdx] as T;
        chunk[slotIdx] = undefined;
        this._size = targetIdx;
        this._metrics.onPop();

        const neededChunks = targetIdx === 0 ? 1 : ((targetIdx >>> CHUNK_SHIFT) + 1) | 0;
        if ((this._chunkCount - neededChunks) > HYSTERESIS_THRESHOLD) {
            this._trimExcessChunks(neededChunks);
        }

        return item;
    }

    tryPop(): Result<T, StackUnderflowError | StackDisposedError> {
        if (this._lifecycle !== LIFECYCLE_ACTIVE) {
            return err(new StackDisposedError(this.id, this._disposedReason));
        }
        const size = this._size;
        if (size === 0) {
            return EMPTY_UNDERFLOW as Result<T, StackUnderflowError>;
        }
        const targetIdx = (size - 1) | 0;
        const chunkIdx = targetIdx >>> CHUNK_SHIFT;
        const slotIdx = targetIdx & CHUNK_MASK;

        const chunk = this._chunks[chunkIdx]!;
        const item = chunk[slotIdx] as T;
        chunk[slotIdx] = undefined;
        this._size = targetIdx;
        this._metrics.onPop();

        const neededChunks = targetIdx === 0 ? 1 : ((targetIdx >>> CHUNK_SHIFT) + 1) | 0;
        if ((this._chunkCount - neededChunks) > HYSTERESIS_THRESHOLD) {
            this._trimExcessChunks(neededChunks);
        }

        return ok(item);
    }

    peek(): T {
        if (this._lifecycle !== LIFECYCLE_ACTIVE) {
            this._coldFailDisposed();
        }
        const size = this._size;
        if (size === 0) {
            this._coldFailUnderflow();
        }
        const targetIdx = (size - 1) | 0;
        const chunkIdx = targetIdx >>> CHUNK_SHIFT;
        const slotIdx = targetIdx & CHUNK_MASK;
        return this._chunks[chunkIdx]![slotIdx] as T;
    }

    peekUnsafe(): T | undefined {
        const size = this._size;
        if (size === 0) {
            return undefined;
        }
        const targetIdx = (size - 1) | 0;
        const chunkIdx = targetIdx >>> CHUNK_SHIFT;
        const slotIdx = targetIdx & CHUNK_MASK;
        return this._chunks[chunkIdx]![slotIdx] as T;
    }

    tryPeek(): Result<T, StackUnderflowError | StackDisposedError> {
        if (this._lifecycle !== LIFECYCLE_ACTIVE) {
            return err(new StackDisposedError(this.id, this._disposedReason));
        }
        const size = this._size;
        if (size === 0) {
            return EMPTY_UNDERFLOW as Result<T, StackUnderflowError>;
        }
        const targetIdx = (size - 1) | 0;
        const chunkIdx = targetIdx >>> CHUNK_SHIFT;
        const slotIdx = targetIdx & CHUNK_MASK;
        return ok(this._chunks[chunkIdx]![slotIdx] as T);
    }

    pushMany(items: readonly T[]): void {
        if (this._lifecycle !== LIFECYCLE_ACTIVE) {
            this._coldFailDisposed();
        }
        const count = items.length;
        if (count === 0) {
            return;
        }
        if (this._size + count > this._maxCapacity) {
            this._coldFailOverflow();
        }

        for (let i = 0; i < count; i = (i + 1) | 0) {
            const size = this._size;
            const chunkIdx = size >>> CHUNK_SHIFT;
            const slotIdx = size & CHUNK_MASK;
            if (chunkIdx >= this._chunkCount) {
                this._allocateChunk();
            }
            this._chunks[chunkIdx]![slotIdx] = items[i] as T;
            this._size = (size + 1) | 0;
            this._metrics.onPush();
        }
    }

    toArray(): T[] {
        if (this._lifecycle !== LIFECYCLE_ACTIVE) {
            this._coldFailDisposed();
        }
        const len = this._size;
        if (len === 0) {
            return [];
        }
        const result: T[] = new Array(len);
        let readIdx = (len - 1) | 0;
        let writeIdx = 0;

        while (writeIdx < len) {
            const chunkIdx = readIdx >>> CHUNK_SHIFT;
            const slotIdx = readIdx & CHUNK_MASK;
            result[writeIdx] = this._chunks[chunkIdx]![slotIdx] as T;
            writeIdx = (writeIdx + 1) | 0;
            readIdx = (readIdx - 1) | 0;
        }
        return result;
    }

    [Symbol.iterator](): Iterator<T> {
        if (this._lifecycle !== LIFECYCLE_ACTIVE) {
            this._coldFailDisposed();
        }
        return new ChunkedStackIterator<T>(this._chunks, this._size);
    }

    async drain(
        signal?: AbortSignal | undefined
    ): Promise<Result<T[], StackAbortError | StackDisposedError>> {
        if (this._lifecycle !== LIFECYCLE_ACTIVE) {
            return err(new StackDisposedError(this.id, this._disposedReason));
        }
        if (signal?.aborted) {
            return err(new StackAbortError(signal.reason));
        }

        const drained: T[] = new Array(this._size);
        let writeIdx = 0;
        let batchCounter = 0;

        let abortListener: (() => void) | undefined = undefined;
        let abortedReason: unknown = undefined;

        if (signal !== undefined) {
            abortListener = () => {
                abortedReason = signal.reason;
            };
            signal.addEventListener("abort", abortListener, { once: true });
        }

        try {
            while (this._size > 0) {
                if (abortedReason !== undefined) {
                    return err(new StackAbortError(abortedReason));
                }

                const targetIdx = (this._size - 1) | 0;
                const chunkIdx = targetIdx >>> CHUNK_SHIFT;
                const slotIdx = targetIdx & CHUNK_MASK;

                const chunk = this._chunks[chunkIdx]!;
                const item = chunk[slotIdx] as T;
                chunk[slotIdx] = undefined;
                this._size = targetIdx;
                this._metrics.onPop();

                drained[writeIdx] = item;
                writeIdx = (writeIdx + 1) | 0;
                batchCounter = (batchCounter + 1) | 0;

                if ((batchCounter & 511) === 0 && this._size > 0) {
                    await new Promise<void>((resolve) => {
                        queueMicrotask(resolve);
                    });
                }
            }

            this._trimExcessChunks(1);
            return ok(drained);
        } finally {
            if (signal !== undefined && abortListener !== undefined) {
                signal.removeEventListener("abort", abortListener);
            }
        }
    }

    close(error?: Error | undefined): void {
        if (this._lifecycle === LIFECYCLE_DISPOSED) {
            return;
        }
        this._lifecycle = LIFECYCLE_DISPOSED;
        this._disposedReason = error;

        const chunks = this._chunks;
        const total = this._chunkCount;
        for (let i = 0; i < total; i = (i + 1) | 0) {
            const chunk = chunks[i]!;
            for (let j = 0; j < CHUNK_SIZE; j = (j + 1) | 0) {
                chunk[j] = undefined;
            }
            this._metrics.onChunkDeallocated();
        }

        chunks.length = 0;
        this._chunkPool.length = 0;
        this._size = 0;
        this._chunkCount = 0;
    }

    [Symbol.dispose](): void {
        this.close();
    }

    async [Symbol.asyncDispose](): Promise<void> {
        if (this._lifecycle === LIFECYCLE_DISPOSED) {
            return;
        }
        await this.drain();
        this.close();
    }

    private _allocateChunk(): void {
        const pool = this._chunkPool;
        let chunk = pool.pop();
        if (chunk === undefined) {
            chunk = allocatePackedChunk<T>();
            this._metrics.onChunkAllocated();
        }
        this._chunks.push(chunk);
        this._chunkCount = (this._chunkCount + 1) | 0;
    }

    private _trimExcessChunks(neededChunks: number): void {
        while (this._chunkCount > neededChunks) {
            const chunk = this._chunks.pop();
            this._chunkCount = (this._chunkCount - 1) | 0;
            if (chunk !== undefined) {
                for (let i = 0; i < CHUNK_SIZE; i = (i + 1) | 0) {
                    chunk[i] = undefined;
                }
                if (this._chunkPool.length < MAX_POOLED_CHUNKS) {
                    this._chunkPool.push(chunk);
                } else {
                    this._metrics.onChunkDeallocated();
                }
            }
        }
    }

    private _coldFailOverflow(): never {
        throw new StackOverflowError(this._maxCapacity);
    }

    private _coldFailUnderflow(): never {
        throw new StackUnderflowError();
    }

    private _coldFailDisposed(): never {
        throw new StackDisposedError(this.id, this._disposedReason);
    }

    static create<T>(): ChunkedStack<T> {
        const capacity = Number.MAX_SAFE_INTEGER;
        assertStackCapacity(capacity);
        return new ChunkedStack<T>(
            generateStackId(),
            capacity,
            MonomorphicNoOpMetricsSink
        );
    }

    static from<T>(items: readonly T[]): ChunkedStack<T> {
        const capacity = Number.MAX_SAFE_INTEGER;
        assertStackCapacity(capacity);
        const stack = new ChunkedStack<T>(
            generateStackId(),
            capacity,
            MonomorphicNoOpMetricsSink
        );
        stack.pushMany(items);
        return stack;
    }
}
