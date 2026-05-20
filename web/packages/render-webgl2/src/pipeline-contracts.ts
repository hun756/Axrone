import type {
    RenderExecutionContext,
    RenderFrameResult,
    RenderPassKind,
    RenderPipelineBackend,
    RenderResourceName,
    ResolvedRenderPass,
} from '@axrone/render-core/types';

declare const webgl2RenderPassExecutorIdBrand: unique symbol;

export interface WebGL2RenderTextureNativeHandle {
    readonly kind: 'texture';
    readonly texture: WebGLTexture;
    readonly target: number;
}

export interface WebGL2DefaultFramebufferHandle {
    readonly kind: 'default-framebuffer';
}

export type WebGL2RenderResourceHandle =
    | WebGL2RenderTextureNativeHandle
    | WebGL2DefaultFramebufferHandle;

export interface WebGL2RenderPassExecutionResult {
    readonly drawCalls?: number;
    readonly notes?: readonly string[];
}

export interface WebGL2ResolvedFramebufferBinding {
    readonly framebuffer: WebGLFramebuffer | null;
    readonly colorTarget: RenderResourceName | null;
    readonly depthTarget: RenderResourceName | null;
    readonly width: number;
    readonly height: number;
    readonly defaultFramebuffer: boolean;
}

export interface WebGL2RenderPassExecutionContext {
    readonly binding: WebGL2ResolvedFramebufferBinding;
    readonly pass: ResolvedRenderPass;
    readonly frame: number;
}

export type WebGL2RenderPassOf<K extends RenderPassKind = RenderPassKind> = Extract<
    ResolvedRenderPass,
    {
        readonly kind: K;
    }
>;

export type WebGL2RenderPassExecutorId<K extends RenderPassKind = RenderPassKind> =
    `${K}:${string}` & {
        readonly [webgl2RenderPassExecutorIdBrand]: K;
    };

export type WebGL2RenderPassMatcher<K extends RenderPassKind = RenderPassKind> = (
    pass: WebGL2RenderPassOf<K>,
    context: RenderExecutionContext<WebGL2RenderResourceHandle>,
    execution: WebGL2RenderPassExecutionContext
) => boolean;

export type WebGL2RenderPassExecutor<K extends RenderPassKind = RenderPassKind> = (
    pass: WebGL2RenderPassOf<K>,
    context: RenderExecutionContext<WebGL2RenderResourceHandle>,
    execution: WebGL2RenderPassExecutionContext
) => void | WebGL2RenderPassExecutionResult | Promise<void | WebGL2RenderPassExecutionResult>;

export interface WebGL2RenderPassExecutorDefinition<K extends RenderPassKind = RenderPassKind> {
    readonly kind: K;
    readonly name: string;
    readonly priority?: number;
    readonly matches?: WebGL2RenderPassMatcher<K>;
    readonly execute: WebGL2RenderPassExecutor<K>;
}

export interface WebGL2RenderPassExecutorDescriptor<K extends RenderPassKind = RenderPassKind> {
    readonly kind: K;
    readonly name: string;
    readonly id: WebGL2RenderPassExecutorId<K>;
    readonly priority: number;
    readonly matches?: WebGL2RenderPassMatcher<K>;
    readonly execute: WebGL2RenderPassExecutor<K>;
}

export type WebGL2AnyRenderPassExecutorDefinition = {
    [K in RenderPassKind]: WebGL2RenderPassExecutorDefinition<K>;
}[RenderPassKind];

export type WebGL2AnyRenderPassExecutorDescriptor = {
    [K in RenderPassKind]: WebGL2RenderPassExecutorDescriptor<K>;
}[RenderPassKind];

export type WebGL2RenderPassExecutorRegistration<K extends RenderPassKind = RenderPassKind> =
    | WebGL2RenderPassExecutorDefinition<K>
    | WebGL2RenderPassExecutorDescriptor<K>;

export type WebGL2AnyRenderPassExecutorRegistration = {
    [K in RenderPassKind]: WebGL2RenderPassExecutorRegistration<K>;
}[RenderPassKind];

export interface WebGL2RenderPassCapture {
    readonly name: string;
    readonly kind: RenderPassKind;
    readonly target: string | null;
    readonly queue: string;
    readonly itemCount: number;
    readonly lightCount: number;
    readonly probeCount: number;
    readonly defaultFramebuffer: boolean;
    readonly executorId: string | null;
    readonly notes: readonly string[];
}

export interface WebGL2RenderFrameCapture {
    readonly frame: number;
    readonly passes: readonly WebGL2RenderPassCapture[];
}

export interface WebGL2RenderBackendProfilerSnapshot {
    readonly frame: number;
    readonly passCount: number;
    readonly drawCalls: number;
    readonly clears: number;
    readonly presents: number;
    readonly cpuTimeMs: number;
}

export interface ManagedWebGL2RenderPassLibrary
    extends RenderPipelineBackend<WebGL2RenderResourceHandle> {
    getLastFrameCapture(): WebGL2RenderFrameCapture | null;
    getProfilerSnapshot(): WebGL2RenderBackendProfilerSnapshot;
    hasExecutor(kind: RenderPassKind): boolean;
    listExecutors(kind?: RenderPassKind): readonly WebGL2AnyRenderPassExecutorDescriptor[];
    dispose(): void;
    [Symbol.dispose](): void;
}

export interface WebGL2RenderPassLibraryOptions {
    readonly gl: WebGL2RenderingContext;
    readonly executors?: readonly WebGL2AnyRenderPassExecutorRegistration[];
    readonly defaultFramebuffer?: WebGLFramebuffer | null;
    readonly strictUnsupportedPasses?: boolean;
    readonly directFrameOutput?: boolean;
    readonly locale?: string;
}

export interface ManagedWebGL2RenderPipelineBackend extends ManagedWebGL2RenderPassLibrary {}

export interface ManagedWebGL2RenderPipelineBackendOptions
    extends WebGL2RenderPassLibraryOptions {}

export const createWebGL2RenderPassExecutorId = <const K extends RenderPassKind>(
    kind: K,
    name: string
): WebGL2RenderPassExecutorId<K> => `${kind}:${name}` as WebGL2RenderPassExecutorId<K>;

export const isWebGL2RenderPassExecutorDescriptor = <const K extends RenderPassKind>(
    value: WebGL2RenderPassExecutorRegistration<K>
): value is WebGL2RenderPassExecutorDescriptor<K> => 'id' in value;

export const defineWebGL2RenderPassExecutor = <const K extends RenderPassKind>(
    definition: WebGL2RenderPassExecutorDefinition<K>
): WebGL2RenderPassExecutorDescriptor<K> =>
    Object.freeze({
        ...definition,
        id: createWebGL2RenderPassExecutorId(definition.kind, definition.name),
        priority: definition.priority ?? 0,
    });