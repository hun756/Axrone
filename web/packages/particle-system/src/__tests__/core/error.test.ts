import { describe, expect, it } from 'vitest';
import { ParticleSystemException } from '../../core/error';

describe('ParticleSystemException', () => {
    it('sets code, message, context, and name via constructor', () => {
        const err = new ParticleSystemException('CAPACITY_EXCEEDED', 'too many', {
            capacity: 100,
        });
        expect(err).toBeInstanceOf(Error);
        expect(err).toBeInstanceOf(ParticleSystemException);
        expect(err.name).toBe('ParticleSystemException');
        expect(err.code).toBe('CAPACITY_EXCEEDED');
        expect(err.message).toBe('too many');
        expect(err.context).toEqual({ capacity: 100 });
    });

    it('falls back to error code as message when no message provided', () => {
        const err = new ParticleSystemException('INVALID_STATE');
        expect(err.message).toBe('INVALID_STATE');
    });

    it('restores prototype chain (instanceof works after throw/catch)', () => {
        try {
            throw ParticleSystemException.systemNotInitialized();
        } catch (e) {
            expect(e).toBeInstanceOf(ParticleSystemException);
        }
    });

    describe('static factory methods', () => {
        it('systemNotInitialized', () => {
            const err = ParticleSystemException.systemNotInitialized('sys-42');
            expect(err.code).toBe('SYSTEM_NOT_INITIALIZED');
            expect(err.message).toBe('Particle system must be initialized before use');
            expect(err.context).toEqual({ systemId: 'sys-42' });
        });

        it('particleNotFound', () => {
            const err = ParticleSystemException.particleNotFound(7);
            expect(err.code).toBe('PARTICLE_NOT_FOUND');
            expect(err.message).toBe('Particle not found in system');
            expect(err.context).toEqual({ particleId: 7 });
        });

        it('moduleNotFound', () => {
            const err = ParticleSystemException.moduleNotFound('mod-1');
            expect(err.code).toBe('MODULE_NOT_FOUND');
            expect(err.message).toBe('Module not found in system');
            expect(err.context).toEqual({ moduleId: 'mod-1' });
        });

        it('capacityExceeded', () => {
            const err = ParticleSystemException.capacityExceeded(100, 200);
            expect(err.code).toBe('CAPACITY_EXCEEDED');
            expect(err.message).toContain('200');
            expect(err.message).toContain('100');
            expect(err.context).toEqual({ capacity: 100, requested: 200 });
        });

        it('invalidConfiguration', () => {
            const err = ParticleSystemException.invalidConfiguration('bad rate');
            expect(err.code).toBe('INVALID_CONFIGURATION');
            expect(err.message).toContain('bad rate');
        });

        it('resourceNotAvailable', () => {
            const err = ParticleSystemException.resourceNotAvailable('texture-buffer');
            expect(err.code).toBe('RESOURCE_NOT_AVAILABLE');
            expect(err.message).toContain('texture-buffer');
            expect(err.context).toEqual({ resource: 'texture-buffer' });
        });

        it('operationNotSupported', () => {
            const err = ParticleSystemException.operationNotSupported('gpu-sort');
            expect(err.code).toBe('OPERATION_NOT_SUPPORTED');
            expect(err.message).toContain('gpu-sort');
            expect(err.context).toEqual({ operation: 'gpu-sort' });
        });

        it('memoryAllocationFailed', () => {
            const err = ParticleSystemException.memoryAllocationFailed(4096);
            expect(err.code).toBe('MEMORY_ALLOCATION_FAILED');
            expect(err.message).toContain('4096');
            expect(err.context).toEqual({ size: 4096 });
        });

        it('invalidState', () => {
            const err = ParticleSystemException.invalidState('playing', 'stopped');
            expect(err.code).toBe('INVALID_STATE');
            expect(err.message).toContain('playing');
            expect(err.message).toContain('stopped');
            expect(err.context).toEqual({ expected: 'playing', actual: 'stopped' });
        });

        it('threadSafetyViolation', () => {
            const err = ParticleSystemException.threadSafetyViolation('concurrent-write');
            expect(err.code).toBe('THREAD_SAFETY_VIOLATION');
            expect(err.message).toContain('concurrent-write');
            expect(err.context).toEqual({ operation: 'concurrent-write' });
        });
    });
});
