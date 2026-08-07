import { afterEach, describe, expect, it } from 'vitest';
import { createDiagnosticSink } from '../diagnostics';
import {
    clearShaderLibraries,
    defineShaderLibrary,
    expandShaderIncludes,
    getShaderLibrary,
    hasShaderLibrary,
    listShaderLibraries,
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

    it('emits closing #line after include to restore outer source', () => {
        defineShaderLibrary('chunk', 'float x = 1.0;');
        const { code } = expandShaderIncludes(
            'float a = 0.0;\n#include <chunk>\nfloat b = 2.0;',
            { preserveLineMarkers: true }
        );
        // After the included chunk, a #line should restore the outer context
        expect(code).toContain('#line 3');
    });
});

describe('shader library registry — additional operations', () => {
    afterEach(reset);

    it('getShaderLibrary returns code for a registered id', () => {
        registerShaderLibrary('test/chunk', 'float v = 1.0;');
        expect(getShaderLibrary('test/chunk')).toBe('float v = 1.0;');
    });

    it('getShaderLibrary returns undefined for missing id', () => {
        expect(getShaderLibrary('nonexistent')).toBeUndefined();
    });

    it('listShaderLibraries returns all registered entries', () => {
        registerShaderLibrary('a', 'code-a');
        registerShaderLibrary('b', 'code-b');
        const list = listShaderLibraries();
        expect(list).toHaveLength(2);
        expect(list.map((e) => e.id)).toEqual(['a', 'b']);
        expect(list[0]?.code).toBe('code-a');
    });

    it('resolveShaderLibraries skips missing ids without error', () => {
        registerShaderLibrary('present', 'float x = 1.0;');
        const result = resolveShaderLibraries(['present', 'missing', 'present']);
        expect(result).toContain('float x = 1.0;');
        // 'missing' is silently skipped
        expect(result.split('float x = 1.0;').length - 1).toBe(2);
    });

    it('expandShaderIncludes supports double-quoted include syntax', () => {
        defineShaderLibrary('shared/util', 'float util() { return 1.0; }');
        const { code, diagnostics } = expandShaderIncludes('#include "shared/util"');
        expect(code).toContain('float util()');
        expect(diagnostics).toHaveLength(0);
    });

    it('expandShaderIncludes uses an external sink when provided', () => {
        const sink = createDiagnosticSink();
        expandShaderIncludes('#include <missing>', { sink });
        expect(sink.hasErrors).toBe(true);
        expect(sink.diagnostics[0]?.code).toBe('LIB_MISSING_INCLUDE');
    });
});
