export type HashErrorCode =
    | 'HASH_INVALID_INPUT'
    | 'HASH_INVALID_KEY'
    | 'HASH_INVALID_SEED'
    | 'HASH_STATE_CORRUPTED'
    | 'HASH_ALREADY_FINALIZED'
    | 'HASH_CONTEXT_OVERFLOW'
    | 'HASH_NOT_FINALIZED'
    | 'HASH_ALGORITHM_NOT_FOUND'
    | 'HASH_ALGORITHM_UNSUPPORTED'
    | 'HASH_DOMAIN_TOO_LONG'
    | 'HASH_SERIALIZATION_FAILED'
    | 'HASH_DESERIALIZATION_FAILED'
    | 'HASH_CRYPTO_UNAVAILABLE'
    | 'HASH_CRYPTO_OPERATION_FAILED'
    | 'HASH_KEY_REQUIRED'
    | 'HASH_KEY_NOT_ALLOWED'
    | 'HASH_RESEED_LIMIT_EXCEEDED'
    | 'HASH_BLOOM_CAPACITY_EXCEEDED'
    | 'HASH_BLOOM_INVALID_PARAMS'
    | 'HASH_HLL_INVALID_PARAMS'
    | 'HASH_TREE_INVALID_PARAMS'
    | 'HASH_MERKLE_PROOF_INVALID'
    | 'HASH_GENERIC';

export class HashError extends Error {
    public override readonly name: string = 'HashError';
    public readonly code: HashErrorCode;

    constructor(message: string, code: HashErrorCode = 'HASH_GENERIC') {
        super(message);
        this.code = code;
        Object.setPrototypeOf(this, HashError.prototype);
    }
}

export class HashInvalidInputError extends HashError {
    public override readonly name: string = 'HashInvalidInputError';
    constructor(message: string = 'Invalid input provided to hash function') {
        super(message, 'HASH_INVALID_INPUT');
        Object.setPrototypeOf(this, HashInvalidInputError.prototype);
    }
}

export class HashInvalidKeyError extends HashError {
    public override readonly name: string = 'HashInvalidKeyError';
    constructor(message: string = 'Invalid key provided for keyed hash') {
        super(message, 'HASH_INVALID_KEY');
        Object.setPrototypeOf(this, HashInvalidKeyError.prototype);
    }
}

export class HashInvalidSeedError extends HashError {
    public override readonly name: string = 'HashInvalidSeedError';
    constructor(message: string = 'Invalid seed value for hash') {
        super(message, 'HASH_INVALID_SEED');
        Object.setPrototypeOf(this, HashInvalidSeedError.prototype);
    }
}

export class HashStateCorruptedError extends HashError {
    public override readonly name: string = 'HashStateCorruptedError';
    constructor(message: string = 'Hash state has been corrupted') {
        super(message, 'HASH_STATE_CORRUPTED');
        Object.setPrototypeOf(this, HashStateCorruptedError.prototype);
    }
}

export class HashAlreadyFinalizedError extends HashError {
    public override readonly name: string = 'HashAlreadyFinalizedError';
    constructor(message: string = 'Hasher has already been finalized') {
        super(message, 'HASH_ALREADY_FINALIZED');
        Object.setPrototypeOf(this, HashAlreadyFinalizedError.prototype);
    }
}

export class HashNotFinalizedError extends HashError {
    public override readonly name: string = 'HashNotFinalizedError';
    constructor(message: string = 'Hasher has not been finalized') {
        super(message, 'HASH_NOT_FINALIZED');
        Object.setPrototypeOf(this, HashNotFinalizedError.prototype);
    }
}

export class HashContextOverflowError extends HashError {
    public override readonly name: string = 'HashContextOverflowError';
    constructor(message: string = 'Hash context size limit exceeded') {
        super(message, 'HASH_CONTEXT_OVERFLOW');
        Object.setPrototypeOf(this, HashContextOverflowError.prototype);
    }
}

export class HashAlgorithmNotFoundError extends HashError {
    public override readonly name: string = 'HashAlgorithmNotFoundError';
    constructor(message: string = 'Hash algorithm not found in registry') {
        super(message, 'HASH_ALGORITHM_NOT_FOUND');
        Object.setPrototypeOf(this, HashAlgorithmNotFoundError.prototype);
    }
}

export class HashAlgorithmUnsupportedError extends HashError {
    public override readonly name: string = 'HashAlgorithmUnsupportedError';
    constructor(message: string = 'Hash algorithm is not supported in this environment') {
        super(message, 'HASH_ALGORITHM_UNSUPPORTED');
        Object.setPrototypeOf(this, HashAlgorithmUnsupportedError.prototype);
    }
}

