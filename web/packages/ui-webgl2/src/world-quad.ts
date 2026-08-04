/**
 * World-space UI quad pass.
 *
 * Draws a textured, camera-projected quad that displays a UI surface texture
 * inside the 3D scene (the Unity "World Space Canvas" equivalent). The pass owns
 * its own minimal program and fully captures/restores the GL state it touches so
 * it can be injected into an existing scene render loop without disturbing it.
 */
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

const VERTEX_SOURCE = '#version 300 es\n'
    + 'precision highp float;\n'
    + 'layout(location = 0) in vec2 a_Unit;\n'
    + 'uniform mat4 u_ViewProjection;\n'
    + 'uniform mat4 u_Model;\n'
    + 'uniform vec2 u_Size;\n'
    + 'out vec2 v_Uv;\n'
    + 'void main() {\n'
    + '    // a_Unit spans 0..1; center the quad on the entity origin.\n'
    + '    vec2 local = (a_Unit - 0.5) * u_Size;\n'
    + '    // UI textures have a top-left origin, quad local Y points up.\n'
    + '    v_Uv = vec2(a_Unit.x, 1.0 - a_Unit.y);\n'
    + '    gl_Position = u_ViewProjection * u_Model * vec4(local, 0.0, 1.0);\n'
    + '}';

const FRAGMENT_SOURCE = '#version 300 es\n'
    + 'precision highp float;\n'
    + 'in vec2 v_Uv;\n'
    + 'uniform sampler2D u_Texture;\n'
    + 'uniform float u_Opacity;\n'
    + 'out vec4 fragColor;\n'
    + 'void main() {\n'
    + '    vec4 sampled = texture(u_Texture, v_Uv);\n'
    + '    fragColor = sampled * u_Opacity;\n'
    + '    if (fragColor.a <= 0.0) {\n'
    + '        discard;\n'
    + '    }\n'
    + '}';

const UNIT_QUAD = new Float32Array([0, 0, 1, 0, 0, 1, 1, 1]);

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

/**
 * Restores newline separators in shader source that has been flattened
 * by build-time minification (esbuild template literal transformation).
 * See the identical function in renderer.ts for full rationale.
 */
const normalizeShaderSource = (source: string): string => {
    // Fast path: if the first line is a valid #version directive, source is intact.
    const firstNewline = source.indexOf('\n');
    if (firstNewline > 0 && source.slice(0, firstNewline).trim().startsWith('#version')) {
        return source;
    }

    // Corruption detected — rebuild newlines at GLSL statement boundaries.
    // Uses split/join instead of regex to avoid esbuild transformation issues.
    const parts: string[] = [];
    for (const segment of source.split(/([;{}])/)) {
        if (segment === ';') {
            parts.push(';\n');
        } else if (segment === '{') {
            parts.push('{\n');
        } else if (segment === '}') {
            parts.push('\n}\n');
        } else {
            parts.push(segment);
        }
    }
    let restored = parts.join('');

    // Fix #version directive: ensure newline after 'es' before next token.
    const versionEnd = restored.indexOf('es') + 2;
    if (versionEnd > 2 && versionEnd < restored.length) {
        const after = restored[versionEnd];
        if (after !== '\n' && after !== ' ' && after !== '\t' && after !== '\r') {
            restored = restored.slice(0, versionEnd) + '\n' + restored.slice(versionEnd);
        }
    }

    return restored;
};

const compileShader = (
    gl: WebGL2RenderingContext,
    type: number,
    source: string
): WebGLShader => {
    const shader = gl.createShader(type);
    if (!shader) {
        throw new Error('Failed to create the world-space UI quad shader.');
    }
    const resolvedSource = normalizeShaderSource(source);
    gl.shaderSource(shader, resolvedSource);
    gl.compileShader(shader);
    if (gl.getShaderParameter(shader, gl.COMPILE_STATUS) === false) {
        const log = gl.getShaderInfoLog(shader);
        gl.deleteShader(shader);
        throw new Error(`World-space UI quad shader failed to compile: ${log ?? 'unknown error'}`);
    }
    return shader;
};

export function createUIWorldQuadRenderer(gl: WebGL2RenderingContext): UIWorldQuadRenderer {
    const program = gl.createProgram();
    const buffer = gl.createBuffer();
    const vao = gl.createVertexArray();
    if (!program || !buffer || !vao) {
        throw new Error('Failed to allocate the world-space UI quad renderer.');
    }

    const vertexShader = compileShader(gl, gl.VERTEX_SHADER, VERTEX_SOURCE);
    const fragmentShader = compileShader(gl, gl.FRAGMENT_SHADER, FRAGMENT_SOURCE);
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

    const previousBuffer = gl.getParameter(gl.ARRAY_BUFFER_BINDING) as WebGLBuffer | null;
    const previousVao = gl.getParameter(gl.VERTEX_ARRAY_BINDING) as WebGLVertexArrayObject | null;
    gl.bindVertexArray(vao);
    gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
    gl.bufferData(gl.ARRAY_BUFFER, UNIT_QUAD, gl.STATIC_DRAW);
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);
    gl.bindVertexArray(previousVao);
    gl.bindBuffer(gl.ARRAY_BUFFER, previousBuffer);

    let disposed = false;

    return {
        draw(texture: WebGLTexture, options: UIWorldQuadDrawOptions): void {
            if (disposed) {
                return;
            }

            // ── capture the state this pass mutates ─────────────────────────────
            const restore = {
                program: gl.getParameter(gl.CURRENT_PROGRAM) as WebGLProgram | null,
                vao: gl.getParameter(gl.VERTEX_ARRAY_BINDING) as WebGLVertexArrayObject | null,
                activeTexture: gl.getParameter(gl.ACTIVE_TEXTURE) as number,
                blend: gl.getParameter(gl.BLEND) as boolean,
                blendSrcRgb: gl.getParameter(gl.BLEND_SRC_RGB) as number,
                blendDstRgb: gl.getParameter(gl.BLEND_DST_RGB) as number,
                blendSrcAlpha: gl.getParameter(gl.BLEND_SRC_ALPHA) as number,
                blendDstAlpha: gl.getParameter(gl.BLEND_DST_ALPHA) as number,
                depthTest: gl.getParameter(gl.DEPTH_TEST) as boolean,
                depthMask: gl.getParameter(gl.DEPTH_WRITEMASK) as boolean,
                cullFace: gl.getParameter(gl.CULL_FACE) as boolean,
            };
            gl.activeTexture(gl.TEXTURE0);
            const restoreTexture = gl.getParameter(gl.TEXTURE_BINDING_2D) as WebGLTexture | null;

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
            gl.bindTexture(gl.TEXTURE_2D, restoreTexture);
            gl.activeTexture(restore.activeTexture);
            gl.bindVertexArray(restore.vao);
            gl.useProgram(restore.program);
            gl.depthMask(restore.depthMask);
            if (restore.depthTest) {
                gl.enable(gl.DEPTH_TEST);
            } else {
                gl.disable(gl.DEPTH_TEST);
            }
            if (restore.cullFace) {
                gl.enable(gl.CULL_FACE);
            } else {
                gl.disable(gl.CULL_FACE);
            }
            if (restore.blend) {
                gl.enable(gl.BLEND);
            } else {
                gl.disable(gl.BLEND);
            }
            gl.blendFuncSeparate(
                restore.blendSrcRgb,
                restore.blendDstRgb,
                restore.blendSrcAlpha,
                restore.blendDstAlpha
            );
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
