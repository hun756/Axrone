import { Vec2, Vec3 } from '@axrone/numeric';
import type { SceneLightingState } from './lighting-collector';
import type { SceneMeshObjectBounds } from './scene-mesh-bounds';
import type { SceneShaderResource } from './shader-registry';
import type { SceneUniformWriteTarget } from './uniform-writer';

const RENDER_TARGET_SIZE_UNIFORM = '_RenderTargetSize';
const MAIN_LIGHT_DIRECTION_UNIFORM = '_MainLightDirection';
const MAIN_LIGHT_COLOR_UNIFORM = '_MainLightColor';
const OBJECT_BOUNDS_CENTER_UNIFORM = '_ObjectBoundsCenter';
const OBJECT_BOUNDS_SIZE_UNIFORM = '_ObjectBoundsSize';

const FALLBACK_MAIN_LIGHT_DIRECTION = Object.freeze([0, 1, 0] as const);
const FALLBACK_MAIN_LIGHT_COLOR = Object.freeze([1, 1, 1] as const);

export const SCENE_EXTERNAL_FRAME_UNIFORM_NAMES = Object.freeze([
    RENDER_TARGET_SIZE_UNIFORM,
    MAIN_LIGHT_DIRECTION_UNIFORM,
    MAIN_LIGHT_COLOR_UNIFORM,
] as const);

export const SCENE_OBJECT_BOUNDS_UNIFORM_NAMES = Object.freeze([
    OBJECT_BOUNDS_CENTER_UNIFORM,
    OBJECT_BOUNDS_SIZE_UNIFORM,
] as const);

const isFiniteNumber = (value: number): boolean => Number.isFinite(value);

/**
 * Writes the engine-supplied ("external") uniforms a material author may read
 * but never set: the active render-target size and main light for the frame,
 * plus the drawn mesh's object-space AABB per draw.
 *
 * Both frame groups are always written, even when the frame has no directional
 * light: GL program state survives across frames, so skipping the write would
 * leave the previous frame's light in place for a shader that lost its light.
 */
export class SceneExternalUniformBinder {
    private readonly _renderTargetSize = new Vec2();
    private readonly _mainLightDirection = new Vec3(
        FALLBACK_MAIN_LIGHT_DIRECTION[0],
        FALLBACK_MAIN_LIGHT_DIRECTION[1],
        FALLBACK_MAIN_LIGHT_DIRECTION[2]
    );
    private readonly _mainLightColor = new Vec3(
        FALLBACK_MAIN_LIGHT_COLOR[0],
        FALLBACK_MAIN_LIGHT_COLOR[1],
        FALLBACK_MAIN_LIGHT_COLOR[2]
    );

    constructor(private readonly _writer: SceneUniformWriteTarget) {}

    applyFrame(
        shader: SceneShaderResource,
        renderTargetWidth: number,
        renderTargetHeight: number,
        lighting: SceneLightingState
    ): void {
        if (
            renderTargetWidth > 0 &&
            renderTargetHeight > 0 &&
            isFiniteNumber(renderTargetWidth) &&
            isFiniteNumber(renderTargetHeight)
        ) {
            this._renderTargetSize.x = renderTargetWidth;
            this._renderTargetSize.y = renderTargetHeight;
            this._writer.write(shader, RENDER_TARGET_SIZE_UNIFORM, this._renderTargetSize);
        }

        this._applyMainLight(shader, lighting);
    }

    /**
     * Object bounds are per-draw, so this is intentionally kept out of any
     * per-frame write-once cache. The cached vectors belong to the mesh-bounds
     * memo and are handed straight to the writer, which only reads them.
     */
    applyObjectBounds(
        shader: SceneShaderResource,
        bounds: SceneMeshObjectBounds | undefined
    ): void {
        if (!bounds) {
            return;
        }

        this._writer.write(shader, OBJECT_BOUNDS_CENTER_UNIFORM, bounds.center);
        this._writer.write(shader, OBJECT_BOUNDS_SIZE_UNIFORM, bounds.size);
    }

    private _applyMainLight(shader: SceneShaderResource, lighting: SceneLightingState): void {
        const directions = lighting.directionalDirections;
        const colors = lighting.directionalColors;
        const intensities = lighting.directionalIntensities;
        const hasMainLight =
            (lighting.stats?.selectedDirectionalCount ?? 0) > 0 &&
            directions !== undefined &&
            colors !== undefined &&
            intensities !== undefined &&
            directions.length >= 3 &&
            colors.length >= 3 &&
            isFiniteNumber(directions[0]) &&
            isFiniteNumber(directions[1]) &&
            isFiniteNumber(directions[2]);

        if (!hasMainLight) {
            resetVec3(this._mainLightDirection, FALLBACK_MAIN_LIGHT_DIRECTION);
            resetVec3(this._mainLightColor, FALLBACK_MAIN_LIGHT_COLOR);
            this._writer.write(shader, MAIN_LIGHT_DIRECTION_UNIFORM, this._mainLightDirection);
            this._writer.write(shader, MAIN_LIGHT_COLOR_UNIFORM, this._mainLightColor);
            return;
        }

        // `directionalDirections` stores the light's forward (travel) direction
        // while `_MainLightDirection` must point from the surface TOWARD the
        // light, mirroring the `normalize(-u_DirectionalLightDirection[i])` the
        // built-in effects already do at their use sites.
        const x = -directions[0]!;
        const y = -directions[1]!;
        const z = -directions[2]!;
        const length = Math.sqrt(x * x + y * y + z * z);

        if (length > 0) {
            this._mainLightDirection.x = x / length;
            this._mainLightDirection.y = y / length;
            this._mainLightDirection.z = z / length;
        } else {
            resetVec3(this._mainLightDirection, FALLBACK_MAIN_LIGHT_DIRECTION);
        }

        const intensity = Math.max(0, intensities[0] ?? 0);

        this._mainLightColor.x = Math.max(0, (colors[0] ?? 0) * intensity);
        this._mainLightColor.y = Math.max(0, (colors[1] ?? 0) * intensity);
        this._mainLightColor.z = Math.max(0, (colors[2] ?? 0) * intensity);

        this._writer.write(shader, MAIN_LIGHT_DIRECTION_UNIFORM, this._mainLightDirection);
        this._writer.write(shader, MAIN_LIGHT_COLOR_UNIFORM, this._mainLightColor);
    }
}

const resetVec3 = (target: Vec3, source: readonly number[]): void => {
    target.x = source[0] ?? 0;
    target.y = source[1] ?? 0;
    target.z = source[2] ?? 0;
};