export class HashDomainTooLongError extends HashError {
    public override readonly name: string = 'HashDomainTooLongError';
    constructor(message: string = 'Domain separation tag exceeds maximum length') {
        super(message, 'HASH_DOMAIN_TOO_LONG');
        Object.setPrototypeOf(this, HashDomainTooLongError.prototype);
    }
}

export class HashSerializationError extends HashError {
    public override readonly name: string = 'HashSerializationError';
    constructor(message: string = 'Failed to serialize hash value') {
        super(message, 'HASH_SERIALIZATION_FAILED');
        Object.setPrototypeOf(this, HashSerializationError.prototype);
    }
}

export class HashDeserializationError extends HashError {
    public override readonly name: string = 'HashDeserializationError';
    constructor(message: string = 'Failed to deserialize hash value') {
        super(message, 'HASH_DESERIALIZATION_FAILED');
        Object.setPrototypeOf(this, HashDeserializationError.prototype);
    }
}

export class HashCryptoUnavailableError extends HashError {
    public override readonly name: string = 'HashCryptoUnavailableError';
    constructor(message: string = 'WebCrypto API is not available in this environment') {
        super(message, 'HASH_CRYPTO_UNAVAILABLE');
        Object.setPrototypeOf(this, HashCryptoUnavailableError.prototype);
    }
}

export class HashCryptoOperationError extends HashError {
    public override readonly name: string = 'HashCryptoOperationError';
    constructor(message: string = 'WebCrypto operation failed') {
        super(message, 'HASH_CRYPTO_OPERATION_FAILED');
        Object.setPrototypeOf(this, HashCryptoOperationError.prototype);
    }
}

export class HashKeyRequiredError extends HashError {
    public override readonly name: string = 'HashKeyRequiredError';
    constructor(message: string = 'Hash algorithm requires a key') {
        super(message, 'HASH_KEY_REQUIRED');
        Object.setPrototypeOf(this, HashKeyRequiredError.prototype);
    }
}

export class HashKeyNotAllowedError extends HashError {
    public override readonly name: string = 'HashKeyNotAllowedError';
    constructor(message: string = 'Hash algorithm does not support keys') {
        super(message, 'HASH_KEY_NOT_ALLOWED');
        Object.setPrototypeOf(this, HashKeyNotAllowedError.prototype);
    }
}

export class HashReseedLimitError extends HashError {
    public override readonly name: string = 'HashReseedLimitError';
    constructor(message: string = 'PRNG reseed limit exceeded') {
        super(message, 'HASH_RESEED_LIMIT_EXCEEDED');
        Object.setPrototypeOf(this, HashReseedLimitError.prototype);
    }
}

export class HashBloomCapacityError extends HashError {
    public override readonly name: string = 'HashBloomCapacityError';
    constructor(message: string = 'Bloom filter capacity exceeded') {
        super(message, 'HASH_BLOOM_CAPACITY_EXCEEDED');
        Object.setPrototypeOf(this, HashBloomCapacityError.prototype);
    }
}

export class HashBloomInvalidParamsError extends HashError {
    public override readonly name: string = 'HashBloomInvalidParamsError';
    constructor(message: string = 'Invalid Bloom filter parameters') {
        super(message, 'HASH_BLOOM_INVALID_PARAMS');
        Object.setPrototypeOf(this, HashBloomInvalidParamsError.prototype);
    }
}

export class HashHLLInvalidParamsError extends HashError {
    public override readonly name: string = 'HashHLLInvalidParamsError';
    constructor(message: string = 'Invalid HyperLogLog parameters') {
        super(message, 'HASH_HLL_INVALID_PARAMS');
        Object.setPrototypeOf(this, HashHLLInvalidParamsError.prototype);
    }
}

export class HashTreeInvalidParamsError extends HashError {
    public override readonly name: string = 'HashTreeInvalidParamsError';
    constructor(message: string = 'Invalid Merkle tree parameters') {
        super(message, 'HASH_TREE_INVALID_PARAMS');
        Object.setPrototypeOf(this, HashTreeInvalidParamsError.prototype);
    }
}

export class HashMerkleProofError extends HashError {
    public override readonly name: string = 'HashMerkleProofError';
    constructor(message: string = 'Invalid Merkle proof') {
        super(message, 'HASH_MERKLE_PROOF_INVALID');
        Object.setPrototypeOf(this, HashMerkleProofError.prototype);
    }
}
