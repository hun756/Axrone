import { Mat4, Vec3 } from '@axrone/numeric';
import { Transform, type Actor } from '@axrone/ecs-runtime';
import type { SceneCameraFrameState } from '../camera-frame-state';
import { BillboardRenderer } from '../components/billboard-renderer';
import { SceneMeshError } from '../errors';
import { SceneDirectGlPassGuard } from './internal/render-state-guard';
import { createBillboardShaderDefinition } from '../billboard-shader';
import type { SceneRenderFrameState } from './render-frame-state';
import type { SceneRenderStateApplier } from './render-state-applier';
import { SceneShaderFactory } from '../scene-shader-factory';
import type { SceneShaderResource } from '../shader-registry';
import type { SceneUniformWriteTarget } from '../uniform-writer';

export interface SceneBillboardBatchRuntimeOptions {
    readonly gl: WebGL2RenderingContext;
    readonly uniformWriter: SceneUniformWriteTarget;
    readonly renderStateApplier: Pick<SceneRenderStateApplier, 'reset'>;
}

export interface SceneBillboardBatchRuntimeRenderParams {
    readonly actors: readonly Actor[];
    readonly cameraFrame: SceneCameraFrameState;
    readonly frameState: SceneRenderFrameState;
}

export interface SceneBillboardBatchRuntimeRenderStats {
    readonly drawnBillboardCount: number;
    readonly totalVertexCount: number;
    readonly totalIndexCount: number;
}

const EMPTY_STATS: SceneBillboardBatchRuntimeRenderStats = Object.freeze({
    drawnBillboardCount: 0,
    totalVertexCount: 0,
    totalIndexCount: 0,
});

/**
 * Number of floats per billboard vertex: position (3) + uv (2) + color (4) = 9.
 * Same layout as the line/ribbon renderer.
 */
const BILLBOARD_VERTEX_FLOATS = 9;

/**
 * Number of indices per billboard quad: 2 triangles × 3 indices = 6.
 */
const BILLBOARD_INDICES_PER_QUAD = 6;

/**
 * Number of vertices per billboard quad.
 */
const BILLBOARD_VERTICES_PER_QUAD = 4;

const _identityMatrix = Mat4.IDENTITY;

const BILLBOARD_MODE_SPHERICAL = 0;
const BILLBOARD_MODE_CYLINDRICAL = 1;
const BILLBOARD_MODE_VELOCITY_ORIENTED = 2;

const resolveBillboardModeValue = (mode: string): number => {
    switch (mode) {
        case 'spherical':
            return BILLBOARD_MODE_SPHERICAL;
        case 'cylindrical':
            return BILLBOARD_MODE_CYLINDRICAL;
        case 'velocity-oriented':
            return BILLBOARD_MODE_VELOCITY_ORIENTED;
        default:
            return BILLBOARD_MODE_SPHERICAL;
    }
};

interface BillboardSubject {
    readonly actor: Actor;
    readonly renderer: BillboardRenderer;
}

/**
 * Renders every active {@link BillboardRenderer} in the scene as camera-facing
 * quads.
 *
 * Follows the {@link SceneLineBatchRuntime} pattern: owns a dynamic vertex
 * buffer + index buffer plus a self-managed shader, rebuilds quad geometry
 * each frame, and draws with alpha blending.
 */
export class SceneBillboardBatchRuntime {
    private readonly _shaderFactory: SceneShaderFactory;
    private readonly _guard: SceneDirectGlPassGuard;
    private _shader: SceneShaderResource | null = null;
    private _vertexArray: WebGLVertexArrayObject | null = null;
    private _vertexBuffer: WebGLBuffer | null = null;
    private _indexBuffer: WebGLBuffer | null = null;
    private _vertexData: Float32Array;
    private _indexData: Uint16Array;
    private _vertexCapacity: number;
    private _indexCapacity: number;
    private readonly _subjects: BillboardSubject[] = [];

    constructor(private readonly _options: SceneBillboardBatchRuntimeOptions) {
        this._shaderFactory = new SceneShaderFactory({ gl: _options.gl });
        this._guard = new SceneDirectGlPassGuard({
            gl: _options.gl,
            renderStateApplier: _options.renderStateApplier,
            label: 'billboard-batch',
        });
        this._vertexCapacity = 256;
        this._indexCapacity = 384;
        this._vertexData = new Float32Array(this._vertexCapacity * BILLBOARD_VERTEX_FLOATS);
        this._indexData = new Uint16Array(this._indexCapacity);
    }

