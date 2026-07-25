import { Mat4 } from '@axrone/numeric';
import { Transform, type Actor } from '@axrone/ecs-runtime';
import type { SceneCameraFrameState } from './camera-frame-state';
import { ParticleSystem } from './components/particle-system';
import type { ParticleRenderData } from './components/particle-system';
import { SceneMeshError } from './errors';
import { createParticleShaderDefinition } from './particle-shader';
import type { SceneRenderFrameState } from './render-frame-state';
import type { SceneRenderStateApplier } from './render-state-applier';
import { SceneShaderFactory } from './scene-shader-factory';
import type { SceneShaderResource } from './shader-registry';
import type { SceneUniformWriteTarget } from './uniform-writer';

/**
 * Interleaved point-sprite vertex layout (9 floats / 36 bytes):
 *   [0..2]  position (vec3)  -> attribute location 0
 *   [3..4]  size, seed (vec2)-> attribute location 2 (uv0)
 *   [5..8]  color rgba (vec4)-> attribute location 3 (color0)
 */
const PARTICLE_VERTEX_FLOATS = 9;
const PARTICLE_VERTEX_STRIDE_BYTES = PARTICLE_VERTEX_FLOATS * Float32Array.BYTES_PER_ELEMENT;
const MAX_POINT_SIZE = 256;
const ALPHA_EPSILON = 0.001;

const SPRITE_MODE_INDEX: Readonly<Record<string, number>> = Object.freeze({
    glow: 0,
    disc: 1,
    star: 2,
    spark: 3,
});

export interface SceneParticleBatchRuntimeOptions {
    readonly gl: WebGL2RenderingContext;
    readonly uniformWriter: SceneUniformWriteTarget;
    readonly renderStateApplier: Pick<SceneRenderStateApplier, 'reset'>;
}

export interface SceneParticleBatchRuntimeRenderParams {
    readonly actors: readonly Actor[];
    readonly cameraFrame: SceneCameraFrameState;
    readonly frameState: SceneRenderFrameState;
    readonly viewportWidth: number;
    readonly viewportHeight: number;
}

export interface SceneParticleBatchRuntimeRenderStats {
    readonly drawnParticleCount: number;
    readonly particleSystemCount: number;
}

const EMPTY_STATS: SceneParticleBatchRuntimeRenderStats = Object.freeze({
    drawnParticleCount: 0,
    particleSystemCount: 0,
});

interface SceneParticleRenderSubject {
    readonly actor: Actor;
    readonly system: ParticleSystem;
}

/**
 * Renders every active {@link ParticleSystem} in the scene as GPU point
 * sprites in a small number of draw calls (one per system).
 *
 * The runtime mirrors {@link SceneSpriteBatchRuntime}: it owns a dynamic
 * `DYNAMIC_DRAW` vertex buffer plus a self-managed shader, rebuilds the point
 * vertex cloud from each system's simulation buffers every frame, and draws
 * with per-system blending and sprite mode.
 *
 * The vertex construction is isolated in {@link _buildPointVertices} so an
 * instanced-billboard strategy can be introduced later without changing the
 * public contract (hybrid point/billboard design).
 */
export class SceneParticleBatchRuntime {
    private readonly _shaderFactory: SceneShaderFactory;
    private _shader: SceneShaderResource | null = null;
    private _vertexArray: WebGLVertexArrayObject | null = null;
    private _vertexBuffer: WebGLBuffer | null = null;
    private _vertexData: Float32Array;
    private _vertexCapacity: number;
    private readonly _subjects: SceneParticleRenderSubject[] = [];
    private _failed = false;

    constructor(private readonly _options: SceneParticleBatchRuntimeOptions) {
        this._shaderFactory = new SceneShaderFactory({ gl: _options.gl });
        this._vertexCapacity = 1024;
        this._vertexData = new Float32Array(this._vertexCapacity * PARTICLE_VERTEX_FLOATS);
    }

    render(params: SceneParticleBatchRuntimeRenderParams): SceneParticleBatchRuntimeRenderStats {
        if (this._failed) {
            return EMPTY_STATS;
        }

        this._subjects.length = 0;
        for (const actor of params.actors) {
            if (!actor.active) {
                continue;
            }
            const system = actor.getComponent(ParticleSystem);
            if (system && system.enabled) {
                this._subjects.push({ actor, system });
            }
        }

        if (this._subjects.length === 0) {
            return EMPTY_STATS;
        }

        const gl = this._options.gl;
        let drawnParticleCount = 0;
        let particleSystemCount = 0;

        try {
            this._ensureResources();

            const shader = this._shader!;

            gl.useProgram(shader.program);
            gl.bindVertexArray(this._vertexArray);

            gl.enable(gl.BLEND);
            gl.blendEquation(gl.FUNC_ADD);
            gl.enable(gl.DEPTH_TEST);
            gl.depthMask(false);
            gl.disable(gl.CULL_FACE);

            this._options.uniformWriter.write(
                shader,
                'u_ViewProjection',
                params.cameraFrame.viewProjectionMatrix
            );
            this._options.uniformWriter.write(shader, 'u_PointScale', params.viewportHeight * 0.5);
            this._options.uniformWriter.write(shader, 'u_MaxPointSize', MAX_POINT_SIZE);

            for (const subject of this._subjects) {
                const vertexCount = this._drawSubject(gl, shader, subject, params);
                if (vertexCount > 0) {
                    drawnParticleCount += vertexCount;
                    particleSystemCount += 1;
                }
            }
        } catch (error) {
            // A rendering add-on must never be able to halt the scene loop.
            // Disable further attempts and surface the failure once.
            this._failed = true;
            // eslint-disable-next-line no-console
            console.error('[particle-batch] disabled after render failure:', error);
        } finally {
            // Return the GL context to a known-good baseline and invalidate the
            // shared render-state-applier cache. The applier only re-issues GL
            // state when its cached value changes; our direct GL mutations here
            // desync that cache, so without this reset the next mesh draw would
            // inherit our blend/cull/depth state and corrupt the scene.
            gl.depthMask(true);
            gl.disable(gl.BLEND);
            gl.enable(gl.CULL_FACE);
            gl.bindVertexArray(null);
            gl.bindBuffer(gl.ARRAY_BUFFER, null);
            this._options.renderStateApplier.reset();
        }

        return { drawnParticleCount, particleSystemCount };
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
        this._subjects.length = 0;
    }

