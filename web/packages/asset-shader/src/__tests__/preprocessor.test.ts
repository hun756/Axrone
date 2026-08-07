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

describe('shader preprocessor — expression evaluator', () => {
    it('evaluates bitwise AND, OR, XOR', () => {
        const src = `#if (0xFF & 0x0F) == 0x0F
int a = 1;
#endif
#if (0xF0 | 0x0F) == 0xFF
int b = 1;
#endif
#if (0xAA ^ 0xFF) == 0x55
int c = 1;
#endif`;
        const result = preprocessGLSL(src);
        expect(result.code).toContain('int a = 1;');
        expect(result.code).toContain('int b = 1;');
        expect(result.code).toContain('int c = 1;');
    });

    it('evaluates bitwise NOT and shifts', () => {
        const src = `#if ~0 == -1
int not = 1;
#endif
#if (1 << 3) == 8
int shl = 1;
#endif
#if (16 >> 2) == 4
int shr = 1;
#endif`;
        const result = preprocessGLSL(src);
        expect(result.code).toContain('int not = 1;');
        expect(result.code).toContain('int shl = 1;');
        expect(result.code).toContain('int shr = 1;');
    });

    it('evaluates ternary expressions', () => {
        const src = `#if 1 ? 10 : 20
int a = 1;
#endif
#if 0 ? 10 : 0
int b = 1;
#endif`;
        const result = preprocessGLSL(src);
        expect(result.code).toContain('int a = 1;');
        expect(result.code).not.toContain('int b = 1;');
    });

    it('parses hex literals', () => {
        const result = preprocessGLSL(`#if 0xFF == 255\nint hex = 1;\n#endif`);
        expect(result.code).toContain('int hex = 1;');
    });

    it('truncates float literals to integers', () => {
        const result = preprocessGLSL(`#if 1.5 == 1\nint trunc = 1;\n#endif`);
        expect(result.code).toContain('int trunc = 1;');
    });

    it('handles division by zero as 0', () => {
        const result = preprocessGLSL(`#if 1 / 0 == 0\nint divzero = 1;\n#endif`);
        expect(result.code).toContain('int divzero = 1;');
    });

    it('handles modulo by zero as 0', () => {
        const result = preprocessGLSL(`#if 1 % 0 == 0\nint modzero = 1;\n#endif`);
        expect(result.code).toContain('int modzero = 1;');
    });

    it('resolves undefined identifiers to 0', () => {
        const result = preprocessGLSL(`#if UNDEFINED_MACRO == 0\nint undef = 1;\n#endif`);
        expect(result.code).toContain('int undef = 1;');
    });

    it('handles nested parentheses in expressions', () => {
        const result = preprocessGLSL(`#if ((2 + 3) * (4 - 1)) == 15\nint nested = 1;\n#endif`);
        expect(result.code).toContain('int nested = 1;');
    });

    it('evaluates all comparison operators', () => {
        const src = `#if 1 < 2
int lt = 1;
#endif
#if 2 <= 2
int le = 1;
#endif
#if 3 > 2
int gt = 1;
#endif
#if 2 >= 2
int ge = 1;
#endif
#if 1 == 1
int eq = 1;
#endif
#if 1 != 2
int ne = 1;
#endif`;
        const result = preprocessGLSL(src);
        expect(result.code).toContain('int lt = 1;');
        expect(result.code).toContain('int le = 1;');
        expect(result.code).toContain('int gt = 1;');
        expect(result.code).toContain('int ge = 1;');
        expect(result.code).toContain('int eq = 1;');
        expect(result.code).toContain('int ne = 1;');
    });

    it('evaluates logical operators', () => {
        const src = `#if 1 && 1
int and_true = 1;
#endif
#if 1 && 0
int and_false = 1;
#endif
#if 0 || 1
int or_true = 1;
#endif
#if 0 || 0
int or_false = 1;
#endif
#if !0
int not_true = 1;
#endif
#if !1
int not_false = 1;
#endif`;
        const result = preprocessGLSL(src);
        expect(result.code).toContain('int and_true = 1;');
        expect(result.code).not.toContain('int and_false = 1;');
        expect(result.code).toContain('int or_true = 1;');
        expect(result.code).not.toContain('int or_false = 1;');
        expect(result.code).toContain('int not_true = 1;');
        expect(result.code).not.toContain('int not_false = 1;');
    });
});