    render(params: SceneBillboardBatchRuntimeRenderParams): SceneBillboardBatchRuntimeRenderStats {
        if (this._guard.isDisabled) {
            return EMPTY_STATS;
        }

        this._subjects.length = 0;

        for (const actor of params.actors) {
            if (!actor.active) {
                continue;
            }

            const billboardRenderer = actor.getComponent(BillboardRenderer);
            if (billboardRenderer && billboardRenderer.enabled && billboardRenderer.visible) {
                this._subjects.push({ actor, renderer: billboardRenderer });
            }
        }

        if (this._subjects.length === 0) {
            return EMPTY_STATS;
        }

        const gl = this._options.gl;

        return this._guard.run(EMPTY_STATS, () => {
            let drawnBillboardCount = 0;
            let totalVertexCount = 0;
            let totalIndexCount = 0;

            this._ensureResources();

            const shader = this._shader!;

            gl.useProgram(shader.program);
            gl.bindVertexArray(this._vertexArray);

            gl.enable(gl.BLEND);
            gl.blendEquation(gl.FUNC_ADD);
            gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
            gl.enable(gl.DEPTH_TEST);
            gl.disable(gl.CULL_FACE);

            this._options.uniformWriter.write(
                shader,
                'u_ViewProjection',
                params.cameraFrame.viewProjectionMatrix
            );

            this._options.uniformWriter.write(shader, 'u_Model', _identityMatrix);
            this._options.uniformWriter.write(shader, 'u_UseTexture', 0);

            const cameraPosition = params.cameraFrame.position;

            for (const subject of this._subjects) {
                const result = this._drawBillboard(gl, shader, subject, cameraPosition, params);
                if (result) {
                    drawnBillboardCount++;
                    totalVertexCount += result.vertexCount;
                    totalIndexCount += result.indexCount;
                }
            }

            return { drawnBillboardCount, totalVertexCount, totalIndexCount };
        });
    }

    clear(): void {
        const gl = this._options.gl;
        if (this._shader) {
            this._shaderFactory.delete(this._shader);
            this._shader = null;
        }
        if (this._vertexArray) {
            gl.deleteVertexArray(this._vertexArray);
            this._vertexArray = null;
        }
        if (this._vertexBuffer) {
            gl.deleteBuffer(this._vertexBuffer);
            this._vertexBuffer = null;
        }
        if (this._indexBuffer) {
            gl.deleteBuffer(this._indexBuffer);
            this._indexBuffer = null;
        }
        this._subjects.length = 0;
    }