    private _drawSubject(
        gl: WebGL2RenderingContext,
        shader: SceneShaderResource,
        subject: SceneParticleRenderSubject,
        params: SceneParticleBatchRuntimeRenderParams
    ): number {
        const data = subject.system.getRenderData();
        const vertexCount = this._buildPointVertices(data);
        if (vertexCount === 0) {
            return 0;
        }

        gl.bindBuffer(gl.ARRAY_BUFFER, this._vertexBuffer);
        gl.bufferData(
            gl.ARRAY_BUFFER,
            this._vertexData.subarray(0, vertexCount * PARTICLE_VERTEX_FLOATS),
            gl.DYNAMIC_DRAW
        );

        const modelMatrix =
            subject.system.simulationSpace === 'world'
                ? Mat4.IDENTITY
                : (subject.actor.getComponent(Transform)?.worldMatrix ?? Mat4.IDENTITY);
        this._options.uniformWriter.write(shader, 'u_Model', modelMatrix);
        this._options.uniformWriter.write(
            shader,
            'u_SpriteMode',
            SPRITE_MODE_INDEX[subject.system.spriteMode] ?? 0
        );

        if (subject.system.blendMode === 'additive') {
            gl.blendFunc(gl.SRC_ALPHA, gl.ONE);
        } else {
            gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
        }

        gl.drawArrays(gl.POINTS, 0, vertexCount);
        params.frameState.recordDraw({
            topology: 'points',
            indexCount: 0,
            vertexCount,
        });

        return vertexCount;
    }

    /**
     * Packs live particles (alpha > epsilon) into the interleaved point vertex
     * buffer and returns the number of vertices written.
     */
    private _buildPointVertices(data: ParticleRenderData): number {
        const liveCount = data.count;
        this._ensureVertexCapacity(liveCount);

        const positions = data.positions;
        const colors = data.colors;
        const sizes = data.sizes;
        const alphas = data.alphas;
        const seeds = data.seeds;
        const vertexData = this._vertexData;

        let vertexIndex = 0;
        for (let i = 0; i < liveCount; i += 1) {
            const alpha = alphas[i] ?? 0;
            if (alpha <= ALPHA_EPSILON) {
                continue;
            }

            const source = i * 3;
            const offset = vertexIndex * PARTICLE_VERTEX_FLOATS;
            vertexData[offset] = positions[source] ?? 0;
            vertexData[offset + 1] = positions[source + 1] ?? 0;
            vertexData[offset + 2] = positions[source + 2] ?? 0;
            vertexData[offset + 3] = sizes[i] ?? 0;
            vertexData[offset + 4] = seeds[i] ?? 0;
            vertexData[offset + 5] = colors[source] ?? 0;
            vertexData[offset + 6] = colors[source + 1] ?? 0;
            vertexData[offset + 7] = colors[source + 2] ?? 0;
            vertexData[offset + 8] = alpha;
            vertexIndex += 1;
        }

        return vertexIndex;
    }

    private _ensureVertexCapacity(particleCount: number): void {
        if (particleCount <= this._vertexCapacity) {
            return;
        }
        this._vertexCapacity = particleCount;
        this._vertexData = new Float32Array(this._vertexCapacity * PARTICLE_VERTEX_FLOATS);
    }

    private _ensureResources(): void {
        const gl = this._options.gl;

        if (!this._shader) {
            this._shader = this._shaderFactory.create(createParticleShaderDefinition());
        }

        if (this._vertexArray && this._vertexBuffer) {
            return;
        }

        this._vertexArray = gl.createVertexArray();
        if (!this._vertexArray) {
            throw new SceneMeshError('Failed to create particle vertex array');
        }

        this._vertexBuffer = gl.createBuffer();
        if (!this._vertexBuffer) {
            throw new SceneMeshError('Failed to create particle vertex buffer');
        }

        gl.bindVertexArray(this._vertexArray);
        gl.bindBuffer(gl.ARRAY_BUFFER, this._vertexBuffer);

        // position (location 0)
        gl.enableVertexAttribArray(0);
        gl.vertexAttribPointer(0, 3, gl.FLOAT, false, PARTICLE_VERTEX_STRIDE_BYTES, 0);
        // uv0 -> size, seed (location 2)
        gl.enableVertexAttribArray(2);
        gl.vertexAttribPointer(
            2,
            2,
            gl.FLOAT,
            false,
            PARTICLE_VERTEX_STRIDE_BYTES,
            3 * Float32Array.BYTES_PER_ELEMENT
        );
        // color0 -> rgba (location 3)
        gl.enableVertexAttribArray(3);
        gl.vertexAttribPointer(
            3,
            4,
            gl.FLOAT,
            false,
            PARTICLE_VERTEX_STRIDE_BYTES,
            5 * Float32Array.BYTES_PER_ELEMENT
        );

        gl.bindVertexArray(null);
        gl.bindBuffer(gl.ARRAY_BUFFER, null);
    }
}
