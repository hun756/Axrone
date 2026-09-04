/**
 * Shared shader compilation helpers for the WebGL2 UI renderer.
 *
 * Extracted from renderer.ts and world-quad.ts where these functions were
 * verbatim twins. Both call sites now import from this module so that shader
 * source normalisation, compilation error reporting and program linking logic
 * live in a single location.
 */

/**
 * Restores newline separators in shader source that has been flattened
 * by build-time minification (esbuild template literal transformation).
 *
 * When newlines are stripped, GLSL tokens merge across former line boundaries:
 *   #version 300 esprecision mediump float;   (es + precision)
 *   float;layout(location = 0) in vec2 ...    (float; + layout)
 *
 * This function detects and repairs such corruption by re-inserting
 * newlines at statement boundaries. Uses split/join instead of regex
 * to avoid esbuild transformation issues with replacement strings.
 */
export const normalizeShaderSource = (source: string): string => {
    // Fast path: if the first line is a valid #version directive, source is intact.
    // Uses a strict regex to reject corrupted lines like '#version 300 esprecision ...'
    // where esbuild merged tokens across a stripped newline boundary.
    const firstNewline = source.indexOf('\n');
    if (firstNewline > 0 && /^#version\s+\d+\s+\w+$/.test(source.slice(0, firstNewline).trim())) {
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
    // Use a precise regex to avoid matching 'es' inside comments or identifiers.
    const versionMatch = /^#version\s+300\s+es\b/m.exec(restored);
    if (versionMatch) {
        const versionEnd = versionMatch.index + versionMatch[0].length;
        if (versionEnd < restored.length) {
            const after = restored[versionEnd];
            if (after !== '\n' && after !== ' ' && after !== '\t' && after !== '\r') {
                restored = restored.slice(0, versionEnd) + '\n' + restored.slice(versionEnd);
            }
        }
    }

    return restored;
};

/**
 * Compiles a single GLSL shader stage. Normalises the source (repairing
 * esbuild-minified newlines), compiles, and throws a descriptive error
 * on failure.
 *
 * @param gl - WebGL2 rendering context.
 * @param type - `gl.VERTEX_SHADER` or `gl.FRAGMENT_SHADER`.
 * @param source - Raw GLSL source (template literal; may be minified).
 * @param label - Human-readable label used in error messages (e.g. "quad vertex").
 */
export const compileShader = (
    gl: WebGL2RenderingContext,
    type: number,
    source: string,
    label: string
): WebGLShader => {
    const shader = gl.createShader(type);
    if (!shader) {
        throw new Error(`Failed to create ${label} shader.`);
    }
    const resolvedSource = normalizeShaderSource(source);
    if (resolvedSource !== source) {
        // eslint-disable-next-line no-console
        console.warn(`[WebGL2UIRenderer] ${label} shader source was corrupted — newlines were restored. This indicates esbuild minification stripped newlines from the shader template literal.`);
    }
    gl.shaderSource(shader, resolvedSource);
    gl.compileShader(shader);
    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        const message = gl.getShaderInfoLog(shader) ?? 'Unknown shader compile error.';
        gl.deleteShader(shader);
        throw new Error(
            `${label} shader compilation failed: ${message}\nSource (first 200 chars): ${resolvedSource.slice(0, 200)}`
        );
    }
    return shader;
};

/**
 * Links a vertex + fragment shader pair into a WebGL program.
 * Compiles both stages, attaches, links, and cleans up the shader objects.
 * Throws with the program info log on link failure.
 */
export const createProgram = (
    gl: WebGL2RenderingContext,
    vertexSource: string,
    fragmentSource: string,
    label: string
): WebGLProgram => {
    const program = gl.createProgram();
    if (!program) {
        throw new Error(`Failed to create ${label} program.`);
    }
    const vertex = compileShader(gl, gl.VERTEX_SHADER, vertexSource, `${label} vertex`);
    const fragment = compileShader(gl, gl.FRAGMENT_SHADER, fragmentSource, `${label} fragment`);
    gl.attachShader(program, vertex);
    gl.attachShader(program, fragment);
    gl.linkProgram(program);
    gl.deleteShader(vertex);
    gl.deleteShader(fragment);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
        const message = gl.getProgramInfoLog(program) ?? 'Unknown program link error.';
        gl.deleteProgram(program);
        throw new Error(`${label} program link failed: ${message}`);
    }
    return program;
};