describe('shader preprocessor — directive edge cases', () => {
    it('#undef removes a previously defined macro', () => {
        const src = `#define FOO 42
#undef FOO
#ifdef FOO
int kept = 1;
#else
int removed = 1;
#endif`;
        const result = preprocessGLSL(src);
        expect(result.code).not.toContain('int kept = 1;');
        expect(result.code).toContain('int removed = 1;');
    });

    it('#pragma passes through verbatim', () => {
        const src = `#pragma optimize(off)
float x = 1.0;`;
        const result = preprocessGLSL(src);
        expect(result.code).toContain('#pragma optimize(off)');
    });

    it('#line passes through verbatim', () => {
        const src = `#line 100 "custom.glsl"
float x = 1.0;`;
        const result = preprocessGLSL(src);
        expect(result.code).toContain('#line 100 "custom.glsl"');
    });

    it('#elif without matching #if reports PP_UNEXPECTED_ELIF', () => {
        const sink = createDiagnosticSink();
        try {
            preprocessGLSL('#elif true\nint x = 1;', { sink });
        } catch {
            // expected
        }
        expect(sink.diagnostics.some((d) => d.code === 'PP_UNEXPECTED_ELIF')).toBe(true);
    });

    it('#elif after #else reports PP_ELIF_AFTER_ELSE', () => {
        const sink = createDiagnosticSink();
        try {
            preprocessGLSL('#if 0\nint a = 1;\n#else\nint b = 1;\n#elif 1\nint c = 1;\n#endif', { sink });
        } catch {
            // expected
        }
        expect(sink.diagnostics.some((d) => d.code === 'PP_ELIF_AFTER_ELSE')).toBe(true);
    });

    it('#else without matching #if reports PP_UNEXPECTED_ELSE', () => {
        const sink = createDiagnosticSink();
        try {
            preprocessGLSL('#else\nint x = 1;', { sink });
        } catch {
            // expected
        }
        expect(sink.diagnostics.some((d) => d.code === 'PP_UNEXPECTED_ELSE')).toBe(true);
    });

    it('duplicate #else reports PP_DUPLICATE_ELSE', () => {
        const sink = createDiagnosticSink();
        try {
            preprocessGLSL('#if 0\nint a = 1;\n#else\nint b = 1;\n#else\nint c = 1;\n#endif', { sink });
        } catch {
            // expected
        }
        expect(sink.diagnostics.some((d) => d.code === 'PP_DUPLICATE_ELSE')).toBe(true);
    });

    it('#endif without #if reports PP_UNEXPECTED_ENDIF', () => {
        const sink = createDiagnosticSink();
        try {
            preprocessGLSL('#endif', { sink });
        } catch {
            // expected
        }
        expect(sink.diagnostics.some((d) => d.code === 'PP_UNEXPECTED_ENDIF')).toBe(true);
    });

    it('#define inside an inactive branch is not registered', () => {
        const src = `#if 0
#define HIDDEN 1
#endif
#ifdef HIDDEN
int visible = 1;
#else
int hidden = 1;
#endif`;
        const result = preprocessGLSL(src);
        expect(result.code).not.toContain('int visible = 1;');
        expect(result.code).toContain('int hidden = 1;');
    });

    it('#error inside an inactive branch is not reported', () => {
        const src = `#if 0
#error "should not fire"
#endif
float x = 1.0;`;
        const result = preprocessGLSL(src);
        expect(result.code).toContain('float x = 1.0;');
    });

    it('defined() operator works in both parenthesized and bare forms', () => {
        const src = `#if defined(FOO)
int paren = 1;
#endif
#if defined BAR
int bare = 1;
#endif`;
        const result = preprocessGLSL(src, { defines: { FOO: 1, BAR: 1 } });
        expect(result.code).toContain('int paren = 1;');
        expect(result.code).toContain('int bare = 1;');
    });
});

describe('shader preprocessor — function-like macro edge cases', () => {
    it('handles empty arguments', () => {
        const src = `#define FILL(a, b) a + b
float v = FILL( , );`;
        const result = preprocessGLSL(src);
        expect(result.code).toContain(' + ');
    });

    it('handles nested parentheses in arguments', () => {
        const src = `#define DOUBLE(x) ((x) * 2)
float v = DOUBLE((1.0 + 2.0));`;
        const result = preprocessGLSL(src);
        expect(result.code).toContain('(((1.0 + 2.0)) * 2)');
    });

    it('macro with no body expands to empty', () => {
        const src = `#define EMPTY
int v = EMPTY 42;`;
        const result = preprocessGLSL(src);
        expect(result.code).toContain('int v =  42;');
    });
});
