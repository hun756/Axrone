import { describe, it, expect } from 'vitest';
import { optimizeShaderSource } from '../source-optimizer';

describe('optimizeShaderSource', () => {
    it('strips line comments', () => {
        const source = `#version 300 es
// this is a comment
void main() {}`;
        const result = optimizeShaderSource(source);
        expect(result).not.toContain('// this is a comment');
        expect(result).toContain('void main()');
    });

    it('strips block comments', () => {
        const source = `#version 300 es
/* block comment */
void main() {}`;
        const result = optimizeShaderSource(source);
        expect(result).not.toContain('block comment');
        expect(result).toContain('void main()');
    });

    it('strips inline block comments preserving surrounding tokens', () => {
        const source = `#version 300 es
int x /* mid */ y;`;
        const result = optimizeShaderSource(source);
        expect(result).toContain('int x y;');
        expect(result).not.toContain('mid');
    });

    it('collapses redundant whitespace', () => {
        const source = `#version 300 es
void    main(   )  {
    int     x   =   1;
}`;
        const result = optimizeShaderSource(source);
        expect(result).not.toMatch(/ {2,}/);
        expect(result).toContain('void main(');
        expect(result).toContain('int x = 1;');
    });

    it('removes empty lines', () => {
        const source = `#version 300 es

precision highp float;

void main() {}`;
        const result = optimizeShaderSource(source);
        const lines = result.split('\n');
        expect(lines.every((line) => line.length > 0)).toBe(true);
    });

    it('preserves #version as first line', () => {
        const source = `#version 300 es
void main() {}`;
        const result = optimizeShaderSource(source);
        expect(result.startsWith('#version 300 es')).toBe(true);
    });

    it('preserves preprocessor directives on their own lines', () => {
        const source = `#version 300 es
#define FOO 1
#ifdef FOO
int x = FOO;
#endif`;
        const result = optimizeShaderSource(source);
        expect(result).toContain('#define FOO 1');
        expect(result).toContain('#ifdef FOO');
        expect(result).toContain('#endif');
    });

    it('reduces source size for a realistic shader', () => {
        const source = `#version 300 es
precision highp float;

// Vertex transform
uniform mat4 u_Model;
uniform mat4 u_View;
uniform mat4 u_Projection;

in vec3 a_Position;
in vec2 a_UV0;

out vec2 v_UV;

void main() {
    v_UV = a_UV0;
    vec4 worldPos = u_Model * vec4(a_Position, 1.0);
    gl_Position = u_Projection * u_View * worldPos;
}`;
        const result = optimizeShaderSource(source);
        expect(result.length).toBeLessThan(source.length);
        expect(result).toContain('#version 300 es');
        expect(result).toContain('void main()');
        expect(result).toContain('gl_Position');
    });

    it('handles comments containing comment-like sequences', () => {
        const source = `#version 300 es
// fake /* not a block */
void main() {
    int x = 1; /* real block */ int y = 2;
}`;
        const result = optimizeShaderSource(source);
        expect(result).not.toContain('fake');
        expect(result).not.toContain('real block');
        expect(result).toContain('int x = 1;');
        expect(result).toContain('int y = 2;');
    });

    it('can be disabled via options', () => {
        const source = `#version 300 es
// comment
void main() {}`;
        const result = optimizeShaderSource(source, {
            stripComments: false,
            collapseWhitespace: false,
            removeEmptyLines: false,
        });
        expect(result).toBe(source);
    });
});
