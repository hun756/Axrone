/** @stable */
export abstract class QueueError extends Error {
    abstract readonly code: string;

    constructor(message: string) {
        super(message);
        this.name = this.constructor.name;
        (
            Error as typeof Error & { captureStackTrace?: (target: object, ctor: Function) => void }
        ).captureStackTrace?.(this, this.constructor);
    }
}

/** @stable */
export class EmptyQueueError extends QueueError {
    readonly code = 'EMPTY_QUEUE' as const;

    constructor() {
        super('Queue is empty');
    }
}

/** @stable */
export class InvalidCapacityError extends QueueError {
    readonly code = 'INVALID_CAPACITY' as const;

    constructor(capacity: number) {
        super(`Invalid capacity: ${capacity}`);
    }
}
