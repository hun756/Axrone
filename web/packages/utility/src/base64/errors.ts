import type { Base64Alphabet, Base64ErrorKind } from './types';

export abstract class Base64Error extends Error {
    abstract readonly kind: Base64ErrorKind;

    constructor(message: string, options?: ErrorOptions) {
        super(message, options);
        this.name = new.target.name;
        Object.setPrototypeOf(this, new.target.prototype);
    }
}

export class InvalidInputError extends Base64Error {
    readonly kind: 'INVALID_INPUT' = 'INVALID_INPUT';

    constructor(message = 'Invalid input.', options?: ErrorOptions) {
        super(message, options);
    }
}

export class InvalidCharacterError extends Base64Error {
    readonly kind: 'INVALID_CHARACTER' = 'INVALID_CHARACTER';
    readonly character: string;
    readonly index: number;
    readonly alphabet: Base64Alphabet;

    constructor(character: string, index: number, alphabet: Base64Alphabet, options?: ErrorOptions) {
        super(`Invalid Base64 character "${character}" at index ${index} for alphabet "${alphabet}".`, options);
        this.character = character;
        this.index = index;
        this.alphabet = alphabet;
    }
}

export class InvalidLengthError extends Base64Error {
    readonly kind: 'INVALID_LENGTH' = 'INVALID_LENGTH';
    readonly inputLength: number;

    constructor(inputLength: number, message = `Invalid Base64 input length: ${inputLength}.`, options?: ErrorOptions) {
        super(message, options);
        this.inputLength = inputLength;
    }
}

export class InvalidPaddingError extends Base64Error {
    readonly kind: 'INVALID_PADDING' = 'INVALID_PADDING';
    readonly index: number | undefined;

    constructor(index: number | undefined, message = 'Invalid Base64 padding.', options?: ErrorOptions) {
        super(message, options);
        this.index = index;
    }
}

export class InvalidDataError extends Base64Error {
    readonly kind: 'INVALID_DATA' = 'INVALID_DATA';

    constructor(message = 'Invalid Base64 data.', options?: ErrorOptions) {
        super(message, options);
    }
}

export class EnvironmentError extends Base64Error {
    readonly kind: 'ENVIRONMENT' = 'ENVIRONMENT';

    constructor(message = 'Required environment capability is unavailable.', options?: ErrorOptions) {
        super(message, options);
    }
}

export class UnexpectedError extends Base64Error {
    readonly kind: 'UNEXPECTED' = 'UNEXPECTED';

    constructor(message = 'An unexpected error occurred.', options?: ErrorOptions) {
        super(message, options);
    }
}
