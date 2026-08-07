import { describe, expect, it } from 'vitest';
import {
    ShaderInstanceError,
    ShaderInstanceValidationError,
    ShaderInstanceLifecycleError,
    ShaderInstanceBackendError,
    isShaderInstanceError,
} from '../errors';

describe('ShaderInstanceError', () => {
    it('creates error with code and default en locale', () => {
        const error = new ShaderInstanceError('SHADER_COMPILE_FAILED');
        expect(error.code).toBe('SHADER_COMPILE_FAILED');
        expect(error.locale).toBe('en');
        expect(error.name).toBe('ShaderInstanceError');
        expect(error.message).toContain('[ShaderInstance:SHADER_COMPILE_FAILED]');
        expect(error.message).toContain('shader compilation failed');
    });

    it('uses tr locale when specified', () => {
        const error = new ShaderInstanceError('SHADER_COMPILE_FAILED', 'tr');
        expect(error.locale).toBe('tr');
        expect(error.message).toContain('basarisiz');
    });

    it('falls back to en for unknown locale', () => {
        const error = new ShaderInstanceError('UNIFORM_NOT_FOUND', 'de');
        expect(error.message).toContain('uniform was not found');
    });

    it('serializes context into message', () => {
        const error = new ShaderInstanceError('UNIFORM_NOT_FOUND', 'en', { name: 'u_Color' });
        expect(error.context).toEqual({ name: 'u_Color' });
        expect(error.message).toContain('"name":"u_Color"');
    });

    it('handles non-serializable context gracefully', () => {
        const circular: Record<string, unknown> = {};
        circular.self = circular;
        const error = new ShaderInstanceError('INVALID_ARGUMENT', 'en', circular);
        expect(error.message).toContain('[ShaderInstance:INVALID_ARGUMENT]');
    });

    it('handles empty context without appending extra text', () => {
        const error = new ShaderInstanceError('INVALID_ARGUMENT', 'en', {});
        expect(error.message).toContain('invalid argument provided to shader instance');
    });

    it('chains cause error', () => {
        const cause = new Error('gl error');
        const error = new ShaderInstanceError('SHADER_LINK_FAILED', 'en', undefined, cause);
        expect(error.cause).toBe(cause);
    });

    it('passes instanceof check', () => {
        const error = new ShaderInstanceError('INVALID_ARGUMENT');
        expect(error).toBeInstanceOf(Error);
        expect(error).toBeInstanceOf(ShaderInstanceError);
    });
});

describe('ShaderInstanceValidationError', () => {
    it('has correct name and inherits from ShaderInstanceError', () => {
        const error = new ShaderInstanceValidationError('INVALID_VALUE_TYPE');
        expect(error.name).toBe('ShaderInstanceValidationError');
        expect(error).toBeInstanceOf(ShaderInstanceError);
        expect(error.code).toBe('INVALID_VALUE_TYPE');
    });

    it('accepts all validation error codes', () => {
        const codes = ['INVALID_VALUE_TYPE', 'UNIFORM_NOT_FOUND', 'ATTRIBUTE_NOT_FOUND', 'INVALID_ARGUMENT'] as const;
        for (const code of codes) {
            const error = new ShaderInstanceValidationError(code);
            expect(error.code).toBe(code);
        }
    });
});

describe('ShaderInstanceLifecycleError', () => {
    it('has correct name and accepts lifecycle codes', () => {
        const codes = ['PROGRAM_DISPOSED', 'PROGRAM_NOT_LINKED', 'OUT_OF_MEMORY', 'BATCH_OVERFLOW'] as const;
        for (const code of codes) {
            const error = new ShaderInstanceLifecycleError(code);
            expect(error.name).toBe('ShaderInstanceLifecycleError');
            expect(error.code).toBe(code);
        }
    });
});

describe('ShaderInstanceBackendError', () => {
    it('has correct name and accepts backend codes with cause', () => {
        const cause = new Error('driver crash');
        const error = new ShaderInstanceBackendError('SHADER_COMPILE_FAILED', 'en', undefined, cause);
        expect(error.name).toBe('ShaderInstanceBackendError');
        expect(error.code).toBe('SHADER_COMPILE_FAILED');
        expect(error.cause).toBe(cause);
    });

    it('accepts all backend error codes', () => {
        const codes = ['SHADER_COMPILE_FAILED', 'SHADER_LINK_FAILED', 'BACKEND_UNAVAILABLE'] as const;
        for (const code of codes) {
            const error = new ShaderInstanceBackendError(code);
            expect(error.code).toBe(code);
        }
    });
});

describe('isShaderInstanceError', () => {
    it('returns true for all ShaderInstanceError instances', () => {
        expect(isShaderInstanceError(new ShaderInstanceError('INVALID_ARGUMENT'))).toBe(true);
        expect(isShaderInstanceError(new ShaderInstanceValidationError('INVALID_VALUE_TYPE'))).toBe(true);
        expect(isShaderInstanceError(new ShaderInstanceLifecycleError('PROGRAM_DISPOSED'))).toBe(true);
        expect(isShaderInstanceError(new ShaderInstanceBackendError('SHADER_COMPILE_FAILED'))).toBe(true);
    });

    it('returns false for non-ShaderInstanceError values', () => {
        expect(isShaderInstanceError(new Error('plain'))).toBe(false);
        expect(isShaderInstanceError('string')).toBe(false);
        expect(isShaderInstanceError(null)).toBe(false);
        expect(isShaderInstanceError(42)).toBe(false);
    });
});
