import { createGltfUnlitShaderDefinition, createGltfPbrShaderDefinition } from '@axrone/scene-runtime-gltf';
import {
    FULLSCREEN_VERTEX_SHADER_SOURCE,
    TONEMAP_FRAGMENT_SHADER_SOURCE,
    POST_PROCESS_FRAGMENT_SHADER_SOURCE,
} from '@axrone/render-webgl2/internal/render-pass-shaders';
import { optimizeShaderSource } from '@axrone/render-webgl2/shader/source-optimizer';
import { compileRenderShaderEffect } from '@axrone/render-core/shader-effect';
import { SceneShaderFactory } from '@axrone/scene-runtime/scene-shader-factory';
import type { SceneShaderResource } from '@axrone/scene-runtime/shader-registry';
import type { SceneShaderDefinition } from '@axrone/scene-runtime/types';

interface ShaderSample {
    readonly id: string;
    readonly label: string;
    readonly vertexSource: string;
    readonly fragmentSource: string;
}

interface MetricSummary {
    readonly mean: number;
    readonly median: number;
    readonly p95: number;
    readonly min: number;
    readonly max: number;
    readonly stdev: number;
    readonly coefficientOfVariationPct: number;
    readonly samples: readonly number[];
}

interface ShaderBenchmarkResult {
    readonly id: string;
    readonly label: string;
    readonly vertexSourceChars: number;
    readonly fragmentSourceChars: number;
    readonly totalSourceChars: number;
    readonly compileTimeMs: MetricSummary;
    readonly linkTimeMs: MetricSummary;
    readonly totalTimeMs: MetricSummary;
}

interface BatchResult {
    readonly totalTimeMs: MetricSummary;
    readonly programCount: number;
}

interface BenchmarkConfig {
    readonly warmup: number;
    readonly iterations: number;
    readonly optimizeSource?: boolean;
}

interface BenchmarkReport {
    readonly generatedAt: string;
    readonly environment: {
        readonly userAgent: string;
        readonly webglVersion: string;
        readonly glslVersion: string;
        readonly renderer: string;
        readonly vendor: string;
        readonly unmaskedRenderer: string;
        readonly unmaskedVendor: string;
        readonly parallelShaderCompileAvailable: boolean;
        readonly maxVertexUniformVectors: number;
        readonly maxFragmentUniformVectors: number;
        readonly maxTextureImageUnits: number;
    };
    readonly config: BenchmarkConfig;
    readonly sourceOptimization?: {
        readonly applied: boolean;
        readonly optimizeTimeMs: number;
        readonly reductionPct: number;
    };
    readonly shaders: readonly ShaderBenchmarkResult[];
    readonly sequentialBatch: BatchResult;
    readonly parallelBatch: BatchResult | null;
}

const TRIVIAL_FRAGMENT_SOURCE = `#version 300 es
precision highp float;
out vec4 outColor;
void main() {
    outColor = vec4(1.0, 0.0, 0.0, 1.0);
}
`;

const round = (value: number, digits = 3): number => {
    const factor = Math.pow(10, digits);
    return Math.round(value * factor) / factor;
};

const collectShaders = (): readonly ShaderSample[] => {
    const unlit = createGltfUnlitShaderDefinition();
    const pbr = createGltfPbrShaderDefinition();

    return [
        {
            id: 'tiny',
            label: 'Trivial fullscreen',
            vertexSource: FULLSCREEN_VERTEX_SHADER_SOURCE,
            fragmentSource: TRIVIAL_FRAGMENT_SOURCE,
        },
        {
            id: 'unlit',
            label: 'GLTF Unlit',
            vertexSource: unlit.vertexSource ?? '',
            fragmentSource: unlit.fragmentSource ?? '',
        },
        {
            id: 'pbr',
            label: 'GLTF PBR',
            vertexSource: pbr.vertexSource ?? '',
            fragmentSource: pbr.fragmentSource ?? '',
        },
        {
            id: 'tonemap',
            label: 'Tonemap (ACES/AGX)',
            vertexSource: FULLSCREEN_VERTEX_SHADER_SOURCE,
            fragmentSource: TONEMAP_FRAGMENT_SHADER_SOURCE,
        },
        {
            id: 'post-process',
            label: 'Post-process (FXAA/Bloom/TAA)',
            vertexSource: FULLSCREEN_VERTEX_SHADER_SOURCE,
            fragmentSource: POST_PROCESS_FRAGMENT_SHADER_SOURCE,
        },
    ];
};

