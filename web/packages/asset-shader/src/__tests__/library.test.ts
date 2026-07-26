import { afterEach, describe, expect, it } from 'vitest';
import {
    clearShaderLibraries,
    defineShaderLibrary,
    expandShaderIncludes,
    hasShaderLibrary,
    registerShaderLibrary,
    resolveShaderLibraries,
} from '../library';

const reset = () => clearShaderLibraries();

describe('shader library registry', () => {
    afterEach(reset);

    it('registers and resolves chunks by id', () => {
        registerShaderLibrary('noise/hash', 'float hash(vec3 p) { return fract(p.x); }');
        expect(hasShaderLibrary('noise/hash')).toBe(true);
        expect(resolveShaderLibraries(['noise/hash'])).toContain('float hash');
    });

    it('expands #include directives recursively with nesting', () => {
        defineShaderLibrary('base', 'float baseFn() { return 0.0; }');
        defineShaderLibrary('derived', '#include <base>\nfloat derivedFn() { return baseFn(); }');
        const source = '#include <derived>\nvoid main() { derivedFn(); }';

        const { code } = expandShaderIncludes(source);

        expect(code).toContain('float baseFn');
        expect(code).toContain('float derivedFn');
        expect(code).not.toContain('#include');
    });

    it('reports a diagnostic for unknown includes', () => {
        const { diagnostics } = expandShaderIncludes('#include <missing>');
        expect(diagnostics.some((d) => d.code === 'LIB_MISSING_INCLUDE')).toBe(true);
    });

    it('detects include cycles', () => {
        defineShaderLibrary('a', '#include <b>');
        defineShaderLibrary('b', '#include <a>');
        const { diagnostics } = expandShaderIncludes('#include <a>');
        expect(diagnostics.some((d) => d.code === 'LIB_INCLUDE_CYCLE')).toBe(true);
    });

    it('emits #line markers into nested chunks when requested', () => {
        defineShaderLibrary('chunk', 'float x = 1.0;');
        const { code } = expandShaderIncludes('#include <chunk>\nfloat y = 2.0;', {
            preserveLineMarkers: true,
        });
        expect(code).toContain('#line 1 "chunk"');
    });
});