    private _drawBillboard(
        gl: WebGL2RenderingContext,
        shader: SceneShaderResource,
        subject: BillboardSubject,
        cameraPosition: Vec3,
        params: SceneBillboardBatchRuntimeRenderParams
    ): { vertexCount: number; indexCount: number } | null {
        const renderer = subject.renderer;
        const transform = subject.actor.getComponent(Transform);

        if (!transform) {
            return null;
        }

        const worldPos = transform.worldPosition;
        const width = renderer.width;
        const height = renderer.height;

        if (width <= 0 || height <= 0) {
            return null;
        }

        const pivot = renderer.pivot;
        const mode = renderer.mode;

        // Compute billboard orientation vectors
        const right = new Vec3();
        const up = new Vec3();

        if (mode === 'spherical') {
            // Spherical: quad always faces camera
            const viewDir = new Vec3(
                worldPos.x - cameraPosition.x,
                worldPos.y - cameraPosition.y,
                worldPos.z - cameraPosition.z
            );
            const viewLen = viewDir.length;
            if (viewLen < 1e-6) {
                // Billboard at camera position, use default orientation
                right.set(1, 0, 0);
                up.set(0, 1, 0);
            } else {
                viewDir.x /= viewLen;
                viewDir.y /= viewLen;
                viewDir.z /= viewLen;

                // cameraUp is the Y axis of the view
                const cameraUp = new Vec3(0, 1, 0);

                // right = normalize(cross(cameraUp, viewDir))
                right.x = cameraUp.y * viewDir.z - cameraUp.z * viewDir.y;
                right.y = cameraUp.z * viewDir.x - cameraUp.x * viewDir.z;
                right.z = cameraUp.x * viewDir.y - cameraUp.y * viewDir.x;
                const rightLen = right.length;
                if (rightLen < 1e-6) {
                    // viewDir is parallel to cameraUp, pick arbitrary right
                    right.set(1, 0, 0);
                } else {
                    right.x /= rightLen;
                    right.y /= rightLen;
                    right.z /= rightLen;
                }

                // up = cross(viewDir, right)
                up.x = viewDir.y * right.z - viewDir.z * right.y;
                up.y = viewDir.z * right.x - viewDir.x * right.z;
                up.z = viewDir.x * right.y - viewDir.y * right.x;
            }
        } else if (mode === 'cylindrical') {
            // Cylindrical: only rotate around Y axis
            const viewDirY = new Vec3(
                worldPos.x - cameraPosition.x,
                0,
                worldPos.z - cameraPosition.z
            );
            const viewDirYLen = viewDirY.length;
            if (viewDirYLen < 1e-6) {
                right.set(1, 0, 0);
            } else {
                viewDirY.x /= viewDirYLen;
                viewDirY.z /= viewDirYLen;

                // right = cross(worldUp, viewDirY)
                const worldUp = new Vec3(0, 1, 0);
                right.x = worldUp.y * viewDirY.z - worldUp.z * viewDirY.y;
                right.y = worldUp.z * viewDirY.x - worldUp.x * viewDirY.z;
                right.z = worldUp.x * viewDirY.y - worldUp.y * viewDirY.x;
                const rightLen = right.length;
                if (rightLen < 1e-6) {
                    right.set(1, 0, 0);
                } else {
                    right.x /= rightLen;
                    right.y /= rightLen;
                    right.z /= rightLen;
                }
            }
            // Up is always world up for cylindrical
            up.set(0, 1, 0);
        } else {
            // Velocity-oriented: use identity orientation (local axes)
            right.set(1, 0, 0);
            up.set(0, 1, 0);
        }

        // Compute adjusted UVs
        const [u0, v0, uWidth, vHeight] = renderer.getAdjustedUVs();
        const u1 = u0 + uWidth;
        const v1 = v0 + vHeight;

        // Compute vertex color from component's color + opacity
        const color = renderer.color;
        const opacity = renderer.opacity;
        const cr = color.x;
        const cg = color.y;
        const cb = color.z;
        const ca = opacity;

        // Compute corner offsets relative to pivot
        // pivot (0.5, 0.5) means center; (0, 0) means bottom-left
        const halfW = width * 0.5;
        const halfH = height * 0.5;
        const pivotOffsetX = (0.5 - pivot.x) * width;
        const pivotOffsetY = (0.5 - pivot.y) * height;

        // 4 corners: bottom-left, bottom-right, top-right, top-left
        // Each corner: position = worldPos + (cornerOffset + pivotOffset) * right/up
        const cornerOffsetsX = [-halfW, halfW, halfW, -halfW];
        const cornerOffsetsY = [-halfH, -halfH, halfH, halfH];
        const cornerUVs: readonly [number, number][] = [
            [u0, v1],
            [u1, v1],
            [u1, v0],
            [u0, v0],
        ];

        const vertexCount = BILLBOARD_VERTICES_PER_QUAD;
        const indexCount = BILLBOARD_INDICES_PER_QUAD;

        this._ensureVertexCapacity(vertexCount);
        this._ensureIndexCapacity(indexCount);

        const vertexData = this._vertexData;
        const baseIndex = 0;

        for (let i = 0; i < BILLBOARD_VERTICES_PER_QUAD; i++) {
            const offset = baseIndex + i * BILLBOARD_VERTEX_FLOATS;
            const cx = cornerOffsetsX[i]! + pivotOffsetX;
            const cy = cornerOffsetsY[i]! + pivotOffsetY;

            // position = worldPos + cx * right + cy * up
            vertexData[offset + 0] = worldPos.x + cx * right.x + cy * up.x;
            vertexData[offset + 1] = worldPos.y + cx * right.y + cy * up.y;
            vertexData[offset + 2] = worldPos.z + cx * right.z + cy * up.z;

            // uv
            vertexData[offset + 3] = cornerUVs[i]![0]!;
            vertexData[offset + 4] = cornerUVs[i]![1]!;

            // color
            vertexData[offset + 5] = cr;
            vertexData[offset + 6] = cg;
            vertexData[offset + 7] = cb;
            vertexData[offset + 8] = ca;
        }

        // Indices: two triangles (0,1,2) and (0,2,3)
        const indexData = this._indexData;
        indexData[0] = 0;
        indexData[1] = 1;
        indexData[2] = 2;
        indexData[3] = 0;
        indexData[4] = 2;
        indexData[5] = 3;

        gl.bindBuffer(gl.ARRAY_BUFFER, this._vertexBuffer);
        gl.bufferData(
            gl.ARRAY_BUFFER,
            vertexData.subarray(0, vertexCount * BILLBOARD_VERTEX_FLOATS),
            gl.DYNAMIC_DRAW
        );

        gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, this._indexBuffer);
        gl.bufferData(
            gl.ELEMENT_ARRAY_BUFFER,
            indexData.subarray(0, indexCount),
            gl.DYNAMIC_DRAW
        );