const computeStats = (values: readonly number[]): MetricSummary => {
    if (values.length === 0) {
        return {
            mean: 0,
            median: 0,
            p95: 0,
            min: 0,
            max: 0,
            stdev: 0,
            coefficientOfVariationPct: 0,
            samples: [],
        };
    }

    const sorted = [...values].sort((left, right) => left - right);
    const sum = sorted.reduce((accumulator, value) => accumulator + value, 0);
    const average = sum / sorted.length;
    const median = sorted[Math.floor(sorted.length * 0.5)] ?? 0;
    const p95Index = Math.min(sorted.length - 1, Math.max(0, Math.ceil(sorted.length * 0.95) - 1));
    const p95 = sorted[p95Index] ?? 0;

    const variance =
        sorted.reduce((accumulator, value) => accumulator + (value - average) * (value - average), 0) /
        sorted.length;
    const standardDeviation = Math.sqrt(variance);

    return {
        mean: round(average),
        median: round(median),
        p95: round(p95),
        min: round(sorted[0] ?? 0),
        max: round(sorted[sorted.length - 1] ?? 0),
        stdev: round(standardDeviation),
        coefficientOfVariationPct: average === 0 ? 0 : round((standardDeviation / average) * 100, 2),
        samples: sorted.map((value) => round(value)),
    };
};

interface SyncCompileResult {
    readonly compileTimeMs: number;
    readonly linkTimeMs: number;
    readonly totalTimeMs: number;
    readonly program: WebGLProgram;
}

const compileProgramSync = (
    gl: WebGL2RenderingContext,
    vertexSource: string,
    fragmentSource: string
): SyncCompileResult => {
    const totalStart = performance.now();

    const vertexShader = gl.createShader(gl.VERTEX_SHADER)!;
    gl.shaderSource(vertexShader, vertexSource);
    gl.compileShader(vertexShader);
    if (!gl.getShaderParameter(vertexShader, gl.COMPILE_STATUS)) {
        const info = gl.getShaderInfoLog(vertexShader) ?? 'vertex compile failed';
        gl.deleteShader(vertexShader);
        throw new Error(`Vertex shader compile failed: ${info}`);
    }

    const fragmentShader = gl.createShader(gl.FRAGMENT_SHADER)!;
    gl.shaderSource(fragmentShader, fragmentSource);
    gl.compileShader(fragmentShader);
    if (!gl.getShaderParameter(fragmentShader, gl.COMPILE_STATUS)) {
        const info = gl.getShaderInfoLog(fragmentShader) ?? 'fragment compile failed';
        gl.deleteShader(fragmentShader);
        gl.deleteShader(vertexShader);
        throw new Error(`Fragment shader compile failed: ${info}`);
    }

    const compileTimeMs = performance.now() - totalStart;

    const linkStart = performance.now();
    const program = gl.createProgram()!;
    gl.attachShader(program, vertexShader);
    gl.attachShader(program, fragmentShader);
    gl.linkProgram(program);

    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
        const info = gl.getProgramInfoLog(program) ?? 'link failed';
        gl.deleteProgram(program);
        gl.deleteShader(vertexShader);
        gl.deleteShader(fragmentShader);
        throw new Error(`Program link failed: ${info}`);
    }

    const linkTimeMs = performance.now() - linkStart;
    const totalTimeMs = performance.now() - totalStart;

    gl.deleteShader(vertexShader);
    gl.deleteShader(fragmentShader);

    return { compileTimeMs, linkTimeMs, totalTimeMs, program };
};

