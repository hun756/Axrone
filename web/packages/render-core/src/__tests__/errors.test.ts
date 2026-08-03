import { describe, expect, it } from 'vitest';
import {
    RenderPipelineError,
    RenderValidationError,
    RenderResourceError,
    RenderExecutionError,
    RenderBakeTaskError,
    isRenderPipelineError,
} from '../errors';

describe('RenderPipelineError', () => {
    it('creates error with code and default en locale', () => {
        const error = new RenderPipelineError('PIPELINE_DISPOSED');
        expect(error.code).toBe('PIPELINE_DISPOSED');
        expect(error.locale).toBe('en');
        expect(error.context).toBeUndefined();
        expect(error.name).toBe('RenderPipelineError');
        expect(error.message).toContain('[RenderPipeline:PIPELINE_DISPOSED]');
        expect(error.message).toContain('render pipeline has been disposed');
    });

    it('uses tr locale when specified', () => {
        const error = new RenderPipelineError('PIPELINE_DISPOSED', 'tr');
        expect(error.locale).toBe('tr');
        expect(error.message).toContain('dispose edilmis');
    });

    it('falls back to en for unknown locale', () => {
        const error = new RenderPipelineError('INVALID_CAMERA', 'fr');
        expect(error.message).toContain('invalid camera state');
    });

    it('serializes context into message', () => {
        const error = new RenderPipelineError('INVALID_CAMERA', 'en', { id: 'cam:1' });
        expect(error.context).toEqual({ id: 'cam:1' });
        expect(error.message).toContain('"id":"cam:1"');
    });

    it('handles non-serializable context gracefully', () => {
        const circular: Record<string, unknown> = {};
        circular.self = circular;
        const error = new RenderPipelineError('INVALID_CAMERA', 'en', circular);
        expect(error.message).not.toContain('undefined');
    });

    it('handles empty context without appending extra text', () => {
        const error = new RenderPipelineError('INVALID_CAMERA', 'en', {});
        expect(error.message).toContain('invalid camera state provided to render pipeline');
        // Empty context should not append JSON
        expect(error.message).not.toContain('{');
    });

    it('chains cause error', () => {
        const cause = new Error('root');
        const error = new RenderPipelineError('BACKEND_FAILED', 'en', undefined, cause);
        expect(error.cause).toBe(cause);
    });

    it('passes instanceof check for Error and RenderPipelineError', () => {
        const error = new RenderPipelineError('INVALID_ARGUMENT');
        expect(error).toBeInstanceOf(Error);
        expect(error).toBeInstanceOf(RenderPipelineError);
    });
});

describe('RenderValidationError', () => {
    it('has correct name and inherits from RenderPipelineError', () => {
        const error = new RenderValidationError('INVALID_CAMERA');
        expect(error.name).toBe('RenderValidationError');
        expect(error).toBeInstanceOf(RenderPipelineError);
        expect(error.code).toBe('INVALID_CAMERA');
    });

    it('accepts all validation error codes', () => {
        const codes = [
            'INVALID_CAMERA', 'INVALID_VIEWPORT', 'INVALID_PRIMITIVE',
            'INVALID_LIGHT', 'INVALID_EFFECT', 'INVALID_ARGUMENT',
        ] as const;
        for (const code of codes) {
            const error = new RenderValidationError(code);
            expect(error.code).toBe(code);
        }
    });
});

describe('RenderResourceError', () => {
    it('has correct name and accepts resource codes', () => {
        const conflict = new RenderResourceError('RESOURCE_CONFLICT');
        expect(conflict.name).toBe('RenderResourceError');
        expect(conflict.code).toBe('RESOURCE_CONFLICT');

        const notFound = new RenderResourceError('RESOURCE_NOT_FOUND');
        expect(notFound.code).toBe('RESOURCE_NOT_FOUND');
    });
});

describe('RenderExecutionError', () => {
    it('has correct name and accepts execution codes with cause', () => {
        const cause = new Error('gl error');
        const error = new RenderExecutionError('BACKEND_FAILED', 'en', undefined, cause);
        expect(error.name).toBe('RenderExecutionError');
        expect(error.code).toBe('BACKEND_FAILED');
        expect(error.cause).toBe(cause);
    });

    it('accepts PASS_EXECUTION_FAILED code', () => {
        const error = new RenderExecutionError('PASS_EXECUTION_FAILED');
        expect(error.code).toBe('PASS_EXECUTION_FAILED');
    });
});

describe('RenderBakeTaskError', () => {
    it('always uses BAKE_TASK_NOT_FOUND code', () => {
        const error = new RenderBakeTaskError('en', { id: 'task:1' });
        expect(error.name).toBe('RenderBakeTaskError');
        expect(error.code).toBe('BAKE_TASK_NOT_FOUND');
        expect(error.message).toContain('bake');
    });
});

describe('isRenderPipelineError', () => {
    it('returns true for RenderPipelineError instances', () => {
        expect(isRenderPipelineError(new RenderPipelineError('INVALID_ARGUMENT'))).toBe(true);
    });

    it('returns true for subclass instances', () => {
        expect(isRenderPipelineError(new RenderValidationError('INVALID_CAMERA'))).toBe(true);
        expect(isRenderPipelineError(new RenderResourceError('RESOURCE_CONFLICT'))).toBe(true);
        expect(isRenderPipelineError(new RenderExecutionError('BACKEND_FAILED'))).toBe(true);
        expect(isRenderPipelineError(new RenderBakeTaskError())).toBe(true);
    });

    it('returns false for plain Error and non-error values', () => {
        expect(isRenderPipelineError(new Error('test'))).toBe(false);
        expect(isRenderPipelineError('string')).toBe(false);
        expect(isRenderPipelineError(null)).toBe(false);
        expect(isRenderPipelineError(42)).toBe(false);
    });
});
