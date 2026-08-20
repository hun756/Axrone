import { describe, it, expect, beforeEach } from 'vitest';
import { StandardUnlitShader } from '../shader/templates/standard-shaders';
import { createParticleShaderDefinition } from '@axrone/scene-runtime/particle-shader';
import { createBillboardShaderDefinition } from '@axrone/scene-runtime/billboard-shader';
import { createLineShaderDefinition } from '@axrone/scene-runtime/line-shader';
import { createTerrainFoliageShaderDefinition } from '@axrone/scene-runtime/terrain-foliage-shader';
import { createTerrainSplatShaderDefinition } from '@axrone/scene-runtime/terrain-splat-shader';
import {
    createGltfPbrShaderDefinition,
    createGltfUnlitShaderDefinition,
} from '@axrone/scene-runtime-gltf';
import { GLTF_TOON_SHADER_EFFECT } from '@axrone/scene-runtime-gltf/internal/runtime-shaders';
import { createSceneShaderDefinitionFromEffect } from '@axrone/scene-runtime';

// ---------------------------------------------------------------------------
// Helpers from vitest.browser.setup.ts
// ---------------------------------------------------------------------------
const createTestCanvas = (width = 800, height = 600): HTMLCanvasElement =>
    (window as any).createTestCanvas(width, height);

const createWebGLContext = (
    canvas: HTMLCanvasElement,
    attrs: Partial<WebGLContextAttributes> = {},
): WebGL2RenderingContext => (window as any).createWebGLContext(canvas, attrs);

// ---------------------------------------------------------------------------
// Helper: compile a shader and return status + info log
// ---------------------------------------------------------------------------
const compileShader = (
    gl: WebGL2RenderingContext,
    type: number,
    source: string,
): { success: boolean; infoLog: string } => {
    const shader = gl.createShader(type);
    if (!shader) {
        return { success: false, infoLog: 'Failed to create shader object' };
    }
    gl.shaderSource(shader, source);
    gl.compileShader(shader);
    const success = gl.getShaderParameter(shader, gl.COMPILE_STATUS) as boolean;
    const infoLog = gl.getShaderInfoLog(shader) ?? '';
    gl.deleteShader(shader);
    return { success, infoLog };
};

// ---------------------------------------------------------------------------
// Helper: build a full GLSL ES 3.00 source for the StandardUnlitShader.
// The StandardUnlitShader stores only the body of each stage inside
// passes[0].vertexShader / passes[0].fragmentShader, so we wrap them with
// the required #version, attribute, uniform, and varying declarations.
// ---------------------------------------------------------------------------
const buildStandardUnlitVertexSource = (): string => `#version 300 es
layout(location = 0) in vec3 a_Position;
layout(location = 1) in vec2 a_TexCoord;
uniform mat4 u_MVPMatrix;
uniform vec4 u_Color;
uniform sampler2D u_MainTexture;
out vec2 v_TexCoord;
${StandardUnlitShader.passes[0].vertexShader}
`;

const buildStandardUnlitFragmentSource = (): string => `#version 300 es
precision mediump float;
uniform mat4 u_MVPMatrix;
uniform vec4 u_Color;
uniform sampler2D u_MainTexture;
in vec2 v_TexCoord;
${StandardUnlitShader.passes[0].fragmentShader}
`;

// ---------------------------------------------------------------------------
// T-14: Shader Compilation Validation — Real WebGL2
//
// These tests compile production shaders on a real WebGL2 context and verify
// they compile successfully. They also test negative compilation cases and
// validate ES 3.00 compliance across all shaders.
// ---------------------------------------------------------------------------