const compileBatchSequential = (
    gl: WebGL2RenderingContext,
    samples: readonly ShaderSample[]
): number => {
    const start = performance.now();
    const programs: WebGLProgram[] = [];

    try {
        for (const sample of samples) {
            const result = compileProgramSync(gl, sample.vertexSource, sample.fragmentSource);
            programs.push(result.program);
        }
    } finally {
        for (const program of programs) {
            gl.deleteProgram(program);
        }
    }

    return performance.now() - start;
};

const compileBatchParallel = (
    gl: WebGL2RenderingContext,
    ext: KHR_parallel_shader_compile | null,
    samples: readonly ShaderSample[]
): number => {
    if (!ext) {
        throw new Error('KHR_parallel_shader_compile not available');
    }

    const start = performance.now();

    interface PendingProgram {
        readonly program: WebGLProgram;
        readonly vertexShader: WebGLShader;
        readonly fragmentShader: WebGLShader;
    }

    const pending: PendingProgram[] = [];

    try {
        for (const sample of samples) {
            const vertexShader = gl.createShader(gl.VERTEX_SHADER)!;
            gl.shaderSource(vertexShader, sample.vertexSource);
            gl.compileShader(vertexShader);

            const fragmentShader = gl.createShader(gl.FRAGMENT_SHADER)!;
            gl.shaderSource(fragmentShader, sample.fragmentSource);
            gl.compileShader(fragmentShader);

            const program = gl.createProgram()!;
            gl.attachShader(program, vertexShader);
            gl.attachShader(program, fragmentShader);
            gl.linkProgram(program);

            pending.push({ program, vertexShader, fragmentShader });
        }

        for (const entry of pending) {
            while (!gl.getProgramParameter(entry.program, ext.COMPLETION_STATUS_KHR)) {
                // Busy-poll: the GPU driver compiles in parallel threads
            }

            if (!gl.getProgramParameter(entry.program, gl.LINK_STATUS)) {
                const info = gl.getProgramInfoLog(entry.program) ?? 'link failed';
                throw new Error(`Parallel program link failed: ${info}`);
            }
        }
    } finally {
        for (const entry of pending) {
            gl.deleteShader(entry.vertexShader);
            gl.deleteShader(entry.fragmentShader);
            gl.deleteProgram(entry.program);
        }
    }

    return performance.now() - start;
};

const createContext = (): WebGL2RenderingContext => {
    const canvas = document.createElement('canvas');
    canvas.width = 64;
    canvas.height = 64;
    const gl = canvas.getContext('webgl2', {
        antialias: false,
        depth: false,
        stencil: false,
        alpha: false,
        premultipliedAlpha: false,
        preserveDrawingBuffer: false,
        powerPreference: 'high-performance',
    });

    if (!gl) {
        throw new Error('WebGL2 is not supported in this browser');
    }

    return gl;
};

const getEnvironment = (gl: WebGL2RenderingContext) => {
    const debugInfo = gl.getExtension('WEBGL_debug_renderer_info');
    const parallelExt = gl.getExtension('KHR_parallel_shader_compile');

    return {
        userAgent: navigator.userAgent,
        webglVersion: String(gl.getParameter(gl.VERSION)),
        glslVersion: String(gl.getParameter(gl.SHADING_LANGUAGE_VERSION)),
        renderer: String(gl.getParameter(gl.RENDERER)),
        vendor: String(gl.getParameter(gl.VENDOR)),
        unmaskedRenderer: debugInfo
            ? String(gl.getParameter(debugInfo.UNMASKED_RENDERER_WEBGL))
            : 'unavailable',
        unmaskedVendor: debugInfo
            ? String(gl.getParameter(debugInfo.UNMASKED_VENDOR_WEBGL))
            : 'unavailable',
        parallelShaderCompileAvailable: Boolean(parallelExt),
        maxVertexUniformVectors: Number(gl.getParameter(gl.MAX_VERTEX_UNIFORM_VECTORS)),
        maxFragmentUniformVectors: Number(gl.getParameter(gl.MAX_FRAGMENT_UNIFORM_VECTORS)),
        maxTextureImageUnits: Number(gl.getParameter(gl.MAX_TEXTURE_IMAGE_UNITS)),
    };
};

