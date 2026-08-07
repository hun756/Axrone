declare const base64Brand: unique symbol;
declare const base64Alphabet: unique symbol;

export type Base64Alphabet = 'standard' | 'urlsafe';

export type Base64String<A extends Base64Alphabet = Base64Alphabet> = string & {
    readonly [base64Brand]: 'Base64String';
    readonly [base64Alphabet]: A;
};

export type BinaryInput = string | ArrayBuffer | ArrayBufferView;

export type PaddingPolicy = 'include' | 'omit';

export type PaddingVerificationPolicy = 'require' | 'optional' | 'forbid';

export interface EncodingOptions<A extends Base64Alphabet = Base64Alphabet> {
    readonly alphabet?: A;
    readonly padding?: PaddingPolicy;
}

export interface DecodingOptions<A extends Base64Alphabet = Base64Alphabet> {
    readonly alphabet?: A;
    readonly padding?: PaddingVerificationPolicy;
}

export type ValidationOptions<A extends Base64Alphabet = Base64Alphabet> = DecodingOptions<A>;

export type Base64ErrorKind =
    | 'INVALID_INPUT'
    | 'INVALID_CHARACTER'
    | 'INVALID_LENGTH'
    | 'INVALID_PADDING'
    | 'INVALID_DATA'
    | 'ENVIRONMENT'
    | 'UNEXPECTED';

/**
 * Lightweight discriminated-union result for Base64 operations.
 * Named differently from the package-level `Result<T,E>` (Ok/Err class union)
 * to avoid export conflicts.
 */
export type Base64Result<T, E = Error> =
    | { readonly ok: true; readonly value: T }
    | { readonly ok: false; readonly error: E };
