import { describe, expect, it } from 'vitest';
import {
    ScenePrefabError,
    ScenePrefabValidationError,
    ScenePrefabResolutionError,
    ScenePrefabConflictError,
} from '../errors';

describe('ScenePrefabError', () => {
    it('sets message, code, name, and cause', () => {
        const cause = new Error('root');
        const err = new ScenePrefabError('something broke', 'SCENE_PREFAB_ERROR', cause);
        expect(err.message).toBe('something broke');
        expect(err.code).toBe('SCENE_PREFAB_ERROR');
        expect(err.name).toBe('ScenePrefabError');
        expect(err.cause).toBe(cause);
    });

    it('is an instance of Error', () => {
        const err = new ScenePrefabError('test');
        expect(err).toBeInstanceOf(Error);
    });

    it('works without a cause', () => {
        const err = new ScenePrefabError('no cause');
        expect(err.message).toBe('no cause');
        expect(err.cause).toBeUndefined();
    });
});

describe('ScenePrefabValidationError', () => {
    it('has code SCENE_PREFAB_VALIDATION_ERROR', () => {
        const err = new ScenePrefabValidationError('invalid');
        expect(err.code).toBe('SCENE_PREFAB_VALIDATION_ERROR');
    });

    it('is an instance of ScenePrefabError', () => {
        const err = new ScenePrefabValidationError('invalid');
        expect(err).toBeInstanceOf(ScenePrefabError);
        expect(err).toBeInstanceOf(Error);
    });

    it('propagates cause', () => {
        const cause = new Error('inner');
        const err = new ScenePrefabValidationError('bad', cause);
        expect(err.cause).toBe(cause);
    });
});

describe('ScenePrefabResolutionError', () => {
    it('has code SCENE_PREFAB_RESOLUTION_ERROR', () => {
        const err = new ScenePrefabResolutionError('missing');
        expect(err.code).toBe('SCENE_PREFAB_RESOLUTION_ERROR');
    });

    it('is an instance of ScenePrefabError', () => {
        const err = new ScenePrefabResolutionError('missing');
        expect(err).toBeInstanceOf(ScenePrefabError);
    });
});

describe('ScenePrefabConflictError', () => {
    it('has code SCENE_PREFAB_CONFLICT_ERROR', () => {
        const err = new ScenePrefabConflictError('conflict');
        expect(err.code).toBe('SCENE_PREFAB_CONFLICT_ERROR');
    });

    it('is an instance of ScenePrefabError', () => {
        const err = new ScenePrefabConflictError('conflict');
        expect(err).toBeInstanceOf(ScenePrefabError);
    });
});

describe('prototype chain correctness', () => {
    it('can catch subclass by base class', () => {
        try {
            throw new ScenePrefabValidationError('test');
        } catch (e) {
            expect(e instanceof ScenePrefabError).toBe(true);
            expect(e instanceof Error).toBe(true);
        }
    });

    it('can catch resolution error by base class', () => {
        try {
            throw new ScenePrefabResolutionError('test');
        } catch (e) {
            expect(e instanceof ScenePrefabError).toBe(true);
        }
    });

    it('can catch conflict error by base class', () => {
        try {
            throw new ScenePrefabConflictError('test');
        } catch (e) {
            expect(e instanceof ScenePrefabError).toBe(true);
        }
    });
});