const benchmarkShader = (
    gl: WebGL2RenderingContext,
    sample: ShaderSample,
    warmup: number,
    iterations: number
): ShaderBenchmarkResult => {
    const compileTimes: number[] = [];
    const linkTimes: number[] = [];
    const totalTimes: number[] = [];

    const runOnce = (): void => {
        const result = compileProgramSync(gl, sample.vertexSource, sample.fragmentSource);
        gl.deleteProgram(result.program);
        compileTimes.push(result.compileTimeMs);
        linkTimes.push(result.linkTimeMs);
        totalTimes.push(result.totalTimeMs);
    };

    for (let i = 0; i < warmup; i += 1) {
        runOnce();
    }

    for (let i = 0; i < iterations; i += 1) {
        runOnce();
    }

    return {
        id: sample.id,
        label: sample.label,
        vertexSourceChars: sample.vertexSource.length,
        fragmentSourceChars: sample.fragmentSource.length,
        totalSourceChars: sample.vertexSource.length + sample.fragmentSource.length,
        compileTimeMs: computeStats(compileTimes),
        linkTimeMs: computeStats(linkTimes),
        totalTimeMs: computeStats(totalTimes),
    };
};

const benchmarkBatchSequential = (
    gl: WebGL2RenderingContext,
    samples: readonly ShaderSample[],
    warmup: number,
    iterations: number
): BatchResult => {
    const times: number[] = [];

    for (let i = 0; i < warmup; i += 1) {
        compileBatchSequential(gl, samples);
    }

    for (let i = 0; i < iterations; i += 1) {
        times.push(compileBatchSequential(gl, samples));
    }

    return {
        totalTimeMs: computeStats(times),
        programCount: samples.length,
    };
};

const benchmarkBatchParallel = (
    gl: WebGL2RenderingContext,
    ext: KHR_parallel_shader_compile | null,
    samples: readonly ShaderSample[],
    warmup: number,
    iterations: number
): BatchResult | null => {
    if (!ext) {
        return null;
    }

    const times: number[] = [];

    for (let i = 0; i < warmup; i += 1) {
        compileBatchParallel(gl, ext, samples);
    }

    for (let i = 0; i < iterations; i += 1) {
        times.push(compileBatchParallel(gl, ext, samples));
    }

    return {
        totalTimeMs: computeStats(times),
        programCount: samples.length,
    };
};

const runBenchmark = (config: BenchmarkConfig): BenchmarkReport => {
    const gl = createContext();
    const parallelExt = gl.getExtension('KHR_parallel_shader_compile');
    const rawSamples = collectShaders();

    let samples = rawSamples;
    let sourceOptimization: BenchmarkReport['sourceOptimization'];

    if (config.optimizeSource) {
        const optimizeStart = performance.now();
        samples = rawSamples.map((sample) => ({
            ...sample,
            vertexSource: optimizeShaderSource(sample.vertexSource),
            fragmentSource: optimizeShaderSource(sample.fragmentSource),
        }));
        const optimizeTimeMs = performance.now() - optimizeStart;

        const originalTotal = rawSamples.reduce(
            (sum, s) => sum + s.vertexSource.length + s.fragmentSource.length,
            0
        );
        const optimizedTotal = samples.reduce(
            (sum, s) => sum + s.vertexSource.length + s.fragmentSource.length,
            0
        );
        const reductionPct =
            originalTotal === 0
                ? 0
                : Number((((originalTotal - optimizedTotal) / originalTotal) * 100).toFixed(2));

        sourceOptimization = { applied: true, optimizeTimeMs, reductionPct };
    } else {
        sourceOptimization = { applied: false, optimizeTimeMs: 0, reductionPct: 0 };
    }

    const shaderResults = samples.map((sample) =>
        benchmarkShader(gl, sample, config.warmup, config.iterations)
    );

    const sequentialBatch = benchmarkBatchSequential(gl, samples, config.warmup, config.iterations);
    const parallelBatch = benchmarkBatchParallel(
        gl,
        parallelExt,
        samples,
        config.warmup,
        config.iterations
    );

    const report: BenchmarkReport = {
        generatedAt: new Date().toISOString(),
        environment: getEnvironment(gl),
        config,
        sourceOptimization,
        shaders: shaderResults,
        sequentialBatch,
        parallelBatch,
    };

    const loseContext = gl.getExtension('WEBGL_lose_context');
    if (loseContext) {
        loseContext.loseContext();
    }

    return report;
};

