export interface ShaderSourceOptimizationOptions {
    readonly stripComments?: boolean;
    readonly collapseWhitespace?: boolean;
    readonly removeEmptyLines?: boolean;
}

export interface ShaderSourceOptimizationResult {
    readonly vertexSource: string;
    readonly fragmentSource: string;
    readonly originalVertexChars: number;
    readonly originalFragmentChars: number;
    readonly optimizedVertexChars: number;
    readonly optimizedFragmentChars: number;
    readonly vertexReductionPct: number;
    readonly fragmentReductionPct: number;
}

const DEFAULT_OPTIONS: Required<ShaderSourceOptimizationOptions> = {
    stripComments: true,
    collapseWhitespace: true,
    removeEmptyLines: true,
};

const resolveOptions = (
    options?: ShaderSourceOptimizationOptions
): Required<ShaderSourceOptimizationOptions> => ({
    stripComments: options?.stripComments ?? DEFAULT_OPTIONS.stripComments,
    collapseWhitespace: options?.collapseWhitespace ?? DEFAULT_OPTIONS.collapseWhitespace,
    removeEmptyLines: options?.removeEmptyLines ?? DEFAULT_OPTIONS.removeEmptyLines,
});

const stripComments = (source: string): string => {
    let result = '';
    let cursor = 0;
    const length = source.length;

    while (cursor < length) {
        const current = source[cursor]!;
        const next = source[cursor + 1];

        if (current === '/' && next === '/') {
            cursor += 2;
            while (cursor < length && source[cursor] !== '\n') {
                cursor += 1;
            }
            continue;
        }

        if (current === '/' && next === '*') {
            cursor += 2;
            while (cursor < length && !(source[cursor] === '*' && source[cursor + 1] === '/')) {
                cursor += 1;
            }
            cursor += 2;
            result += ' ';
            continue;
        }

        result += current;
        cursor += 1;
    }

    return result;
};

const collapseWhitespace = (source: string): string => {
    const lines = source.split('\n');
    const processed: string[] = [];

    for (let index = 0; index < lines.length; index += 1) {
        const collapsed = lines[index]!.replace(/[ \t]+/g, ' ').replace(/ *$/, '').trimStart();
        processed.push(collapsed);
    }

    return processed.join('\n');
};

const removeEmptyLines = (source: string): string => {
    return source
        .split('\n')
        .filter((line) => line.length > 0)
        .join('\n');
};

const ensureVersionFirst = (source: string): string => {
    const lines = source.split('\n');
    const versionIndex = lines.findIndex((line) => line.trimStart().startsWith('#version'));

    if (versionIndex <= 0) {
        return source;
    }

    const versionLine = lines[versionIndex]!;
    const remaining = lines.filter((_, index) => index !== versionIndex);
    return [versionLine, ...remaining].join('\n');
};

export const optimizeShaderSource = (
    source: string,
    options?: ShaderSourceOptimizationOptions
): string => {
    const resolved = resolveOptions(options);
    let result = source;

    if (resolved.stripComments) {
        result = stripComments(result);
    }

    if (resolved.collapseWhitespace) {
        result = collapseWhitespace(result);
    }

    if (resolved.removeEmptyLines) {
        result = removeEmptyLines(result);
    }

    return ensureVersionFirst(result);
};

const calculateReductionPct = (original: number, optimized: number): number => {
    if (original === 0) {
        return 0;
    }

    return Number((((original - optimized) / original) * 100).toFixed(2));
};

export const optimizeShaderSources = (
    vertexSource: string,
    fragmentSource: string,
    options?: ShaderSourceOptimizationOptions
): ShaderSourceOptimizationResult => {
    const optimizedVertex = optimizeShaderSource(vertexSource, options);
    const optimizedFragment = optimizeShaderSource(fragmentSource, options);

    return {
        vertexSource: optimizedVertex,
        fragmentSource: optimizedFragment,
        originalVertexChars: vertexSource.length,
        originalFragmentChars: fragmentSource.length,
        optimizedVertexChars: optimizedVertex.length,
        optimizedFragmentChars: optimizedFragment.length,
        vertexReductionPct: calculateReductionPct(vertexSource.length, optimizedVertex.length),
        fragmentReductionPct: calculateReductionPct(fragmentSource.length, optimizedFragment.length),
    };
};