        // Set per-billboard uniforms
        const billboardModeValue = resolveBillboardModeValue(mode);
        this._options.uniformWriter.write(shader, 'u_BillboardMode', billboardModeValue);
        this._options.uniformWriter.write(shader, 'u_AlphaTest', renderer.alphaTest > 0 ? renderer.alphaTest : 0.004);

        // Depth mask based on component's depthWrite setting
        if (renderer.depthWrite) {
            gl.depthMask(true);
        } else {
            gl.depthMask(false);
        }

        gl.drawElements(gl.TRIANGLES, indexCount, gl.UNSIGNED_SHORT, 0);

        params.frameState.recordDraw({
            topology: 'triangles',
            indexCount,
            vertexCount,
        });

        return { vertexCount, indexCount };
    }

    private _ensureVertexCapacity(vertexCount: number): void {
        const requiredFloats = vertexCount * BILLBOARD_VERTEX_FLOATS;
        if (requiredFloats <= this._vertexCapacity * BILLBOARD_VERTEX_FLOATS) {
            return;
        }
        this._vertexCapacity = vertexCount;
        this._vertexData = new Float32Array(this._vertexCapacity * BILLBOARD_VERTEX_FLOATS);
    }

    private _ensureIndexCapacity(indexCount: number): void {
        if (indexCount <= this._indexCapacity) {
            return;
        }
        this._indexCapacity = indexCount;
        this._indexData = new Uint16Array(this._indexCapacity);
    }

    private _ensureResources(): void {
        const gl = this._options.gl;

        if (!this._shader) {
            this._shader = this._shaderFactory.create(createBillboardShaderDefinition());
        }

        if (this._vertexArray && this._vertexBuffer && this._indexBuffer) {
            return;
        }

        this._vertexArray = gl.createVertexArray();
        if (!this._vertexArray) {
            throw new SceneMeshError('Failed to create billboard vertex array');
        }

        this._vertexBuffer = gl.createBuffer();
        if (!this._vertexBuffer) {
            throw new SceneMeshError('Failed to create billboard vertex buffer');
        }

        this._indexBuffer = gl.createBuffer();
        if (!this._indexBuffer) {
            throw new SceneMeshError('Failed to create billboard index buffer');
        }

        gl.bindVertexArray(this._vertexArray);

        gl.bindBuffer(gl.ARRAY_BUFFER, this._vertexBuffer);
        gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, this._indexBuffer);

        const strideBytes = BILLBOARD_VERTEX_FLOATS * Float32Array.BYTES_PER_ELEMENT;

        // position (location 0) — vec3 at offset 0
        gl.enableVertexAttribArray(0);
        gl.vertexAttribPointer(0, 3, gl.FLOAT, false, strideBytes, 0);

        // uv0 (location 2) — vec2 at offset 12
        gl.enableVertexAttribArray(2);
        gl.vertexAttribPointer(2, 2, gl.FLOAT, false, strideBytes, 3 * Float32Array.BYTES_PER_ELEMENT);

        // color0 (location 3) — vec4 at offset 20
        gl.enableVertexAttribArray(3);
        gl.vertexAttribPointer(3, 4, gl.FLOAT, false, strideBytes, 5 * Float32Array.BYTES_PER_ELEMENT);

        gl.bindVertexArray(null);
        gl.bindBuffer(gl.ARRAY_BUFFER, null);
        gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, null);
    }
}
