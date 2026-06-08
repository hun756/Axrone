import { describe, expect, it } from 'vitest';
import {
    HashError,
    HashInvalidInputError,
    HashInvalidKeyError,
    HashInvalidSeedError,
    HashStateCorruptedError,
    HashAlreadyFinalizedError,
    HashNotFinalizedError,
    HashContextOverflowError,
    HashAlgorithmNotFoundError,
    HashAlgorithmUnsupportedError,
    HashDomainTooLongError,
    HashSerializationError,
    HashDeserializationError,
    HashCryptoUnavailableError,
    HashCryptoOperationError,
    HashKeyRequiredError,
    HashKeyNotAllowedError,
    HashReseedLimitError,
    HashBloomCapacityError,
    HashBloomInvalidParamsError,
    HashHLLInvalidParamsError,
    HashTreeInvalidParamsError,
    HashMerkleProofError,
} from '../hash/errors';

describe('hash/errors — error hierarchy and codes', () => {
    describe('HashError base', () => {
        it('is an Error subclass', () => {
            const e = new HashError('test');
            expect(e).toBeInstanceOf(Error);
            expect(e).toBeInstanceOf(HashError);
            expect(e.name).toBe('HashError');
            expect(e.message).toBe('test');
            expect(e.code).toBe('HASH_GENERIC');
        });

        it('accepts custom code', () => {
            const e = new HashError('msg', 'HASH_INVALID_INPUT');
            expect(e.code).toBe('HASH_INVALID_INPUT');
        });
    });

    describe('error subclasses', () => {
        const cases: Array<[new (msg?: string) => HashError, string, string]> = [
            [HashInvalidInputError, 'HASH_INVALID_INPUT', 'HashInvalidInputError'],
            [HashInvalidKeyError, 'HASH_INVALID_KEY', 'HashInvalidKeyError'],
            [HashInvalidSeedError, 'HASH_INVALID_SEED', 'HashInvalidSeedError'],
            [HashStateCorruptedError, 'HASH_STATE_CORRUPTED', 'HashStateCorruptedError'],
            [HashAlreadyFinalizedError, 'HASH_ALREADY_FINALIZED', 'HashAlreadyFinalizedError'],
            [HashNotFinalizedError, 'HASH_NOT_FINALIZED', 'HashNotFinalizedError'],
            [HashContextOverflowError, 'HASH_CONTEXT_OVERFLOW', 'HashContextOverflowError'],
            [HashAlgorithmNotFoundError, 'HASH_ALGORITHM_NOT_FOUND', 'HashAlgorithmNotFoundError'],
            [HashAlgorithmUnsupportedError, 'HASH_ALGORITHM_UNSUPPORTED', 'HashAlgorithmUnsupportedError'],
            [HashDomainTooLongError, 'HASH_DOMAIN_TOO_LONG', 'HashDomainTooLongError'],
            [HashSerializationError, 'HASH_SERIALIZATION_FAILED', 'HashSerializationError'],
            [HashDeserializationError, 'HASH_DESERIALIZATION_FAILED', 'HashDeserializationError'],
            [HashCryptoUnavailableError, 'HASH_CRYPTO_UNAVAILABLE', 'HashCryptoUnavailableError'],
            [HashCryptoOperationError, 'HASH_CRYPTO_OPERATION_FAILED', 'HashCryptoOperationError'],
            [HashKeyRequiredError, 'HASH_KEY_REQUIRED', 'HashKeyRequiredError'],
            [HashKeyNotAllowedError, 'HASH_KEY_NOT_ALLOWED', 'HashKeyNotAllowedError'],
            [HashReseedLimitError, 'HASH_RESEED_LIMIT_EXCEEDED', 'HashReseedLimitError'],
            [HashBloomCapacityError, 'HASH_BLOOM_CAPACITY_EXCEEDED', 'HashBloomCapacityError'],
            [HashBloomInvalidParamsError, 'HASH_BLOOM_INVALID_PARAMS', 'HashBloomInvalidParamsError'],
            [HashHLLInvalidParamsError, 'HASH_HLL_INVALID_PARAMS', 'HashHLLInvalidParamsError'],
            [HashTreeInvalidParamsError, 'HASH_TREE_INVALID_PARAMS', 'HashTreeInvalidParamsError'],
            [HashMerkleProofError, 'HASH_MERKLE_PROOF_INVALID', 'HashMerkleProofError'],
        ];

        for (const [Ctor, expectedCode, expectedName] of cases) {
            it(`${expectedName} has correct code and is throwable`, () => {
                const e = new Ctor('test message');
                expect(e).toBeInstanceOf(HashError);
                expect(e).toBeInstanceOf(Error);
                expect(e).toBeInstanceOf(Ctor);
                expect(e.code).toBe(expectedCode);
                expect(e.name).toBe(expectedName);
                expect(e.message).toBe('test message');
            });
        }
    });

    describe('catchable as HashError', () => {
        it('can be caught as HashError', () => {
            try {
                throw new HashInvalidInputError('caught');
            } catch (e) {
                expect(e).toBeInstanceOf(HashError);
                expect((e as HashError).code).toBe('HASH_INVALID_INPUT');
            }
        });
    });

    describe('stack trace preservation', () => {
        it('preserves stack trace', () => {
            const e = new HashInvalidInputError('with stack');
            expect(e.stack).toBeDefined();
            expect(e.stack).toContain('HashInvalidInputError');
        });
    });
});
