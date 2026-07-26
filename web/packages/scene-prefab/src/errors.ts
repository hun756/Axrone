/**
 * @axrone/scene-prefab — Error classes for the prefab subsystem.
 *
 * These mirror the SceneError interface (code + cause) but extend Error
 * directly so that @axrone/scene-prefab has no runtime dependency on
 * @axrone/scene-runtime.  scene-runtime re-exports these classes to
 * preserve backward compatibility.
 */

export class ScenePrefabError extends Error {
    constructor(
        message: string,
        readonly code = 'SCENE_PREFAB_ERROR',
        readonly cause?: unknown
    ) {
        super(message);
        this.name = 'ScenePrefabError';
        Object.setPrototypeOf(this, new.target.prototype);
        (
            Error as typeof Error & { captureStackTrace?: (target: object, ctor: Function) => void }
        ).captureStackTrace?.(this, this.constructor);
    }
}

export class ScenePrefabValidationError extends ScenePrefabError {
    constructor(message: string, cause?: unknown) {
        super(message, 'SCENE_PREFAB_VALIDATION_ERROR', cause);
        this.name = 'ScenePrefabValidationError';
    }
}

export class ScenePrefabResolutionError extends ScenePrefabError {
    constructor(message: string, cause?: unknown) {
        super(message, 'SCENE_PREFAB_RESOLUTION_ERROR', cause);
        this.name = 'ScenePrefabResolutionError';
    }
}

export class ScenePrefabConflictError extends ScenePrefabError {
    constructor(message: string, cause?: unknown) {
        super(message, 'SCENE_PREFAB_CONFLICT_ERROR', cause);
        this.name = 'ScenePrefabConflictError';
    }
}
