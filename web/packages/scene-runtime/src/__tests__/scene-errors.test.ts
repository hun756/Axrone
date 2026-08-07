import { describe, expect, it } from 'vitest';
import {
    SceneError,
    SceneCanvasError,
    SceneShaderError,
    SceneMeshError,
    SceneMaterialError,
    SceneLifecycleError,
    SceneCapabilityError,
} from '../errors';

describe('SceneError hierarchy', () => {
    it('SceneError is an instance of Error', () => {
        const error = new SceneError('test message', 'TEST_CODE');
        expect(error).toBeInstanceOf(Error);
        expect(error).toBeInstanceOf(SceneError);
        expect(error.message).toBe('test message');
        expect(error.code).toBe('TEST_CODE');
        expect(error.name).toBe('SceneError');
    });

    it('SceneError preserves cause', () => {
        const cause = new Error('root cause');
        const error = new SceneError('wrapper', 'WRAP_CODE', cause);
        expect(error.cause).toBe(cause);
    });

    it('SceneCanvasError has SCENE_CANVAS_ERROR code', () => {
        const error = new SceneCanvasError('canvas failed');
        expect(error).toBeInstanceOf(SceneError);
        expect(error).toBeInstanceOf(SceneCanvasError);
        expect(error.code).toBe('SCENE_CANVAS_ERROR');
        expect(error.name).toBe('SceneCanvasError');
    });

    it('SceneShaderError has SCENE_SHADER_ERROR code', () => {
        const error = new SceneShaderError('shader compile failed');
        expect(error).toBeInstanceOf(SceneError);
        expect(error.code).toBe('SCENE_SHADER_ERROR');
        expect(error.name).toBe('SceneShaderError');
    });

    it('SceneMeshError has SCENE_MESH_ERROR code', () => {
        const error = new SceneMeshError('invalid mesh');
        expect(error).toBeInstanceOf(SceneError);
        expect(error.code).toBe('SCENE_MESH_ERROR');
        expect(error.name).toBe('SceneMeshError');
    });

    it('SceneMaterialError has SCENE_MATERIAL_ERROR code', () => {
        const error = new SceneMaterialError('bad material');
        expect(error).toBeInstanceOf(SceneError);
        expect(error.code).toBe('SCENE_MATERIAL_ERROR');
        expect(error.name).toBe('SceneMaterialError');
    });

    it('SceneLifecycleError has SCENE_LIFECYCLE_ERROR code', () => {
        const error = new SceneLifecycleError('lifecycle violation');
        expect(error).toBeInstanceOf(SceneError);
        expect(error.code).toBe('SCENE_LIFECYCLE_ERROR');
        expect(error.name).toBe('SceneLifecycleError');
    });

    it('SceneCapabilityError has SCENE_CAPABILITY_ERROR code', () => {
        const error = new SceneCapabilityError('unsupported');
        expect(error).toBeInstanceOf(SceneError);
        expect(error.code).toBe('SCENE_CAPABILITY_ERROR');
        expect(error.name).toBe('SceneCapabilityError');
    });

    it('all subclasses preserve cause parameter', () => {
        const cause = new Error('root');
        const errors = [
            new SceneCanvasError('msg', cause),
            new SceneShaderError('msg', cause),
            new SceneMeshError('msg', cause),
            new SceneMaterialError('msg', cause),
            new SceneLifecycleError('msg', cause),
            new SceneCapabilityError('msg', cause),
        ];

        for (const error of errors) {
            expect(error.cause).toBe(cause);
        }
    });

    it('catch block can distinguish error types via instanceof', () => {
        try {
            throw new SceneMeshError('broken mesh');
        } catch (e) {
            expect(e).toBeInstanceOf(SceneMeshError);
            expect(e).toBeInstanceOf(SceneError);
            expect(e).not.toBeInstanceOf(SceneShaderError);
        }
    });
});
