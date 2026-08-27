/**
 * Global shader library / chunk registry.
 *
 * Beyond the per-effect `libraries` array on `RenderShaderEffectDefinition`,
 * the toolkit exposes a process-wide registry of reusable GLSL chunks that any
 * effect can pull in by name with a familiar `#include` directive:
 *
 *   ```glsl
 *   #include <noise/fbm>
 *   #include "shared/hash"
 *   ```
 *
 * Includes are resolved recursively (a chunk may include other chunks), with
 * cycle detection and structured diagnostics — the same model Three.js uses
 * with `ShaderChunk`, but authorable from `.ts` modules and resolvable against
 * the fully compiled shader source.
 */

import {
    type ShaderDiagnostic,
    type ShaderDiagnosticSink,
    createDiagnosticSink,
} from './diagnostics';

const INCLUDE_PATTERN = /^[ \t]*#[ \t]*include[ \t]+[<"]([^>"]+)[>"][ \t]*$/;

const registry = new Map<string, string>();

export interface ShaderLibraryEntry {
    readonly id: string;
    readonly code: string;
}

/**
 * Register (or replace) a global shader chunk. Returns the entry so it can be
 * used inline, e.g. `const noise = registerShaderLibrary('noise/fbm', glsl\`…\`)`.
 */
export const registerShaderLibrary = (id: string, code: string): ShaderLibraryEntry => {
    registry.set(id, code);
    return { id, code };
};

/** Alias kept for authoring ergonomics (`defineShaderLibrary`). */
export const defineShaderLibrary = registerShaderLibrary;

export const hasShaderLibrary = (id: string): boolean => registry.has(id);

export const getShaderLibrary = (id: string): string | undefined => registry.get(id);

export const listShaderLibraries = (): readonly ShaderLibraryEntry[] =>
    [...registry.entries()].map(([id, code]) => ({ id, code }));

/** Remove every registered chunk — primarily for deterministic tests. */
export const clearShaderLibraries = (): void => {
    registry.clear();
};

/** Remove a single registered chunk by id. Returns true if the entry existed. */
export const disposeShaderLibrary = (id: string): boolean => registry.delete(id);

/** Concatenate a set of chunks by id, in order. Missing ids are skipped. */
export const resolveShaderLibraries = (ids: readonly string[]): string =>
    ids
        .map((id) => registry.get(id))
        .filter((code): code is string => code !== undefined)
        .join('\n\n');

export interface ShaderIncludeOptions {
    readonly sink?: ShaderDiagnosticSink;
    readonly preserveLineMarkers?: boolean;
}

export interface ShaderIncludeResult {
    readonly code: string;
    readonly diagnostics: readonly ShaderDiagnostic[];
}

const expandSource = (
    source: string,
    sourceId: string | undefined,
    stack: ReadonlySet<string>,
    sink: ShaderDiagnosticSink,
    preserveLineMarkers: boolean
): string => {
    const lines = source.split('\n');
    const fragments: string[] = [];

    lines.forEach((line, index) => {
        const match = line.match(INCLUDE_PATTERN);
        if (!match) {
            fragments.push(line);
            return;
        }

        const id = match[1];
        const physicalLine = index + 1;
        const code = registry.get(id);

        if (code === undefined) {
            sink.reportError('LIB_MISSING_INCLUDE', `Unknown shader library "${id}"`, {
                line: physicalLine,
                sourceId,
            });
            fragments.push(line);
            return;
        }

        if (stack.has(id)) {
            const trail = [...stack, id].join(' -> ');
            sink.reportError('LIB_INCLUDE_CYCLE', `Shader include cycle detected: ${trail}`, {
                line: physicalLine,
                sourceId,
            });
            fragments.push(line);
            return;
        }

        const nextStack = new Set(stack);
        nextStack.add(id);

        if (preserveLineMarkers) {
            fragments.push(`#line 1 "${id}"`);
        }
        fragments.push(
            expandSource(code, id, nextStack, sink, preserveLineMarkers)
        );
        if (preserveLineMarkers) {
            fragments.push(`#line ${physicalLine + 1}${sourceId ? ` "${sourceId}"` : ''}`);
        }
    });

    return fragments.join('\n');
};

/**
 * Expand every `#include <id>` / `#include "id"` directive in `source`,
 * recursively, with cycle detection. Returns the resolved source together with
 * any collected diagnostics.
 */
export const expandShaderIncludes = (
    source: string,
    options: ShaderIncludeOptions = {}
): ShaderIncludeResult => {
    const sink = options.sink ?? createDiagnosticSink();
    const code = expandSource(
        source,
        undefined,
        new Set(),
        sink,
        options.preserveLineMarkers ?? false
    );
    return { code, diagnostics: sink.diagnostics };
};
