/**
 * Error classes for binary heap operations.
 * @stable
 */

/** @stable */
export class HeapError extends Error {
    public override readonly name: string = 'HeapError';

    constructor(message: string) {
        super(message);
        Object.setPrototypeOf(this, new.target.prototype);
    }
}

/** @stable */
export class HeapComparatorError extends HeapError {
    public override readonly name: string = 'HeapComparatorError';

    constructor(message = 'A valid comparator function is required for this heap.') {
        super(message);
    }
}

/** @stable */
export class HeapIndexError extends HeapError {
    public override readonly name: string = 'HeapIndexError';

    constructor(message = 'Heap index is out of range.') {
        super(message);
    }
}

/** @stable */
export class HeapSerializationError extends HeapError {
    public override readonly name: string = 'HeapSerializationError';

    constructor(message = 'Invalid heap serialization payload.') {
        super(message);
    }
}