interface EffectCompileReport {
    readonly generatedAt: string;
    readonly shaders: readonly {
        readonly id: string;
        readonly vertexSourceChars: number;
        readonly fragmentSourceChars: number;
        readonly hasPrecompiledSources: boolean;
        readonly effectCompileMs: MetricSummary;
    }[];
    readonly summary: {
        readonly totalWastedMsPerCreateCall: number;
        readonly shadersWithRedundantCompile: number;
    };
}

const measureEffectCompileCost = (iterations = 500): EffectCompileReport => {
    const defs = collectSceneDefinitions();
    const shaders = defs.map((def) => {
        const compiled = def.effect ? compileRenderShaderEffect(def.effect) : null;
        const vertexSource = def.vertexSource ?? compiled?.vertexSource ?? '';
        const fragmentSource = def.fragmentSource ?? compiled?.fragmentSource ?? '';

        const hasPrecompiledSources = Boolean(def.vertexSource && def.fragmentSource);

        const times: number[] = [];
        for (let w = 0; w < 50; w += 1) {
            if (def.effect) compileRenderShaderEffect(def.effect);
        }
        for (let i = 0; i < iterations; i += 1) {
            if (!def.effect) {
                times.push(0);
                continue;
            }
            const start = performance.now();
            compileRenderShaderEffect(def.effect);
            times.push(performance.now() - start);
        }

        return {
            id: def.id,
            vertexSourceChars: vertexSource.length,
            fragmentSourceChars: fragmentSource.length,
            hasPrecompiledSources,
            effectCompileMs: computeStats(times),
        };
    });

    const wastedShaders = shaders.filter((s) => s.hasPrecompiledSources && s.effectCompileMs.mean > 0);
    const totalWasted = wastedShaders.reduce((sum, s) => sum + s.effectCompileMs.mean, 0);

    return {
        generatedAt: new Date().toISOString(),
        shaders,
        summary: {
            totalWastedMsPerCreateCall: Number(totalWasted.toFixed(4)),
            shadersWithRedundantCompile: wastedShaders.length,
        },
    };
};

interface ShaderBenchmarkApi {
    readonly isReady: true;
    readonly getEnvironment: () => ReturnType<typeof getEnvironment>;
    readonly runBenchmark: (config: BenchmarkConfig) => BenchmarkReport;
    readonly runComparison: (config: Omit<BenchmarkConfig, 'optimizeSource'>) => ComparisonReport;
    readonly runFactoryBenchmark: (config: {
        warmup: number;
        iterations: number;
        duplicateCount?: number;
    }) => Promise<FactoryBenchmarkReport>;
    readonly getShaderSources: () => { id: string; vertexChars: number; fragmentChars: number }[];
    readonly measureEffectCompileCost: (iterations?: number) => EffectCompileReport;
}

interface ComparisonEntry {
    readonly label: string;
    readonly sourceChars: number;
    readonly sequentialBatchMs: MetricSummary;
    readonly parallelBatchMs: MetricSummary | null;
}

