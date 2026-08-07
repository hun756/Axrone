export type {
    Base64Alphabet,
    Base64String,
    BinaryInput,
    PaddingPolicy,
    PaddingVerificationPolicy,
    EncodingOptions,
    DecodingOptions,
    ValidationOptions,
    Base64ErrorKind,
    Base64Result,
} from './types';

export {
    Base64Error,
    InvalidInputError,
    InvalidCharacterError,
    InvalidLengthError,
    InvalidPaddingError,
    InvalidDataError,
    EnvironmentError,
    UnexpectedError,
} from './errors';

export {
    encode,
    encodeToBytes,
    decode,
    decodeToString,
    assertBase64,
    isBase64,
    tryEncode,
    tryDecode,
} from './base64';
