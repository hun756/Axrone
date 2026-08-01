import { describe, it, expect } from 'vitest';
import {
    decode,
    Base64Error,
    InvalidInputError,
    InvalidCharacterError,
    InvalidLengthError,
    InvalidPaddingError,
    InvalidDataError,
    EnvironmentError,
    UnexpectedError,
} from '../../base64/index';

describe('base64 · error hierarchy', () => {
    it('all errors extend Base64Error and Error', () => {
        const samples: unknown[] = [
            new InvalidInputError(),
            new InvalidCharacterError('x', 0, 'standard'),
            new InvalidLengthError(5),
            new InvalidPaddingError(undefined),
            new InvalidDataError(),
            new EnvironmentError(),
            new UnexpectedError(),
        ];
        for (const error of samples) {
            expect(error).toBeInstanceOf(Base64Error);
            expect(error).toBeInstanceOf(Error);
        }
    });

    it('kind field works as discriminated union', () => {
        const error: Base64Error = new InvalidCharacterError('*', 4, 'standard');
        if (error.kind === 'INVALID_CHARACTER') {
            expect(error.character).toBe('*');
            expect(error.index).toBe(4);
            expect(error.alphabet).toBe('standard');
        } else {
            expect.unreachable('expected INVALID_CHARACTER');
        }
    });

    it('name field carries the class name', () => {
        expect(new InvalidLengthError(1).name).toBe('InvalidLengthError');
        expect(new InvalidPaddingError(0).name).toBe('InvalidPaddingError');
        expect(new InvalidInputError().name).toBe('InvalidInputError');
        expect(new InvalidCharacterError('!', 0, 'urlsafe').name).toBe('InvalidCharacterError');
        expect(new InvalidDataError().name).toBe('InvalidDataError');
        expect(new EnvironmentError().name).toBe('EnvironmentError');
        expect(new UnexpectedError().name).toBe('UnexpectedError');
    });

    it('preserves cause chain', () => {
        const root = new Error('root cause');
        const wrapped = new UnexpectedError('wrapper', { cause: root });
        expect(wrapped.cause).toBe(root);
    });

    it('instanceof chain is correct through all levels', () => {
        try {
            decode('Zm9v*');
            expect.unreachable('error was expected');
        } catch (error) {
            expect(error).toBeInstanceOf(InvalidCharacterError);
            expect(error).toBeInstanceOf(Base64Error);
            expect(error).toBeInstanceOf(Error);
        }
    });

    it('each error kind has a unique string value', () => {
        const kinds = new Set([
            new InvalidInputError().kind,
            new InvalidCharacterError('x', 0, 'standard').kind,
            new InvalidLengthError(1).kind,
            new InvalidPaddingError(undefined).kind,
            new InvalidDataError().kind,
            new EnvironmentError().kind,
            new UnexpectedError().kind,
        ]);
        expect(kinds.size).toBe(7);
    });

    it('InvalidCharacterError stores character, index, and alphabet', () => {
        const err = new InvalidCharacterError('!', 7, 'urlsafe');
        expect(err.character).toBe('!');
        expect(err.index).toBe(7);
        expect(err.alphabet).toBe('urlsafe');
        expect(err.kind).toBe('INVALID_CHARACTER');
    });

    it('InvalidLengthError stores inputLength', () => {
        const err = new InvalidLengthError(13);
        expect(err.inputLength).toBe(13);
        expect(err.kind).toBe('INVALID_LENGTH');
    });

    it('InvalidPaddingError stores index', () => {
        const withIndex = new InvalidPaddingError(4);
        expect(withIndex.index).toBe(4);

        const noIndex = new InvalidPaddingError(undefined);
        expect(noIndex.index).toBeUndefined();
    });
});