interface ComparisonReport {
    readonly generatedAt: string;
    readonly environment: ReturnType<typeof getEnvironment>;
    readonly config: Omit<BenchmarkConfig, 'optimizeSource'>;
    readonly sourceReductionPct: number;
    readonly optimizeTimeMs: number;
    readonly raw: ComparisonEntry;
    readonly optimized: ComparisonEntry;
    readonly deltas: {
        readonly sequentialBatchDeltaMs: number;
        readonly sequentialBatchImprovementPct: number;
        readonly parallelBatchDeltaMs: number | null;
        readonly parallelBatchImprovementPct: number | null;
        readonly parallelSpeedupRaw: number | null;
        readonly parallelSpeedupOptimized: number | null;
    };
}

const totalChars = (samples: readonly ShaderSample[]): number =>
    samples.reduce((sum, s) => sum + s.vertexSource.length + s.fragmentSource.length, 0);

const runComparison = (config: Omit<BenchmarkConfig, 'optimizeSource'>): ComparisonReport => {
    const gl = createContext();
    const parallelExt = gl.getExtension('KHR_parallel_shader_compile');
    const rawSamples = collectShaders();

    const optimizeStart = performance.now();
    const optimizedSamples = rawSamples.map((sample) => ({
        ...sample,
        vertexSource: optimizeShaderSource(sample.vertexSource),
        fragmentSource: optimizeShaderSource(sample.fragmentSource),
    }));
    const optimizeTimeMs = performance.now() - optimizeStart;

    const rawChars = totalChars(rawSamples);
    const optChars = totalChars(optimizedSamples);
    const sourceReductionPct =
        rawChars === 0 ? 0 : Number((((rawChars - optChars) / rawChars) * 100).toFixed(2));

    const rawSeq = benchmarkBatchSequential(gl, rawSamples, config.warmup, config.iterations);
    const rawPar = benchmarkBatchParallel(gl, parallelExt, rawSamples, config.warmup, config.iterations);

    const optSeq = benchmarkBatchSequential(gl, optimizedSamples, config.warmup, config.iterations);
    const optPar = benchmarkBatchParallel(gl, parallelExt, optimizedSamples, config.warmup, config.iterations);

    const rawEntry: ComparisonEntry = {
        label: 'raw',
        sourceChars: rawChars,
        sequentialBatchMs: rawSeq.totalTimeMs,
        parallelBatchMs: rawPar?.totalTimeMs ?? null,
    };

    const optEntry: ComparisonEntry = {
        label: 'optimized',
        sourceChars: optChars,
        sequentialBatchMs: optSeq.totalTimeMs,
        parallelBatchMs: optPar?.totalTimeMs ?? null,
    };

    const seqDelta = rawSeq.totalTimeMs.mean - optSeq.totalTimeMs.mean;
    const seqImprovementPct =
        rawSeq.totalTimeMs.mean === 0
            ? 0
            : Number(((seqDelta / rawSeq.totalTimeMs.mean) * 100).toFixed(2));

    let parDelta: number | null = null;
    let parImprovementPct: number | null = null;
    if (rawPar && optPar) {
        parDelta = rawPar.totalTimeMs.mean - optPar.totalTimeMs.mean;
        parImprovementPct =
            rawPar.totalTimeMs.mean === 0
                ? 0
                : Number(((parDelta / rawPar.totalTimeMs.mean) * 100).toFixed(2));
    }

    const parallelSpeedupRaw =
        rawPar && rawSeq.totalTimeMs.mean > 0
            ? Number((rawSeq.totalTimeMs.mean / rawPar.totalTimeMs.mean).toFixed(2))
            : null;
    const parallelSpeedupOptimized =
        optPar && optSeq.totalTimeMs.mean > 0
            ? Number((optSeq.totalTimeMs.mean / optPar.totalTimeMs.mean).toFixed(2))
            : null;

    const loseContext = gl.getExtension('WEBGL_lose_context');
    if (loseContext) {
        loseContext.loseContext();
    }

    return {
        generatedAt: new Date().toISOString(),
        environment: getEnvironment(gl),
        config,
        sourceReductionPct,
        optimizeTimeMs,
        raw: rawEntry,
        optimized: optEntry,
        deltas: {
            sequentialBatchDeltaMs: Number(seqDelta.toFixed(2)),
            sequentialBatchImprovementPct: seqImprovementPct,
            parallelBatchDeltaMs: parDelta !== null ? Number(parDelta.toFixed(2)) : null,
            parallelBatchImprovementPct: parImprovementPct,
            parallelSpeedupRaw,
            parallelSpeedupOptimized,
        },
    };
};

