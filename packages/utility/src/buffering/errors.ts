class BaseBufferError extends Error {
    constructor(name: string, message: string) {
        super(message);
        this.name = name;
        Object.setPrototypeOf(this, new.target.prototype);
    }
}

export class BufferOverflowError extends BaseBufferError {
    constructor(message = 'Buffer overflow') {
        super('BufferOverflowError', message);
    }
}

export class BufferUnderflowError extends BaseBufferError {
    constructor(message = 'Buffer underflow') {
        super('BufferUnderflowError', message);
    }
}

export class ReadOnlyBufferError extends BaseBufferError {
    constructor(message = 'Buffer is read-only') {
        super('ReadOnlyBufferError', message);
    }
}

export class InvalidMarkError extends BaseBufferError {
    constructor(message = 'Mark not defined') {
        super('InvalidMarkError', message);
    }
}

export class BufferAlignmentError extends BaseBufferError {
    constructor(message = 'Invalid buffer alignment') {
        super('BufferAlignmentError', message);
    }
}

export class BufferReleasedError extends BaseBufferError {
    constructor(message = 'Buffer has been released to pool') {
        super('BufferReleasedError', message);
    }
}
