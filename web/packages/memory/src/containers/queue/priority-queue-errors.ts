/**
 * Error classes for priority queue operations.
 * @stable
 */

/** @stable */
export class PriorityQueueError extends Error {
    public override readonly name: string = 'PriorityQueueError';

    constructor(message: string) {
        super(message);
        Object.setPrototypeOf(this, new.target.prototype);
    }
}

/** @stable */
export class PriorityQueueComparatorError extends PriorityQueueError {
    public override readonly name: string = 'PriorityQueueComparatorError';

    constructor(message = 'A valid comparator function is required for this priority queue.') {
        super(message);
    }
}

/** @stable */
export class PriorityQueuePriorityError extends PriorityQueueError {
    public override readonly name: string = 'PriorityQueuePriorityError';

    constructor(message = 'A priority selector or explicit priority value is required for this operation.') {
        super(message);
    }
}

/** @stable */
export class PriorityQueueHandleError extends PriorityQueueError {
    public override readonly name: string = 'PriorityQueueHandleError';

    constructor(message = 'The provided priority queue handle is invalid or does not exist.') {
        super(message);
    }
}

/** @stable */
export class PriorityQueueSerializationError extends PriorityQueueError {
    public override readonly name: string = 'PriorityQueueSerializationError';

    constructor(message = 'Invalid priority queue serialization payload.') {
        super(message);
    }
}
