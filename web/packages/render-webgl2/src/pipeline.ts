import type {
    ReadonlyRenderResourceRegistry,
    RenderClearState,
    RenderExecutionContext,
    RenderFrameResult,
    RenderPassKind,
    RenderPipelineBackend,
    RenderResourceAllocator,
    RenderResourceName,
    RenderTextureDescriptor,
    ResolvedRenderPass,
} from '@axrone/render-core/types';

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

export type WebGL2RenderPassHandler = (
    pass: ResolvedRenderPass,
    context: RenderExecutionContext<WebGL2RenderResourceHandle>,
    execution: WebGL2RenderPassExecutionContext
) => void | WebGL2RenderPassExecutionResult | Promise<void | WebGL2RenderPassExecutionResult>;

export type WebGL2RenderPassHandlerMap = Partial<
    Record<RenderPassKind, WebGL2RenderPassHandler>
>;

export interface WebGL2RenderPassCapture {
    readonly name: string;
    readonly kind: RenderPassKind;
    readonly target: string | null;
    readonly queue: string;
    readonly itemCount: number;
    readonly lightCount: number;
    readonly probeCount: number;
    readonly defaultFramebuffer: boolean;
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

export interface ManagedWebGL2RenderPipelineBackend
    extends RenderPipelineBackend<WebGL2RenderResourceHandle> {
    getLastFrameCapture(): WebGL2RenderFrameCapture | null;
    getProfilerSnapshot(): WebGL2RenderBackendProfilerSnapshot;
    dispose(): void;
    [Symbol.dispose](): void;
}

export interface ManagedWebGL2RenderPipelineBackendOptions {
    readonly gl: WebGL2RenderingContext;
    readonly handlers?: WebGL2RenderPassHandlerMap;
    readonly defaultFramebuffer?: WebGLFramebuffer | null;
    readonly strictUnsupportedPasses?: boolean;
}

interface WebGL2FormatInfo {
    readonly internalFormat: number;
    readonly format: number;
    readonly type: number;
}

interface MutableProfilerState {
    frame: number;
    passCount: number;
    drawCalls: number;
    clears: number;
    presents: number;
    cpuTimeMs: number;
}

const DEFAULT_FRAMEBUFFER_HANDLE: WebGL2DefaultFramebufferHandle = Object.freeze({
    kind: 'default-framebuffer',
});

const PASS_REQUIRES_HANDLER = new Set<RenderPassKind>([
    'depth-prepass',
    'shadow',
    'opaque',
    'reflection-probe',
    'global-illumination',
    'volumetric',
    'skybox',
    'transparent',
    'post-process',
    'tonemap',
    'light-bake',
]);

const getNow = (): number =>
    typeof performance !== 'undefined' && typeof performance.now === 'function'
        ? performance.now()
        : Date.now();

const resolveFormatInfo = (
    gl: WebGL2RenderingContext,
    format: RenderTextureDescriptor['format']
): WebGL2FormatInfo => {
    switch (format) {
        case 'r11g11b10f':
            return {
                internalFormat: gl.R11F_G11F_B10F,
                format: gl.RGB,
                type: gl.UNSIGNED_INT_10F_11F_11F_REV,
            };
        case 'rgba8':
            return {
                internalFormat: gl.RGBA8,
                format: gl.RGBA,
                type: gl.UNSIGNED_BYTE,
            };
        case 'rgba16f':
            return {
                internalFormat: gl.RGBA16F,
                format: gl.RGBA,
                type: gl.HALF_FLOAT,
            };
        case 'rgba32f':
            return {
                internalFormat: gl.RGBA32F,
                format: gl.RGBA,
                type: gl.FLOAT,
            };
        case 'rg16f':
            return {
                internalFormat: gl.RG16F,
                format: gl.RG,
                type: gl.HALF_FLOAT,
            };
        case 'rg32f':
            return {
                internalFormat: gl.RG32F,
                format: gl.RG,
                type: gl.FLOAT,
            };
        case 'r16f':
            return {
                internalFormat: gl.R16F,
                format: gl.RED,
                type: gl.HALF_FLOAT,
            };
        case 'r32f':
            return {
                internalFormat: gl.R32F,
                format: gl.RED,
                type: gl.FLOAT,
            };
        case 'depth24':
            return {
                internalFormat: gl.DEPTH_COMPONENT24,
                format: gl.DEPTH_COMPONENT,
                type: gl.UNSIGNED_INT,
            };
        case 'depth32f':
            return {
                internalFormat: gl.DEPTH_COMPONENT32F,
                format: gl.DEPTH_COMPONENT,
                type: gl.FLOAT,
            };
        case 'depth24-stencil8':
            return {
                internalFormat: gl.DEPTH24_STENCIL8,
                format: gl.DEPTH_STENCIL,
                type: gl.UNSIGNED_INT_24_8,
            };
    }
};

const createTextureTarget = (
    gl: WebGL2RenderingContext,
    descriptor: Readonly<RenderTextureDescriptor>
): number => {
    if (descriptor.cube) {
        return gl.TEXTURE_CUBE_MAP;
    }
    if ((descriptor.arrayLayers ?? 1) > 1) {
        return gl.TEXTURE_2D_ARRAY;
    }
    if ((descriptor.depth ?? 1) > 1) {
        return gl.TEXTURE_3D;
    }
    return gl.TEXTURE_2D;
};

const createTextureStorage = (
    gl: WebGL2RenderingContext,
    texture: WebGLTexture,
    descriptor: Readonly<RenderTextureDescriptor>
): WebGL2RenderTextureNativeHandle => {
    const target = createTextureTarget(gl, descriptor);
    const formatInfo = resolveFormatInfo(gl, descriptor.format);
    const mipLevels = Math.max(1, descriptor.mipLevels ?? 1);

    gl.bindTexture(target, texture);
    gl.texParameteri(target, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
    gl.texParameteri(target, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
    gl.texParameteri(target, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(target, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);

    if (target === gl.TEXTURE_3D || target === gl.TEXTURE_2D_ARRAY) {
        gl.texParameteri(target, gl.TEXTURE_WRAP_R, gl.CLAMP_TO_EDGE);
    }

    if (target === gl.TEXTURE_2D || target === gl.TEXTURE_CUBE_MAP) {
        gl.texStorage2D(target, mipLevels, formatInfo.internalFormat, descriptor.width, descriptor.height);
    } else {
        gl.texStorage3D(
            target,
            mipLevels,
            formatInfo.internalFormat,
            descriptor.width,
            descriptor.height,
            descriptor.depth ?? descriptor.arrayLayers ?? 1
        );
    }

    gl.bindTexture(target, null);

    return {
        kind: 'texture',
        texture,
        target,
    };
};

export const createWebGL2RenderResourceAllocator = (
    gl: WebGL2RenderingContext
): RenderResourceAllocator<WebGL2RenderResourceHandle> => ({
    createTexture(descriptor, previous) {
        if (descriptor.usage.includes('present')) {
            if (previous?.kind === 'texture') {
                gl.deleteTexture(previous.texture);
            }
            return DEFAULT_FRAMEBUFFER_HANDLE;
        }

        if ((descriptor.samples ?? 1) !== 1) {
            throw new Error('WebGL2 render resource allocator does not support multisampled textures yet');
        }

        if (previous?.kind === 'texture') {
            gl.deleteTexture(previous.texture);
        }

        const texture = gl.createTexture();
        if (!texture) {
            throw new Error('Failed to allocate WebGL2 render texture');
        }

        return createTextureStorage(gl, texture, descriptor);
    },
    destroyTexture(native, _descriptor) {
        if (native.kind === 'texture') {
            gl.deleteTexture(native.texture);
        }
    },
});

const resolveFramebufferTargets = (
    pass: ResolvedRenderPass
): {
    readonly colorTarget: RenderResourceName | null;
    readonly depthTarget: RenderResourceName | null;
} => {
    switch (pass.kind) {
        case 'depth-prepass':
            return {
                colorTarget: null,
                depthTarget: pass.metadata.depth,
            };
        case 'shadow':
            return {
                colorTarget: null,
                depthTarget: pass.metadata.atlas,
            };
        case 'opaque':
            return {
                colorTarget: pass.metadata.color,
                depthTarget: pass.metadata.depth,
            };
        case 'skybox':
            return {
                colorTarget: pass.metadata.color,
                depthTarget: pass.metadata.depth,
            };
        case 'transparent':
            return {
                colorTarget: pass.metadata.color,
                depthTarget: pass.metadata.depth,
            };
        case 'post-process':
            return {
                colorTarget: pass.metadata.target,
                depthTarget: null,
            };
        case 'tonemap':
            return {
                colorTarget: pass.metadata.target,
                depthTarget: null,
            };
        case 'reflection-probe':
            return {
                colorTarget: pass.metadata.target,
                depthTarget: null,
            };
        case 'global-illumination':
            return {
                colorTarget: pass.metadata.target,
                depthTarget: null,
            };
        case 'volumetric':
            return {
                colorTarget: pass.metadata.froxelGrid,
                depthTarget: null,
            };
        case 'present':
        case 'light-bake':
            return {
                colorTarget: null,
                depthTarget: null,
            };
    }
};

const clearFramebuffer = (
    gl: WebGL2RenderingContext,
    clearState: RenderClearState | null | undefined
): number => {
    if (!clearState) {
        return 0;
    }

    let bits = 0;

    if (clearState.color !== undefined && clearState.color !== null) {
        const color = clearState.color as readonly number[];
        gl.clearColor(color[0] ?? 0, color[1] ?? 0, color[2] ?? 0, color[3] ?? 1);
        bits |= gl.COLOR_BUFFER_BIT;
    }

    if (clearState.depth !== undefined && clearState.depth !== null) {
        gl.clearDepth(clearState.depth);
        bits |= gl.DEPTH_BUFFER_BIT;
    }

    if (clearState.stencil !== undefined && clearState.stencil !== null) {
        gl.clearStencil(clearState.stencil);
        bits |= gl.STENCIL_BUFFER_BIT;
    }

    if (bits !== 0) {
        gl.clear(bits);
    }

    return bits;
};

const createFramebufferCacheKey = (
    colorTarget: RenderResourceName | null,
    depthTarget: RenderResourceName | null
): string => `${colorTarget ?? 'none'}|${depthTarget ?? 'none'}`;

const getTextureHandle = (
    graph: ReadonlyRenderResourceRegistry<WebGL2RenderResourceHandle>,
    resourceName: RenderResourceName | null
): WebGL2RenderTextureNativeHandle | null => {
    if (!resourceName) {
        return null;
    }

    const snapshot = graph.getTexture(resourceName);
    if (!snapshot || !snapshot.native || snapshot.native.kind !== 'texture') {
        return null;
    }

    return snapshot.native;
};

export const createManagedWebGL2RenderPipelineBackend = (
    options: ManagedWebGL2RenderPipelineBackendOptions
): ManagedWebGL2RenderPipelineBackend => {
    const { gl } = options;
    const framebufferCache = new Map<string, WebGLFramebuffer>();
    const strictUnsupportedPasses = options.strictUnsupportedPasses ?? true;
    let lastFrameCapture: WebGL2RenderFrameCapture | null = null;
    let frameCaptures: WebGL2RenderPassCapture[] = [];
    let frameStartTime = 0;

    const profiler: MutableProfilerState = {
        frame: 0,
        passCount: 0,
        drawCalls: 0,
        clears: 0,
        presents: 0,
        cpuTimeMs: 0,
    };

    const deleteFramebufferCache = (): void => {
        for (const framebuffer of framebufferCache.values()) {
            gl.deleteFramebuffer(framebuffer);
        }
        framebufferCache.clear();
    };

    const resolveTextureSnapshot = (
        graph: ReadonlyRenderResourceRegistry<WebGL2RenderResourceHandle>,
        resourceName: RenderResourceName | null
    ) => (resourceName ? graph.getTexture(resourceName) : null);

    const resolveManagedFramebuffer = (
        colorTarget: RenderResourceName | null,
        depthTarget: RenderResourceName | null,
        context: RenderExecutionContext<WebGL2RenderResourceHandle>
    ): WebGL2ResolvedFramebufferBinding => {
        const colorHandle = getTextureHandle(context.graph, colorTarget);
        const depthHandle = getTextureHandle(context.graph, depthTarget);

        if (!colorHandle && !depthHandle) {
            return {
                framebuffer: options.defaultFramebuffer ?? null,
                colorTarget,
                depthTarget,
                width: context.viewport.width,
                height: context.viewport.height,
                defaultFramebuffer: true,
            };
        }

        if (
            (colorHandle && colorHandle.target !== gl.TEXTURE_2D) ||
            (depthHandle && depthHandle.target !== gl.TEXTURE_2D)
        ) {
            throw new Error('Managed WebGL2 render backend currently supports 2D framebuffer attachments only');
        }

        const cacheKey = createFramebufferCacheKey(colorTarget, depthTarget);
        let framebuffer = framebufferCache.get(cacheKey);
        if (!framebuffer) {
            framebuffer = gl.createFramebuffer();
            if (!framebuffer) {
                throw new Error('Failed to create WebGL2 framebuffer for render backend');
            }
            framebufferCache.set(cacheKey, framebuffer);
        }

        gl.bindFramebuffer(gl.FRAMEBUFFER, framebuffer);

        if (colorHandle) {
            gl.framebufferTexture2D(
                gl.FRAMEBUFFER,
                gl.COLOR_ATTACHMENT0,
                gl.TEXTURE_2D,
                colorHandle.texture,
                0
            );
            gl.drawBuffers([gl.COLOR_ATTACHMENT0]);
        } else {
            gl.drawBuffers([]);
        }

        if (depthHandle) {
            const depthDescriptor = resolveTextureSnapshot(context.graph, depthTarget)?.descriptor;
            const attachment = depthDescriptor?.format === 'depth24-stencil8'
                ? gl.DEPTH_STENCIL_ATTACHMENT
                : gl.DEPTH_ATTACHMENT;

            gl.framebufferTexture2D(
                gl.FRAMEBUFFER,
                attachment,
                gl.TEXTURE_2D,
                depthHandle.texture,
                0
            );
        }

        const sizeSnapshot = resolveTextureSnapshot(context.graph, colorTarget ?? depthTarget);

        return {
            framebuffer,
            colorTarget,
            depthTarget,
            width: sizeSnapshot?.descriptor.width ?? context.viewport.width,
            height: sizeSnapshot?.descriptor.height ?? context.viewport.height,
            defaultFramebuffer: false,
        };
    };

    const resolveFramebuffer = (
        pass: ResolvedRenderPass,
        context: RenderExecutionContext<WebGL2RenderResourceHandle>
    ): WebGL2ResolvedFramebufferBinding => {
        if (pass.kind === 'present') {
            return {
                framebuffer: options.defaultFramebuffer ?? null,
                colorTarget: null,
                depthTarget: null,
                width: context.viewport.width,
                height: context.viewport.height,
                defaultFramebuffer: true,
            };
        }

        const targets = resolveFramebufferTargets(pass);
        return resolveManagedFramebuffer(targets.colorTarget, targets.depthTarget, context);
    };

    const performPresent = (
        pass: Extract<ResolvedRenderPass, { kind: 'present' }>,
        context: RenderExecutionContext<WebGL2RenderResourceHandle>
    ): void => {
        const sourceBinding = resolveManagedFramebuffer(pass.metadata.source, null, context);
        gl.bindFramebuffer(gl.READ_FRAMEBUFFER, sourceBinding.framebuffer);
        gl.bindFramebuffer(gl.DRAW_FRAMEBUFFER, options.defaultFramebuffer ?? null);
        gl.blitFramebuffer(
            0,
            0,
            sourceBinding.width,
            sourceBinding.height,
            0,
            0,
            context.viewport.width,
            context.viewport.height,
            gl.COLOR_BUFFER_BIT,
            gl.NEAREST
        );
    };

    return {
        async beginFrame(context) {
            profiler.frame = context.frame;
            profiler.passCount = 0;
            profiler.drawCalls = 0;
            profiler.clears = 0;
            profiler.presents = 0;
            profiler.cpuTimeMs = 0;
            frameCaptures = [];
            frameStartTime = getNow();
        },
        async executePass(pass, context) {
            const handler = options.handlers?.[pass.kind];
            const binding = resolveFramebuffer(pass, context);

            gl.bindFramebuffer(gl.FRAMEBUFFER, binding.framebuffer);
            gl.viewport(0, 0, binding.width, binding.height);

            if (clearFramebuffer(gl, pass.clearState) !== 0) {
                profiler.clears += 1;
            }

            let result: WebGL2RenderPassExecutionResult | undefined;

            if (pass.kind === 'present') {
                performPresent(pass, context);
                profiler.presents += 1;
            } else if (handler) {
                const handlerResult = await handler(pass, context, {
                    binding,
                    pass,
                    frame: context.frame,
                });
                result = handlerResult ?? undefined;
            } else if (strictUnsupportedPasses && PASS_REQUIRES_HANDLER.has(pass.kind)) {
                throw new Error(
                    `No WebGL2 render pass handler is registered for '${pass.kind}' passes`
                );
            }

            profiler.passCount += 1;
            profiler.drawCalls += (pass.kind === 'present' ? 1 : 0) + (result?.drawCalls ?? 0);
            frameCaptures.push(
                Object.freeze({
                    name: pass.name,
                    kind: pass.kind,
                    target: pass.target,
                    queue: pass.queue,
                    itemCount: pass.items?.length ?? 0,
                    lightCount: pass.lights?.length ?? 0,
                    probeCount: pass.probes?.length ?? 0,
                    defaultFramebuffer: binding.defaultFramebuffer,
                    notes: result?.notes ?? Object.freeze([]),
                })
            );
        },
        async endFrame(result, _context) {
            gl.bindFramebuffer(gl.FRAMEBUFFER, options.defaultFramebuffer ?? null);
            gl.bindFramebuffer(gl.READ_FRAMEBUFFER, null);
            gl.bindFramebuffer(gl.DRAW_FRAMEBUFFER, null);

            profiler.cpuTimeMs = getNow() - frameStartTime;
            lastFrameCapture = Object.freeze({
                frame: result.frame,
                passes: Object.freeze([...frameCaptures]),
            });
        },
        getLastFrameCapture() {
            return lastFrameCapture;
        },
        getProfilerSnapshot() {
            return Object.freeze({
                frame: profiler.frame,
                passCount: profiler.passCount,
                drawCalls: profiler.drawCalls,
                clears: profiler.clears,
                presents: profiler.presents,
                cpuTimeMs: profiler.cpuTimeMs,
            });
        },
        dispose() {
            deleteFramebufferCache();
            lastFrameCapture = null;
            frameCaptures = [];
        },
        [Symbol.dispose]() {
            deleteFramebufferCache();
            lastFrameCapture = null;
            frameCaptures = [];
        },
    };
};