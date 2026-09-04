/**
 * World-space UI quad pass.
 *
 * Draws a textured, camera-projected quad that displays a UI surface texture
 * inside the 3D scene (the Unity "World Space Canvas" equivalent). The pass owns
 * its own minimal program and fully captures/restores the GL state it touches so
 * it can be injected into an existing scene render loop without disturbing it.
 */
import { LazyGLStateGuard, GL_STATE_PROGRAM, GL_STATE_VERTEX_ARRAY, GL_STATE_ARRAY_BUFFER, GL_STATE_ACTIVE_TEXTURE, GL_STATE_UNIT0_TEXTURE, GL_STATE_BLEND, GL_STATE_BLEND_FUNC, GL_STATE_DEPTH_TEST, GL_STATE_DEPTH_WRITEMASK, GL_STATE_CULL_FACE } from './gl-state';
import { compileShader } from './shader-source';
import { UNIT_QUAD } from './webgl-utils';
export interface UIWorldQuadDrawOptions {
    /** Entity world matrix, 4x4 column-major. */
    readonly modelMatrix: Float32Array;
    /** Camera view-projection matrix, 4x4 column-major. */
    readonly viewProjection: Float32Array;
    /** Quad extents in world units. */
    readonly width: number;
    readonly height: number;
    readonly opacity?: number;
    /** Depth testing lets scene geometry occlude the UI. Defaults to true. */
    readonly depthTest?: boolean;
    /** Transparent surfaces normally keep the depth buffer intact. Defaults to false. */
    readonly depthWrite?: boolean;
}

export interface UIWorldQuadRenderer extends Disposable {
    draw(texture: WebGLTexture, options: UIWorldQuadDrawOptions): void;
    dispose(): void;
}

const VERTEX_SOURCE = `#version 300 es
precision mediump float;
layout(location = 0) in vec2 a_Unit;
uniform mat4 u_ViewProjection;
uniform mat4 u_Model;
uniform vec2 u_Size;
out vec2 v_Uv;
void main() {
    // a_Unit spans 0..1; center the quad on the entity origin.
    vec2 local = (a_Unit - 0.5) * u_Size;
    // UI textures have a top-left origin, quad local Y points up.
    v_Uv = vec2(a_Unit.x, 1.0 - a_Unit.y);
    gl_Position = u_ViewProjection * u_Model * vec4(local, 0.0, 1.0);
}`;

const FRAGMENT_SOURCE = `#version 300 es
precision mediump float;
in vec2 v_Uv;
uniform sampler2D u_Texture;
uniform float u_Opacity;
out vec4 fragColor;
void main() {
    vec4 sampled = texture(u_Texture, v_Uv);
    fragColor = sampled * u_Opacity;
    if (fragColor.a <= 0.0) {
        discard;
    }
}`;

/**
 * Rebuilds a quad model matrix so it squarely faces the camera, keeping the
 * entity translation and per-axis scale. Both matrices are column-major.
 *
 * Shared by the runtime binding and the editor viewport so billboard placement
 * cannot drift between them.
 */
export function orientQuadTowardCamera(
    model: Float32Array,
    cameraWorld: Float32Array
): Float32Array {
    const result = new Float32Array(16);
    for (let axis = 0; axis < 3; axis += 1) {
        const base = axis * 4;
        const modelScale =
            Math.hypot(model[base], model[base + 1], model[base + 2]) || 1;
        const cameraScale =
            Math.hypot(cameraWorld[base], cameraWorld[base + 1], cameraWorld[base + 2]) || 1;
        const factor = modelScale / cameraScale;
        result[base] = cameraWorld[base] * factor;
        result[base + 1] = cameraWorld[base + 1] * factor;
        result[base + 2] = cameraWorld[base + 2] * factor;
        result[base + 3] = 0;
    }
    result[12] = model[12];
    result[13] = model[13];
    result[14] = model[14];
    result[15] = 1;
    return result;
}