interface FactoryScenarioResult {
    readonly label: string;
    readonly totalTimeMs: MetricSummary;
    readonly shaderCount: number;
}

interface FactoryBenchmarkReport {
    readonly generatedAt: string;
    readonly environment: ReturnType<typeof getEnvironment>;
    readonly config: { readonly warmup: number; readonly iterations: number };
    readonly scenarios: readonly FactoryScenarioResult[];
    readonly summary: {
        readonly parallelSpeedupVsSequential: number;
        readonly cacheDedupSpeedupVsNoCache: number;
        readonly cacheWarmSpeedupVsCold: number;
    };
}

const collectSceneDefinitions = (): readonly SceneShaderDefinition[] => {
    const pbr = createGltfPbrShaderDefinition();
    const unlit = createGltfUnlitShaderDefinition();

    return [
        { ...pbr, id: 'scene/pbr-opaque' },
        { ...unlit, id: 'scene/unlit' },
        {
            id: 'scene/post-tonemap',
            vertexSource: FULLSCREEN_VERTEX_SHADER_SOURCE,
            fragmentSource: TONEMAP_FRAGMENT_SHADER_SOURCE,
        },
        {
            id: 'scene/post-process',
            vertexSource: FULLSCREEN_VERTEX_SHADER_SOURCE,
            fragmentSource: POST_PROCESS_FRAGMENT_SHADER_SOURCE,
        },
    ];
};

const collectDuplicateDefinitions = (count: number): readonly SceneShaderDefinition[] => {
    const pbr = createGltfPbrShaderDefinition();
    return Array.from({ length: count }, (_, index) => ({
        ...pbr,
        id: `scene/pbr-instance-${index}`,
    }));
};

