import type { SceneFogState } from './fog-state';
import type { SceneShaderResource } from './shader-registry';
import type { SceneUniformWriteTarget } from './uniform-writer';

/**
 * Writes per-frame fog uniforms to the active shader program.
 *
 * Follows the same dependency-injection seam as `SceneLightingUniformBinder`
 * and `SceneFrameUniformBinder`: receives a `SceneUniformWriteTarget` and
 * writes named uniforms. Shaders that do not declare these uniforms simply
 * ignore the writes (the uniform writer skips unknown locations).
 *
 * Uniform contract (PBR and unlit GLTF shaders under FOG keyword):
 *   u_FogEnabled  : int   (0/1)
 *   u_FogColor    : vec3  (linear RGB)
 *   u_FogMode     : int   (0=Linear, 1=Exp, 2=Exp2)
 *   u_FogDensity  : float
 *   u_FogStartEnd : vec2  (start, end)
 */
export class SceneFogUniformBinder {
    constructor(private readonly _writer: SceneUniformWriteTarget) {}

    apply(shader: SceneShaderResource, fog: SceneFogState): void {
        this._writer.write(shader, 'u_FogEnabled', fog.enabled ? 1 : 0);

        if (!fog.enabled) {
            return;
        }

        this._writer.write(shader, 'u_FogColor', fog.color);
        this._writer.write(shader, 'u_FogMode', fog.mode);
        this._writer.write(shader, 'u_FogDensity', fog.density);
        this._writer.write(shader, 'u_FogStartEnd', fog.range);
    }
}