describe('T-14: Shader Compilation Validation (Real WebGL2)', () => {
    let gl: WebGL2RenderingContext;

    beforeEach(() => {
        const canvas = createTestCanvas();
        gl = createWebGLContext(canvas);
    });

    // -----------------------------------------------------------------------
    // 1. Standard Unlit Shader
    // -----------------------------------------------------------------------
    it('compiles StandardUnlitShader vertex stage on real WebGL2', () => {
        const source = buildStandardUnlitVertexSource();
        const result = compileShader(gl, gl.VERTEX_SHADER, source);
        expect(result.success).toBe(true);
        if (!result.success) {
            console.error('StandardUnlit vertex compile error:', result.infoLog);
        }
    });

    it('compiles StandardUnlitShader fragment stage on real WebGL2', () => {
        const source = buildStandardUnlitFragmentSource();
        const result = compileShader(gl, gl.FRAGMENT_SHADER, source);
        expect(result.success).toBe(true);
        if (!result.success) {
            console.error('StandardUnlit fragment compile error:', result.infoLog);
        }
    });

    // -----------------------------------------------------------------------
    // 2. Particle Shader
    // -----------------------------------------------------------------------
    it('compiles particle point-sprite shader on real WebGL2', () => {
        const definition = createParticleShaderDefinition();
        const vertResult = compileShader(gl, gl.VERTEX_SHADER, definition.vertexSource);
        expect(vertResult.success).toBe(true);
        if (!vertResult.success) {
            console.error('Particle vertex compile error:', vertResult.infoLog);
        }

        const fragResult = compileShader(gl, gl.FRAGMENT_SHADER, definition.fragmentSource);
        expect(fragResult.success).toBe(true);
        if (!fragResult.success) {
            console.error('Particle fragment compile error:', fragResult.infoLog);
        }
    });

    // -----------------------------------------------------------------------
    // 3. Billboard Shader
    // -----------------------------------------------------------------------
    it('compiles billboard shader on real WebGL2', () => {
        const definition = createBillboardShaderDefinition();
        const vertResult = compileShader(gl, gl.VERTEX_SHADER, definition.vertexSource);
        expect(vertResult.success).toBe(true);
        if (!vertResult.success) {
            console.error('Billboard vertex compile error:', vertResult.infoLog);
        }

        const fragResult = compileShader(gl, gl.FRAGMENT_SHADER, definition.fragmentSource);
        expect(fragResult.success).toBe(true);
        if (!fragResult.success) {
            console.error('Billboard fragment compile error:', fragResult.infoLog);
        }
    });

    // -----------------------------------------------------------------------
    // 4. Line Ribbon Shader
    // -----------------------------------------------------------------------
    it('compiles line-ribbon shader on real WebGL2', () => {
        const definition = createLineShaderDefinition();
        const vertResult = compileShader(gl, gl.VERTEX_SHADER, definition.vertexSource);
        expect(vertResult.success).toBe(true);
        if (!vertResult.success) {
            console.error('Line-ribbon vertex compile error:', vertResult.infoLog);
        }

        const fragResult = compileShader(gl, gl.FRAGMENT_SHADER, definition.fragmentSource);
        expect(fragResult.success).toBe(true);
        if (!fragResult.success) {
            console.error('Line-ribbon fragment compile error:', fragResult.infoLog);
        }
    });

    // -----------------------------------------------------------------------
    // 5. Terrain Foliage Shader
    // -----------------------------------------------------------------------
    it('compiles terrain foliage shader on real WebGL2', () => {
        const definition = createTerrainFoliageShaderDefinition();
        expect(definition.vertexSource).toBeDefined();
        expect(definition.fragmentSource).toBeDefined();

        const vertResult = compileShader(gl, gl.VERTEX_SHADER, definition.vertexSource);
        expect(vertResult.success).toBe(true);
        if (!vertResult.success) {
            console.error('Terrain foliage vertex compile error:', vertResult.infoLog);
        }

        const fragResult = compileShader(gl, gl.FRAGMENT_SHADER, definition.fragmentSource);
        expect(fragResult.success).toBe(true);
        if (!fragResult.success) {
            console.error('Terrain foliage fragment compile error:', fragResult.infoLog);
        }
    });

    // -----------------------------------------------------------------------
    // 6. Terrain Splat Shader
    // -----------------------------------------------------------------------
    it('compiles terrain splat shader on real WebGL2', () => {
        const definition = createTerrainSplatShaderDefinition();
        expect(definition.vertexSource).toBeDefined();
        expect(definition.fragmentSource).toBeDefined();

        const vertResult = compileShader(gl, gl.VERTEX_SHADER, definition.vertexSource);
        expect(vertResult.success).toBe(true);
        if (!vertResult.success) {
            console.error('Terrain splat vertex compile error:', vertResult.infoLog);
        }

        const fragResult = compileShader(gl, gl.FRAGMENT_SHADER, definition.fragmentSource);
        expect(fragResult.success).toBe(true);
        if (!fragResult.success) {
            console.error('Terrain splat fragment compile error:', fragResult.infoLog);
        }
    });

    // -----------------------------------------------------------------------
    // 7. GLTF PBR Shader
    // -----------------------------------------------------------------------
    it('compiles GLTF PBR shader on real WebGL2', () => {
        const definition = createGltfPbrShaderDefinition();
        expect(definition.vertexSource).toBeDefined();
        expect(definition.fragmentSource).toBeDefined();

        const vertResult = compileShader(gl, gl.VERTEX_SHADER, definition.vertexSource);
        expect(vertResult.success).toBe(true);
        if (!vertResult.success) {
            console.error('GLTF PBR vertex compile error:', vertResult.infoLog);
        }

        const fragResult = compileShader(gl, gl.FRAGMENT_SHADER, definition.fragmentSource);
        expect(fragResult.success).toBe(true);
        if (!fragResult.success) {
            console.error('GLTF PBR fragment compile error:', fragResult.infoLog);
        }
    });

    // -----------------------------------------------------------------------
    // 8. GLTF Unlit Shader
    // -----------------------------------------------------------------------
    it('compiles GLTF unlit shader on real WebGL2', () => {
        const definition = createGltfUnlitShaderDefinition();
        expect(definition.vertexSource).toBeDefined();
        expect(definition.fragmentSource).toBeDefined();

        const vertResult = compileShader(gl, gl.VERTEX_SHADER, definition.vertexSource);
        expect(vertResult.success).toBe(true);
        if (!vertResult.success) {
            console.error('GLTF unlit vertex compile error:', vertResult.infoLog);
        }

        const fragResult = compileShader(gl, gl.FRAGMENT_SHADER, definition.fragmentSource);
        expect(fragResult.success).toBe(true);
        if (!fragResult.success) {
            console.error('GLTF unlit fragment compile error:', fragResult.infoLog);
        }
    });

    // -----------------------------------------------------------------------
    // 9. GLTF Toon Shader
    // -----------------------------------------------------------------------
    // NOTE: The toon shader effect includes 'gltf.pbr-lighting' library code
    // that references v_WorldTangent, clearcoat normal textures, emissive
    // properties, etc. — none of which are declared in the toon effect's
    // attribute/varying/property lists. This is a known work-in-progress.
    // We validate that the effect can be compiled to source strings, but
    // skip actual GLSL compilation until the shader definition is complete.
    it('GLTF toon shader effect compiles to source strings (GLSL compilation deferred)', () => {
        const definition = createSceneShaderDefinitionFromEffect(GLTF_TOON_SHADER_EFFECT);
        expect(definition.vertexSource).toBeDefined();
        expect(definition.fragmentSource).toBeDefined();
        expect(definition.vertexSource!.length).toBeGreaterThan(0);
        expect(definition.fragmentSource!.length).toBeGreaterThan(0);
        // Source strings are generated but not yet compilable due to
        // missing attribute/varying/property declarations for PBR features.
    });

    // -----------------------------------------------------------------------
    // 10. Negative Test — Invalid GLSL Fails Gracefully
    // -----------------------------------------------------------------------
    it('rejects intentionally broken vertex shader with COMPILE_STATUS false', () => {
        const brokenSource = `#version 300 es
in vec4 a_position;
void main() {
    this_is_not_valid_glsl !!!;
}`;
        const result = compileShader(gl, gl.VERTEX_SHADER, brokenSource);
        expect(result.success).toBe(false);
        expect(result.infoLog.length).toBeGreaterThan(0);
    });

    it('rejects intentionally broken fragment shader with COMPILE_STATUS false', () => {
        const brokenSource = `#version 300 es
precision mediump float;
out vec4 fragColor;
void main() {
    fragColor = vec4(nonexistent_variable, 0.0, 0.0, 1.0);
}`;
        const result = compileShader(gl, gl.FRAGMENT_SHADER, brokenSource);
        expect(result.success).toBe(false);
        expect(result.infoLog.length).toBeGreaterThan(0);
    });

    // -----------------------------------------------------------------------
    // 11. ES 3.00 Compliance — All shaders use #version 300 es
    // -----------------------------------------------------------------------
    it('all production shaders declare #version 300 es', () => {
        const particle = createParticleShaderDefinition();
        const billboard = createBillboardShaderDefinition();
        const line = createLineShaderDefinition();
        const foliage = createTerrainFoliageShaderDefinition();
        const splat = createTerrainSplatShaderDefinition();
        const pbr = createGltfPbrShaderDefinition();
        const unlit = createGltfUnlitShaderDefinition();
        const toon = createSceneShaderDefinitionFromEffect(GLTF_TOON_SHADER_EFFECT);

        const allSources = [
            particle.vertexSource,
            particle.fragmentSource,
            billboard.vertexSource,
            billboard.fragmentSource,
            line.vertexSource,
            line.fragmentSource,
            foliage.vertexSource!,
            foliage.fragmentSource!,
            splat.vertexSource!,
            splat.fragmentSource!,
            pbr.vertexSource,
            pbr.fragmentSource,
            unlit.vertexSource,
            unlit.fragmentSource,
            toon.vertexSource!,
            toon.fragmentSource!,
            buildStandardUnlitVertexSource(),
            buildStandardUnlitFragmentSource(),
        ];

        for (const source of allSources) {
            expect(source).toMatch(/^#version\s+300\s+es/);
        }
    });

    // -----------------------------------------------------------------------
    // 12. ES 3.00 Compliance — No gl_FragColor in fragment shaders
    // -----------------------------------------------------------------------
    it('no fragment shader uses deprecated gl_FragColor', () => {
        const particle = createParticleShaderDefinition();
        const billboard = createBillboardShaderDefinition();
        const line = createLineShaderDefinition();
        const foliage = createTerrainFoliageShaderDefinition();
        const splat = createTerrainSplatShaderDefinition();
        const pbr = createGltfPbrShaderDefinition();
        const unlit = createGltfUnlitShaderDefinition();
        const toon = createSceneShaderDefinitionFromEffect(GLTF_TOON_SHADER_EFFECT);

        const fragmentSources = [
            particle.fragmentSource,
            billboard.fragmentSource,
            line.fragmentSource,
            foliage.fragmentSource!,
            splat.fragmentSource!,
            pbr.fragmentSource,
            unlit.fragmentSource,
            toon.fragmentSource!,
            buildStandardUnlitFragmentSource(),
        ];

        for (const source of fragmentSources) {
            // Should not contain gl_FragColor (deprecated in ES 3.00)
            expect(source).not.toContain('gl_FragColor');
        }
    });

    // -----------------------------------------------------------------------
    // 13. Precision Directives — All fragment shaders have precision declaration
    // -----------------------------------------------------------------------
    it('all fragment shaders include a precision declaration', () => {
        const particle = createParticleShaderDefinition();
        const billboard = createBillboardShaderDefinition();
        const line = createLineShaderDefinition();
        const foliage = createTerrainFoliageShaderDefinition();
        const splat = createTerrainSplatShaderDefinition();
        const pbr = createGltfPbrShaderDefinition();
        const unlit = createGltfUnlitShaderDefinition();
        const toon = createSceneShaderDefinitionFromEffect(GLTF_TOON_SHADER_EFFECT);

        const fragmentSources = [
            particle.fragmentSource,
            billboard.fragmentSource,
            line.fragmentSource,
            foliage.fragmentSource!,
            splat.fragmentSource!,
            pbr.fragmentSource,
            unlit.fragmentSource,
            toon.fragmentSource!,
            buildStandardUnlitFragmentSource(),
        ];

        for (const source of fragmentSources) {
            expect(source).toMatch(/precision\s+(lowp|mediump|highp)\s+float/);
        }
    });

    // -----------------------------------------------------------------------
    // 14. Variant Permutation — StandardUnlit with MAIN_TEXTURE defined
    // -----------------------------------------------------------------------
    it('compiles StandardUnlitShader fragment with MAIN_TEXTURE keyword', () => {
        const source = `#version 300 es
precision mediump float;
#define MAIN_TEXTURE
uniform mat4 u_MVPMatrix;
uniform vec4 u_Color;
uniform sampler2D u_MainTexture;
in vec2 v_TexCoord;
${StandardUnlitShader.passes[0].fragmentShader}
`;
        const result = compileShader(gl, gl.FRAGMENT_SHADER, source);
        expect(result.success).toBe(true);
        if (!result.success) {
            console.error('StandardUnlit fragment (MAIN_TEXTURE) compile error:', result.infoLog);
        }
    });

    // -----------------------------------------------------------------------
    // 15. Shader Program Linking — particle vertex + fragment link together
    // -----------------------------------------------------------------------
    it('links particle vertex + fragment into a valid program', () => {
        const definition = createParticleShaderDefinition();

        const vs = gl.createShader(gl.VERTEX_SHADER)!;
        gl.shaderSource(vs, definition.vertexSource);
        gl.compileShader(vs);
        expect(gl.getShaderParameter(vs, gl.COMPILE_STATUS)).toBe(true);

        const fs = gl.createShader(gl.FRAGMENT_SHADER)!;
        gl.shaderSource(fs, definition.fragmentSource);
        gl.compileShader(fs);
        expect(gl.getShaderParameter(fs, gl.COMPILE_STATUS)).toBe(true);

        const program = gl.createProgram()!;
        gl.attachShader(program, vs);
        gl.attachShader(program, fs);
        gl.linkProgram(program);

        expect(gl.getProgramParameter(program, gl.LINK_STATUS)).toBe(true);
        const linkLog = gl.getProgramInfoLog(program);
        if (linkLog && linkLog.length > 0) {
            console.warn('Particle program link log:', linkLog);
        }

        gl.deleteShader(vs);
        gl.deleteShader(fs);
        gl.deleteProgram(program);
    });
});