const runFactoryBenchmark = async (config: {
    warmup: number;
    iterations: number;
    duplicateCount?: number;
}): Promise<FactoryBenchmarkReport> => {
    const gl = createContext();
    const defs = collectSceneDefinitions();
    const dupCount = config.duplicateCount ?? 10;
    const dupDefs = collectDuplicateDefinitions(dupCount);

    const scenarios: FactoryScenarioResult[] = [];
    const cleanupResources = (factory: SceneShaderFactory, resources: SceneShaderResource[]): void => {
        resources.forEach((r) => factory.delete(r));
        factory.clearCache();
    };

    const measureSync = (
        label: string,
        defsToUse: readonly SceneShaderDefinition[],
        enableCache: boolean,
        action: (factory: SceneShaderFactory, defs: readonly SceneShaderDefinition[]) => SceneShaderResource[]
    ): void => {
        const times: number[] = [];
        const runOnce = (): void => {
            const factory = new SceneShaderFactory({ gl, enableCache });
            const start = performance.now();
            const resources = action(factory, defsToUse);
            times.push(performance.now() - start);
            cleanupResources(factory, resources);
        };

        for (let i = 0; i < config.warmup; i += 1) runOnce();
        for (let i = 0; i < config.iterations; i += 1) runOnce();

        scenarios.push({ label, totalTimeMs: computeStats(times), shaderCount: defsToUse.length });
    };

    measureSync(
        'sequential-no-cache',
        defs,
        false,
        (factory, d) => d.map((def) => factory.create(def))
    );

    measureSync(
        'sequential-with-cache',
        defs,
        true,
        (factory, d) => d.map((def) => factory.create(def))
    );

    {
        const times: number[] = [];
        const runOnce = async (): Promise<void> => {
            const factory = new SceneShaderFactory({ gl, enableCache: true });
            const start = performance.now();
            const resources = await factory.createBatch([...defs], { pollMode: 'busy' });
            times.push(performance.now() - start);
            cleanupResources(factory, resources);
        };

        for (let i = 0; i < config.warmup; i += 1) await runOnce();
        for (let i = 0; i < config.iterations; i += 1) await runOnce();

        scenarios.push({ label: 'parallel-batch', totalTimeMs: computeStats(times), shaderCount: defs.length });
    }

    measureSync(
        'no-cache-dedup',
        dupDefs,
        false,
        (factory, d) => d.map((def) => factory.create(def))
    );

    measureSync(
        'cache-dedup',
        dupDefs,
        true,
        (factory, d) => d.map((def) => factory.create(def))
    );

    const findScenario = (label: string): FactoryScenarioResult =>
        scenarios.find((s) => s.label === label)!;

    const seqNoCache = findScenario('sequential-no-cache');
    const parallelBatch = findScenario('parallel-batch');
    const cacheDedup = findScenario('cache-dedup');
    const noCacheDedup = findScenario('no-cache-dedup');

    const parallelSpeedup =
        parallelBatch.totalTimeMs.mean > 0
            ? Number((seqNoCache.totalTimeMs.mean / parallelBatch.totalTimeMs.mean).toFixed(2))
            : 0;
    const dedupSpeedup =
        cacheDedup.totalTimeMs.mean > 0
            ? Number((noCacheDedup.totalTimeMs.mean / cacheDedup.totalTimeMs.mean).toFixed(2))
            : 0;

    const factory = new SceneShaderFactory({ gl, enableCache: true });
    const warmResources = defs.map((def) => factory.create(def));
    const warmTimes: number[] = [];
    for (let i = 0; i < config.iterations; i += 1) {
        const start = performance.now();
        const resources = defs.map((def) => factory.create(def));
        warmTimes.push(performance.now() - start);
        resources.forEach((r) => factory.delete(r));
    }
    warmResources.forEach((r) => factory.delete(r));
    factory.clearCache();
    const warmStats = computeStats(warmTimes);
    const warmSpeedup =
        warmStats.mean > 0
            ? Number((seqNoCache.totalTimeMs.mean / warmStats.mean).toFixed(2))
            : 0;

    scenarios.push({ label: 'cache-warm-repeat', totalTimeMs: warmStats, shaderCount: defs.length });

    const loseContext = gl.getExtension('WEBGL_lose_context');
    if (loseContext) {
        loseContext.loseContext();
    }

    return {
        generatedAt: new Date().toISOString(),
        environment: getEnvironment(gl),
        config,
        scenarios,
        summary: {
            parallelSpeedupVsSequential: parallelSpeedup,
            cacheDedupSpeedupVsNoCache: dedupSpeedup,
            cacheWarmSpeedupVsCold: warmSpeedup,
        },
    };
};

const api: ShaderBenchmarkApi = {
    isReady: true,
    getEnvironment: () => {
        const gl = createContext();
        const env = getEnvironment(gl);
        const loseContext = gl.getExtension('WEBGL_lose_context');
        if (loseContext) {
            loseContext.loseContext();
        }
        return env;
    },
    runBenchmark,
    runComparison,
    runFactoryBenchmark,
    getShaderSources: () => {
        const samples = collectShaders();
        return samples.map((sample) => ({
            id: sample.id,
            vertexChars: sample.vertexSource.length,
            fragmentChars: sample.fragmentSource.length,
        }));
    },
    measureEffectCompileCost,
};

declare global {
    interface Window {
        __AXRONE_SHADER_BENCHMARK__: ShaderBenchmarkApi;
    }
}

window.__AXRONE_SHADER_BENCHMARK__ = api;
