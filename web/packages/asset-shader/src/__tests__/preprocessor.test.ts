import { describe, expect, it } from 'vitest';
import { preprocessGLSL, ShaderPreprocessError } from '../preprocessor';
import { createDiagnosticSink } from '../diagnostics';

describe('shader preprocessor — macros', () => {
    it('expands object-like macros', () => {
        const result = preprocessGLSL('vec3 c = BASE_COLOR;', {
            defines: { BASE_COLOR: 'vec3(1.0, 0.0, 0.0)' },
        });
        expect(result.code).toContain('vec3(1.0, 0.0, 0.0)');
    });

    it('expands function-like macros with argument substitution', () => {
        const result = preprocessGLSL(
            '#define SQUARE(a) ((a) * (a))\nfloat v = SQUARE(3.0);',
            {}
        );
        expect(result.code).toContain('((3.0) * (3.0))');
    });

    it('does not recurse infinitely for self-referential macros', () => {
        const result = preprocessGLSL('foo', {
            defines: { foo: 'foo + 1' },
        });
        expect(result.code.trim()).toBe('foo + 1');
    });

    it('preserves identifiers that are not macros', () => {
        const result = preprocessGLSL('uniform mat4 u_viewProj;', {});
        expect(result.code).toContain('uniform mat4 u_viewProj;');
    });
});

describe('shader preprocessor — conditionals', () => {
    it('keeps the active #if branch', () => {
        const src = `#if USE_FOG
float fog = 1.0;
#else
float fog = 0.0;
#endif
return fog;`;
        const on = preprocessGLSL(src, { defines: { USE_FOG: true } });
        expect(on.code).toContain('float fog = 1.0;');
        expect(on.code).not.toContain('float fog = 0.0;');

        const off = preprocessGLSL(src, { defines: { USE_FOG: false } });
        expect(off.code).toContain('float fog = 0.0;');
        expect(off.code).not.toContain('float fog = 1.0;');
    });

    it('evaluates constant expressions in #if', () => {
        const src = `#if (COUNT + 1) * 2 == 6
int mode = 1;
#else
int mode = 0;
#endif`;
        const hit = preprocessGLSL(src, { defines: { COUNT: 2 } });
        expect(hit.code).toContain('int mode = 1;');
        const miss = preprocessGLSL(src, { defines: { COUNT: 4 } });
        expect(miss.code).toContain('int mode = 0;');
    });

    it('resolves defined() and #ifdef / #ifndef', () => {
        const src = `#ifdef ALPHA
int a = 1;
#endif
#ifndef BETA
int b = 1;
#endif`;
        const result = preprocessGLSL(src, { defines: { ALPHA: 1 } });
        expect(result.code).toContain('int a = 1;');
        expect(result.code).toContain('int b = 1;');
    });

    it('supports #elif chains', () => {
        const src = `#if MODE == 0
int x = 0;
#elif MODE == 1
int x = 1;
#elif MODE == 2
int x = 2;
#endif`;
        expect(preprocessGLSL(src, { defines: { MODE: 1 } }).code).toContain('int x = 1;');
        expect(preprocessGLSL(src, { defines: { MODE: 2 } }).code).toContain('int x = 2;');
    });

    it('supports nested conditionals', () => {
        const src = `#if A
  #if B
int ab = 1;
  #else
int a = 1;
  #endif
#endif`;
        const result = preprocessGLSL(src, { defines: { A: true, B: false } });
        expect(result.code).toContain('int a = 1;');
        expect(result.code).not.toContain('int ab = 1;');
    });
});

describe('shader preprocessor — diagnostics', () => {
    it('reports #error and throws', () => {
        expect(() =>
            preprocessGLSL('#error "unsupported configuration"')
        ).toThrow(ShaderPreprocessError);
    });

    it('reports unterminated conditionals', () => {
        expect(() => preprocessGLSL('#if USE_FOG\nfloat x = 1.0;')).toThrow(
            ShaderPreprocessError
        );
    });

    it('collects diagnostics into a shared sink', () => {
        const sink = createDiagnosticSink();
        expect(sink.diagnostics).toHaveLength(0);
        try {
            preprocessGLSL('#error boom', { sink });
        } catch {
            // expected
        }
        expect(sink.hasErrors).toBe(true);
        expect(sink.diagnostics[0]?.code).toBe('PP_ERROR');
    });
});

describe('shader preprocessor — comments and continuation', () => {
    it('strips comments without touching directives', () => {
        const src = `// a comment
uniform float u_time; /* inline */ float x = u_time;
/*
multi
line
*/
float y = 1.0;`;
        const result = preprocessGLSL(src);
        expect(result.code).toContain('uniform float u_time;');
        expect(result.code).toContain('float x = u_time;');
        expect(result.code).toContain('float y = 1.0;');
        expect(result.code).not.toContain('comment');
    });

    it('joins line-continued directives', () => {
        const src = `#define LONG \\\n    (1 + \\\n    2 + 3)
int sum = LONG;`;
        const result = preprocessGLSL(src);
        expect(result.code).toContain('(1 +     2 + 3)');
    });
});

describe('shader preprocessor — passthrough', () => {
    it('preserves #version and #extension verbatim', () => {
        const src = `#version 300 es
#extension GL_OES_standard_derivatives : enable
void main() {}`;
        const result = preprocessGLSL(src);
        expect(result.code.startsWith('#version 300 es')).toBe(true);
        expect(result.code).toContain('#extension GL_OES_standard_derivatives : enable');
    });

    it('emits #line markers when requested', () => {
        const result = preprocessGLSL('float a = 1.0;\nfloat b = 2.0;', {
            sourceId: 'test.glsl',
            preserveLineMarkers: true,
        });
        expect(result.code).toContain('#line 1 "test.glsl"');
    });
});