export function createUIWorldQuadRenderer(gl: WebGL2RenderingContext): UIWorldQuadRenderer {
    const program = gl.createProgram();
    const buffer = gl.createBuffer();
    const vao = gl.createVertexArray();
    if (!program || !buffer || !vao) {
        throw new Error('Failed to allocate the world-space UI quad renderer.');
    }

    const vertexShader = compileShader(gl, gl.VERTEX_SHADER, VERTEX_SOURCE, 'world-quad vertex');
    const fragmentShader = compileShader(gl, gl.FRAGMENT_SHADER, FRAGMENT_SOURCE, 'world-quad fragment');
    gl.attachShader(program, vertexShader);
    gl.attachShader(program, fragmentShader);
    gl.linkProgram(program);
    gl.deleteShader(vertexShader);
    gl.deleteShader(fragmentShader);
    if (gl.getProgramParameter(program, gl.LINK_STATUS) === false) {
        const log = gl.getProgramInfoLog(program);
        gl.deleteProgram(program);
        throw new Error(`World-space UI quad program failed to link: ${log ?? 'unknown error'}`);
    }

    const uniforms = {
        viewProjection: gl.getUniformLocation(program, 'u_ViewProjection'),
        model: gl.getUniformLocation(program, 'u_Model'),
        size: gl.getUniformLocation(program, 'u_Size'),
        texture: gl.getUniformLocation(program, 'u_Texture'),
        opacity: gl.getUniformLocation(program, 'u_Opacity'),
    };

    const setupGuard = new LazyGLStateGuard();
    setupGuard.capture(gl, GL_STATE_ARRAY_BUFFER | GL_STATE_VERTEX_ARRAY);
    gl.bindVertexArray(vao);
    gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
    gl.bufferData(gl.ARRAY_BUFFER, UNIT_QUAD, gl.STATIC_DRAW);
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);
    setupGuard.restore(gl);

    let disposed = false;
    const drawGuard = new LazyGLStateGuard();
    const DRAW_GROUPS = GL_STATE_PROGRAM | GL_STATE_VERTEX_ARRAY | GL_STATE_ACTIVE_TEXTURE | GL_STATE_UNIT0_TEXTURE | GL_STATE_BLEND | GL_STATE_BLEND_FUNC | GL_STATE_DEPTH_TEST | GL_STATE_DEPTH_WRITEMASK | GL_STATE_CULL_FACE;

    return {
        draw(texture: WebGLTexture, options: UIWorldQuadDrawOptions): void {
            if (disposed) {
                return;
            }

            // ── capture the caller's active texture unit FIRST so the shadow records it ──
            drawGuard.capture(gl, GL_STATE_ACTIVE_TEXTURE);

            // ── switch to unit 0 for our texture bind ──
            gl.activeTexture(gl.TEXTURE0);

            // ── capture the remaining groups (unit0 binding, program, etc.) ──
            drawGuard.capture(gl, DRAW_GROUPS & ~GL_STATE_ACTIVE_TEXTURE);

            gl.useProgram(program);
            gl.bindVertexArray(vao);
            gl.bindTexture(gl.TEXTURE_2D, texture);
            gl.uniform1i(uniforms.texture, 0);
            gl.uniformMatrix4fv(uniforms.viewProjection, false, options.viewProjection);
            gl.uniformMatrix4fv(uniforms.model, false, options.modelMatrix);
            gl.uniform2f(uniforms.size, options.width, options.height);
            gl.uniform1f(uniforms.opacity, options.opacity ?? 1);

            gl.enable(gl.BLEND);
            // The UI frame is rendered with straight alpha, so blend accordingly.
            gl.blendFuncSeparate(
                gl.SRC_ALPHA,
                gl.ONE_MINUS_SRC_ALPHA,
                gl.ONE,
                gl.ONE_MINUS_SRC_ALPHA
            );
            if (options.depthTest ?? true) {
                gl.enable(gl.DEPTH_TEST);
            } else {
                gl.disable(gl.DEPTH_TEST);
            }
            gl.depthMask(options.depthWrite ?? false);
            // UI must stay visible from both sides of the quad.
            gl.disable(gl.CULL_FACE);

            gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);

            // ── restore ────────────────────────────────────────────────────────
            drawGuard.restore(gl);
        },
        dispose(): void {
            if (disposed) {
                return;
            }
            disposed = true;
            gl.deleteVertexArray(vao);
            gl.deleteBuffer(buffer);
            gl.deleteProgram(program);
        },
        [Symbol.dispose]() {
            this.dispose();
        },
    };
}
